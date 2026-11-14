using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Auth;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Endpoints;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>
/// Two guards that exist only through pipeline wiring: antiforgery on the /cart form endpoints (it comes from
/// the [FromForm] binding, so reading the form by hand would silently drop it) and the position of the CSRF header
/// middleware after UseAuthentication (it reads context.User).
/// </summary>
public class SecurityPipelineTests
{
    private sealed class FakeCart : ICartStore
    {
        public int Mutations;
        public IReadOnlyList<CartLine> Lines => [];
        public int Count => 0;
        public bool Add(int productId, decimal quantity) { Mutations++; return true; }
        public void Update(int productId, decimal? quantity, int step) => Mutations++;
        public void Remove(int productId) => Mutations++;
        public void Clear() => Mutations++;
    }

    private sealed class SignedInHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], AuthSchemes.Cookie);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.Cookie)));
        }
    }

    private static async Task<WebApplication> StartCartHostAsync(FakeCart cart)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddAntiforgery();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddSingleton<ICartStore>(cart);
        var app = builder.Build();
        app.UseRouting();
        app.UseAntiforgery(); // as in Program.cs, after UseRouting
        app.MapCartEndpoints();
        await app.StartAsync();
        return app;
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    [Theory]
    [InlineData("/cart/add")]
    [InlineData("/cart/update")]
    [InlineData("/cart/remove")]
    public async Task Tokenless_post_to_a_cart_form_endpoint_is_rejected_by_antiforgery(string path)
    {
        var cart = new FakeCart();
        await using var app = await StartCartHostAsync(cart);
        using var client = app.GetTestClient();

        var response = await client.PostAsync(path, Form(("productId", "1"), ("quantity", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, cart.Mutations);
    }

    [Fact]
    public async Task Post_to_a_cart_form_endpoint_with_a_valid_token_reaches_the_handler()
    {
        // Control for the test above: the endpoint is reachable, so the 400 there is the antiforgery rule.
        var cart = new FakeCart();
        await using var app = await StartCartHostAsync(cart);
        using var client = app.GetTestClient();

        var ctx = new DefaultHttpContext { RequestServices = app.Services };
        var tokens = app.Services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(ctx);
        var cookie = ctx.Response.Headers.SetCookie.ToString().Split(';')[0];
        var request = new HttpRequestMessage(HttpMethod.Post, "/cart/update")
        {
            Content = Form(("productId", "1"), ("step", "1"), (tokens.FormFieldName!, tokens.RequestToken!))
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cart", response.Headers.Location?.OriginalString);
    }

    private static async Task<HttpClient> StartCsrfHostAsync(bool csrfAfterAuthentication)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s =>
                {
                    s.AddAuthentication(AuthSchemes.Cookie)
                        .AddScheme<AuthenticationSchemeOptions, SignedInHandler>(AuthSchemes.Cookie, null);
                })
                .Configure(app =>
                {
                    if (csrfAfterAuthentication)
                    {
                        app.UseAuthentication();
                        app.UseMiddleware<AdminCsrfHeaderMiddleware>();
                    }
                    else
                    {
                        app.UseMiddleware<AdminCsrfHeaderMiddleware>();
                        app.UseAuthentication();
                    }
                    app.Run(ctx => { ctx.Response.StatusCode = 204; return Task.CompletedTask; });
                }))
            .StartAsync();
        return host.GetTestClient();
    }

    [Fact]
    public async Task Authenticated_cookie_request_without_the_header_is_rejected_when_csrf_runs_after_authentication()
    {
        using var client = await StartCsrfHostAsync(csrfAfterAuthentication: true);

        var without = await client.PutAsync("/api/orders/1/confirm", null);
        var withHeader = new HttpRequestMessage(HttpMethod.Put, "/api/orders/1/confirm");
        withHeader.Headers.Add(AdminCsrfHeaderMiddleware.HeaderName, AdminCsrfHeaderMiddleware.HeaderValue);

        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(withHeader)).StatusCode);
    }

    [Fact]
    public async Task Csrf_middleware_before_authentication_cannot_see_the_user_so_the_order_matters()
    {
        // Documents why the order is pinned: before UseAuthentication the cookie request slips through.
        using var client = await StartCsrfHostAsync(csrfAfterAuthentication: false);

        var response = await client.PutAsync("/api/orders/1/confirm", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public void Program_runs_the_csrf_header_middleware_between_authentication_and_authorization()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "NaturalStoneImpex.Api", "Program.cs"));
        var code = Regex.Replace(program, @"//.*", "");

        var routing = code.IndexOf("app.UseRouting();", StringComparison.Ordinal);
        var authentication = code.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var csrf = code.IndexOf("app.UseMiddleware<AdminCsrfHeaderMiddleware>();", StringComparison.Ordinal);
        var authorization = code.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var antiforgery = code.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);

        Assert.True(routing >= 0 && routing < authentication, "UseRouting must precede UseAuthentication");
        Assert.True(authentication < csrf, "AdminCsrfHeaderMiddleware must run after UseAuthentication");
        Assert.True(csrf >= 0 && csrf < authorization, "AdminCsrfHeaderMiddleware must run before UseAuthorization");
        Assert.True(authorization < antiforgery, "UseAntiforgery must follow UseAuthorization");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NaturalStoneImpex.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("NaturalStoneImpex.slnx not found above " + AppContext.BaseDirectory);
    }
}

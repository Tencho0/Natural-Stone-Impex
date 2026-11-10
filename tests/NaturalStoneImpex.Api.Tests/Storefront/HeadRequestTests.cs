using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NaturalStoneImpex.Api.Endpoints;
using NaturalStoneImpex.Api.Middleware;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>Ruling R11: HEAD to a storefront page behaves like GET without a body; /api is untouched.</summary>
public class HeadRequestTests
{
    private sealed class FakeQueries : IStorefrontQueryService
    {
        public Task<IReadOnlyList<ProductListDto>> GetFeaturedAsync(int count) => throw new NotSupportedException();
        public Task<ProductDto?> GetActiveProductAsync(int id) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductListDto>> GetByIdsAsync(IReadOnlyCollection<int> ids) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, string>> GetCategoryPhotosAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<(int Id, DateTime UpdatedAt)>> GetSitemapProductsAsync()
            => Task.FromResult<IReadOnlyList<(int, DateTime)>>([(7, new DateTime(2026, 1, 2))]);
    }

    private sealed class Probe
    {
        public string? MethodAfter;
        public bool BodyRestored;
    }

    private static async Task<(HttpClient Client, Probe Probe)> StartWithProbeAsync()
    {
        var probe = new Probe();
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddScoped<IStorefrontQueryService, FakeQueries>();
                })
                .Configure(app =>
                {
                    // Outermost: after the whole inner pipeline (including re-execution) the request must be
                    // HEAD again and the response body the original stream.
                    app.Use(async (ctx, next) =>
                    {
                        var original = ctx.Response.Body;
                        await next(ctx);
                        probe.MethodAfter = ctx.Request.Method;
                        probe.BodyRestored = ReferenceEquals(ctx.Response.Body, original);
                    });
                    // The same ordering as Program.cs: HEAD as GET, then error pages, then routing. HEAD as GET has to
                    // be outside the error pages, or the re-executed /not-found and /error bodies are not dropped.
                    app.UseHeadAsGet();
                    app.UseStorefrontErrorPages(isDevelopment: false);
                    app.UseRouting();
                    app.UseEndpoints(e =>
                    {
                        e.MapGet("/", () => "home");
                        e.MapGet("/catalog", () => "catalog");
                        e.MapGet("/contacts", () => "contacts");
                        e.MapGet("/boom", (Func<string>)(() => throw new InvalidOperationException("boom")));
                        e.MapGet("/not-found", async (HttpContext c) =>
                        {
                            c.Response.ContentType = "text/html";
                            c.Response.StatusCode = 404;
                            await c.Response.WriteAsync("not found page");
                        });
                        e.MapGet("/error", async (HttpContext c) =>
                        {
                            c.Response.ContentType = "text/html"; // like the Razor pages, so status code pages leave it alone
                            c.Response.StatusCode = 500;
                            await c.Response.WriteAsync("error page");
                        });
                        e.MapGet("/api/ping", () => "pong");
                        e.MapSeoEndpoints();
                    });
                }))
            .StartAsync();
        return (host.GetTestClient(), probe);
    }

    private static async Task<HttpClient> StartAsync() => (await StartWithProbeAsync()).Client;

    private static Task<HttpResponseMessage> Head(HttpClient c, string path)
        => c.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

    [Theory]
    [InlineData("/")]
    [InlineData("/catalog")]
    [InlineData("/contacts")]
    [InlineData("/sitemap.xml")]
    [InlineData("/robots.txt")]
    public async Task Head_to_a_storefront_path_returns_200_with_an_empty_body(string path)
    {
        using var client = await StartAsync();

        var response = await Head(client, path);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Head_to_an_unknown_path_is_404()
    {
        using var client = await StartAsync();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await Head(client, "/nope")).StatusCode);
    }

    [Fact]
    public async Task Head_to_an_unknown_path_re_executes_not_found_with_an_empty_body_and_restores_the_request()
    {
        var (client, probe) = await StartWithProbeAsync();
        using var _ = client;

        var response = await Head(client, "/nope");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("HEAD", probe.MethodAfter);
        Assert.True(probe.BodyRestored);
    }

    [Fact]
    public async Task Head_to_a_throwing_page_is_500_with_an_empty_body_and_restores_the_request()
    {
        var (client, probe) = await StartWithProbeAsync();
        using var _ = client;

        var response = await Head(client, "/boom");

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("HEAD", probe.MethodAfter);
        Assert.True(probe.BodyRestored);
    }

    [Fact]
    public async Task Get_to_an_unknown_path_still_serves_the_not_found_page()
    {
        using var client = await StartAsync();

        var response = await client.GetAsync("/nope");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not found page", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/uploads/products/a.jpg")]
    public void Head_is_not_rewritten_for_static_assets(string path)
    {
        // Static assets and uploads answer HEAD natively (checked live against MapStaticAssets).
        Assert.True(NaturalStoneImpex.Api.Storefront.ErrorPages.IsStaticFileRequest(path));
        Assert.False(SeoEndpoints.IsSeoPath(path));
    }

    [Fact]
    public async Task Head_to_a_static_asset_path_is_left_as_head()
    {
        // No endpoint matches HEAD here: if the middleware rewrote it, the GET-only endpoint would answer 200.
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(s => s.AddRouting())
                .Configure(app =>
                {
                    app.UseHeadAsGet();
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapGet("/css/site.css", () => "css"));
                }))
            .StartAsync();
        using var client = host.GetTestClient();

        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, (await Head(client, "/css/site.css")).StatusCode);
    }

    [Fact]
    public async Task Head_to_api_keeps_its_current_behaviour()
    {
        using var client = await StartAsync();
        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, (await Head(client, "/api/ping")).StatusCode);
    }

    [Fact]
    public async Task Get_still_returns_the_body()
    {
        using var client = await StartAsync();
        Assert.Equal("home", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Sitemap_and_robots_send_type_cache_and_use_the_request_origin()
    {
        using var client = await StartAsync();

        var sitemap = await client.GetAsync("/sitemap.xml");
        Assert.StartsWith("application/xml", sitemap.Content.Headers.ContentType!.ToString());
        Assert.Contains("max-age=3600", sitemap.Headers.CacheControl!.ToString());
        Assert.Contains("http://localhost/products/7", await sitemap.Content.ReadAsStringAsync());

        var robots = await client.GetAsync("/robots.txt");
        Assert.StartsWith("text/plain", robots.Content.Headers.ContentType!.ToString());
        Assert.Contains("Sitemap: http://localhost/sitemap.xml", await robots.Content.ReadAsStringAsync());
    }
}

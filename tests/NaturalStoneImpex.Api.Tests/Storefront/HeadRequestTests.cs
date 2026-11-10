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

    private static async Task<HttpClient> StartAsync()
    {
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
                    app.UseHeadAsGet();
                    app.UseRouting();
                    app.UseEndpoints(e =>
                    {
                        e.MapGet("/", () => "home");
                        e.MapGet("/catalog", () => "catalog");
                        e.MapGet("/contacts", () => "contacts");
                        e.MapGet("/api/ping", () => "pong");
                        e.MapSeoEndpoints();
                    });
                }))
            .StartAsync();
        return host.GetTestClient();
    }

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

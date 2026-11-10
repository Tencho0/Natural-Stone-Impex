using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Endpoints;

public static class SeoEndpoints
{
    private const string CacheControl = "public, max-age=3600";

    public static bool IsSeoPath(PathString path)
        => path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase);

    public static void MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", async (HttpContext http, IStorefrontQueryService storefront) =>
        {
            var products = await storefront.GetSitemapProductsAsync();
            http.Response.Headers.CacheControl = CacheControl;
            return Results.Text(SitemapBuilder.Build(BaseUrl(http), products), "application/xml", System.Text.Encoding.UTF8);
        });

        app.MapGet("/robots.txt", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = CacheControl;
            return Results.Text(SitemapBuilder.Robots(BaseUrl(http)), "text/plain", System.Text.Encoding.UTF8);
        });
    }

    // Same helper SeoHead uses for canonical URLs, so the sitemap and the canonicals always agree.
    private static string BaseUrl(HttpContext http)
    {
        var request = http.Request;
        return SeoFormat.AbsoluteUrl(request.Scheme, request.Host.ToUriComponent(), request.PathBase.ToUriComponent(), "/")
            .TrimEnd('/');
    }
}

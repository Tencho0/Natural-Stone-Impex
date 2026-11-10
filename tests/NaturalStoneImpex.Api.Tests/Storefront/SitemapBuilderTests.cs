using System.Xml.Linq;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class SitemapBuilderTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [Fact]
    public void Sitemap_lists_static_pages_and_products()
    {
        var xml = SitemapBuilder.Build("https://host", [(43, new DateTime(2026, 3, 9, 22, 30, 0, DateTimeKind.Utc))]);

        var urls = XDocument.Parse(xml).Root!.Elements(Ns + "url").ToList();
        var locs = urls.Select(u => u.Element(Ns + "loc")!.Value).ToList();
        Assert.Equal(
            ["https://host/", "https://host/catalog", "https://host/contacts", "https://host/visualizer", "https://host/products/43"],
            locs);
        Assert.Equal("2026-03-09", urls[4].Element(Ns + "lastmod")!.Value);
        Assert.Null(urls[0].Element(Ns + "lastmod"));
    }

    [Fact]
    public void Sitemap_never_lists_private_pages()
    {
        var xml = SitemapBuilder.Build("https://host", []);

        foreach (var path in new[] { "/cart", "/checkout", "/order-confirmation", "/admin", "/api" })
            Assert.DoesNotContain("https://host" + path, xml);
    }

    [Fact]
    public void Sitemap_escapes_the_base_url()
    {
        var xml = SitemapBuilder.Build("https://host/a&b", []);

        Assert.Contains("https://host/a&amp;b/catalog", xml);
        XDocument.Parse(xml); // still well-formed
    }

    [Fact]
    public void Sitemap_dates_use_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
        try
        {
            var xml = SitemapBuilder.Build("https://host", [(1, new DateTime(2026, 1, 2))]);
            Assert.Contains("<lastmod>2026-01-02</lastmod>", xml);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Fact]
    public void Robots_disallows_private_paths_and_points_to_sitemap()
    {
        var robots = SitemapBuilder.Robots("https://host");

        Assert.Contains("User-agent: *", robots);
        foreach (var path in new[] { "/admin", "/cart", "/checkout", "/order-confirmation", "/api" })
            Assert.Contains("Disallow: " + path, robots);
        Assert.Contains("Sitemap: https://host/sitemap.xml", robots);
    }
}

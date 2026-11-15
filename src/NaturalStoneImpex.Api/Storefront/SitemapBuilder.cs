using System.Globalization;
using System.Text;
using System.Xml;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Pure builders for /sitemap.xml and /robots.txt. The base URL comes from SeoFormat.AbsoluteUrl.</summary>
public static class SitemapBuilder
{
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    // Indexable pages only: never the cart, checkout, confirmation, admin or API.
    private static readonly string[] StaticPaths = ["/", "/catalog", "/contacts", "/visualizer"];

    private static readonly string[] PrivatePaths = ["/admin", "/cart", "/checkout", "/order-confirmation", "/api"];

    public static string Build(string baseUrl, IEnumerable<(int Id, DateTime UpdatedAt)> products)
    {
        var origin = baseUrl.TrimEnd('/');
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        var sb = new StringBuilder();

        using (var writer = XmlWriter.Create(new Utf8StringWriter(sb), settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", SitemapNamespace);

            foreach (var path in StaticPaths)
                WriteUrl(writer, origin + path, null);

            foreach (var (id, updatedAt) in products)
                WriteUrl(writer, $"{origin}/products/{id.ToString(CultureInfo.InvariantCulture)}", updatedAt);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    public static string Robots(string baseUrl)
    {
        var origin = baseUrl.TrimEnd('/');
        var sb = new StringBuilder("User-agent: *\n");
        foreach (var path in PrivatePaths)
            sb.Append("Disallow: ").Append(path).Append('\n');
        sb.Append("\nSitemap: ").Append(origin).Append("/sitemap.xml\n");
        return sb.ToString();
    }

    private static void WriteUrl(XmlWriter writer, string loc, DateTime? lastModified)
    {
        writer.WriteStartElement("url");
        writer.WriteElementString("loc", loc);
        if (lastModified is { } date)
            writer.WriteElementString("lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    // StringWriter reports UTF-16; the declaration should say UTF-8 because that is what the endpoint sends.
    private sealed class Utf8StringWriter(StringBuilder sb) : StringWriter(sb, CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(false);
    }
}

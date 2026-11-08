using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// schema.org Product structured data for the product page (spec §5.6): name, image, category, description and an
/// Offer in EUR. Products priced 0 get no price. There is no sku (the shop has no code field).
/// </summary>
public static class ProductJsonLd
{
    private const string InStock = "https://schema.org/InStock";
    private const string BackOrder = "https://schema.org/BackOrder";

    // Cyrillic stays readable; the encoder still escapes HTML-sensitive characters (<, >, &, '), so the JSON
    // cannot close the script element it is embedded in.
    private static readonly JsonWriterOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Build(ProductDto p, string absoluteUrl, string? absoluteImageUrl)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("@context", "https://schema.org");
            writer.WriteString("@type", "Product");
            writer.WriteString("name", p.Name);
            writer.WriteString("url", absoluteUrl);
            if (!string.IsNullOrWhiteSpace(absoluteImageUrl))
                writer.WriteString("image", absoluteImageUrl);
            if (!string.IsNullOrWhiteSpace(p.CategoryName))
                writer.WriteString("category", p.CategoryName);
            if (!string.IsNullOrWhiteSpace(p.Description))
                writer.WriteString("description", p.Description.Trim());

            writer.WriteStartObject("offers");
            writer.WriteString("@type", "Offer");
            writer.WriteString("url", absoluteUrl);
            if (p.PriceWithVat > 0)
            {
                // A string with two decimals, invariant culture: "20.45".
                writer.WriteString("price", p.PriceWithVat.ToString("0.00", CultureInfo.InvariantCulture));
                writer.WriteString("priceCurrency", "EUR");
            }
            writer.WriteString("availability", p.StockQuantity > 0 ? InStock : BackOrder);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

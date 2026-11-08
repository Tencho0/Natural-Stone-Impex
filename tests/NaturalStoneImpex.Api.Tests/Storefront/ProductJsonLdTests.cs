using System.Text.Json;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class ProductJsonLdTests
{
    private const string Url = "https://shop.example/products/43";
    private const string Image = "https://shop.example/uploads/products/product-81.jpg";

    private static ProductDto Product(decimal price = 20.45m, decimal stock = 46.5m, string? description = "Код: GNF-0003") => new()
    {
        Id = 43,
        Name = "Бежов оформен глиц 15L",
        Description = description,
        CategoryId = 2,
        CategoryName = "Гнайс",
        PriceWithoutVat = price == 0 ? 0 : 17.04m,
        VatAmount = price == 0 ? 0 : 3.41m,
        PriceWithVat = price,
        Unit = 1,
        UnitDisplay = "м²",
        StockQuantity = stock,
        IsActive = true
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Includes_offer_price_in_eur_when_priced()
    {
        var json = ProductJsonLd.Build(Product(price: 20.45m), Url, Image);

        Assert.Contains("\"price\":\"20.45\"", json);
        Assert.Contains("\"priceCurrency\":\"EUR\"", json);
        var offer = Parse(json).GetProperty("offers");
        Assert.Equal("Offer", offer.GetProperty("@type").GetString());
        Assert.Equal(Url, offer.GetProperty("url").GetString());
    }

    [Fact]
    public void Omits_price_when_zero()
    {
        var json = ProductJsonLd.Build(Product(price: 0m), Url, Image);

        var root = Parse(json);
        Assert.False(root.TryGetProperty("price", out _));
        var offer = root.GetProperty("offers");
        Assert.False(offer.TryGetProperty("price", out _));
        Assert.False(offer.TryGetProperty("priceCurrency", out _));
    }

    [Fact]
    public void Availability_instock_when_stock_positive()
    {
        var json = ProductJsonLd.Build(Product(stock: 0.01m), Url, Image);

        Assert.Equal("https://schema.org/InStock",
            Parse(json).GetProperty("offers").GetProperty("availability").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8.2)]
    public void Availability_backorder_when_stock_zero_or_negative(double stock)
    {
        var json = ProductJsonLd.Build(Product(stock: (decimal)stock), Url, Image);

        Assert.Equal("https://schema.org/BackOrder",
            Parse(json).GetProperty("offers").GetProperty("availability").GetString());
    }

    [Fact]
    public void Has_no_sku()
    {
        var json = ProductJsonLd.Build(Product(), Url, Image);

        Assert.DoesNotContain("sku", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describes_the_product_with_absolute_urls()
    {
        var root = Parse(ProductJsonLd.Build(Product(), Url, Image));

        Assert.Equal("https://schema.org", root.GetProperty("@context").GetString());
        Assert.Equal("Product", root.GetProperty("@type").GetString());
        Assert.Equal("Бежов оформен глиц 15L", root.GetProperty("name").GetString());
        Assert.Equal("Гнайс", root.GetProperty("category").GetString());
        Assert.Equal("Код: GNF-0003", root.GetProperty("description").GetString());
        Assert.Equal(Url, root.GetProperty("url").GetString());
        Assert.Equal(Image, root.GetProperty("image").GetString());
    }

    [Fact]
    public void Omits_image_and_blank_description()
    {
        var root = Parse(ProductJsonLd.Build(Product(description: "  "), Url, null));

        Assert.False(root.TryGetProperty("image", out _));
        Assert.False(root.TryGetProperty("description", out _));
    }

    [Fact]
    public void Cannot_close_the_script_element()
    {
        const string hostile = "</script><script>alert(1)</script> & 'x'";
        var json = ProductJsonLd.Build(Product(description: hostile), Url, Image);

        Assert.DoesNotContain("<", json);
        Assert.Equal(hostile, Parse(json).GetProperty("description").GetString());
    }

    [Fact]
    public void Keeps_cyrillic_readable()
    {
        var json = ProductJsonLd.Build(Product(), Url, Image);

        Assert.Contains("Бежов оформен глиц 15L", json);
    }
}

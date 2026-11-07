using System.Text.Json;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class SeoFormatTests
{
    [Theory]
    [InlineData("Каталог", "Каталог | Natural Stone Impex")]
    [InlineData("  Контакти ", "Контакти | Natural Stone Impex")]
    [InlineData("", "Natural Stone Impex")]
    [InlineData(null, "Natural Stone Impex")]
    public void Title_appends_site_name(string? title, string expected)
        => Assert.Equal(expected, SeoFormat.Title(title));

    [Theory]
    [InlineData("/catalog", "https://shop.example/catalog")]
    [InlineData("catalog?category=2", "https://shop.example/catalog?category=2")]
    [InlineData("/", "https://shop.example/")]
    [InlineData(null, "https://shop.example/")]
    [InlineData("", "https://shop.example/")]
    public void AbsoluteUrl_joins_origin_and_local_path(string? path, string expected)
        => Assert.Equal(expected, SeoFormat.AbsoluteUrl("https", "shop.example", null, path));

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("https://evil.example/x")]
    [InlineData("/\\evil.example")]
    public void AbsoluteUrl_rejects_non_local_paths(string path)
        => Assert.Equal("https://shop.example/", SeoFormat.AbsoluteUrl("https", "shop.example", null, path));

    [Fact]
    public void AbsoluteUrl_keeps_port_and_path_base()
        => Assert.Equal("https://localhost:5001/shop/contacts",
            SeoFormat.AbsoluteUrl("https", "localhost:5001", "/shop/", "/contacts"));

    [Fact]
    public void ImageUrl_makes_local_paths_absolute_and_passes_http_urls()
    {
        Assert.Equal("https://shop.example/uploads/products/a.jpg",
            SeoFormat.ImageUrl("https", "shop.example", null, "/uploads/products/a.jpg"));
        Assert.Equal("https://cdn.example/a.jpg",
            SeoFormat.ImageUrl("https", "shop.example", null, "https://cdn.example/a.jpg"));
        Assert.Equal("https://shop.example/",
            SeoFormat.ImageUrl("https", "shop.example", null, "javascript:alert(1)"));
    }

    [Fact]
    public void JsonLd_serializes_objects_without_a_script_breakout()
    {
        var json = SeoFormat.JsonLd(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["name"] = "</script><script>alert(1)</script>"
        });

        Assert.DoesNotContain("<", json);
        Assert.Contains("\"@context\":\"https://schema.org\"", json);
        Assert.Equal("</script><script>alert(1)</script>",
            JsonDocument.Parse(json).RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void JsonLd_escapes_angle_brackets_in_prebuilt_strings()
    {
        var json = SeoFormat.JsonLd("{\"name\":\"</script>\"}");

        Assert.DoesNotContain("<", json);
        Assert.Equal("</script>", JsonDocument.Parse(json).RootElement.GetProperty("name").GetString());
    }
}

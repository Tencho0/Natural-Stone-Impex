using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class NavMatchTests
{
    [Theory]
    [InlineData("/catalog", "/catalog", true)]
    [InlineData("/catalog/", "/catalog", true)]
    [InlineData("/Catalog?page=2", "/catalog", true)]
    [InlineData("/products/12", "/catalog", true)]
    [InlineData("/catalogue", "/catalog", false)]
    [InlineData("/cart", "/catalog", false)]
    [InlineData("/contacts", "/contacts", true)]
    [InlineData("/checkout", "/cart", false)]
    [InlineData("/", "/", true)]
    [InlineData("", "/", true)]
    [InlineData("/catalog", "/", false)]
    [InlineData("/visualizer#top", "/visualizer", true)]
    public void IsActive_matches_section(string current, string link, bool expected)
        => Assert.Equal(expected, NavMatch.IsActive(current, link));

    [Fact]
    public void BrowserPathAndQuery_uses_the_request_url_normally()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/catalog";
        ctx.Request.QueryString = new QueryString("?page=2");

        Assert.Equal("/catalog?page=2", NavMatch.BrowserPathAndQuery(ctx));
    }

    [Fact]
    public void BrowserPathAndQuery_uses_the_original_url_on_a_reexecuted_status_page()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/not-found";
        ctx.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = "/nope",
            OriginalQueryString = "?a=1"
        });

        Assert.Equal("/nope?a=1", NavMatch.BrowserPathAndQuery(ctx));
    }

    [Fact]
    public void BrowserPathAndQuery_uses_the_failed_path_on_the_error_page()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/error";
        ctx.Request.QueryString = new QueryString("?x=1");
        ctx.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Path = "/catalog",
            Error = new InvalidOperationException()
        });

        Assert.Equal("/catalog?x=1", NavMatch.BrowserPathAndQuery(ctx));
    }

    [Theory]
    [InlineData("//evil.example/x", "/evil.example/x")]
    [InlineData("/\\evil.example", "/evil.example")]
    public void BrowserPathAndQuery_never_returns_a_protocol_relative_url(string path, string expected)
    {
        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = path,
            OriginalQueryString = null
        });

        Assert.Equal(expected, NavMatch.BrowserPathAndQuery(ctx));
    }

    [Theory]
    [InlineData(1, "1 продукт")]
    [InlineData(2, "2 продукта")]
    [InlineData(50, "50 продукта")]
    public void CartCountText_uses_bulgarian_count_form(int count, string expected)
        => Assert.Equal(expected, NavMatch.CartCountText(count));
}

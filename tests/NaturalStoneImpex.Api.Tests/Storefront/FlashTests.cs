using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class FlashTests
{
    private static DefaultHttpContext WithCookie(string? value)
    {
        var ctx = new DefaultHttpContext();
        if (value != null)
            ctx.Request.Headers.Cookie = $"{Flash.CookieName}={value}";
        return ctx;
    }

    [Fact]
    public void Set_writes_short_lived_httponly_secure_lax_cookie()
    {
        var ctx = new DefaultHttpContext();
        Flash.Set(ctx.Response, Flash.Added);
        var header = ctx.Response.Headers.SetCookie.ToString().ToLowerInvariant();

        Assert.Contains("nsi_flash=added", header);
        Assert.Contains("max-age=60", header);
        Assert.Contains("httponly", header);
        Assert.Contains("secure", header);
        Assert.Contains("samesite=lax", header);
        Assert.Contains("path=/", header);
    }

    [Theory]
    [InlineData("added")]
    [InlineData("removed")]
    [InlineData("full")]
    public void Take_returns_known_code_and_deletes_cookie(string code)
    {
        var ctx = WithCookie(code);

        Assert.Equal(code, Flash.Take(ctx));
        var header = ctx.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        Assert.Contains("nsi_flash=;", header);
        Assert.Contains("1970", header);
    }

    [Fact]
    public void Take_returns_null_for_unknown_value_and_still_deletes_cookie()
    {
        var ctx = WithCookie("<script>");

        Assert.Null(Flash.Take(ctx));
        Assert.Contains("nsi_flash=;", ctx.Response.Headers.SetCookie.ToString().ToLowerInvariant());
    }

    [Fact]
    public void Take_returns_null_without_cookie()
    {
        var ctx = WithCookie(null);

        Assert.Null(Flash.Take(ctx));
        Assert.Empty(ctx.Response.Headers.SetCookie.ToString());
    }
}

using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CartStoreTests
{
    private sealed class Accessor(HttpContext ctx) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = ctx;
    }

    private static (CartStore Store, DefaultHttpContext Ctx) Create(string? cookie = null)
    {
        var ctx = new DefaultHttpContext();
        if (cookie != null)
            ctx.Request.Headers.Cookie = $"{CartCookie.Name}={cookie}";
        return (new CartStore(new Accessor(ctx)), ctx);
    }

    private static string SetCookie(HttpContext ctx) => ctx.Response.Headers.SetCookie.ToString();

    [Fact]
    public void Add_merges_and_writes_cookie()
    {
        var (store, ctx) = Create("12:1");
        store.Add(12, 2.5m);
        store.Add(7, 1m);

        Assert.Equal(2, store.Count);
        Assert.Equal(3.5m, store.Lines.Single(l => l.ProductId == 12).Quantity);
        Assert.Contains("nsi_cart=12:3.5|7:1", Uri.UnescapeDataString(SetCookie(ctx)));
    }

    [Fact]
    public void Cookie_is_httponly_secure_lax_30_days()
    {
        var (store, ctx) = Create();
        store.Add(1, 1m);
        var header = SetCookie(ctx).ToLowerInvariant();

        Assert.Contains("httponly", header);
        Assert.Contains("secure", header);
        Assert.Contains("samesite=lax", header);
        Assert.Contains("path=/", header);
        var expires = header.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires="))["expires=".Length..];
        var days = (DateTimeOffset.Parse(expires) - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(days, 29.9, 30.1);
    }

    [Fact]
    public void Clear_expires_cookie()
    {
        var (store, ctx) = Create("1:1");
        store.Clear();

        Assert.Empty(store.Lines);
        var header = SetCookie(ctx).ToLowerInvariant();
        Assert.Contains("nsi_cart=;", header);
        Assert.Contains("1970", header);
    }

    [Fact]
    public void SetQuantity_and_Remove_update_lines()
    {
        var (store, _) = Create("1:1|2:2");
        store.SetQuantity(1, 5m);
        store.SetQuantity(99, 5m); // unknown line: no-op
        store.Remove(2);

        Assert.Equal(new[] { new CartLine(1, 5m) }, store.Lines);
    }

    private static string FullCookie() => string.Join("|", Enumerable.Range(1, CartCookie.MaxLines).Select(i => $"{i}:1"));

    [Fact]
    public void Add_new_product_on_full_cart_returns_false_and_leaves_cookie_unchanged()
    {
        var (store, ctx) = Create(FullCookie());

        Assert.False(store.Add(9999, 1m));
        Assert.Equal(CartCookie.MaxLines, store.Count);
        Assert.Empty(SetCookie(ctx));
    }

    [Fact]
    public void Add_existing_product_on_full_cart_returns_true()
    {
        var (store, _) = Create(FullCookie());

        Assert.True(store.Add(3, 2m));
        Assert.Equal(3m, store.Lines.Single(l => l.ProductId == 3).Quantity);
    }

    [Fact]
    public void Add_clamps_to_max_quantity()
    {
        var (store, _) = Create();
        store.Add(1, 9000m);

        Assert.True(store.Add(1, 5000m));
        Assert.Equal(QuantityCalculator.MaxQuantity, store.Lines.Single().Quantity);
    }
}

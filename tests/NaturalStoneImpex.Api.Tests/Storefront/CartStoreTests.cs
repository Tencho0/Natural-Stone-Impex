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

    [Fact]
    public void Update_with_step_adds_and_floors_at_one_cent()
    {
        var (store, _) = Create("1:1|2:2.5");
        store.Update(1, null, -1);
        store.Update(2, null, 1);

        Assert.Equal(new[] { new CartLine(1, 0.01m), new CartLine(2, 3.5m) }, store.Lines);
    }

    [Fact]
    public void Update_applies_step_to_a_valid_typed_quantity_else_to_the_stored_one()
    {
        var (store, _) = Create("1:1|2:2|3:3");
        store.Update(1, 4.5m, 1);   // typed 4,5 then "+": 5.5
        store.Update(2, 0m, 1);     // typed 0 is invalid: stored 2 + 1
        store.Update(3, 20000m, -1); // typed above the cap is invalid: stored 3 - 1

        Assert.Equal(new[] { new CartLine(1, 5.5m), new CartLine(2, 3m), new CartLine(3, 2m) }, store.Lines);
    }

    [Fact]
    public void Update_without_step_sets_a_valid_quantity_rounded_to_cents()
    {
        var (store, _) = Create("1:1");
        store.Update(1, 2.555m, 0);

        Assert.Equal(2.56m, store.Lines.Single().Quantity);
    }

    [Fact]
    public void Update_without_step_ignores_zero_and_out_of_range_quantities()
    {
        var (store, ctx) = Create("1:1");
        store.Update(1, 0m, 0);
        store.Update(1, 0.004m, 0); // rounds to 0
        store.Update(1, 10000.01m, 0);
        store.Update(1, null, 0);
        store.Update(99, 5m, 0);  // unknown line

        Assert.Equal(1m, store.Lines.Single().Quantity);
        Assert.Empty(SetCookie(ctx));
    }

    [Fact]
    public void Update_with_step_caps_at_max_quantity()
    {
        var (store, _) = Create($"1:{QuantityCalculator.MaxQuantity}");
        store.Update(1, null, 1);

        Assert.Equal(QuantityCalculator.MaxQuantity, store.Lines.Single().Quantity);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("-1", -1)]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("2", 0)]
    [InlineData("+1", 0)]
    [InlineData("0.5", 0)]
    [InlineData("-10", 0)]
    [InlineData("abc", 0)]
    public void ParseStep_accepts_only_minus_one_and_one(string? text, int expected)
        => Assert.Equal(expected, CartStore.ParseStep(text));
}

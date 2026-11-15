using System.Globalization;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CartCookieTests
{
    [Fact]
    public void Round_trips_lines()
    {
        var lines = new[] { new CartLine(12, 13.2m), new CartLine(7, 1m) };
        Assert.Equal(lines, CartCookie.Parse(CartCookie.Serialize(lines)));
    }

    [Fact]
    public void Uses_invariant_decimal_point()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("bg-BG");
            Assert.Equal("12:13.2", CartCookie.Serialize(new[] { new CartLine(12, 13.2m) }));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Garbage_returns_empty()
    {
        Assert.Empty(CartCookie.Parse("%%%"));
        Assert.Empty(CartCookie.Parse(null));
        Assert.Empty(CartCookie.Parse(""));
    }

    [Fact]
    public void Drops_invalid_quantities()
        => Assert.Empty(CartCookie.Parse("1:-2|2:0|3:abc|4:20000"));

    [Fact]
    public void Merges_duplicate_ids()
        => Assert.Equal(new[] { new CartLine(5, 3.5m) }, CartCookie.Parse("5:1|5:2.5"));

    [Fact]
    public void Caps_at_50_lines()
    {
        var value = string.Join("|", Enumerable.Range(1, 500).Select(i => $"{i}:1"));
        Assert.Equal(CartCookie.MaxLines, CartCookie.Parse(value).Count);
    }

    [Fact]
    public void Rounds_quantities_to_cents_and_drops_those_that_round_to_zero()
        => Assert.Equal(new[] { new CartLine(1, 2.56m) }, CartCookie.Parse("1:2.555|2:0.004"));
}

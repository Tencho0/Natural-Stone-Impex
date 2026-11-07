using System.Globalization;
using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class PriceFormatTests
{
    private static void UnderBg(Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("bg-BG");
        try { action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(1204.65, "1204.65 €")]
    [InlineData(0.41, "0.41 €")]
    [InlineData(20, "20.00 €")]
    public void Money_uses_dot_and_two_decimals(decimal v, string expected)
    {
        UnderBg(() => Assert.Equal(expected, PriceFormat.Money(v)));
    }

    [Fact]
    public void Quantity_uses_dot_and_unit()
    {
        UnderBg(() => Assert.Equal("13.20 м²", PriceFormat.Quantity(13.2m, "м²")));
    }

    [Fact]
    public void Signed_negative_uses_unicode_minus()
    {
        UnderBg(() => Assert.Equal("\u22128.20 м²", PriceFormat.Signed(-8.2m, "м²")));
    }

    [Fact]
    public void Signed_positive_has_no_sign()
    {
        UnderBg(() => Assert.Equal("8.20 м²", PriceFormat.Signed(8.2m, "м²")));
    }
}

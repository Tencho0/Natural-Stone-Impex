using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class SaleUnitsTests
{
    [Fact]
    public void Only_square_metres_are_whole()
    {
        Assert.True(SaleUnits.IsWhole((int)UnitType.Sqm));
        Assert.False(SaleUnits.IsWhole((int)UnitType.Kg));
    }

    [Theory]
    [InlineData("12.5", true, "13")]
    [InlineData("12.01", true, "13")]
    [InlineData("12.004", true, "12")] // below a cent: the cent rounding comes first, as in the cookie
    [InlineData("12", true, "12")]
    [InlineData("0.3", true, "1")]
    [InlineData("2.555", false, "2.56")]
    [InlineData("2.5", false, "2.5")]
    public void Normalize_rounds_whole_units_up(string quantity, bool whole, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            SaleUnits.Normalize(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), whole));

    [Fact]
    public void Formats_whole_units_without_decimals()
    {
        Assert.Equal("13 м²", SaleUnits.Quantity(13m, "м²", whole: true));
        Assert.Equal("13", SaleUnits.Number(13.00m, whole: true));
        Assert.Equal("2.50 кг", SaleUnits.Quantity(2.5m, "кг", whole: false));
    }

    [Fact]
    public void Minimum_is_one_square_metre_or_one_hundredth_of_a_kilogram()
    {
        Assert.Equal(1m, SaleUnits.Min(whole: true));
        Assert.Equal(0.01m, SaleUnits.Min(whole: false));
    }
}

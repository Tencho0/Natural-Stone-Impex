using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Tests.Admin;

public class ProductFormatTests
{
    [Theory]
    [InlineData(0, "0 продукта")]
    [InlineData(1, "1 продукт")]
    [InlineData(2, "2 продукта")]
    [InlineData(115, "115 продукта")]
    public void Count_text_uses_the_singular_only_for_one(int count, string expected)
    {
        Assert.Equal(expected, ProductFormat.CountText(count));
    }

    [Theory]
    [InlineData(-8.2, "м²", "−8.20 м²")]
    [InlineData(0, "кг", "0.00 кг")]
    [InlineData(12.5, "кг", "12.50 кг")]
    public void Stock_in_the_list_uses_signed_quantity(double stock, string unit, string expected)
    {
        Assert.Equal(expected, PriceFormat.Signed((decimal)stock, unit));
    }
}

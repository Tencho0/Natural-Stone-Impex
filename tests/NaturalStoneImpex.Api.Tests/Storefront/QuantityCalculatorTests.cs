using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class QuantityCalculatorTests
{
    private static QuantityInput Dims(string? l, string? w, bool waste) =>
        new(QuantityMode.Dimensions, l, w, null, null, waste);

    private static QuantityInput Area(string? a, bool waste) =>
        new(QuantityMode.Area, null, null, a, null, waste);

    private static QuantityInput Kg(string? k, bool waste) =>
        new(QuantityMode.Kilograms, null, null, null, k, waste);

    [Fact]
    public void Dimensions_with_waste_4x3_is_13_20()
    {
        var r = QuantityCalculator.Calculate(Dims("4,00", "3,00", true));
        Assert.True(r.IsValid);
        Assert.Equal(13.20m, r.Quantity);
    }

    [Fact]
    public void Dimensions_without_waste_4x3_is_12()
    {
        var r = QuantityCalculator.Calculate(Dims("4,00", "3,00", false));
        Assert.True(r.IsValid);
        Assert.Equal(12.00m, r.Quantity);
    }

    [Fact]
    public void Area_with_waste_rounds_up_to_cent()
    {
        var r = QuantityCalculator.Calculate(Area("10,01", true));
        Assert.True(r.IsValid);
        Assert.Equal(11.02m, r.Quantity);
    }

    [Fact]
    public void Kilograms_ignore_waste()
    {
        var r = QuantityCalculator.Calculate(Kg("25", true));
        Assert.True(r.IsValid);
        Assert.Equal(25m, r.Quantity);
    }

    [Fact]
    public void Missing_width_reports_width_field()
    {
        var r = QuantityCalculator.Calculate(Dims("4", "", false));
        Assert.False(r.IsValid);
        Assert.Equal("Width", r.ErrorField);
        Assert.Equal("Въведете ширина в метри.", r.Error);
    }

    [Fact]
    public void Zero_or_negative_is_invalid()
    {
        var r = QuantityCalculator.Calculate(Area("0", false));
        Assert.False(r.IsValid);
        Assert.Equal("Area", r.ErrorField);
    }

    [Fact]
    public void Above_max_is_invalid()
    {
        var r = QuantityCalculator.Calculate(Area("20000", false));
        Assert.False(r.IsValid);
        Assert.Equal("Количеството е твърде голямо.", r.Error);
    }
}

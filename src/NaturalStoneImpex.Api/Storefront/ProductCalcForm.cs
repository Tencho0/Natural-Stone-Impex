namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// The product page's add-to-cart form ([SupplyParameterFromForm] Calc, form "add-to-cart"). The inputs stay
/// strings so the page echoes exactly what was typed; <see cref="QuantityCalculator"/> parses them.
/// </summary>
public sealed class ProductCalcForm
{
    public const string DimensionsMode = "dims";
    public const string AreaMode = "area";

    /// <summary>"dims" or "area" for m² products; anything else counts as "dims". Ignored for kg products.</summary>
    public string? Mode { get; set; }
    public string? Length { get; set; }
    public string? Width { get; set; }
    public string? Area { get; set; }
    public string? Kilograms { get; set; }

    /// <summary>
    /// False unless posted: an unchecked box sends nothing, so a bound form must not default to true.
    /// The "on by default" state of the first render comes from <see cref="Defaults"/>.
    /// </summary>
    public bool Waste { get; set; }

    public bool IsArea => Mode == AreaMode;

    /// <summary>The first render: 4 × 3 m with the waste allowance, which gives 13.20 м².</summary>
    public static ProductCalcForm Defaults() => new()
    {
        Mode = DimensionsMode,
        Length = "4,00",
        Width = "3,00",
        Area = "12,00",
        Kilograms = "1,00",
        Waste = true
    };

    public QuantityInput ToInput(bool kilograms)
    {
        var mode = kilograms ? QuantityMode.Kilograms
            : IsArea ? QuantityMode.Area
            : QuantityMode.Dimensions;
        return new QuantityInput(mode, Length, Width, Area, Kilograms, Waste);
    }
}

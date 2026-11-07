using System.Globalization;

namespace NaturalStoneImpex.Client.Formatting;

/// <summary>Invariant-culture money and quantity formatting: "12.50 €", "13.20 м²".</summary>
public static class PriceFormat
{
    private const string Format = "0.00";

    public static string Money(decimal value) =>
        value.ToString(Format, CultureInfo.InvariantCulture) + " €";

    public static string Quantity(decimal value, string unitDisplay) =>
        value.ToString(Format, CultureInfo.InvariantCulture) + " " + unitDisplay;

    /// <summary>Like <see cref="Quantity"/>, but negatives use the Unicode minus sign (U+2212).</summary>
    public static string Signed(decimal value, string unitDisplay)
    {
        var abs = Math.Abs(value).ToString(Format, CultureInfo.InvariantCulture);
        var sign = value < 0 && abs != Format ? "\u2212" : "";
        return sign + abs + " " + unitDisplay;
    }
}

using System.Globalization;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Parses user-typed non-negative decimals in Bulgarian ("1 204,5") or invariant ("4.5") form.</summary>
public static class DecimalInput
{
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var cleaned = text
            .Replace(" ", "")
            .Replace("\u00A0", "")
            .Replace("\u202F", "")
            .Replace(',', '.');

        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}

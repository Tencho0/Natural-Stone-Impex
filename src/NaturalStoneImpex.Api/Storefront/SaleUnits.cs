using System.Globalization;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// How much of a unit can be bought. Square metres sell in whole units only, so any m² quantity is rounded up
/// (12.5 m² of floor needs 13 m² of stone). Kilograms keep two decimals, the precision of OrderItem.Quantity.
/// </summary>
public static class SaleUnits
{
    public const string WholeNote = "Продава се на цели м². Закръгляме нагоре.";
    public const string NotWholeError = "Продуктите на м² се поръчват в цели квадратни метри.";

    public static bool IsWhole(int unit) => unit == (int)UnitType.Sqm;

    /// <summary>The smallest quantity a cart line can hold: 1 m², or one hundredth of a kilogram.</summary>
    public static decimal Min(bool whole) => whole ? 1m : 0.01m;

    /// <summary>Whole units round up; otherwise whole hundredths, half away from zero (CartCookie.Normalize).</summary>
    public static decimal Normalize(decimal quantity, bool whole)
    {
        var cents = CartCookie.Normalize(quantity);
        return whole ? Math.Ceiling(cents) : cents;
    }

    /// <summary>"13" for whole units, "2.50" otherwise: the value of a quantity field.</summary>
    public static string Number(decimal quantity, bool whole)
        => whole ? quantity.ToString("0", CultureInfo.InvariantCulture) : PriceFormat.Number(quantity);

    /// <summary>"13 м²" or "2.50 кг".</summary>
    public static string Quantity(decimal quantity, string unitDisplay, bool whole)
        => Number(quantity, whole) + " " + unitDisplay;
}

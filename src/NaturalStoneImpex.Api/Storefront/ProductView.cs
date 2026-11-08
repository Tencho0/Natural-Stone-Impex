using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Text on the product page (spec §7.3) that does not belong in markup: availability, price notes, labels.</summary>
public static class ProductView
{
    public const string BackOrder = "Доставка по заявка. Ще ви се обадим за срок.";
    public const string AddPlain = "Добави в количката";
    public const string MissingSqm = "Попълнете размерите, за да видите сумата.";
    public const string MissingArea = "Попълнете площта, за да видите сумата.";
    public const string MissingKg = "Попълнете количеството, за да видите сумата.";

    /// <summary>Where calculator.js puts its number in the templates below.</summary>
    public const string Placeholder = "{0}";

    private const string AddFormat = "Добави {0} в количката";
    private const string Zero = "0.00"; // what PriceFormat prints for 0, swapped for the placeholder

    public static bool IsKilograms(ProductDto p) => p.Unit == (int)UnitType.Kg;

    /// <summary>"Гнайс, продава се на м²".</summary>
    public static string SaleUnit(ProductDto p) => $"{p.CategoryName}, продава се на {p.UnitDisplay}";

    /// <summary>"за м², с ДДС (3.41 € ДДС)": the VAT amount is named once.</summary>
    public static string PriceNote(ProductDto p) => p.VatAmount > 0
        ? $"за {p.UnitDisplay}, с ДДС ({PriceFormat.Money(p.VatAmount)} ДДС)"
        : $"за {p.UnitDisplay}, с ДДС";

    /// <summary>Stock never blocks ordering, and a negative number is never shown.</summary>
    public static string Availability(decimal stock, string unitDisplay)
        => stock > 0 ? $"В наличност: {PriceFormat.Quantity(stock, unitDisplay)}" : BackOrder;

    public static string MetaDescription(ProductDto p) => p.PriceWithVat > 0
        ? $"{p.CategoryName}, {PriceFormat.Money(p.PriceWithVat)}/{p.UnitDisplay} с ДДС."
        : $"{p.CategoryName}. Цена при запитване.";

    /// <summary>"Добави 13.20 м² в количката", or the plain label while the input is incomplete.</summary>
    public static string AddLabel(QuantityResult result, string unitDisplay) => result.IsValid
        ? string.Format(AddFormat, PriceFormat.Quantity(result.Quantity, unitDisplay))
        : AddPlain;

    // Templates for calculator.js, built from the same formats, so the page and the script cannot drift apart.

    /// <summary>"{0} м²": PriceFormat.Quantity with a placeholder for the number.</summary>
    public static string QuantityTemplate(string unitDisplay)
        => PriceFormat.Quantity(0m, unitDisplay).Replace(Zero, Placeholder);

    /// <summary>"{0} €": PriceFormat.Money with a placeholder for the number.</summary>
    public static string MoneyTemplate => PriceFormat.Money(0m).Replace(Zero, Placeholder);

    /// <summary>"Добави {0} м² в количката".</summary>
    public static string AddTemplate(string unitDisplay) => string.Format(AddFormat, QuantityTemplate(unitDisplay));

    public static string? Total(QuantityResult result, decimal priceWithVat)
        => result.IsValid ? PriceFormat.Money(result.Quantity * priceWithVat) : null;

    /// <summary>The result line's text while there is no quantity. Field errors appear under the fields.</summary>
    public static string ResultNote(QuantityResult result, QuantityMode mode)
        => result.Error == QuantityCalculator.TooLargeError ? QuantityCalculator.TooLargeError : MissingNote(mode);

    public static string MissingNote(QuantityMode mode) => mode switch
    {
        QuantityMode.Area => MissingArea,
        QuantityMode.Kilograms => MissingKg,
        _ => MissingSqm,
    };
}

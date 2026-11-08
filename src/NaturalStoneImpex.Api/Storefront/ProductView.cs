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
    public const string MissingKg = "Попълнете количеството, за да видите сумата.";

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
        ? $"Добави {PriceFormat.Quantity(result.Quantity, unitDisplay)} в количката"
        : AddPlain;

    public static string? Total(QuantityResult result, decimal priceWithVat)
        => result.IsValid ? PriceFormat.Money(result.Quantity * priceWithVat) : null;

    /// <summary>The result line's text while there is no quantity. Field errors appear under the fields.</summary>
    public static string ResultNote(QuantityResult result, bool kilograms)
        => result.Error == QuantityCalculator.TooLargeError ? QuantityCalculator.TooLargeError
            : kilograms ? MissingKg : MissingSqm;
}

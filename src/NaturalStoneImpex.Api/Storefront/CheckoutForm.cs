using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// The checkout form ([SupplyParameterFromForm] Form, form "checkout"), ported from the old Client checkout:
/// the same fields, rules and Bulgarian messages, plus the ЕИК check (9 or 13 digits). The fields of the
/// customer type that is not chosen, and the address on pickup, are kept for the re-render but never checked
/// or sent.
/// </summary>
public sealed partial class CheckoutForm : IValidatableObject
{
    public const int Individual = 0;
    public const int Company = 1;
    public const int Pickup = 0;
    public const int Delivery = 1;

    public const string RequiredMessage = "Полето е задължително.";
    public const string EikMessage = "ЕИК трябва да съдържа 9 или 13 цифри.";
    private const string CustomerTypeMessage = "Моля, изберете тип клиент.";
    private const string DeliveryMethodMessage = "Моля, изберете метод на получаване.";

    [Required(ErrorMessage = CustomerTypeMessage), Range(Individual, Company, ErrorMessage = CustomerTypeMessage)]
    public int? CustomerType { get; set; }

    [Required(ErrorMessage = DeliveryMethodMessage), Range(Pickup, Delivery, ErrorMessage = DeliveryMethodMessage)]
    public int? DeliveryMethod { get; set; }

    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Eik { get; set; }
    public string? Mol { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    /// <summary>One-time token from the GET (ruling R24): a repeated POST returns the first outcome.</summary>
    public string? Token { get; set; }

    /// <summary>The orderable cart rows the form was shown with (<see cref="Stamp"/>).</summary>
    public string? CartStamp { get; set; }

    public bool IsCompany => CustomerType == Company;
    public bool IsDelivery => DeliveryMethod == Delivery;

    /// <summary>The first render: an individual, pickup, and a new token. The page adds the cart stamp.</summary>
    public static CheckoutForm Start() => new()
    {
        CustomerType = Individual,
        DeliveryMethod = Pickup,
        Token = CheckoutGuard.NewToken()
    };

    // Runs only when CustomerType and DeliveryMethod passed their attributes, so both are 0 or 1 here.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsCompany)
        {
            if (Missing(CompanyName)) yield return Required(nameof(CompanyName));
            if (Missing(Eik)) yield return Required(nameof(Eik));
            else if (!EikPattern().IsMatch(Eik!.Trim())) yield return new ValidationResult(EikMessage, [nameof(Eik)]);
            if (Missing(Mol)) yield return Required(nameof(Mol));
            if (Missing(ContactPerson)) yield return Required(nameof(ContactPerson));
            if (Missing(ContactPhone)) yield return Required(nameof(ContactPhone));
        }
        else
        {
            if (Missing(FullName)) yield return Required(nameof(FullName));
            if (Missing(Phone)) yield return Required(nameof(Phone));
        }

        if (IsDelivery && Missing(Address))
            yield return Required(nameof(Address));
    }

    /// <summary>The old checkout's mapping: trimmed values, only the chosen type's fields, the address only on delivery.</summary>
    public CreateOrderRequest ToRequest(List<CreateOrderItemRequest> items)
    {
        var company = IsCompany;
        return new CreateOrderRequest
        {
            CustomerType = CustomerType ?? Individual,
            DeliveryMethod = DeliveryMethod ?? Pickup,
            CustomerInfo = new CreateOrderCustomerInfoRequest
            {
                FullName = company ? null : FullName?.Trim(),
                Phone = company ? null : Phone?.Trim(),
                CompanyName = company ? CompanyName?.Trim() : null,
                Eik = company ? Eik?.Trim() : null,
                Mol = company ? Mol?.Trim() : null,
                ContactPerson = company ? ContactPerson?.Trim() : null,
                ContactPhone = company ? ContactPhone?.Trim() : null,
                Address = IsDelivery ? Address?.Trim() : null
            },
            Items = items
        };
    }

    /// <summary>
    /// A short fingerprint of what the order would contain (orderable rows: id, quantity, price with VAT).
    /// The page renders it with the form; a POST whose cart no longer matches is shown again instead of ordered.
    /// </summary>
    public static string Stamp(CartViewModel cart)
    {
        var text = string.Join(';', cart.Rows.Where(r => r.Orderable).Select(r => string.Create(
            CultureInfo.InvariantCulture, $"{r.Product.Id}:{r.Quantity:0.00}:{r.Product.PriceWithVat:0.00}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 8);
    }

    private static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);

    private static ValidationResult Required(string member) => new(RequiredMessage, [member]);

    // ECMAScript: \d is 0-9 only, not every Unicode digit.
    [GeneratedRegex(@"^\d{9}(\d{4})?$", RegexOptions.ECMAScript)]
    private static partial Regex EikPattern();
}

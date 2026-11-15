using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// The checkout form ([SupplyParameterFromForm] Form, form "checkout"), ported from the old Client checkout:
/// the same fields, rules and Bulgarian messages, plus the ЕИК check (9 or 13 digits) and the database column
/// lengths. Nothing is preselected: the customer type and the delivery method must be chosen (ruling R27).
/// The fields of the customer type that is not chosen, and the address on pickup, are kept for the re-render
/// but never required or sent.
/// </summary>
public sealed partial class CheckoutForm : IValidatableObject
{
    public const int Individual = 0;
    public const int Company = 1;
    public const int Pickup = 0;
    public const int Delivery = 1;

    public const string RequiredMessage = "Полето е задължително.";
    public const string EikMessage = "ЕИК трябва да съдържа 9 или 13 цифри.";
    public const string CustomerTypeMessage = "Моля, изберете тип клиент.";
    public const string DeliveryMethodMessage = "Моля, изберете метод на получаване.";
    private const string LengthMessage = "Полето може да съдържа най-много {1} знака.";

    // Checked in Validate, not by attributes: a missing choice must not hide the other errors (the old form
    // showed them together).
    public int? CustomerType { get; set; }
    public int? DeliveryMethod { get; set; }

    // Lengths as in AppDbContext (OrderCustomerInfo), so a long post is a field error, not a failed save.
    [StringLength(200, ErrorMessage = LengthMessage)] public string? FullName { get; set; }
    [StringLength(20, ErrorMessage = LengthMessage)] public string? Phone { get; set; }
    [StringLength(200, ErrorMessage = LengthMessage)] public string? CompanyName { get; set; }
    public string? Eik { get; set; } // nvarchar(13): bounded by the 9-or-13-digit check, whose message is clearer
    [StringLength(200, ErrorMessage = LengthMessage)] public string? Mol { get; set; }
    [StringLength(200, ErrorMessage = LengthMessage)] public string? ContactPerson { get; set; }
    [StringLength(20, ErrorMessage = LengthMessage)] public string? ContactPhone { get; set; }
    [StringLength(500, ErrorMessage = LengthMessage)] public string? Address { get; set; }

    /// <summary>One-time token from the GET (ruling R24): a repeated POST returns the first outcome.</summary>
    public string? Token { get; set; }

    /// <summary>The orderable cart rows the form was shown with (<see cref="Stamp"/>).</summary>
    public string? CartStamp { get; set; }

    public bool IsCompany => CustomerType == Company;
    public bool IsDelivery => DeliveryMethod == Delivery;

    /// <summary>The first render: nothing chosen yet, and a new token. The page adds the cart stamp.</summary>
    public static CheckoutForm Start() => new() { Token = CheckoutGuard.NewToken() };

    // Runs once the length attributes pass. In form order: type, that type's fields, delivery, address.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerType is not (Individual or Company))
            yield return new ValidationResult(CustomerTypeMessage, [nameof(CustomerType)]);
        else if (IsCompany)
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

        if (DeliveryMethod is not (Pickup or Delivery))
            yield return new ValidationResult(DeliveryMethodMessage, [nameof(DeliveryMethod)]);
        else if (IsDelivery && Missing(Address))
            yield return Required(nameof(Address));
    }

    /// <summary>The old checkout's mapping: trimmed values, only the chosen type's fields, the address only on delivery.</summary>
    public CreateOrderRequest ToRequest(List<CreateOrderItemRequest> items)
    {
        var company = IsCompany;
        return new CreateOrderRequest
        {
            // Never null after validation; -1 would be rejected by OrderService, not guessed.
            CustomerType = CustomerType ?? -1,
            DeliveryMethod = DeliveryMethod ?? -1,
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

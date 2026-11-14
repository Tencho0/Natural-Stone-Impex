namespace NaturalStoneImpex.Client;

/// <summary>
/// Shop contact details: the storefront footer, Contacts, the error and confirmation pages, and the admin receipt.
/// In the Client project so the server and WebAssembly share one source (like PriceFormat).
/// </summary>
public static class ShopInfo
{
    // TODO(owner): real contact data. These are the placeholders from the old Contacts page.
    public const string Name = "Natural Stone Impex";
    public const string Phone = "+359 888 123 456";
    public const string PhoneHref = "tel:+359888123456";
    public const string Email = "info@naturalstonimpex.bg";
    public const string Address = "ул. Примерна 1, София";
    public static readonly IReadOnlyList<string> Hours =
    [
        "Понеделник – петък, 08:00–17:00",
        "Събота, 09:00–13:00"
    ];
    public const string MapUrl = "https://www.google.com/maps/search/?api=1&query=42.6977,23.3219";
}

using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Storefront;

/// <param name="Error">Bulgarian, shown at the top of the form; null on success.</param>
/// <param name="ClearsCart">On success: whether the caller clears the cart (see <see cref="CheckoutHandler.PlaceAsync"/>).</param>
public sealed record CheckoutOutcome(bool Success, string? OrderNumber, string? Error, bool ClearsCart = false);

/// <summary>
/// Places the checkout order (spec §7.5). Sends only the orderable cart rows (no inactive or price-0 ids;
/// unknown ids are already dropped by <see cref="CartViewModel"/>). Never throws: order service errors come
/// back as <see cref="CheckoutOutcome.Error"/>. On success the caller clears the cart when
/// <see cref="CheckoutOutcome.ClearsCart"/> says so.
/// </summary>
public sealed class CheckoutHandler(IOrderService orders, CheckoutGuard guard, ILogger<CheckoutHandler> logger)
{
    public const string EmptyCartError = "Количката е празна.";
    public const string CartChangedError =
        "Количката се промени, докато попълвахте данните. Прегледайте обобщението и изпратете поръчката отново.";
    // Under the heading "Поръчката не е изпратена".
    public const string UnexpectedError = "Опитайте отново или ни се обадете.";

    public async Task<CheckoutOutcome> PlaceAsync(CheckoutForm form, CartViewModel cart)
    {
        var (outcome, replayed) = await guard.RunOnceAsync(form.Token, () => PlaceNowAsync(form, cart));
        // A replayed token (double-click, duplicated tab, restored form) confirms the order it placed. The cart is
        // cleared only if it is still the one that was ordered (or empty), never a cart filled since.
        return replayed && outcome.Success
            ? outcome with { ClearsCart = cart.Rows.Count == 0 || CheckoutForm.Stamp(cart) == form.CartStamp }
            : outcome;
    }

    private async Task<CheckoutOutcome> PlaceNowAsync(CheckoutForm form, CartViewModel cart)
    {
        var items = cart.Rows
            .Where(r => r.Orderable)
            .Select(r => new CreateOrderItemRequest { ProductId = r.Product.Id, Quantity = r.Quantity })
            .ToList();
        if (items.Count == 0)
            return new CheckoutOutcome(false, null, EmptyCartError);

        // A form from before the cart changed (another tab, a price change, a product out of sale) is not
        // ordered silently. A form without a stamp (stale markup) is.
        if (form.CartStamp is not null && form.CartStamp != CheckoutForm.Stamp(cart))
            return new CheckoutOutcome(false, null, CartChangedError);

        try
        {
            var response = await orders.CreateAsync(form.ToRequest(items));
            return new CheckoutOutcome(true, response.OrderNumber, null, ClearsCart: true);
        }
        catch (InvalidOperationException ex)
        {
            // OrderService's validation messages are Bulgarian (the same ones the API returns).
            return new CheckoutOutcome(false, null, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Checkout order could not be created.");
            return new CheckoutOutcome(false, null, UnexpectedError);
        }
    }
}

using NaturalStoneImpex.Client.Formatting;
using NaturalStoneImpex.Client.Models;
using NaturalStoneImpex.Client.Services;

namespace NaturalStoneImpex.Client.Components;

/// <summary>
/// The inline confirm for "Потвърди" on an order. Stock never blocks a confirmation (owner decision, 07.11.2026),
/// so the stock check only adds one warning line per product that would go below zero.
/// </summary>
public static class OrderConfirmPrompt
{
    public const string FailureText = "Поръчката не е потвърдена. Проверете връзката и опитайте отново.";

    public static async Task<ConfirmPrompt> BuildAsync(IOrderService orderService, int orderId)
    {
        try
        {
            return From(await orderService.GetStockShortagesAsync(orderId));
        }
        catch
        {
            // The check is advisory: say it did not run, and still let the admin confirm.
            return new ConfirmPrompt("Потвърди поръчката?", ["Не успяхме да проверим наличността."]);
        }
    }

    public static ConfirmPrompt From(IReadOnlyList<StockShortageDetail> shortages) =>
        shortages.Count == 0
            ? new ConfirmPrompt("Потвърди поръчката?")
            : new ConfirmPrompt("Потвърди?", shortages.Select(s => OrderFormat.ShortageLine(s) + ".").ToList());
}

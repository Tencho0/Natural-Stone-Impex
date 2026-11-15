using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Formatting;

/// <summary>Admin order text: status label and marker, received time, and the stock warning of the inline confirm.</summary>
public static class OrderFormat
{
    public static string StatusText(int status, bool isCancelled) =>
        isCancelled ? "Отказана" : status switch
        {
            0 => "Чакаща",
            1 => "Потвърдена",
            2 => "Завършена",
            _ => string.Empty
        };

    /// <summary>Classes for admin.css .status: pending needs action, confirmed is under way, completed recedes.</summary>
    public static string StatusCss(int status, bool isCancelled) =>
        isCancelled ? "status status--danger" : status switch
        {
            0 => "status status--warn",
            1 => "status status--ok",
            _ => "status"
        };

    /// <summary>"Металик 5L ще стане −8.20 м²": the product's stock once the order is confirmed.</summary>
    public static string ShortageLine(StockShortageDetail detail) =>
        $"{detail.ProductName} ще стане {PriceFormat.Signed(detail.Available - detail.Ordered, detail.Unit)}";

    /// <summary>A UTC timestamp in the admin's local time: "07.11.2026, 14:20".</summary>
    public static string Timestamp(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy, HH:mm");
}

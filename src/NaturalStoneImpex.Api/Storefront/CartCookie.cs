using System.Globalization;

namespace NaturalStoneImpex.Api.Storefront;

public record CartLine(int ProductId, decimal Quantity);

/// <summary>Reads and writes the cart cookie value. Only product ids and quantities are stored, never prices.</summary>
public static class CartCookie
{
    public const string Name = "nsi_cart";
    public const int MaxLines = 50;

    public static IReadOnlyList<CartLine> Parse(string? value)
    {
        var lines = new List<CartLine>();
        if (string.IsNullOrWhiteSpace(value))
            return lines;

        foreach (var part in value.Split('|'))
        {
            var pair = part.Split(':');
            if (pair.Length != 2
                || !int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
                || !decimal.TryParse(pair[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var raw))
                continue;
            var qty = Normalize(raw);
            if (qty <= 0 || qty > QuantityCalculator.MaxQuantity)
                continue;

            var index = lines.FindIndex(l => l.ProductId == id);
            if (index >= 0)
                lines[index] = lines[index] with { Quantity = Math.Min(lines[index].Quantity + qty, QuantityCalculator.MaxQuantity) };
            else if (lines.Count < MaxLines)
                lines.Add(new CartLine(id, qty));
        }

        return lines;
    }

    /// <summary>
    /// Quantities are kept to whole cents, the precision of OrderItem.Quantity (decimal(18,2)),
    /// so the cart total is the total the order will store.
    /// </summary>
    public static decimal Normalize(decimal quantity)
        => Math.Round(quantity, 2, MidpointRounding.AwayFromZero);

    public static string Serialize(IEnumerable<CartLine> lines)
        => string.Join("|", lines.Select(l =>
            string.Create(CultureInfo.InvariantCulture, $"{l.ProductId}:{l.Quantity}")));
}

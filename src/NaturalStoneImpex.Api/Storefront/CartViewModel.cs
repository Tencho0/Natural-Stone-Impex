using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <param name="LineTotal">Quantity × price with VAT, not rounded (round only for display).</param>
/// <param name="Problem">Why the row cannot be ordered; null when it can.</param>
/// <param name="BackOrder">More than the stock is wanted. The row stays orderable ("доставка по заявка").</param>
public sealed record CartRow(
    ProductListDto Product, decimal Quantity, decimal LineTotal, bool Orderable, string? Problem, bool BackOrder);

/// <summary>
/// The cart page (spec §7.4): cookie lines joined with current DB prices. Totals sum the orderable rows
/// without per-line rounding, the same way OrderService totals an order's items, so the cart shows
/// what the order will store.
/// </summary>
public sealed class CartViewModel
{
    public const string InactiveProblem = "Продуктът вече не се предлага.";
    public const string OnRequestProblem = "Цена при запитване.";
    public const string BackOrderNote = "Доставка по заявка.";

    public IReadOnlyList<CartRow> Rows { get; private init; } = [];
    public decimal SubtotalWithoutVat { get; private init; }
    public decimal Vat { get; private init; }
    public decimal TotalWithVat { get; private init; }
    public bool HasOrderable { get; private init; }

    /// <summary>Rows follow the cart order. Lines whose product id is unknown are dropped.</summary>
    public static CartViewModel Build(IReadOnlyList<CartLine> lines, IReadOnlyList<ProductListDto> products)
    {
        var byId = products.ToDictionary(p => p.Id);
        var rows = new List<CartRow>();
        decimal withoutVat = 0, vat = 0, withVat = 0;

        foreach (var line in lines)
        {
            if (!byId.TryGetValue(line.ProductId, out var product))
                continue;

            var problem = !product.IsActive ? InactiveProblem
                : product.PriceWithVat <= 0 ? OnRequestProblem
                : null;
            var orderable = problem is null;

            rows.Add(new CartRow(
                product,
                line.Quantity,
                line.Quantity * product.PriceWithVat,
                orderable,
                problem,
                BackOrder: orderable && line.Quantity > product.StockQuantity));

            if (!orderable)
                continue;
            withoutVat += line.Quantity * product.PriceWithoutVat;
            vat += line.Quantity * product.VatAmount;
            withVat += line.Quantity * product.PriceWithVat;
        }

        return new CartViewModel
        {
            Rows = rows,
            SubtotalWithoutVat = withoutVat,
            Vat = vat,
            TotalWithVat = withVat,
            HasOrderable = rows.Any(r => r.Orderable)
        };
    }
}

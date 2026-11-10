using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Storefront;
using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CartViewModelTests
{
    private static ProductListDto Product(int id, decimal withoutVat = 17.04m, decimal vat = 3.41m, decimal withVat = 20.45m,
        decimal stock = 100m, bool active = true) => new()
    {
        Id = id,
        Name = $"Продукт {id}",
        PriceWithoutVat = withoutVat,
        VatAmount = vat,
        PriceWithVat = withVat,
        StockQuantity = stock,
        IsActive = active,
        Unit = 1,
        UnitDisplay = "м²"
    };

    [Fact]
    public void Totals_cover_only_orderable_rows()
    {
        var model = CartViewModel.Build(
            [new CartLine(1, 2m), new CartLine(2, 5m), new CartLine(3, 1m)],
            [Product(1), Product(2, active: false), Product(3, 0m, 0m, 0m)]);

        Assert.Equal(34.08m, model.SubtotalWithoutVat);
        Assert.Equal(6.82m, model.Vat);
        Assert.Equal(40.90m, model.TotalWithVat);
        Assert.True(model.HasOrderable);
    }

    [Fact]
    public void Inactive_and_unpriced_rows_are_flagged_not_counted()
    {
        var model = CartViewModel.Build(
            [new CartLine(2, 5m), new CartLine(3, 1m)],
            [Product(2, active: false), Product(3, 0m, 0m, 0m)]);

        var inactive = model.Rows[0];
        Assert.False(inactive.Orderable);
        Assert.Equal("Продуктът вече не се предлага.", inactive.Problem);

        var unpriced = model.Rows[1];
        Assert.False(unpriced.Orderable);
        Assert.Equal("Цена при запитване.", unpriced.Problem);

        Assert.False(model.HasOrderable);
        Assert.Equal(0m, model.TotalWithVat);
        Assert.Equal(0m, model.SubtotalWithoutVat);
        Assert.Equal(0m, model.Vat);
    }

    [Fact]
    public void Inactive_wins_over_unpriced()
    {
        var model = CartViewModel.Build([new CartLine(4, 1m)], [Product(4, 0m, 0m, 0m, active: false)]);

        Assert.Equal("Продуктът вече не се предлага.", model.Rows.Single().Problem);
    }

    [Fact]
    public void Unknown_ids_are_dropped()
    {
        var model = CartViewModel.Build(
            [new CartLine(999999, 2m), new CartLine(1, 1m)],
            [Product(1)]);

        Assert.Equal(1, Assert.Single(model.Rows).Product.Id);
    }

    [Fact]
    public void Rows_keep_cart_order()
    {
        var model = CartViewModel.Build(
            [new CartLine(3, 1m), new CartLine(1, 1m), new CartLine(2, 1m)],
            [Product(1), Product(2), Product(3)]);

        Assert.Equal(new[] { 3, 1, 2 }, model.Rows.Select(r => r.Product.Id));
    }

    [Fact]
    public void Quantity_above_stock_is_backorder_but_orderable()
    {
        var model = CartViewModel.Build(
            [new CartLine(1, 12.5m), new CartLine(2, 10m), new CartLine(3, 1m)],
            [Product(1, stock: 10m), Product(2, stock: 10m), Product(3, stock: -2m)]);

        Assert.True(model.Rows[0].BackOrder);
        Assert.True(model.Rows[0].Orderable);
        Assert.Null(model.Rows[0].Problem);
        Assert.False(model.Rows[1].BackOrder); // exactly the stock
        Assert.True(model.Rows[2].BackOrder);  // negative stock (confirmed orders may overdraw)
        Assert.True(model.HasOrderable);
    }

    [Fact]
    public void Unorderable_rows_are_never_backorder()
    {
        var model = CartViewModel.Build([new CartLine(2, 50m)], [Product(2, stock: 1m, active: false)]);

        Assert.False(model.Rows.Single().BackOrder);
    }

    [Fact]
    public void Totals_are_quantity_times_snapshot_prices()
    {
        var model = CartViewModel.Build([new CartLine(1, 13.2m)], [Product(1)]);

        var row = model.Rows.Single();
        Assert.Equal(269.94m, row.LineTotal);
        Assert.Equal(269.94m, model.TotalWithVat);
        Assert.Equal(45.012m, model.Vat);          // not rounded per line, same as OrderService
        Assert.Equal(224.928m, model.SubtotalWithoutVat);
        Assert.Equal("45.01 €", PriceFormat.Money(model.Vat));
        Assert.Equal("224.93 €", PriceFormat.Money(model.SubtotalWithoutVat));
    }

    [Fact]
    public void Empty_cart_has_nothing_to_order()
    {
        var model = CartViewModel.Build([], []);

        Assert.Empty(model.Rows);
        Assert.False(model.HasOrderable);
        Assert.Equal(0m, model.TotalWithVat);
    }
}

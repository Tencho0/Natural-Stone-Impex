using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class OrderConfirmTests
{
    private static AppDbContext CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        db.Categories.Add(new Category { Id = 1, Name = "Бои" });
        return db;
    }

    private static Product Product(int id, string name, decimal stock, UnitType unit = UnitType.Sqm) =>
        new()
        {
            Id = id, Name = name, CategoryId = 1, Unit = unit, StockQuantity = stock,
            PriceWithoutVat = 10m, VatAmount = 2m, PriceWithVat = 12m
        };

    private static Order Order(int id, OrderStatus status, params (Product Product, decimal Qty)[] lines)
    {
        var order = new Order
        {
            Id = id,
            OrderNumber = $"NSI-20261108-{id:D4}",
            Status = status,
            CustomerType = CustomerType.Individual,
            DeliveryMethod = DeliveryMethod.Pickup,
            CustomerInfo = new OrderCustomerInfo { FullName = "Тест", Phone = "0888" }
        };
        foreach (var (product, qty) in lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id, ProductName = product.Name, Quantity = qty, Unit = product.Unit,
                UnitPriceWithoutVat = 10m, VatAmount = 2m, UnitPriceWithVat = 12m
            });
        }
        return order;
    }

    [Fact]
    public async Task Confirm_with_zero_stock_confirms_and_goes_negative()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 0m);
        db.Products.Add(paint);
        db.Orders.Add(Order(1, OrderStatus.Pending, (paint, 8.2m)));
        await db.SaveChangesAsync();

        await new OrderService(db).ConfirmAsync(1);

        var order = await db.Orders.SingleAsync(o => o.Id == 1);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal(-8.2m, (await db.Products.SingleAsync(p => p.Id == 1)).StockQuantity);
    }

    [Fact]
    public async Task Stock_check_lists_lines_that_would_go_negative()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 5m);
        var stone = Product(2, "Речен плосък", 100m, UnitType.Kg);
        db.Products.AddRange(paint, stone);
        db.Orders.Add(Order(1, OrderStatus.Pending, (paint, 8.2m), (stone, 40m)));
        await db.SaveChangesAsync();

        var shortages = await new OrderService(db).GetStockShortagesAsync(1);

        var detail = Assert.Single(shortages);
        Assert.Equal("Металик 5L", detail.ProductName);
        Assert.Equal(8.2m, detail.Ordered);
        Assert.Equal(5m, detail.Available);
        Assert.Equal("м²", detail.Unit);
    }

    [Fact]
    public async Task Stock_check_is_empty_when_enough_stock()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 8.2m);
        db.Products.Add(paint);
        db.Orders.Add(Order(1, OrderStatus.Pending, (paint, 8.2m)));
        await db.SaveChangesAsync();

        Assert.Empty(await new OrderService(db).GetStockShortagesAsync(1));
    }

    [Fact]
    public async Task Stock_check_sums_lines_of_the_same_product()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 10m);
        db.Products.Add(paint);
        db.Orders.Add(Order(1, OrderStatus.Pending, (paint, 6m), (paint, 6m)));
        await db.SaveChangesAsync();

        var detail = Assert.Single(await new OrderService(db).GetStockShortagesAsync(1));
        Assert.Equal(12m, detail.Ordered);
        Assert.Equal(10m, detail.Available);
    }

    [Fact]
    public async Task Stock_check_for_missing_order_throws_not_found()
    {
        await using var db = CreateContext();
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new OrderService(db).GetStockShortagesAsync(99));
    }

    [Fact]
    public async Task Confirm_non_pending_still_throws()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 20m);
        db.Products.Add(paint);
        db.Orders.Add(Order(1, OrderStatus.Confirmed, (paint, 8.2m)));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new OrderService(db).ConfirmAsync(1));
        Assert.Equal("Само чакащи поръчки могат да бъдат потвърдени.", ex.Message);
        Assert.Equal(20m, (await db.Products.SingleAsync(p => p.Id == 1)).StockQuantity);
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_confirmed()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 20m);
        db.Products.Add(paint);
        var order = Order(1, OrderStatus.Pending, (paint, 8.2m));
        order.IsCancelled = true;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new OrderService(db).ConfirmAsync(1));
        Assert.Equal("Отказана поръчка не може да бъде потвърдена.", ex.Message);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(o => o.Id == 1)).Status);
        Assert.Equal(20m, (await db.Products.SingleAsync(p => p.Id == 1)).StockQuantity);
    }

    [Fact]
    public async Task Confirm_with_enough_stock_reduces_stock_to_the_remainder()
    {
        await using var db = CreateContext();
        var paint = Product(1, "Металик 5L", 20m);
        var stone = Product(2, "Речен плосък", 100m, UnitType.Kg);
        db.Products.AddRange(paint, stone);
        db.Orders.Add(Order(1, OrderStatus.Pending, (paint, 8.2m), (stone, 40m)));
        await db.SaveChangesAsync();

        await new OrderService(db).ConfirmAsync(1);

        Assert.Equal(OrderStatus.Confirmed, (await db.Orders.SingleAsync(o => o.Id == 1)).Status);
        Assert.Equal(11.8m, (await db.Products.SingleAsync(p => p.Id == 1)).StockQuantity);
        Assert.Equal(60m, (await db.Products.SingleAsync(p => p.Id == 2)).StockQuantity);
    }
}

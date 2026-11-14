using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests;

/// <summary>
/// A page past the end returns the last page. (page - 1) * pageSize used to overflow int for a huge ?page=, which
/// SQL Server rejected as a negative OFFSET (HTTP 500 on /catalog and /api/products). The in-memory provider treats a
/// negative Skip as 0, so these tests assert the returned page, not just "no exception".
/// </summary>
public class PaginationOverflowTests
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

    private static async Task SeedProductsAsync(AppDbContext db, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            db.Products.Add(new Product
            {
                Id = i, Name = $"Продукт {i:D3}", CategoryId = 1, IsActive = true,
                PriceWithoutVat = 10m, VatAmount = 2m, PriceWithVat = 12m
            });
        }
        await db.SaveChangesAsync();
    }

    // Storefront catalog: pageSize 24. 89478486 is the largest page that does not overflow, 89478487 the first that does.
    // API: pageSize up to 50 (42949673 / 42949674).
    public static TheoryData<int, int> HugePages => new()
    {
        { int.MaxValue, CatalogPaging.PageSize },
        { 89_478_487, CatalogPaging.PageSize },
        { int.MaxValue, 50 },
        { 42_949_674, 50 },
    };

    [Theory]
    [MemberData(nameof(HugePages))]
    public async Task Products_page_past_the_end_returns_the_last_page(int page, int pageSize)
    {
        await using var db = CreateContext();
        await SeedProductsAsync(db, 60);
        var service = new ProductService(db, new FakeWebHostEnvironment());

        var result = await service.GetAllAsync(null, null, page, pageSize, includeInactive: false);

        var lastPage = (int)Math.Ceiling(60 / (double)pageSize);
        Assert.Equal(lastPage, result.TotalPages);
        Assert.Equal(lastPage, result.Page);
        Assert.Equal(60, result.TotalCount);
        Assert.Equal(60 - (lastPage - 1) * pageSize, result.Items.Count);
        Assert.Equal("Продукт 060", result.Items[^1].Name);
    }

    [Fact]
    public async Task Products_empty_catalog_with_a_huge_page_returns_page_one()
    {
        await using var db = CreateContext();
        var service = new ProductService(db, new FakeWebHostEnvironment());

        var result = await service.GetAllAsync(null, null, int.MaxValue, CatalogPaging.PageSize, includeInactive: false);

        Assert.Equal(1, result.Page);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(42_949_674)]
    public async Task Orders_page_past_the_end_returns_the_last_page(int page)
    {
        await using var db = CreateContext();
        for (var i = 1; i <= 60; i++)
        {
            db.Orders.Add(new Order
            {
                Id = i, OrderNumber = $"NSI-20261108-{i:D4}", Status = OrderStatus.Pending,
                CustomerType = CustomerType.Individual, DeliveryMethod = DeliveryMethod.Pickup,
                CreatedAt = new DateTime(2026, 11, 8).AddMinutes(i),
                CustomerInfo = new OrderCustomerInfo { FullName = "Тест", Phone = "0888" }
            });
        }
        await db.SaveChangesAsync();

        var result = await new OrderService(db).GetAllAsync(null, page, 50);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.Items.Count);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(42_949_674)]
    public async Task Invoices_page_past_the_end_returns_the_last_page(int page)
    {
        await using var db = CreateContext();
        for (var i = 1; i <= 60; i++)
        {
            db.Invoices.Add(new Invoice
            {
                Id = i, SupplierName = "Доставчик", InvoiceNumber = $"F-{i}",
                InvoiceDate = new DateTime(2026, 11, 8), EntryDate = new DateTime(2026, 11, 8).AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var result = await new InvoiceService(db).GetAllAsync(page, 50);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.Items.Count);
    }
}

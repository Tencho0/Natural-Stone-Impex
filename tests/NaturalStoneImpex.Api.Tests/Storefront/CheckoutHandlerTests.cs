using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CheckoutHandlerTests
{
    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<string> SeedAsync(params Product[] products)
    {
        var name = Guid.NewGuid().ToString();
        await using var db = CreateContext(name);
        db.Categories.Add(new Category { Id = 1, Name = "Плочи" });
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return name;
    }

    private static Product P(int id, decimal price = 20.45m, decimal stock = 100m, bool active = true) => new()
    {
        Id = id,
        Name = $"Продукт {id}",
        CategoryId = 1,
        PriceWithoutVat = price == 0 ? 0 : 17.04m,
        VatAmount = price == 0 ? 0 : 3.41m,
        PriceWithVat = price,
        Unit = UnitType.Sqm,
        StockQuantity = stock,
        IsActive = active
    };

    // The cart as the page builds it: cookie lines joined with the DB rows.
    private static async Task<CartViewModel> CartAsync(AppDbContext db, params CartLine[] lines)
    {
        var products = await new StorefrontQueryService(db).GetByIdsAsync(lines.Select(l => l.ProductId).ToList());
        return CartViewModel.Build(lines, products);
    }

    private static CheckoutHandler Handler(AppDbContext db, CheckoutGuard? guard = null) =>
        new(new OrderService(db), guard ?? new CheckoutGuard(), NullLogger<CheckoutHandler>.Instance);

    private static CheckoutForm Individual(string? token = null, string? stamp = null) => new()
    {
        CustomerType = CheckoutForm.Individual,
        DeliveryMethod = CheckoutForm.Pickup,
        FullName = " Иван Петров ",
        Phone = "0888 123 456",
        Token = token,
        CartStamp = stamp
    };

    [Fact]
    public async Task Places_order_with_only_orderable_rows()
    {
        var name = await SeedAsync(P(1), P(2, active: false), P(3, price: 0m));
        await using var db = CreateContext(name);
        var cart = await CartAsync(db, new CartLine(1, 2.5m), new CartLine(2, 1m), new CartLine(3, 4m), new CartLine(99, 1m));

        var outcome = await Handler(db).PlaceAsync(Individual(), cart);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Matches(@"^NSI-\d{8}-\d{4}$", outcome.OrderNumber);
        Assert.Null(outcome.Error);
        await using var check = CreateContext(name);
        var order = await check.Orders.Include(o => o.Items).Include(o => o.CustomerInfo).SingleAsync();
        Assert.Equal(outcome.OrderNumber, order.OrderNumber);
        var item = Assert.Single(order.Items);
        Assert.Equal(1, item.ProductId);
        Assert.Equal(2.5m, item.Quantity);
        Assert.Equal(20.45m, item.UnitPriceWithVat);
        // The old checkout's mapping: trimmed, and only the fields of the chosen customer type.
        Assert.Equal("Иван Петров", order.CustomerInfo.FullName);
        Assert.Equal("0888 123 456", order.CustomerInfo.Phone);
        Assert.Null(order.CustomerInfo.CompanyName);
        Assert.Null(order.CustomerInfo.Address);
    }

    [Fact]
    public async Task Empty_cart_creates_no_order()
    {
        var name = await SeedAsync(P(1, active: false), P(2, price: 0m));
        await using var db = CreateContext(name);
        var handler = Handler(db);

        var nothingOrderable = await handler.PlaceAsync(Individual(), await CartAsync(db, new CartLine(1, 1m), new CartLine(2, 1m)));
        var empty = await handler.PlaceAsync(Individual(), await CartAsync(db));

        Assert.False(nothingOrderable.Success);
        Assert.Equal("Количката е празна.", nothingOrderable.Error);
        Assert.Null(nothingOrderable.OrderNumber);
        Assert.Equal(CheckoutHandler.EmptyCartError, empty.Error);
        await using var check = CreateContext(name);
        Assert.Equal(0, await check.Orders.CountAsync());
    }

    [Fact]
    public async Task Stock_zero_does_not_block_order()
    {
        var name = await SeedAsync(P(1, stock: 0m));
        await using var db = CreateContext(name);
        var cart = await CartAsync(db, new CartLine(1, 12m));
        Assert.True(cart.Rows[0].BackOrder);

        var outcome = await Handler(db).PlaceAsync(Individual(), cart);

        Assert.True(outcome.Success, outcome.Error);
    }

    [Fact]
    public void Company_eik_must_be_9_or_13_digits()
    {
        static CheckoutForm Company(string? eik) => new()
        {
            CustomerType = CheckoutForm.Company,
            DeliveryMethod = CheckoutForm.Pickup,
            CompanyName = "Камък ООД",
            Eik = eik,
            Mol = "Петър Петров",
            ContactPerson = "Мария Иванова",
            ContactPhone = "0888 000 111"
        };

        Assert.Empty(Validate(Company("123456789")));
        Assert.Empty(Validate(Company("1234567890123")));
        Assert.Empty(Validate(Company(" 123456789 "))); // surrounding spaces are trimmed, as the old form did

        foreach (var bad in new[] { "12345678", "1234567890", "12345678901234", "12345678a", "123 456 789", "１２３４５６７８９" })
        {
            var error = Assert.Single(Validate(Company(bad)));
            Assert.Equal("ЕИК трябва да съдържа 9 или 13 цифри.", error.ErrorMessage);
            Assert.Equal([nameof(CheckoutForm.Eik)], error.MemberNames);
        }

        var missing = Assert.Single(Validate(Company("  ")));
        Assert.Equal("Полето е задължително.", missing.ErrorMessage);

        // An individual's ЕИК field is not checked (it is not sent either).
        var individual = Individual();
        individual.Eik = "abc";
        Assert.Empty(Validate(individual));
    }

    private static List<ValidationResult> Validate(CheckoutForm form)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Required_fields_follow_customer_type_and_delivery()
    {
        var company = Validate(new CheckoutForm { CustomerType = CheckoutForm.Company, DeliveryMethod = CheckoutForm.Delivery });
        Assert.Equal(
            [nameof(CheckoutForm.CompanyName), nameof(CheckoutForm.Eik), nameof(CheckoutForm.Mol),
             nameof(CheckoutForm.ContactPerson), nameof(CheckoutForm.ContactPhone), nameof(CheckoutForm.Address)],
            company.Select(r => r.MemberNames.Single()));
        Assert.All(company, r => Assert.Equal("Полето е задължително.", r.ErrorMessage));

        var individual = Validate(new CheckoutForm { CustomerType = CheckoutForm.Individual, DeliveryMethod = CheckoutForm.Pickup });
        Assert.Equal([nameof(CheckoutForm.FullName), nameof(CheckoutForm.Phone)], individual.Select(r => r.MemberNames.Single()));

        var hostile = Validate(new CheckoutForm { CustomerType = 5, DeliveryMethod = -1 });
        Assert.Equal(["Моля, изберете тип клиент.", "Моля, изберете метод на получаване."], hostile.Select(r => r.ErrorMessage));
    }

    // Ruling R27: as in the old checkout, nothing is preselected and both choices are required.
    [Fact]
    public void Customer_type_and_delivery_must_be_chosen()
    {
        var start = CheckoutForm.Start();
        Assert.Null(start.CustomerType);
        Assert.Null(start.DeliveryMethod);
        Assert.True(CheckoutGuard.IsToken(start.Token));

        var nothing = Validate(start);
        Assert.Equal(
            [(nameof(CheckoutForm.CustomerType), "Моля, изберете тип клиент."),
             (nameof(CheckoutForm.DeliveryMethod), "Моля, изберете метод на получаване.")],
            nothing.Select(r => (r.MemberNames.Single(), r.ErrorMessage)));

        // A chosen type is checked even while the delivery method is missing (the old form did the same).
        var typeOnly = Validate(new CheckoutForm { CustomerType = CheckoutForm.Individual, FullName = "Иван" });
        Assert.Equal([nameof(CheckoutForm.Phone), nameof(CheckoutForm.DeliveryMethod)], typeOnly.Select(r => r.MemberNames.Single()));
    }

    [Fact]
    public void Lengths_match_the_database_columns()
    {
        static CheckoutForm Company() => new()
        {
            CustomerType = CheckoutForm.Company, DeliveryMethod = CheckoutForm.Delivery,
            CompanyName = "Камък ООД", Eik = "123456789", Mol = "Петър Петров", ContactPerson = "Мария Иванова",
            ContactPhone = "0888 000 111", Address = "ул. Камъка 5", FullName = "Иван", Phone = "0888"
        };
        Assert.Empty(Validate(Company()));

        var limits = new (string Member, int Max, Action<CheckoutForm, string> Set)[]
        {
            (nameof(CheckoutForm.FullName), 200, (f, v) => f.FullName = v),
            (nameof(CheckoutForm.Phone), 20, (f, v) => f.Phone = v),
            (nameof(CheckoutForm.Address), 500, (f, v) => f.Address = v),
            (nameof(CheckoutForm.CompanyName), 200, (f, v) => f.CompanyName = v),
            (nameof(CheckoutForm.Mol), 200, (f, v) => f.Mol = v),
            (nameof(CheckoutForm.ContactPerson), 200, (f, v) => f.ContactPerson = v),
            (nameof(CheckoutForm.ContactPhone), 20, (f, v) => f.ContactPhone = v)
        };
        foreach (var (member, max, set) in limits)
        {
            var atMax = Company();
            set(atMax, new string('1', max));
            Assert.Empty(Validate(atMax));

            var over = Company();
            set(over, new string('1', max + 1));
            var error = Assert.Single(Validate(over));
            Assert.Equal([member], error.MemberNames);
            Assert.Equal($"Полето може да съдържа най-много {max} знака.", error.ErrorMessage);
        }

        // ЕИК (nvarchar(13)) is bounded by its own check, which says what is wrong.
        var eik = Company();
        eik.Eik = new string('1', 14);
        Assert.Equal(CheckoutForm.EikMessage, Assert.Single(Validate(eik)).ErrorMessage);
    }

    [Fact]
    public async Task Company_delivery_maps_like_the_old_checkout()
    {
        var name = await SeedAsync(P(1));
        await using var db = CreateContext(name);
        var form = new CheckoutForm
        {
            CustomerType = CheckoutForm.Company,
            DeliveryMethod = CheckoutForm.Delivery,
            FullName = "не се изпраща",
            CompanyName = " Камък ООД ",
            Eik = " 123456789 ",
            Mol = "Петър Петров",
            ContactPerson = "Мария Иванова",
            ContactPhone = "0888 000 111",
            Address = " ул. Камъка 5, Пловдив "
        };

        var outcome = await Handler(db).PlaceAsync(form, await CartAsync(db, new CartLine(1, 1m)));

        Assert.True(outcome.Success, outcome.Error);
        await using var check = CreateContext(name);
        var order = await check.Orders.Include(o => o.CustomerInfo).SingleAsync();
        Assert.Equal(CustomerType.Company, order.CustomerType);
        Assert.Equal(DeliveryMethod.Delivery, order.DeliveryMethod);
        Assert.Null(order.CustomerInfo.FullName);
        Assert.Equal("Камък ООД", order.CustomerInfo.CompanyName);
        Assert.Equal("123456789", order.CustomerInfo.Eik);
        Assert.Equal("ул. Камъка 5, Пловдив", order.CustomerInfo.Address);
    }

    [Fact]
    public async Task Same_token_places_one_order_and_repeats_its_number()
    {
        var name = await SeedAsync(P(1));
        await using var db = CreateContext(name);
        var guard = new CheckoutGuard();
        var token = CheckoutGuard.NewToken();
        var cart = await CartAsync(db, new CartLine(1, 1m));

        var first = await Handler(db, guard).PlaceAsync(Individual(token), cart);
        // The second POST of a double-click carries the same, still-full cart; a later one may find it empty.
        var second = await Handler(db, guard).PlaceAsync(Individual(token), cart);
        var afterClear = await Handler(db, guard).PlaceAsync(Individual(token), await CartAsync(db));

        Assert.True(second.Success);
        Assert.Equal(first.OrderNumber, second.OrderNumber);
        Assert.Equal(first.OrderNumber, afterClear.OrderNumber);
        await using var check = CreateContext(name);
        Assert.Equal(1, await check.Orders.CountAsync());
    }

    [Fact]
    public async Task Replayed_token_clears_only_the_cart_it_ordered()
    {
        var name = await SeedAsync(P(1), P(2));
        await using var db = CreateContext(name);
        var guard = new CheckoutGuard();
        var token = CheckoutGuard.NewToken();
        var ordered = await CartAsync(db, new CartLine(1, 1m));
        var form = Individual(token, CheckoutForm.Stamp(ordered));

        var first = await Handler(db, guard).PlaceAsync(form, ordered);
        // The double-click's second POST: the same cart, so it is cleared again.
        var sameCart = await Handler(db, guard).PlaceAsync(form, ordered);
        // A duplicated tab or restored form resubmitted after a new cart was filled: confirm, keep the new cart.
        var newerCart = await Handler(db, guard).PlaceAsync(form, await CartAsync(db, new CartLine(2, 3m)));
        var emptyCart = await Handler(db, guard).PlaceAsync(form, await CartAsync(db));

        Assert.True(first.Success && first.ClearsCart);
        Assert.True(sameCart.Success && sameCart.ClearsCart);
        Assert.True(newerCart.Success);
        Assert.False(newerCart.ClearsCart);
        Assert.True(emptyCart.ClearsCart);
        Assert.All([sameCart, newerCart, emptyCart], o => Assert.Equal(first.OrderNumber, o.OrderNumber));
        Assert.False((await Handler(db, guard).PlaceAsync(Individual(), await CartAsync(db))).ClearsCart);
        await using var check = CreateContext(name);
        Assert.Equal(1, await check.Orders.CountAsync());
    }

    [Fact]
    public async Task Two_concurrent_calls_with_one_token_create_one_order()
    {
        var name = await SeedAsync(P(1));
        var guard = new CheckoutGuard();
        var token = CheckoutGuard.NewToken();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var db1 = CreateContext(name);
        await using var db2 = CreateContext(name);
        var service1 = new GatedOrderService(new OrderService(db1), gate.Task);
        var service2 = new GatedOrderService(new OrderService(db2), gate.Task);
        var cart = await CartAsync(db1, new CartLine(1, 1m));

        // Both requests are inside PlaceAsync before either order is written.
        var a = new CheckoutHandler(service1, guard, NullLogger<CheckoutHandler>.Instance).PlaceAsync(Individual(token), cart);
        var b = new CheckoutHandler(service2, guard, NullLogger<CheckoutHandler>.Instance).PlaceAsync(Individual(token), cart);
        gate.SetResult();
        var outcomes = await Task.WhenAll(a, b);

        Assert.Equal(1, service1.Calls + service2.Calls);
        Assert.All(outcomes, o => Assert.True(o.Success));
        Assert.Equal(outcomes[0].OrderNumber, outcomes[1].OrderNumber);
        await using var check = CreateContext(name);
        Assert.Equal(1, await check.Orders.CountAsync());
    }

    [Fact]
    public async Task Missing_or_unknown_token_still_places_the_order()
    {
        var name = await SeedAsync(P(1));
        await using var db = CreateContext(name);
        var guard = new CheckoutGuard();
        var cart = await CartAsync(db, new CartLine(1, 1m));

        var missing = await Handler(db, guard).PlaceAsync(Individual(token: null), cart);
        var malformed = await Handler(db, guard).PlaceAsync(Individual(token: "not-a-token"), cart);
        var unknown = await Handler(db, guard).PlaceAsync(Individual(token: CheckoutGuard.NewToken()), cart);

        Assert.True(missing.Success && malformed.Success && unknown.Success);
        await using var check = CreateContext(name);
        Assert.Equal(3, await check.Orders.CountAsync());
    }

    [Fact]
    public async Task Order_service_error_is_returned_and_the_token_can_retry()
    {
        var name = await SeedAsync(P(1));
        await using var db = CreateContext(name);
        var guard = new CheckoutGuard();
        var token = CheckoutGuard.NewToken();
        var cart = await CartAsync(db, new CartLine(1, 1m));

        // The product goes out of sale after the cart was read, just before the order is written.
        var product = await db.Products.SingleAsync();
        product.IsActive = false;
        await db.SaveChangesAsync();
        var failed = await Handler(db, guard).PlaceAsync(Individual(token), cart);

        product.IsActive = true;
        await db.SaveChangesAsync();
        var retried = await Handler(db, guard).PlaceAsync(Individual(token), cart);

        Assert.False(failed.Success);
        Assert.Equal("Продукт 'Продукт 1' не е наличен.", failed.Error);
        Assert.True(retried.Success, retried.Error);
    }

    [Fact]
    public async Task Unexpected_failure_shows_a_bulgarian_message()
    {
        var name = await SeedAsync(P(1));
        await using var db = CreateContext(name);
        var cart = await CartAsync(db, new CartLine(1, 1m));
        var handler = new CheckoutHandler(new ThrowingOrderService(), new CheckoutGuard(), NullLogger<CheckoutHandler>.Instance);

        var outcome = await handler.PlaceAsync(Individual(CheckoutGuard.NewToken()), cart);

        Assert.False(outcome.Success);
        Assert.Equal(CheckoutHandler.UnexpectedError, outcome.Error);
    }

    [Fact]
    public async Task Cart_changed_since_the_form_was_shown_is_not_ordered()
    {
        var name = await SeedAsync(P(1), P(2));
        await using var db = CreateContext(name);
        var shown = await CartAsync(db, new CartLine(1, 1m), new CartLine(2, 1m));
        var stamp = CheckoutForm.Stamp(shown);

        // Same cart: the stamp matches. Quantity, price or an orderable row changed: it does not.
        Assert.Equal(stamp, CheckoutForm.Stamp(await CartAsync(db, new CartLine(1, 1m), new CartLine(2, 1m))));
        Assert.NotEqual(stamp, CheckoutForm.Stamp(await CartAsync(db, new CartLine(1, 2m), new CartLine(2, 1m))));
        var product = await db.Products.SingleAsync(p => p.Id == 2);
        product.IsActive = false;
        await db.SaveChangesAsync();
        var now = await CartAsync(db, new CartLine(1, 1m), new CartLine(2, 1m));
        Assert.NotEqual(stamp, CheckoutForm.Stamp(now));

        var outcome = await Handler(db).PlaceAsync(Individual(stamp: stamp), now);

        Assert.False(outcome.Success);
        Assert.Equal(CheckoutHandler.CartChangedError, outcome.Error);
        await using var check = CreateContext(name);
        Assert.Equal(0, await check.Orders.CountAsync());
    }

    private sealed class GatedOrderService(IOrderService inner, Task gate) : IOrderService
    {
        private int _calls;
        public int Calls => _calls;

        public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request)
        {
            Interlocked.Increment(ref _calls);
            await gate;
            return await inner.CreateAsync(request);
        }

        public Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize) => throw new NotSupportedException();
        public Task<OrderDetailDto> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<object> ConfirmAsync(int id) => throw new NotSupportedException();
        public Task CompleteAsync(int id) => throw new NotSupportedException();
        public Task CancelAsync(int id) => throw new NotSupportedException();
        public Task<object> SetDeliveryFeeAsync(int id, decimal deliveryFee) => throw new NotSupportedException();
        public Task<OrderStatsDto> GetStatsAsync() => throw new NotSupportedException();
        public Task<List<OrderListDto>> GetRecentAsync(int count) => throw new NotSupportedException();
        public Task<List<InsufficientStockDetail>> GetStockShortagesAsync(int id) => throw new NotSupportedException();
    }

    private sealed class ThrowingOrderService : IOrderService
    {
        public Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request) => throw new DbUpdateException("boom");
        public Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize) => throw new NotSupportedException();
        public Task<OrderDetailDto> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<object> ConfirmAsync(int id) => throw new NotSupportedException();
        public Task CompleteAsync(int id) => throw new NotSupportedException();
        public Task CancelAsync(int id) => throw new NotSupportedException();
        public Task<object> SetDeliveryFeeAsync(int id, decimal deliveryFee) => throw new NotSupportedException();
        public Task<OrderStatsDto> GetStatsAsync() => throw new NotSupportedException();
        public Task<List<OrderListDto>> GetRecentAsync(int count) => throw new NotSupportedException();
        public Task<List<InsufficientStockDetail>> GetStockShortagesAsync(int id) => throw new NotSupportedException();
    }
}

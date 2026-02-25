using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;

    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request)
    {
        // Validate customer type
        if (request.CustomerType is not (0 or 1))
            throw new InvalidOperationException("Невалиден тип клиент.");

        // Validate delivery method
        if (request.DeliveryMethod is not (0 or 1))
            throw new InvalidOperationException("Невалиден метод на доставка.");

        // Validate items list
        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("Поръчката трябва да съдържа поне един продукт.");

        // Validate customer info
        if (request.CustomerInfo is null)
            throw new InvalidOperationException("Информацията за клиента е задължителна.");

        var customerType = (CustomerType)request.CustomerType;
        var deliveryMethod = (DeliveryMethod)request.DeliveryMethod;

        // Validate fields based on customer type
        if (customerType == CustomerType.Individual)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.FullName))
                throw new InvalidOperationException("Полето 'Име и фамилия' е задължително.");
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.Phone))
                throw new InvalidOperationException("Полето 'Телефон' е задължително.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.CompanyName))
                throw new InvalidOperationException("Полето 'Име на фирмата' е задължително.");
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.Eik))
                throw new InvalidOperationException("Полето 'ЕИК/Булстат' е задължително.");
            if (!Regex.IsMatch(request.CustomerInfo.Eik, @"^\d{9}$|^\d{13}$"))
                throw new InvalidOperationException("ЕИК/Булстат трябва да е 9 или 13 цифри.");
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.Mol))
                throw new InvalidOperationException("Полето 'МОЛ' е задължително.");
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.ContactPerson))
                throw new InvalidOperationException("Полето 'Лице за контакт' е задължително.");
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.ContactPhone))
                throw new InvalidOperationException("Полето 'Телефон за контакт' е задължително.");
        }

        // Validate address if delivery
        if (deliveryMethod == DeliveryMethod.Delivery)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerInfo.Address))
                throw new InvalidOperationException("Полето 'Адрес за доставка' е задължително при доставка.");
        }

        // Validate each item and load products
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException("Количеството трябва да е по-голямо от 0.");
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"Продукт с ID {item.ProductId} не е намерен.");

            if (!product.IsActive)
                throw new InvalidOperationException($"Продукт '{product.Name}' не е наличен.");
        }

        // Generate order number
        var orderNumber = await GenerateOrderNumberAsync();

        // Create order
        var now = DateTime.UtcNow;
        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerType = customerType,
            Status = OrderStatus.Pending,
            DeliveryMethod = deliveryMethod,
            IsCancelled = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Create customer info
        order.CustomerInfo = new OrderCustomerInfo
        {
            FullName = request.CustomerInfo.FullName?.Trim(),
            Phone = request.CustomerInfo.Phone?.Trim(),
            Address = request.CustomerInfo.Address?.Trim(),
            CompanyName = request.CustomerInfo.CompanyName?.Trim(),
            Eik = request.CustomerInfo.Eik?.Trim(),
            Mol = request.CustomerInfo.Mol?.Trim(),
            ContactPerson = request.CustomerInfo.ContactPerson?.Trim(),
            ContactPhone = request.CustomerInfo.ContactPhone?.Trim()
        };

        // Create order items with price snapshots
        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPriceWithoutVat = product.PriceWithoutVat,
                VatAmount = product.VatAmount,
                UnitPriceWithVat = product.PriceWithVat,
                Unit = product.Unit
            });
        }

        // Save within a transaction
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CreateOrderResponse
        {
            OrderNumber = orderNumber,
            Message = "Вашата поръчка е приета успешно."
        };
    }

    public async Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 50) pageSize = 50;

        var query = _context.Orders
            .Include(o => o.CustomerInfo)
            .Include(o => o.Items)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => (int)o.Status == status.Value);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = orders.Select(MapToListDto).ToList();

        return new PaginatedResponse<OrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<OrderDetailDto> GetByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.CustomerInfo)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена.");

        return MapToDetailDto(order);
    }

    public async Task<object> ConfirmAsync(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена.");

        if (order.IsCancelled)
            throw new InvalidOperationException("Отказана поръчка не може да бъде потвърдена.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Само чакащи поръчки могат да бъдат потвърдени.");

        // Load all referenced products
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Check stock for ALL items, collect all shortages
        var shortages = new List<InsufficientStockDetail>();
        foreach (var item in order.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                if (product.StockQuantity < item.Quantity)
                {
                    shortages.Add(new InsufficientStockDetail
                    {
                        ProductName = item.ProductName,
                        Ordered = item.Quantity,
                        Available = product.StockQuantity,
                        Unit = item.Unit == UnitType.Kg ? "кг" : "м²"
                    });
                }
            }
        }

        if (shortages.Count > 0)
        {
            return new OrderConfirmErrorDto
            {
                Error = "Недостатъчна наличност за следните продукти:",
                Details = shortages
            };
        }

        // Decrement stock
        var now = DateTime.UtcNow;
        foreach (var item in order.Items)
        {
            var product = products[item.ProductId];
            product.StockQuantity -= item.Quantity;
            product.UpdatedAt = now;
        }

        order.Status = OrderStatus.Confirmed;
        order.ConfirmedAt = now;
        order.UpdatedAt = now;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new
        {
            message = "Поръчката е потвърдена.",
            orderNumber = order.OrderNumber,
            status = (int)OrderStatus.Confirmed,
            statusDisplay = "Потвърдена"
        };
    }

    public async Task CompleteAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена.");

        if (order.IsCancelled)
            throw new InvalidOperationException("Отказана поръчка не може да бъде завършена.");

        if (order.Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Само потвърдени поръчки могат да бъдат завършени.");

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Completed;
        order.CompletedAt = now;
        order.UpdatedAt = now;

        await _context.SaveChangesAsync();
    }

    public async Task CancelAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена.");

        if (order.IsCancelled)
            throw new InvalidOperationException("Поръчката вече е отказана.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Само чакащи поръчки могат да бъдат отказани.");

        order.IsCancelled = true;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<object> SetDeliveryFeeAsync(int id, decimal deliveryFee)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена.");

        if (order.IsCancelled)
            throw new InvalidOperationException("Не може да се задава цена за доставка на отказана поръчка.");

        if (order.DeliveryMethod != DeliveryMethod.Delivery)
            throw new InvalidOperationException("Цена за доставка може да се задава само за поръчки с доставка.");

        if (deliveryFee < 0)
            throw new InvalidOperationException("Цената за доставка трябва да е по-голяма или равна на 0.");

        order.DeliveryFee = deliveryFee;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var subtotalWithVat = order.Items.Sum(i => i.Quantity * i.UnitPriceWithVat);
        var grandTotal = subtotalWithVat + deliveryFee;

        return new
        {
            message = "Цената за доставка е зададена.",
            deliveryFee,
            grandTotal
        };
    }

    public async Task<OrderStatsDto> GetStatsAsync()
    {
        var totalProducts = await _context.Products.CountAsync(p => p.IsActive);
        var pendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending && !o.IsCancelled);
        var confirmedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed && !o.IsCancelled);
        var completedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Completed && !o.IsCancelled);

        return new OrderStatsDto
        {
            TotalProducts = totalProducts,
            PendingOrders = pendingOrders,
            ConfirmedOrders = confirmedOrders,
            CompletedOrders = completedOrders
        };
    }

    public async Task<List<OrderListDto>> GetRecentAsync(int count)
    {
        if (count < 1) count = 5;
        if (count > 20) count = 20;

        var orders = await _context.Orders
            .Include(o => o.CustomerInfo)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .ToListAsync();

        return orders.Select(MapToListDto).ToList();
    }

    private static OrderListDto MapToListDto(Order order)
    {
        return new OrderListDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            CustomerName = order.CustomerType == CustomerType.Company
                ? order.CustomerInfo.CompanyName ?? string.Empty
                : order.CustomerInfo.FullName ?? string.Empty,
            CustomerType = (int)order.CustomerType,
            CustomerTypeDisplay = order.CustomerType == CustomerType.Individual
                ? "Физическо лице" : "Фирма",
            DeliveryMethod = (int)order.DeliveryMethod,
            DeliveryMethodDisplay = order.DeliveryMethod == DeliveryMethod.Pickup
                ? "Вземане от обекта" : "Доставка",
            Status = (int)order.Status,
            StatusDisplay = GetStatusDisplay(order.Status),
            IsCancelled = order.IsCancelled,
            TotalWithVat = order.Items.Sum(i => i.Quantity * i.UnitPriceWithVat),
            ItemCount = order.Items.Count
        };
    }

    private static OrderDetailDto MapToDetailDto(Order order)
    {
        var items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPriceWithoutVat = i.UnitPriceWithoutVat,
            VatAmount = i.VatAmount,
            UnitPriceWithVat = i.UnitPriceWithVat,
            Unit = (int)i.Unit,
            UnitDisplay = i.Unit == UnitType.Kg ? "кг" : "м²",
            RowTotalWithoutVat = i.Quantity * i.UnitPriceWithoutVat,
            RowVatTotal = i.Quantity * i.VatAmount,
            RowTotalWithVat = i.Quantity * i.UnitPriceWithVat
        }).ToList();

        var subtotalWithoutVat = items.Sum(i => i.RowTotalWithoutVat);
        var totalVat = items.Sum(i => i.RowVatTotal);
        var subtotalWithVat = items.Sum(i => i.RowTotalWithVat);
        var grandTotal = subtotalWithVat + (order.DeliveryFee ?? 0);

        return new OrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = (int)order.Status,
            StatusDisplay = GetStatusDisplay(order.Status),
            CustomerType = (int)order.CustomerType,
            CustomerTypeDisplay = order.CustomerType == CustomerType.Individual
                ? "Физическо лице" : "Фирма",
            DeliveryMethod = (int)order.DeliveryMethod,
            DeliveryMethodDisplay = order.DeliveryMethod == DeliveryMethod.Pickup
                ? "Вземане от обекта" : "Доставка",
            DeliveryFee = order.DeliveryFee,
            IsCancelled = order.IsCancelled,
            CreatedAt = order.CreatedAt,
            ConfirmedAt = order.ConfirmedAt,
            CompletedAt = order.CompletedAt,
            CustomerInfo = new CustomerInfoDto
            {
                FullName = order.CustomerInfo.FullName,
                Phone = order.CustomerInfo.Phone,
                Address = order.CustomerInfo.Address,
                CompanyName = order.CustomerInfo.CompanyName,
                Eik = order.CustomerInfo.Eik,
                Mol = order.CustomerInfo.Mol,
                ContactPerson = order.CustomerInfo.ContactPerson,
                ContactPhone = order.CustomerInfo.ContactPhone
            },
            Items = items,
            SubtotalWithoutVat = subtotalWithoutVat,
            TotalVat = totalVat,
            SubtotalWithVat = subtotalWithVat,
            GrandTotal = grandTotal
        };
    }

    private static string GetStatusDisplay(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Чакаща",
        OrderStatus.Confirmed => "Потвърдена",
        OrderStatus.Completed => "Завършена",
        _ => string.Empty
    };

    private async Task<string> GenerateOrderNumberAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"NSI-{today}-";

        var lastOrder = await _context.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .FirstOrDefaultAsync();

        var nextSequence = 1;
        if (lastOrder != null)
        {
            var lastSeq = int.Parse(lastOrder.OrderNumber.Substring(prefix.Length));
            nextSequence = lastSeq + 1;
        }

        return $"{prefix}{nextSequence:D4}";
    }
}

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

using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;

    public InvoiceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResponse<InvoiceListDto>> GetAllAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 50) pageSize = 50;

        var query = _context.Invoices
            .Include(i => i.Items)
            .AsQueryable();

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var invoices = await query
            .OrderByDescending(i => i.EntryDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = invoices.Select(MapToListDto).ToList();

        return new PaginatedResponse<InvoiceListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<InvoiceDetailDto> GetByIdAsync(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            throw new KeyNotFoundException("Доставката не е намерена.");

        return MapToDetailDto(invoice);
    }

    public async Task<CreateInvoiceResponse> CreateAsync(CreateInvoiceRequest request)
    {
        // Validate supplier name
        if (string.IsNullOrWhiteSpace(request.SupplierName) || request.SupplierName.Trim().Length < 2)
            throw new InvalidOperationException("Името на доставчика е задължително (минимум 2 символа).");

        if (request.SupplierName.Trim().Length > 200)
            throw new InvalidOperationException("Името на доставчика не може да надвишава 200 символа.");

        // Validate invoice number
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            throw new InvalidOperationException("Номерът на фактурата е задължителен.");

        if (request.InvoiceNumber.Trim().Length > 50)
            throw new InvalidOperationException("Номерът на фактурата не може да надвишава 50 символа.");

        // Validate invoice date not in the future
        if (request.InvoiceDate.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("Датата на фактурата не може да бъде в бъдещето.");

        // Validate at least 1 item
        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("Трябва да добавите поне един артикул.");

        // Validate each item
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException("Количеството трябва да е по-голямо от 0.");

            if (item.PurchasePrice < 0)
                throw new InvalidOperationException("Покупната цена не може да бъде отрицателна.");
        }

        // Load and validate all referenced products
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"Продукт с ID {item.ProductId} не е намерен.");

            if (!product.IsActive)
                throw new InvalidOperationException($"Продукт '{product.Name}' не е активен.");
        }

        // Create invoice within a transaction
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            SupplierName = request.SupplierName.Trim(),
            InvoiceNumber = request.InvoiceNumber.Trim(),
            InvoiceDate = request.InvoiceDate,
            EntryDate = now,
            CreatedAt = now
        };

        foreach (var item in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice
            });

            // Increment stock
            var product = products[item.ProductId];
            product.StockQuantity += item.Quantity;
            product.UpdatedAt = now;
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CreateInvoiceResponse
        {
            Id = invoice.Id,
            Message = "Доставката е записана. Наличностите са обновени.",
            SupplierName = invoice.SupplierName,
            InvoiceNumber = invoice.InvoiceNumber
        };
    }

    private static InvoiceListDto MapToListDto(Invoice invoice)
    {
        return new InvoiceListDto
        {
            Id = invoice.Id,
            SupplierName = invoice.SupplierName,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            EntryDate = invoice.EntryDate,
            TotalItems = invoice.Items.Count,
            TotalQuantity = invoice.Items.Sum(i => i.Quantity),
            InvoiceTotal = invoice.Items.Sum(i => i.Quantity * i.PurchasePrice)
        };
    }

    private static InvoiceDetailDto MapToDetailDto(Invoice invoice)
    {
        var items = invoice.Items.Select(i => new InvoiceItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.Product.Name,
            Unit = (int)i.Product.Unit,
            UnitDisplay = i.Product.Unit == UnitType.Kg ? "кг" : "м²",
            Quantity = i.Quantity,
            PurchasePrice = i.PurchasePrice,
            RowTotal = i.Quantity * i.PurchasePrice
        }).ToList();

        return new InvoiceDetailDto
        {
            Id = invoice.Id,
            SupplierName = invoice.SupplierName,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            EntryDate = invoice.EntryDate,
            Items = items,
            InvoiceTotal = items.Sum(i => i.RowTotal)
        };
    }
}

using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ProductService(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<PaginatedResponse<ProductListDto>> GetAllAsync(int? categoryId, string? search, int page, int pageSize, bool includeInactive)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                PriceWithoutVat = p.PriceWithoutVat,
                VatAmount = p.VatAmount,
                PriceWithVat = p.PriceWithVat,
                Unit = (int)p.Unit,
                UnitDisplay = p.Unit == UnitType.Kg ? "кг" : "м²",
                StockQuantity = p.StockQuantity,
                ImagePath = p.ImagePath,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return new PaginatedResponse<ProductListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                PriceWithoutVat = p.PriceWithoutVat,
                VatAmount = p.VatAmount,
                PriceWithVat = p.PriceWithVat,
                Unit = (int)p.Unit,
                UnitDisplay = p.Unit == UnitType.Kg ? "кг" : "м²",
                StockQuantity = p.StockQuantity,
                ImagePath = p.ImagePath,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request)
    {
        if (request.PriceWithVat != request.PriceWithoutVat + request.VatAmount)
            throw new InvalidOperationException("Цената с ДДС трябва да е равна на цената без ДДС + ДДС.");

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
            throw new InvalidOperationException("Категорията не е намерена.");

        var nameExists = await _context.Products
            .AnyAsync(p => p.Name == request.Name && p.CategoryId == request.CategoryId);
        if (nameExists)
            throw new InvalidOperationException("Продукт с това име вече съществува в тази категория.");

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            PriceWithoutVat = request.PriceWithoutVat,
            VatAmount = request.VatAmount,
            PriceWithVat = request.PriceWithVat,
            Unit = (UnitType)request.Unit,
            StockQuantity = request.StockQuantity,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var category = await _context.Categories.FindAsync(product.CategoryId);

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = category!.Name,
            PriceWithoutVat = product.PriceWithoutVat,
            VatAmount = product.VatAmount,
            PriceWithVat = product.PriceWithVat,
            Unit = (int)product.Unit,
            UnitDisplay = product.Unit == UnitType.Kg ? "кг" : "м²",
            StockQuantity = product.StockQuantity,
            ImagePath = product.ImagePath,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
            return null;

        if (request.PriceWithVat != request.PriceWithoutVat + request.VatAmount)
            throw new InvalidOperationException("Цената с ДДС трябва да е равна на цената без ДДС + ДДС.");

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
            throw new InvalidOperationException("Категорията не е намерена.");

        var nameExists = await _context.Products
            .AnyAsync(p => p.Name == request.Name && p.CategoryId == request.CategoryId && p.Id != id);
        if (nameExists)
            throw new InvalidOperationException("Продукт с това име вече съществува в тази категория.");

        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.PriceWithoutVat = request.PriceWithoutVat;
        product.VatAmount = request.VatAmount;
        product.PriceWithVat = request.PriceWithVat;
        product.Unit = (UnitType)request.Unit;
        product.StockQuantity = request.StockQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var category = await _context.Categories.FindAsync(product.CategoryId);

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = category!.Name,
            PriceWithoutVat = product.PriceWithoutVat,
            VatAmount = product.VatAmount,
            PriceWithVat = product.PriceWithVat,
            Unit = (int)product.Unit,
            UnitDisplay = product.Unit == UnitType.Kg ? "кг" : "м²",
            StockQuantity = product.StockQuantity,
            ImagePath = product.ImagePath,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
            return (false, "Продуктът не е намерен.");

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(string? ImagePath, string? Error)> UploadImageAsync(int id, IFormFile file)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
            return (null, "Продуктът не е намерен.");

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType) || file.Length > 5 * 1024 * 1024)
            return (null, "Позволени са само JPG и PNG файлове до 5MB.");

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsDir);

        // Delete old image if exists
        if (!string.IsNullOrEmpty(product.ImagePath))
        {
            var oldPath = Path.Combine(_env.WebRootPath, product.ImagePath.TrimStart('/'));
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }

        var fileName = $"{id}_{file.FileName}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imagePath = $"/uploads/products/{fileName}";
        product.ImagePath = imagePath;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (imagePath, null);
    }

    public async Task<List<ProductListDto>> GetLowStockAsync(decimal threshold)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity <= threshold)
            .OrderBy(p => p.StockQuantity)
            .Select(p => new ProductListDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                PriceWithoutVat = p.PriceWithoutVat,
                VatAmount = p.VatAmount,
                PriceWithVat = p.PriceWithVat,
                Unit = (int)p.Unit,
                UnitDisplay = p.Unit == UnitType.Kg ? "кг" : "м²",
                StockQuantity = p.StockQuantity,
                ImagePath = p.ImagePath,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Storefront;

public class StorefrontQueryService : IStorefrontQueryService
{
    private readonly AppDbContext _context;

    public StorefrontQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductListDto>> GetFeaturedAsync(int count)
    {
        if (count <= 0)
            return [];

        var candidates = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.PriceWithVat > 0 && p.ImagePath != null && p.ImagePath.Trim() != "")
            .Select(ToListDto)
            .ToListAsync();

        // Ordering runs in memory (ordinal) so it is deterministic and independent of the SQL collation.
        var columns = candidates
            .GroupBy(p => p.CategoryName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Id).ToList())
            .ToList();

        var result = new List<ProductListDto>(Math.Min(count, candidates.Count));
        for (var row = 0; result.Count < count; row++)
        {
            var added = false;
            foreach (var column in columns)
            {
                if (row >= column.Count) continue;
                result.Add(column[row]);
                added = true;
                if (result.Count == count) break;
            }
            if (!added) break;
        }
        return result;
    }

    public async Task<ProductDto?> GetActiveProductAsync(int id)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive)
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
                IsVisualizerEnabled = p.IsVisualizerEnabled,
                TextureImagePath = p.TextureImagePath,
                TextureWidthMeters = p.TextureWidthMeters,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ProductListDto>> GetByIdsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
            return [];

        var wanted = ids.Distinct().ToList();
        var found = await _context.Products
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .Select(ToListDto)
            .ToListAsync();

        // Return in input order; unknown ids are simply absent.
        var byId = found.ToDictionary(p => p.Id);
        return wanted.Where(byId.ContainsKey).Select(i => byId[i]).ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetCategoryPhotosAsync()
    {
        var rows = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.ImagePath != null && p.ImagePath.Trim() != "")
            .Select(p => new { p.Id, p.Name, p.CategoryId, ImagePath = p.ImagePath! })
            .ToListAsync();

        return rows
            .GroupBy(r => r.CategoryId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id).First().ImagePath);
    }

    public async Task<IReadOnlyList<(int Id, DateTime UpdatedAt)>> GetSitemapProductsAsync()
    {
        var rows = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.UpdatedAt })
            .ToListAsync();
        return rows.Select(r => (r.Id, r.UpdatedAt)).ToList();
    }

    private static readonly Expression<Func<Product, ProductListDto>> ToListDto = p => new ProductListDto
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
    };
}

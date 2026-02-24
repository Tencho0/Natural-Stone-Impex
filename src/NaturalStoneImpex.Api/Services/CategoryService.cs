using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await _context.Categories
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count(p => p.IsActive),
                CreatedAt = c.CreatedAt
            })
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        return await _context.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count(p => p.IsActive),
                CreatedAt = c.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request)
    {
        var trimmedName = request.Name.Trim();

        var exists = await _context.Categories
            .AnyAsync(c => c.Name == trimmedName);

        if (exists)
            throw new InvalidOperationException("Категория с това име вече съществува.");

        var now = DateTime.UtcNow;
        var category = new Category
        {
            Name = trimmedName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ProductCount = 0,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category is null)
            return null;

        var trimmedName = request.Name.Trim();

        var duplicate = await _context.Categories
            .AnyAsync(c => c.Name == trimmedName && c.Id != id);

        if (duplicate)
            throw new InvalidOperationException("Категория с това име вече съществува.");

        category.Name = trimmedName;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var productCount = await _context.Products
            .CountAsync(p => p.CategoryId == id && p.IsActive);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ProductCount = productCount,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);

        if (category is null)
            return (false, "Категорията не е намерена.");

        var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
            return (false, "Категорията не може да бъде изтрита, защото съдържа продукти.");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return (true, null);
    }
}

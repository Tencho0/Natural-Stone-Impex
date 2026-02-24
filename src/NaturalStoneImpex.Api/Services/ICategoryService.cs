using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request);
    Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryRequest request);
    Task<(bool Success, string? Error)> DeleteAsync(int id);
}

using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IProductService
{
    Task<PaginatedResponse<ProductListDto>> GetAllAsync(int? categoryId, string? search, int page, int pageSize, bool includeInactive);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductRequest request);
    Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request);
    Task<(bool Success, string? Error)> DeleteAsync(int id);
    Task<(string? ImagePath, string? Error)> UploadImageAsync(int id, IFormFile file);
    Task<List<ProductListDto>> GetLowStockAsync(decimal threshold);
}

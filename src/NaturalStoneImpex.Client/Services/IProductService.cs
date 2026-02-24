using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IProductService
{
    Task<PaginatedResponse<ProductListDto>> GetAllAsync(int? categoryId = null, string? search = null, int page = 1, int pageSize = 20);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<(int? ProductId, string? Error)> CreateAsync(CreateProductRequest request);
    Task<string?> UpdateAsync(int id, UpdateProductRequest request);
    Task<string?> DeleteAsync(int id);
    Task<string?> UploadImageAsync(int id, Stream fileStream, string fileName);
}

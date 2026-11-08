using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Read-only catalog queries used in-process by the static SSR storefront pages.</summary>
public interface IStorefrontQueryService
{
    Task<IReadOnlyList<ProductListDto>> GetFeaturedAsync(int count);
    Task<ProductDto?> GetActiveProductAsync(int id);
    Task<IReadOnlyList<ProductListDto>> GetByIdsAsync(IReadOnlyCollection<int> ids);
    Task<IReadOnlyDictionary<int, string>> GetCategoryPhotosAsync();
    Task<IReadOnlyList<(int Id, DateTime UpdatedAt)>> GetSitemapProductsAsync();
}

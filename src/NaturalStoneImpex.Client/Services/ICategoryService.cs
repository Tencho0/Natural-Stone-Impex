using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<string?> CreateAsync(string name);
    Task<string?> UpdateAsync(int id, string name);
    Task<string?> DeleteAsync(int id);
}

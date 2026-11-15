namespace NaturalStoneImpex.Client.Services;

/// <summary>The cookie cart (nsi_cart) as seen from WebAssembly: the visualizer adds through POST /api/cart/items.</summary>
public interface ICartApi
{
    /// <summary>Adds a product to the cart. Returns null on success, otherwise a Bulgarian error message.</summary>
    Task<string?> AddAsync(int productId, decimal quantity);
}

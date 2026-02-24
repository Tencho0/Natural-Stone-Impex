using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class CartService
{
    private readonly List<CartItem> _items = new();

    public event Action? OnCartChanged;

    public void AddItem(CartItem item)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existing != null)
        {
            existing.Quantity += item.Quantity;
        }
        else
        {
            _items.Add(item);
        }
        OnCartChanged?.Invoke();
    }

    public void UpdateQuantity(int productId, decimal quantity)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            item.Quantity = quantity;
            OnCartChanged?.Invoke();
        }
    }

    public void RemoveItem(int productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
        OnCartChanged?.Invoke();
    }

    public void ClearCart()
    {
        _items.Clear();
        OnCartChanged?.Invoke();
    }

    public IReadOnlyList<CartItem> GetItems() => _items.AsReadOnly();

    public decimal GetTotalWithVat() => _items.Sum(i => i.Quantity * i.UnitPriceWithVat);

    public decimal GetTotalVat() => _items.Sum(i => i.Quantity * i.VatAmount);

    public decimal GetTotalWithoutVat() => _items.Sum(i => i.Quantity * i.UnitPriceWithoutVat);

    public int GetItemCount() => _items.Count;
}

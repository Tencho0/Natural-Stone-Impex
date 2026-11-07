namespace NaturalStoneImpex.Api.Storefront;

public interface ICartStore
{
    IReadOnlyList<CartLine> Lines { get; }

    /// <summary>Number of cart lines (not the summed quantity).</summary>
    int Count { get; }

    void Add(int productId, decimal quantity);
    void SetQuantity(int productId, decimal quantity);
    void Remove(int productId);
    void Clear();
}

/// <summary>Cart persisted in an HttpOnly cookie. The cookie is read once per request and rewritten on each change.</summary>
public class CartStore(IHttpContextAccessor accessor) : ICartStore
{
    private List<CartLine>? _lines;

    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP request.");

    private List<CartLine> Current => _lines ??= CartCookie.Parse(
        Context.Request.Cookies[CartCookie.Name]).ToList();

    public IReadOnlyList<CartLine> Lines => Current;

    public int Count => Current.Count;

    public void Add(int productId, decimal quantity)
    {
        var lines = Current;
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index >= 0)
            lines[index] = lines[index] with { Quantity = Math.Min(lines[index].Quantity + quantity, QuantityCalculator.MaxQuantity) };
        else if (lines.Count < CartCookie.MaxLines)
            lines.Add(new CartLine(productId, Math.Min(quantity, QuantityCalculator.MaxQuantity)));
        Write();
    }

    public void SetQuantity(int productId, decimal quantity)
    {
        var lines = Current;
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index < 0 || quantity <= 0 || quantity > QuantityCalculator.MaxQuantity)
            return;
        lines[index] = lines[index] with { Quantity = quantity };
        Write();
    }

    public void Remove(int productId)
    {
        if (Current.RemoveAll(l => l.ProductId == productId) > 0)
            Write();
    }

    public void Clear()
    {
        Current.Clear();
        Context.Response.Cookies.Delete(CartCookie.Name, Options(null));
    }

    private void Write()
    {
        if (Current.Count == 0)
        {
            Context.Response.Cookies.Delete(CartCookie.Name, Options(null));
            return;
        }
        Context.Response.Cookies.Append(CartCookie.Name, CartCookie.Serialize(Current),
            Options(DateTimeOffset.UtcNow.AddDays(30)));
    }

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Expires = expires,
        IsEssential = true,
        Path = "/"
    };
}

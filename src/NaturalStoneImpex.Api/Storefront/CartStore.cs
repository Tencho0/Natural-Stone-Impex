namespace NaturalStoneImpex.Api.Storefront;

public interface ICartStore
{
    IReadOnlyList<CartLine> Lines { get; }

    /// <summary>Number of cart lines (not the summed quantity).</summary>
    int Count { get; }

    /// <summary>Returns false only when a new product was dropped because the cart is full.</summary>
    bool Add(int productId, decimal quantity);
    void SetQuantity(int productId, decimal quantity);

    /// <summary>
    /// The cart row form: a typed quantity, a stepper press, or both (the − and + buttons post the typed value too).
    /// A step of -1 or 1 applies to the typed quantity when it is valid, otherwise to the stored one, and the
    /// result is kept between 0.01 and the maximum. Without a step, a valid typed quantity replaces the stored one;
    /// an invalid one (0 included) changes nothing. Removing a line is <see cref="Remove"/>, never a quantity of 0.
    /// </summary>
    void Update(int productId, decimal? quantity, int step);
    void Remove(int productId);
    void Clear();
}

/// <summary>Cart persisted in an HttpOnly cookie. The cookie is read once per request and rewritten on each change.</summary>
public class CartStore(IHttpContextAccessor accessor) : ICartStore
{
    /// <summary>The smallest quantity a line can hold: one cent of a unit (the − stepper stops here).</summary>
    public const decimal MinQuantity = 0.01m;

    private List<CartLine>? _lines;

    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP request.");

    private List<CartLine> Current => _lines ??= CartCookie.Parse(
        Context.Request.Cookies[CartCookie.Name]).ToList();

    public IReadOnlyList<CartLine> Lines => Current;

    public int Count => Current.Count;

    public bool Add(int productId, decimal quantity)
    {
        // Callers validate quantity > 0; a positive amount below one cent still adds one cent.
        quantity = Math.Max(CartCookie.Normalize(quantity), MinQuantity);
        var lines = Current;
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index >= 0)
            lines[index] = lines[index] with { Quantity = Math.Min(lines[index].Quantity + quantity, QuantityCalculator.MaxQuantity) };
        else if (lines.Count < CartCookie.MaxLines)
            lines.Add(new CartLine(productId, Math.Min(quantity, QuantityCalculator.MaxQuantity)));
        else
            return false;
        Write();
        return true;
    }

    public void SetQuantity(int productId, decimal quantity) => Update(productId, quantity, 0);

    public void Update(int productId, decimal? quantity, int step)
    {
        var lines = Current;
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index < 0)
            return;

        var typed = quantity is { } q ? CartCookie.Normalize(q) : (decimal?)null;
        if (typed is { } t && (t <= 0 || t > QuantityCalculator.MaxQuantity))
            typed = null; // R8: an invalid typed value is ignored

        decimal next;
        if (step is 1 or -1)
            next = Math.Clamp((typed ?? lines[index].Quantity) + step, MinQuantity, QuantityCalculator.MaxQuantity);
        else if (typed is { } value)
            next = value;
        else
            return;

        if (next == lines[index].Quantity)
            return;
        lines[index] = lines[index] with { Quantity = next };
        Write();
    }

    /// <summary>The stepper field: exactly "1" or "-1"; anything else is no step (0).</summary>
    public static int ParseStep(string? text) => text switch
    {
        "1" => 1,
        "-1" => -1,
        _ => 0
    };

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

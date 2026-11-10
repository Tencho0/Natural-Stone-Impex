using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Auth;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Endpoints;

public record CartItemRequest(int ProductId, decimal Quantity);

public static class CartEndpoints
{
    public static void MapCartEndpoints(this WebApplication app)
    {
        // Form endpoints: [FromForm] binding makes the antiforgery middleware validate the token.
        app.MapPost("/cart/add", async (
            [FromForm] int productId, [FromForm] string? quantity, [FromForm] string? returnUrl,
            ICartStore cart, AppDbContext db, HttpContext http) =>
        {
            var target = ReturnUrlValidator.ToLocalOrDefault(returnUrl, "/cart");

            decimal qty = 1;
            if (!string.IsNullOrWhiteSpace(quantity) && !DecimalInput.TryParse(quantity, out qty))
                return Results.LocalRedirect(target);
            if (!IsValidQuantity(qty) || !await IsPurchasable(db, productId))
                return Results.LocalRedirect(target);

            var added = cart.Add(productId, qty);
            Flash.Set(http.Response, added ? Flash.Added : Flash.Full);
            return Results.LocalRedirect(target);
        });

        // The cart row form. The − and + buttons post step=-1 or 1 together with the typed quantity;
        // ICartStore.Update decides which wins. Invalid input is a no-op redirect (R8).
        app.MapPost("/cart/update", async (
            [FromForm] int productId, [FromForm] string? quantity, [FromForm] string? step,
            ICartStore cart, AppDbContext db) =>
        {
            decimal? typed = DecimalInput.TryParse(quantity, out var qty) ? qty : null;
            var delta = CartStore.ParseStep(step);
            if ((typed is not null || delta != 0) && await IsPurchasable(db, productId))
                cart.Update(productId, typed, delta);
            return Results.LocalRedirect("/cart");
        });

        app.MapPost("/cart/remove", (
            [FromForm] int productId, ICartStore cart, HttpContext http) =>
        {
            if (cart.Lines.Any(l => l.ProductId == productId))
            {
                cart.Remove(productId);
                Flash.Set(http.Response, Flash.Removed);
            }
            return Results.LocalRedirect("/cart");
        });

        // JSON endpoint for the WASM visualizer; protected by AdminCsrfHeaderMiddleware (X-NSI-Request).
        app.MapPost("/api/cart/items", async (CartItemRequest request, ICartStore cart, AppDbContext db) =>
        {
            if (!IsValidQuantity(request.Quantity))
                return Results.BadRequest(new { error = "Невалидно количество." });
            if (!await IsPurchasable(db, request.ProductId))
                return Results.BadRequest(new { error = "Продуктът не е наличен." });

            if (!cart.Add(request.ProductId, request.Quantity))
                return Results.BadRequest(new { error = $"Количката е пълна. Максимум {CartCookie.MaxLines} продукта." });
            return Results.NoContent();
        });
    }

    private static bool IsValidQuantity(decimal quantity)
        => quantity > 0 && quantity <= QuantityCalculator.MaxQuantity;

    private static Task<bool> IsPurchasable(AppDbContext db, int productId)
        => db.Products.AnyAsync(p => p.Id == productId && p.IsActive && p.PriceWithVat > 0);
}

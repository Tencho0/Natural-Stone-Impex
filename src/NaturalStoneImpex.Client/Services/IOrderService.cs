using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IOrderService
{
    Task<(CreateOrderResponse? Response, string? Error)> PlaceOrderAsync(CreateOrderRequest request);
}

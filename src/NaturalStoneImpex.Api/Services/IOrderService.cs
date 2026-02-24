using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request);
}

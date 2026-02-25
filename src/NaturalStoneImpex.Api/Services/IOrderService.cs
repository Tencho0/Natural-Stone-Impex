using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request);
    Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize);
    Task<OrderDetailDto> GetByIdAsync(int id);
    Task<object> ConfirmAsync(int id);
    Task CompleteAsync(int id);
    Task CancelAsync(int id);
    Task<object> SetDeliveryFeeAsync(int id, decimal deliveryFee);
    Task<OrderStatsDto> GetStatsAsync();
    Task<List<OrderListDto>> GetRecentAsync(int count);
}

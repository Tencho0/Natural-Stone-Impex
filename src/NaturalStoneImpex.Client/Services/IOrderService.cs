using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IOrderService
{
    Task<(CreateOrderResponse? Response, string? Error)> PlaceOrderAsync(CreateOrderRequest request);
    Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize);
    Task<OrderDetailDto> GetByIdAsync(int id);
    Task<(string? Error, List<StockShortageDetail>? Details)> ConfirmAsync(int id);
    Task<string?> CompleteAsync(int id);
    Task<string?> CancelAsync(int id);
    Task<string?> SetDeliveryFeeAsync(int id, decimal deliveryFee);
    Task<OrderStatsDto> GetStatsAsync();
    Task<List<OrderListDto>> GetRecentAsync(int count);
}

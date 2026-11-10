using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IOrderService
{
    Task<(CreateOrderResponse? Response, string? Error)> PlaceOrderAsync(CreateOrderRequest request);
    Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize);
    Task<OrderDetailDto> GetByIdAsync(int id);
    /// <summary>Lines whose stock would go below zero on confirmation. Throws when the check fails.</summary>
    Task<List<StockShortageDetail>> GetStockShortagesAsync(int id);
    /// <summary>Confirms a pending order; never blocked by stock. Returns null, or a Bulgarian error.</summary>
    Task<string?> ConfirmAsync(int id);
    Task<string?> CompleteAsync(int id);
    Task<string?> CancelAsync(int id);
    Task<string?> SetDeliveryFeeAsync(int id, decimal deliveryFee);
    Task<OrderStatsDto> GetStatsAsync();
    Task<List<OrderListDto>> GetRecentAsync(int count);
}

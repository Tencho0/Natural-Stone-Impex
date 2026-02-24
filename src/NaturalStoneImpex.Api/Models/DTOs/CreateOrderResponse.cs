namespace NaturalStoneImpex.Api.Models.DTOs;

public record CreateOrderResponse
{
    public string OrderNumber { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

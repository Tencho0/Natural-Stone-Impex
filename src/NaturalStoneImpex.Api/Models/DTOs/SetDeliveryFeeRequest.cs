namespace NaturalStoneImpex.Api.Models.DTOs;

public record SetDeliveryFeeRequest
{
    public decimal DeliveryFee { get; init; }
}

namespace NaturalStoneImpex.Api.Models.DTOs;

public record OrderListDto
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int CustomerType { get; init; }
    public string CustomerTypeDisplay { get; init; } = string.Empty;
    public int DeliveryMethod { get; init; }
    public string DeliveryMethodDisplay { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public bool IsCancelled { get; init; }
    public decimal TotalWithVat { get; init; }
    public int ItemCount { get; init; }
}

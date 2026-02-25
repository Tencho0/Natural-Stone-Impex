namespace NaturalStoneImpex.Api.Models.DTOs;

public record OrderConfirmErrorDto
{
    public string Error { get; init; } = string.Empty;
    public List<InsufficientStockDetail> Details { get; init; } = new();
}

public record InsufficientStockDetail
{
    public string ProductName { get; init; } = string.Empty;
    public decimal Ordered { get; init; }
    public decimal Available { get; init; }
    public string Unit { get; init; } = string.Empty;
}

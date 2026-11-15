namespace NaturalStoneImpex.Client.Models;

public record StockShortageDetail
{
    public string ProductName { get; init; } = string.Empty;
    public decimal Ordered { get; init; }
    public decimal Available { get; init; }
    public string Unit { get; init; } = string.Empty;
}

namespace NaturalStoneImpex.Client.Models;

public record OrderStatsDto
{
    public int TotalProducts { get; init; }
    public int PendingOrders { get; init; }
    public int ConfirmedOrders { get; init; }
    public int CompletedOrders { get; init; }
}

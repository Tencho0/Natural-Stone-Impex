namespace NaturalStoneImpex.Client.Models;

public record InvoiceItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal PurchasePrice { get; init; }
    public decimal RowTotal { get; init; }
}

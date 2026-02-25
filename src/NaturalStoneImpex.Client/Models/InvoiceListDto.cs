namespace NaturalStoneImpex.Client.Models;

public record InvoiceListDto
{
    public int Id { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public DateTime EntryDate { get; init; }
    public int TotalItems { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal InvoiceTotal { get; init; }
}

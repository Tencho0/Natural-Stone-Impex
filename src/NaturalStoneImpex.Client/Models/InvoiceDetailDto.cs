namespace NaturalStoneImpex.Client.Models;

public record InvoiceDetailDto
{
    public int Id { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public DateTime EntryDate { get; init; }
    public List<InvoiceItemDto> Items { get; init; } = new();
    public decimal InvoiceTotal { get; init; }
}

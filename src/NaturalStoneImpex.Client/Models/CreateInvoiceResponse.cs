namespace NaturalStoneImpex.Client.Models;

public record CreateInvoiceResponse
{
    public int Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
}

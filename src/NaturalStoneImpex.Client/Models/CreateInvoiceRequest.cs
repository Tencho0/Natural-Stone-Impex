namespace NaturalStoneImpex.Client.Models;

public record CreateInvoiceRequest
{
    public string SupplierName { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public List<CreateInvoiceItemRequest> Items { get; init; } = new();
}

public record CreateInvoiceItemRequest
{
    public int ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal PurchasePrice { get; init; }
}

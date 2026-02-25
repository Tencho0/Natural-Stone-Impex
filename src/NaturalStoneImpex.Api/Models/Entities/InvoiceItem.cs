namespace NaturalStoneImpex.Api.Models.Entities;

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

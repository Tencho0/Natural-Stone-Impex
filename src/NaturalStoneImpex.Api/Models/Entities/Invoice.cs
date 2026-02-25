namespace NaturalStoneImpex.Api.Models.Entities;

public class Invoice
{
    public int Id { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

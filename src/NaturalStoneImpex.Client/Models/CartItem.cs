namespace NaturalStoneImpex.Client.Models;

public class CartItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPriceWithVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal UnitPriceWithoutVat { get; set; }
    public int Unit { get; set; }
    public string UnitDisplay { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? ImagePath { get; set; }
}

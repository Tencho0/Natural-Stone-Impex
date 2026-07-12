namespace NaturalStoneImpex.Api.Models.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public decimal PriceWithoutVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal PriceWithVat { get; set; }
    public UnitType Unit { get; set; }
    public decimal StockQuantity { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsVisualizerEnabled { get; set; }
    public string? TextureImagePath { get; set; }
    public decimal TextureWidthMeters { get; set; } = 1.00m;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
}

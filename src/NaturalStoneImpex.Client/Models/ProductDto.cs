namespace NaturalStoneImpex.Client.Models;

public record ProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal PriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public decimal StockQuantity { get; init; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

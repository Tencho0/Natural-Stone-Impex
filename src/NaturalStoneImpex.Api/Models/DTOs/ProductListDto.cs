namespace NaturalStoneImpex.Api.Models.DTOs;

public record ProductListDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal PriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public decimal StockQuantity { get; init; }
    public string? ImagePath { get; init; }
    public bool IsActive { get; init; }
}

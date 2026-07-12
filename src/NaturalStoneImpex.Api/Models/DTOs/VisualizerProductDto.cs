namespace NaturalStoneImpex.Api.Models.DTOs;

public record VisualizerProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImagePath { get; init; }
    public string TexturePath { get; init; } = string.Empty;
    public decimal TextureWidthMeters { get; init; }
    public decimal PriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}

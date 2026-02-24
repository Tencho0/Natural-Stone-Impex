namespace NaturalStoneImpex.Api.Models.DTOs;

public record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

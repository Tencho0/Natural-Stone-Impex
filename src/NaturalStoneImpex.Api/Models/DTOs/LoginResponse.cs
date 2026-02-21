namespace NaturalStoneImpex.Api.Models.DTOs;

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

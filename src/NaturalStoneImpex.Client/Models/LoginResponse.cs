namespace NaturalStoneImpex.Client.Models;

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

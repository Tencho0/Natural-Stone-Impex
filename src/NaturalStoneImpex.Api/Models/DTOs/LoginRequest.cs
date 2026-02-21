using System.ComponentModel.DataAnnotations;

namespace NaturalStoneImpex.Api.Models.DTOs;

public record LoginRequest
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

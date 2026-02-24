using System.ComponentModel.DataAnnotations;

namespace NaturalStoneImpex.Api.Models.DTOs;

public record CreateCategoryRequest
{
    [Required(ErrorMessage = "Името е задължително.")]
    [MinLength(2, ErrorMessage = "Името трябва да е поне 2 символа.")]
    [MaxLength(100, ErrorMessage = "Името не може да е повече от 100 символа.")]
    public string Name { get; init; } = string.Empty;
}

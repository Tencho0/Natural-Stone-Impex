using System.ComponentModel.DataAnnotations;

namespace NaturalStoneImpex.Api.Models.DTOs;

public record CreateProductRequest
{
    [Required(ErrorMessage = "Името на продукта е задължително.")]
    [MinLength(2, ErrorMessage = "Името трябва да е поне 2 символа.")]
    [MaxLength(200, ErrorMessage = "Името не може да надвишава 200 символа.")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Описанието не може да надвишава 2000 символа.")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Категорията е задължителна.")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Цената без ДДС е задължителна.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Цената без ДДС трябва да е по-голяма от 0.")]
    public decimal PriceWithoutVat { get; init; }

    [Required(ErrorMessage = "ДДС е задължително.")]
    [Range(0, double.MaxValue, ErrorMessage = "ДДС трябва да е по-голямо или равно на 0.")]
    public decimal VatAmount { get; init; }

    [Required(ErrorMessage = "Цената с ДДС е задължителна.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Цената с ДДС трябва да е по-голяма от 0.")]
    public decimal PriceWithVat { get; init; }

    [Required(ErrorMessage = "Мерната единица е задължителна.")]
    [Range(0, 1, ErrorMessage = "Невалидна мерна единица.")]
    public int Unit { get; init; }

    [Required(ErrorMessage = "Количеството е задължително.")]
    [Range(0, double.MaxValue, ErrorMessage = "Количеството трябва да е по-голямо или равно на 0.")]
    public decimal StockQuantity { get; init; }
}

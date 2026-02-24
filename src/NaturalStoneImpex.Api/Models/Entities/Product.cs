namespace NaturalStoneImpex.Api.Models.Entities;

// Placeholder — full implementation in Epic 04 (Product Management).
// Declared now so Category.Products navigation property compiles.
public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}

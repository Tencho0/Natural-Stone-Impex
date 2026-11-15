namespace NaturalStoneImpex.Client.Formatting;

/// <summary>Admin product list text.</summary>
public static class ProductFormat
{
    /// <summary>"1 продукт", "5 продукта".</summary>
    public static string CountText(int count) => count == 1 ? "1 продукт" : $"{count} продукта";
}

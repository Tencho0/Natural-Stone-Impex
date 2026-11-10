namespace NaturalStoneImpex.Client.Formatting;

/// <summary>The dashboard's one-line summary, with Bulgarian singular and plural forms.</summary>
public static class DashboardSummary
{
    public static string Text(int pending, int confirmed, int completed, int products) =>
        $"{Orders(pending, confirmed, completed)} · {products} {(products == 1 ? "продукт" : "продукта")}";

    /// <summary>The order counts alone, for the orders list: "3 чакащи · 5 потвърдени · 41 завършени".</summary>
    public static string Orders(int pending, int confirmed, int completed) =>
        $"{pending} {(pending == 1 ? "чакаща" : "чакащи")} · " +
        $"{confirmed} {(confirmed == 1 ? "потвърдена" : "потвърдени")} · " +
        $"{completed} {(completed == 1 ? "завършена" : "завършени")}";
}

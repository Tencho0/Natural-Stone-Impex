namespace NaturalStoneImpex.Client.Formatting;

/// <summary>The dashboard's one-line summary, with Bulgarian singular and plural forms.</summary>
public static class DashboardSummary
{
    public static string Text(int pending, int confirmed, int completed, int products) =>
        $"{pending} {(pending == 1 ? "чакаща" : "чакащи")} · " +
        $"{confirmed} {(confirmed == 1 ? "потвърдена" : "потвърдени")} · " +
        $"{completed} {(completed == 1 ? "завършена" : "завършени")} · " +
        $"{products} {(products == 1 ? "продукт" : "продукта")}";
}

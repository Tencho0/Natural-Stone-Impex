using NaturalStoneImpex.Client.Formatting;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class DashboardSummaryTests
{
    [Fact]
    public void Plural_forms_match_the_spec_line()
    {
        Assert.Equal("3 чакащи · 5 потвърдени · 41 завършени · 115 продукта", DashboardSummary.Text(3, 5, 41, 115));
    }

    [Fact]
    public void Zero_uses_plural_forms()
    {
        Assert.Equal("0 чакащи · 0 потвърдени · 0 завършени · 0 продукта", DashboardSummary.Text(0, 0, 0, 0));
    }

    [Fact]
    public void One_uses_singular_forms()
    {
        Assert.Equal("1 чакаща · 1 потвърдена · 1 завършена · 1 продукт", DashboardSummary.Text(1, 1, 1, 1));
    }

    [Fact]
    public void Orders_line_leaves_out_products()
    {
        Assert.Equal("3 чакащи · 1 потвърдена · 41 завършени", DashboardSummary.Orders(3, 1, 41));
    }
}

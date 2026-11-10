using NaturalStoneImpex.Client.Components;
using NaturalStoneImpex.Client.Formatting;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Api.Tests.Admin;

public class OrderFormatTests
{
    [Fact]
    public void Shortage_line_names_the_product_and_the_stock_after_confirmation()
    {
        var detail = new StockShortageDetail { ProductName = "Металик 5L", Ordered = 8.2m, Available = 0m, Unit = "м²" };

        Assert.Equal("Металик 5L ще стане −8.20 м²", OrderFormat.ShortageLine(detail));
    }

    [Fact]
    public void Shortage_line_subtracts_the_ordered_quantity_from_current_stock()
    {
        var detail = new StockShortageDetail { ProductName = "Речен плосък", Ordered = 40m, Available = 12.5m, Unit = "кг" };

        Assert.Equal("Речен плосък ще стане −27.50 кг", OrderFormat.ShortageLine(detail));
    }

    [Theory]
    [InlineData(0, false, "Чакаща", "status status--warn")]
    [InlineData(1, false, "Потвърдена", "status status--ok")]
    [InlineData(2, false, "Завършена", "status")]
    [InlineData(0, true, "Отказана", "status status--danger")]
    public void Status_text_and_marker(int status, bool cancelled, string text, string css)
    {
        Assert.Equal(text, OrderFormat.StatusText(status, cancelled));
        Assert.Equal(css, OrderFormat.StatusCss(status, cancelled));
    }

    [Fact]
    public void Prompt_without_shortages_asks_to_confirm_the_order()
    {
        var prompt = OrderConfirmPrompt.From([]);

        Assert.Equal("Потвърди поръчката?", prompt.Question);
        Assert.Empty(prompt.Warnings);
    }

    [Fact]
    public void Prompt_with_shortages_names_each_line_then_asks()
    {
        var prompt = OrderConfirmPrompt.From(
        [
            new StockShortageDetail { ProductName = "Металик 5L", Ordered = 8.2m, Available = 0m, Unit = "м²" },
            new StockShortageDetail { ProductName = "Речен плосък", Ordered = 40m, Available = 12.5m, Unit = "кг" }
        ]);

        Assert.Equal("Потвърди?", prompt.Question);
        Assert.Equal(new[] { "Металик 5L ще стане −8.20 м².", "Речен плосък ще стане −27.50 кг." }, prompt.Warnings);
    }
}

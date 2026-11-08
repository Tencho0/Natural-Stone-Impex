using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class ProductViewTests
{
    private static ProductDto Sqm(decimal price = 20.45m, decimal vat = 3.41m) => new()
    {
        Id = 43, Name = "Бежов оформен глиц 15L", CategoryName = "Гнайс",
        PriceWithVat = price, VatAmount = vat, Unit = 1, UnitDisplay = "м²"
    };

    private static ProductDto Kg() => new()
    {
        Id = 1, Name = "Речни топчета", CategoryName = "Речни камъни",
        PriceWithVat = 0.41m, VatAmount = 0.07m, Unit = 0, UnitDisplay = "кг"
    };

    [Theory]
    [InlineData(46.5, "В наличност: 46.50 м²")]
    [InlineData(0.01, "В наличност: 0.01 м²")]
    [InlineData(0, "Доставка по заявка. Ще ви се обадим за срок.")]
    [InlineData(-8.2, "Доставка по заявка. Ще ви се обадим за срок.")]
    public void Availability_never_shows_a_negative_number(double stock, string expected)
        => Assert.Equal(expected, ProductView.Availability((decimal)stock, "м²"));

    [Fact]
    public void Sale_unit_line_names_category_and_unit()
    {
        Assert.Equal("Гнайс, продава се на м²", ProductView.SaleUnit(Sqm()));
        Assert.Equal("Речни камъни, продава се на кг", ProductView.SaleUnit(Kg()));
    }

    [Fact]
    public void Price_note_states_vat_once()
    {
        Assert.Equal("за м², с ДДС (3.41 € ДДС)", ProductView.PriceNote(Sqm()));
        Assert.Equal("за м², с ДДС", ProductView.PriceNote(Sqm(vat: 0)));
    }

    [Fact]
    public void Meta_description_uses_price_or_enquiry()
    {
        Assert.Equal("Гнайс, 20.45 €/м² с ДДС.", ProductView.MetaDescription(Sqm()));
        Assert.Equal("Гнайс. Цена при запитване.", ProductView.MetaDescription(Sqm(price: 0, vat: 0)));
    }

    [Fact]
    public void Kilogram_products_are_detected_by_unit()
    {
        Assert.True(ProductView.IsKilograms(Kg()));
        Assert.False(ProductView.IsKilograms(Sqm()));
    }

    [Fact]
    public void Default_form_gives_13_20_and_names_it_on_the_button()
    {
        var result = QuantityCalculator.Calculate(ProductCalcForm.Defaults().ToInput(kilograms: false));

        Assert.True(result.IsValid);
        Assert.Equal(13.20m, result.Quantity);
        Assert.Equal("Добави 13.20 м² в количката", ProductView.AddLabel(result, "м²"));
        Assert.Equal("269.94 €", ProductView.Total(result, 20.45m));
    }

    [Fact]
    public void Invalid_result_keeps_the_plain_label_and_explains()
    {
        var missing = QuantityCalculator.Calculate(new ProductCalcForm { Mode = "dims", Length = "4", Width = "" }.ToInput(false));
        var tooBig = QuantityCalculator.Calculate(new ProductCalcForm { Mode = "area", Area = "20000" }.ToInput(false));

        Assert.Equal("Добави в количката", ProductView.AddLabel(missing, "м²"));
        Assert.Null(ProductView.Total(missing, 20.45m));
        Assert.Equal(ProductView.MissingSqm, ProductView.ResultNote(missing, QuantityMode.Dimensions));
        Assert.Equal(ProductView.MissingKg, ProductView.ResultNote(missing, QuantityMode.Kilograms));
        Assert.Equal(QuantityCalculator.TooLargeError, ProductView.ResultNote(tooBig, QuantityMode.Area));
    }

    [Fact]
    public void Missing_area_asks_for_the_area()
    {
        var missing = QuantityCalculator.Calculate(new ProductCalcForm { Mode = "area", Area = "" }.ToInput(false));

        Assert.Equal("Попълнете площта, за да видите сумата.", ProductView.ResultNote(missing, QuantityMode.Area));
        Assert.Equal(ProductView.MissingArea, ProductView.MissingNote(QuantityMode.Area));
        Assert.Equal(ProductView.MissingSqm, ProductView.MissingNote(QuantityMode.Dimensions));
    }

    [Fact]
    public void Js_templates_come_from_the_same_formats()
    {
        Assert.Equal("{0} м²", ProductView.QuantityTemplate("м²"));
        Assert.Equal("{0} €", ProductView.MoneyTemplate);
        Assert.Equal("Добави {0} кг в количката", ProductView.AddTemplate("кг"));

        var result = QuantityCalculator.Calculate(ProductCalcForm.Defaults().ToInput(kilograms: false));
        Assert.Equal(ProductView.AddLabel(result, "м²"), ProductView.AddTemplate("м²").Replace("{0}", "13.20"));
        Assert.Equal(ProductView.Total(result, 20.45m), ProductView.MoneyTemplate.Replace("{0}", "269.94"));
    }

    [Fact]
    public void Form_mode_selects_dimensions_unless_area()
    {
        Assert.Equal(QuantityMode.Area, new ProductCalcForm { Mode = "area" }.ToInput(false).Mode);
        Assert.Equal(QuantityMode.Dimensions, new ProductCalcForm { Mode = "dims" }.ToInput(false).Mode);
        Assert.Equal(QuantityMode.Dimensions, new ProductCalcForm { Mode = "bogus" }.ToInput(false).Mode);
        Assert.Equal(QuantityMode.Dimensions, new ProductCalcForm().ToInput(false).Mode);
    }

    [Fact]
    public void Kilogram_products_ignore_the_posted_mode()
    {
        var input = new ProductCalcForm { Mode = "area", Area = "5", Kilograms = "25", Waste = true }.ToInput(kilograms: true);
        var result = QuantityCalculator.Calculate(input);

        Assert.Equal(QuantityMode.Kilograms, input.Mode);
        Assert.Equal(25m, result.Quantity);
    }

    [Fact]
    public void Unchecked_waste_is_off_when_posted()
    {
        // An unchecked box posts nothing, so a bound form must default Waste to false; the "on" default is in Defaults().
        Assert.False(new ProductCalcForm().Waste);
        Assert.True(ProductCalcForm.Defaults().Waste);

        var result = QuantityCalculator.Calculate(new ProductCalcForm { Mode = "dims", Length = "4", Width = "3" }.ToInput(false));
        Assert.Equal(12.00m, result.Quantity);
    }

    [Fact]
    public void Area_with_waste_rounds_up_like_the_js_check()
    {
        var result = QuantityCalculator.Calculate(new ProductCalcForm { Mode = "area", Area = "10,01", Waste = true }.ToInput(false));

        Assert.Equal(11.02m, result.Quantity);
        Assert.Equal("Добави 11.02 м² в количката", ProductView.AddLabel(result, "м²"));
    }
}

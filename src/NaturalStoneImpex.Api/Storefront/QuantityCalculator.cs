namespace NaturalStoneImpex.Api.Storefront;

public enum QuantityMode { Dimensions, Area, Kilograms }

public record QuantityInput(QuantityMode Mode, string? Length, string? Width, string? Area, string? Kilograms, bool AddWaste);

public record QuantityResult(bool IsValid, decimal Quantity, string? Error, string? ErrorField);

public static class QuantityCalculator
{
    public const decimal WasteFactor = 1.10m;
    public const decimal MaxQuantity = 10000m;

    public static QuantityResult Calculate(QuantityInput input)
    {
        decimal raw;
        bool appliesWaste = true;

        switch (input.Mode)
        {
            case QuantityMode.Dimensions:
                if (!TryPositive(input.Length, out var length))
                    return Fail("Length", "Въведете дължина в метри.");
                if (!TryPositive(input.Width, out var width))
                    return Fail("Width", "Въведете ширина в метри.");
                raw = length * width;
                break;
            case QuantityMode.Area:
                if (!TryPositive(input.Area, out raw))
                    return Fail("Area", "Въведете площ в м².");
                break;
            default:
                if (!TryPositive(input.Kilograms, out raw))
                    return Fail("Kilograms", "Въведете количество в кг.");
                appliesWaste = false;
                break;
        }

        if (appliesWaste && input.AddWaste)
            raw *= WasteFactor;

        var quantity = Math.Ceiling(raw * 100m) / 100m;
        if (quantity > MaxQuantity)
            return Fail(FieldFor(input.Mode), "Количеството е твърде голямо.");

        return new QuantityResult(true, quantity, null, null);
    }

    private static bool TryPositive(string? text, out decimal value) =>
        DecimalInput.TryParse(text, out value) && value > 0;

    private static string FieldFor(QuantityMode mode) => mode switch
    {
        QuantityMode.Dimensions => "Length",
        QuantityMode.Area => "Area",
        _ => "Kilograms",
    };

    private static QuantityResult Fail(string field, string error) => new(false, 0, error, field);
}

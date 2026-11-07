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
                if (!TryInput(input.Length, "Length", "Въведете дължина в метри.", out var length, out var bad))
                    return bad!;
                if (!TryInput(input.Width, "Width", "Въведете ширина в метри.", out var width, out bad))
                    return bad!;
                raw = length * width;
                break;
            case QuantityMode.Area:
                if (!TryInput(input.Area, "Area", "Въведете площ в м².", out raw, out var bad2))
                    return bad2!;
                break;
            default:
                if (!TryInput(input.Kilograms, "Kilograms", "Въведете количество в кг.", out raw, out var bad3))
                    return bad3!;
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

    // Every single input is capped before any arithmetic, so the products below cannot overflow decimal.
    private static bool TryInput(string? text, string field, string missingError, out decimal value, out QuantityResult? failure)
    {
        failure = null;
        if (!DecimalInput.TryParse(text, out value) || value <= 0)
        {
            failure = Fail(field, missingError);
            return false;
        }
        if (value > MaxQuantity)
        {
            failure = Fail(field, "Количеството е твърде голямо.");
            return false;
        }
        return true;
    }

    private static string FieldFor(QuantityMode mode) => mode switch
    {
        QuantityMode.Dimensions => "Length",
        QuantityMode.Area => "Area",
        _ => "Kilograms",
    };

    private static QuantityResult Fail(string field, string error) => new(false, 0, error, field);
}

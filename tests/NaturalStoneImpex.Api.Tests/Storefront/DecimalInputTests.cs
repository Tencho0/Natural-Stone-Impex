using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class DecimalInputTests
{
    [Theory]
    [InlineData("4,5", 4.5)]
    [InlineData("4.5", 4.5)]
    [InlineData(" 12 ", 12)]
    [InlineData("1 204,5", 1204.5)]
    [InlineData("1\u00A0204,5", 1204.5)]
    [InlineData("1\u202F204,5", 1204.5)]
    [InlineData("0,01", 0.01)]
    public void Parses_bulgarian_and_invariant_forms(string text, double expected)
    {
        Assert.True(DecimalInput.TryParse(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("-3")]
    [InlineData("1.2.3")]
    public void Rejects_invalid(string? text)
    {
        Assert.False(DecimalInput.TryParse(text, out _));
    }
}

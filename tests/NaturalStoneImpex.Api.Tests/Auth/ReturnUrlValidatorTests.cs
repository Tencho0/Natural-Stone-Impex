using NaturalStoneImpex.Api.Auth;

namespace NaturalStoneImpex.Api.Tests.Auth;

public class ReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/admin/orders", "/admin/orders")]
    [InlineData("/admin/orders/5?tab=items", "/admin/orders/5?tab=items")]
    [InlineData(null, "/admin")]
    [InlineData("", "/admin")]
    [InlineData("//evil.com", "/admin")]
    [InlineData("/\\evil.com", "/admin")]
    [InlineData("https://evil.com/admin", "/admin")]
    [InlineData("%2F%2Fevil.com", "/admin")]
    [InlineData("javascript:alert(1)", "/admin")]
    [InlineData("admin", "/admin")]
    public void ToLocalOrDefault_accepts_only_local_paths(string? input, string expected)
        => Assert.Equal(expected, ReturnUrlValidator.ToLocalOrDefault(input));
}

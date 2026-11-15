namespace NaturalStoneImpex.Api.Tests.Admin;

/// <summary>
/// The printed receipt shows the shop's address and phone from ShopInfo, the same source as the storefront,
/// instead of English placeholder text.
/// </summary>
public class ReceiptShopDetailsTests
{
    private static readonly string ReceiptPath = Path.Combine(
        FindRepoRoot(), "src", "NaturalStoneImpex.Client", "Pages", "Admin", "Receipt.razor");

    [Fact]
    public void Receipt_has_no_placeholder_text()
    {
        var markup = File.ReadAllText(ReceiptPath);

        Assert.DoesNotContain("[Placeholder", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Receipt_header_uses_ShopInfo()
    {
        var markup = File.ReadAllText(ReceiptPath);

        Assert.Contains("@ShopInfo.Address", markup);
        Assert.Contains("@ShopInfo.Phone", markup);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NaturalStoneImpex.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("NaturalStoneImpex.slnx not found above " + AppContext.BaseDirectory);
    }
}

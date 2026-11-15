using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>
/// Guards for the storefront budgets (spec §5.5, §5.8; R17). They read the source files, so they fail in the
/// change that breaks a budget rather than in the next Lighthouse run.
/// </summary>
public class StorefrontBudgetTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Storefront_js_modules_stay_under_8_KB_brotli()
    {
        var dir = Path.Combine(RepoRoot, "src", "NaturalStoneImpex.Client", "wwwroot", "js", "storefront");
        var files = Directory.GetFiles(dir, "*.js");
        Assert.NotEmpty(files);

        // The build compresses static assets with brotli at its smallest size; this matches the published .br files.
        var total = files.Sum(BrotliSize);

        Assert.True(total < 8192, $"Storefront JS is {total} B brotli, over the 8,192 B budget: "
            + string.Join(", ", files.Select(f => $"{Path.GetFileName(f)} {BrotliSize(f)}")));
    }

    [Fact]
    public void Storefront_components_do_not_use_the_icon_font()
    {
        // Static SSR pages do not load bootstrap-icons.css (130 KB font); they use Shared/Icon.razor instead.
        var dir = Path.Combine(RepoRoot, "src", "NaturalStoneImpex.Api", "Components");
        var iconFont = new Regex(@"class=""[^""]*\bbi\b", RegexOptions.CultureInvariant);

        var offenders = Directory.GetFiles(dir, "*.razor", SearchOption.AllDirectories)
            .Where(f => iconFont.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(dir, f))
            .ToList();

        Assert.Empty(offenders);
    }

    private static int BrotliSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            brotli.Write(bytes);
        return (int)output.Length;
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

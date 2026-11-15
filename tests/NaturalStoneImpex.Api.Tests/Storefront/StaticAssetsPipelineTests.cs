using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>
/// Runtime uploads are data, not build assets. They must not be packed as static web assets (MapStaticAssets
/// would answer deleted uploads with an empty 200, and every publish would ship the developer's uploads folder),
/// and the host must serve them only through UseUploadsStaticFiles.
/// </summary>
public class StaticAssetsPipelineTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Endpoint_manifest_has_no_uploads_routes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "NaturalStoneImpex.Api.staticwebassets.endpoints.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        var uploads = doc.RootElement.GetProperty("Endpoints").EnumerateArray()
            .Select(e => e.GetProperty("Route").GetString()!)
            .Where(r => r.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(uploads.Count == 0,
            $"{uploads.Count} uploads/ routes in the static web assets manifest, e.g. {string.Join(", ", uploads.Take(3))}");
    }

    [Fact]
    public void Development_runtime_manifest_has_no_uploads_folder()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "NaturalStoneImpex.Api.staticwebassets.runtime.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        var children = doc.RootElement.GetProperty("Root").GetProperty("Children");

        Assert.False(children.TryGetProperty("uploads", out _), "wwwroot/uploads is packed as a static web asset");
    }

    [Fact]
    public void Program_serves_static_files_only_through_UseUploadsStaticFiles()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot, "src", "NaturalStoneImpex.Api", "Program.cs"));
        var code = Regex.Replace(program, @"//.*", ""); // ignore comments

        Assert.DoesNotMatch(@"\bUseStaticFiles\s*\(", code);
        Assert.Contains("app.UseUploadsStaticFiles();", code);
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

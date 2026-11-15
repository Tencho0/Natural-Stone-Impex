using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using NaturalStoneImpex.Api.Middleware;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>
/// UseStaticFiles is only for runtime uploads. Build-time assets must reach MapStaticAssets, which serves the
/// brotli/gzip variants and immutable caching. Fingerprinted framework files (e.g. dotnet.native.{hash}.wasm)
/// exist on disk under those names, so a bare UseStaticFiles answered them first, uncompressed.
/// </summary>
public sealed class UploadsStaticFilesTests : IDisposable
{
    private const string Fallthrough = "fell through";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nsi-uploads-" + Guid.NewGuid().ToString("N"));

    public UploadsStaticFilesTests()
    {
        Write("wwwroot/uploads/products/photo.jpg", "jpeg");
        Write("wwwroot/_framework/dotnet.native.abc123.wasm", "wasm");
        Write("wwwroot/fonts/geologica-latin.woff2", "font");
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private async Task<HttpClient> StartAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseContentRoot(_root)
                .UseWebRoot("wwwroot")
                .Configure(app =>
                {
                    app.UseUploadsStaticFiles();
                    // Stands in for routing and MapStaticAssets.
                    app.Run(ctx => ctx.Response.WriteAsync(Fallthrough));
                }))
            .StartAsync();
        return host.GetTestClient();
    }

    [Fact]
    public async Task Uploads_are_served_from_wwwroot()
    {
        var client = await StartAsync();

        var response = await client.GetAsync("/uploads/products/photo.jpg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("jpeg", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/_framework/dotnet.native.abc123.wasm")]
    [InlineData("/fonts/geologica-latin.woff2")]
    public async Task Build_time_assets_are_left_to_MapStaticAssets(string path)
    {
        var client = await StartAsync();

        var body = await client.GetStringAsync(path);

        Assert.Equal(Fallthrough, body);
    }

    [Fact]
    public async Task A_missing_upload_falls_through_to_the_not_found_handling()
    {
        var client = await StartAsync();

        var body = await client.GetStringAsync("/uploads/products/missing.jpg");

        Assert.Equal(Fallthrough, body);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}

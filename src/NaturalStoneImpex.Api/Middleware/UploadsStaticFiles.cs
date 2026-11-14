namespace NaturalStoneImpex.Api.Middleware;

public static class UploadsStaticFilesExtensions
{
    /// <summary>
    /// Serves runtime uploads (/uploads) from wwwroot, and nothing else. Every build-time asset goes through
    /// MapStaticAssets (brotli/gzip, fingerprints, immutable caching). A bare UseStaticFiles would answer any file
    /// that exists on disk first: the fingerprinted framework files (dotnet.native.{hash}.wasm and the assemblies)
    /// and the fonts, uncompressed and without long-lived caching.
    /// </summary>
    public static IApplicationBuilder UseUploadsStaticFiles(this IApplicationBuilder app)
        => app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/uploads"), b => b.UseStaticFiles());
}

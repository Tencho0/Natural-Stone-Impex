using Microsoft.AspNetCore.Diagnostics;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Request rules for the designed /error and /not-found pages (non-API requests only).</summary>
public static class ErrorPages
{
    /// <summary>
    /// Runs after UseExceptionHandler and UseStatusCodePagesWithReExecute, on the first pass and on re-execution.
    /// </summary>
    public static void Prepare(HttpContext context)
    {
        // Missing static files (favicon, images, CSS, JS) keep a plain 404: no layout render, no flash consumed.
        if (IsStaticFileRequest(context.Request.Path)
            && context.Features.Get<IStatusCodePagesFeature>() is { } statusPages)
            statusPages.Enabled = false;

        // Re-execution keeps the original method. A POST to the /error or /not-found component would be read
        // as an unnamed form post and answered with a bare 400, so the pages are always rendered as GET.
        if (IsReExecution(context))
            context.Request.Method = HttpMethods.Get;
    }

    /// <summary>True when the last path segment has a file extension, e.g. "/favicon.ico", "/css/x.css".</summary>
    public static bool IsStaticFileRequest(PathString path)
        => path.HasValue && Path.HasExtension(path.Value);

    private static bool IsReExecution(HttpContext context)
        => context.Features.Get<IStatusCodeReExecuteFeature>() is not null
            || context.Features.Get<IExceptionHandlerFeature>() is not null;
}

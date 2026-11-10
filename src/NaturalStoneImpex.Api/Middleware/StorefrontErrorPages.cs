namespace NaturalStoneImpex.Api.Middleware;

public static class StorefrontErrorPagesExtensions
{
    /// <summary>
    /// Non-API requests get the designed /error and /not-found pages. Shared by Program.cs and the HEAD tests so the
    /// pipeline order cannot drift; UseHeadAsGet and UseRouting must come after it.
    /// </summary>
    public static IApplicationBuilder UseStorefrontErrorPages(this IApplicationBuilder app, bool isDevelopment)
        => app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), b =>
        {
            // Development keeps the developer exception page; verify the designed /error with ASPNETCORE_ENVIRONMENT=Production.
            if (!isDevelopment)
                b.UseExceptionHandler("/error", createScopeForErrors: true);
            b.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            b.Use((ctx, next) =>
            {
                NaturalStoneImpex.Api.Storefront.ErrorPages.Prepare(ctx);
                return next(ctx);
            });
        });
}

namespace NaturalStoneImpex.Api.Middleware;

/// <summary>
/// Razor component and minimal GET endpoints do not match HEAD (ruling R11). For non-/api paths, a HEAD
/// request runs as GET with the response body discarded; /api keeps its own behaviour.
/// </summary>
public static class HeadAsGetMiddleware
{
    public static IApplicationBuilder UseHeadAsGet(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (!HttpMethods.IsHead(request.Method) || request.Path.StartsWithSegments("/api"))
            {
                await next(context);
                return;
            }

            var originalBody = context.Response.Body;
            request.Method = HttpMethods.Get;
            context.Response.Body = Stream.Null;
            try
            {
                await next(context);
            }
            finally
            {
                context.Response.Body = originalBody;
                request.Method = HttpMethods.Head;
            }
        });
}

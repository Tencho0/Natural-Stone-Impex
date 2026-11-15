using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class ErrorPagesTests
{
    private sealed class StatusPagesFeature : IStatusCodePagesFeature
    {
        public bool Enabled { get; set; } = true;
    }

    [Theory]
    [InlineData("/favicon.ico", true)]
    [InlineData("/uploads/products/missing.jpg", true)]
    [InlineData("/css/nsi/missing.css", true)]
    [InlineData("/js/storefront/x.js", true)]
    [InlineData("/nope", false)]
    [InlineData("/products/12", false)]
    [InlineData("/order-confirmation/NSI-20260218-0001", false)]
    [InlineData("/v1.2/page", false)]
    [InlineData("/", false)]
    public void IsStaticFileRequest_checks_the_last_segment_for_an_extension(string path, bool expected)
        => Assert.Equal(expected, ErrorPages.IsStaticFileRequest(new PathString(path)));

    [Theory]
    [InlineData("/favicon.ico", false)]
    [InlineData("/nope", true)]
    public void Prepare_turns_status_pages_off_for_static_files(string path, bool enabledAfter)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        var feature = new StatusPagesFeature();
        ctx.Features.Set<IStatusCodePagesFeature>(feature);

        ErrorPages.Prepare(ctx);

        Assert.Equal(enabledAfter, feature.Enabled);
    }

    [Fact]
    public void Prepare_renders_a_reexecuted_status_page_as_get()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPathBase = "",
            OriginalPath = "/cart/add"
        });

        ErrorPages.Prepare(ctx);

        Assert.Equal(HttpMethods.Get, ctx.Request.Method);
    }

    [Fact]
    public void Prepare_renders_the_error_page_as_get()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Path = "/cart/add",
            Error = new InvalidOperationException()
        });

        ErrorPages.Prepare(ctx);

        Assert.Equal(HttpMethods.Get, ctx.Request.Method);
    }

    [Fact]
    public void Prepare_leaves_a_normal_post_alone()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/cart/add";

        ErrorPages.Prepare(ctx);

        Assert.Equal(HttpMethods.Post, ctx.Request.Method);
    }
}

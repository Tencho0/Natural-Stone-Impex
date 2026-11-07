using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Auth;

namespace NaturalStoneImpex.Api.Tests.Auth;

public class AdminCsrfHeaderTests
{
    [Fact]
    public void Cookie_authenticated_api_post_without_header_is_rejected()
        => Assert.False(AdminCsrfHeaderMiddleware.IsAllowed(
            "POST", "/api/products", AuthSchemes.Cookie, new HeaderDictionary()));

    [Fact]
    public void Cookie_authenticated_api_post_with_header_is_allowed()
        => Assert.True(AdminCsrfHeaderMiddleware.IsAllowed(
            "POST", "/api/products", AuthSchemes.Cookie, new HeaderDictionary { ["X-NSI-Request"] = "1" }));

    [Fact]
    public void Get_requests_are_always_allowed()
        => Assert.True(AdminCsrfHeaderMiddleware.IsAllowed(
            "GET", "/api/products", AuthSchemes.Cookie, new HeaderDictionary()));

    [Fact]
    public void Bearer_authenticated_requests_are_allowed_without_header()
        => Assert.True(AdminCsrfHeaderMiddleware.IsAllowed(
            "POST", "/api/products", AuthSchemes.Bearer, new HeaderDictionary()));

    [Fact]
    public void Anonymous_cart_items_post_requires_header()
        => Assert.False(AdminCsrfHeaderMiddleware.IsAllowed(
            "POST", "/api/cart/items", null, new HeaderDictionary()));
}

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

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void Cookie_authenticated_unsafe_methods_without_header_are_rejected(string method)
        => Assert.False(AdminCsrfHeaderMiddleware.IsAllowed(
            method, "/api/products/5", AuthSchemes.Cookie, new HeaderDictionary()));

    [Fact]
    public void Anonymous_cart_item_subpath_delete_requires_header()
        => Assert.False(AdminCsrfHeaderMiddleware.IsAllowed(
            "DELETE", "/api/cart/items/5", null, new HeaderDictionary()));

    [Fact]
    public void Anonymous_post_to_lookalike_path_is_not_matched_by_cart_rule()
        => Assert.True(AdminCsrfHeaderMiddleware.IsAllowed(
            "POST", "/api/cart/itemsX", null, new HeaderDictionary()));
}

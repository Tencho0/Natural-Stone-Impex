using Microsoft.AspNetCore.Http;
using NaturalStoneImpex.Api.Auth;

namespace NaturalStoneImpex.Api.Tests.Auth;

public class AuthSchemesTests
{
    [Fact]
    public void Selects_bearer_when_authorization_header_has_bearer_token()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer x";

        Assert.Equal(AuthSchemes.Bearer, AuthSchemes.Select(ctx));
    }

    [Fact]
    public void Selects_bearer_for_lowercase_scheme_name()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "bearer x";

        Assert.Equal(AuthSchemes.Bearer, AuthSchemes.Select(ctx));
    }

    [Fact]
    public void Selects_cookie_otherwise()
    {
        var ctx = new DefaultHttpContext();

        Assert.Equal(AuthSchemes.Cookie, AuthSchemes.Select(ctx));
    }
}

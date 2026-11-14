using System.Net;
using System.Text;
using System.Text.Json;
using NaturalStoneImpex.Client.Services;

namespace NaturalStoneImpex.Api.Tests.Storefront;

/// <summary>The visualizer's cart client: 204 → null, 400 → the server's error, anything else → a generic message.</summary>
public class CartApiTests
{
    [Fact]
    public async Task No_content_means_added()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var error = await CreateApi(handler).AddAsync(42, 1m);

        Assert.Null(error);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://localhost/api/cart/items", request.Uri);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(42, body.RootElement.GetProperty("productId").GetInt32());
        Assert.Equal(1m, body.RootElement.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Bad_request_returns_the_server_error()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{ "error": "Продуктът не е наличен." }"""));

        var error = await CreateApi(handler).AddAsync(7, 2.5m);

        Assert.Equal("Продуктът не е наличен.", error);
    }

    [Fact]
    public async Task Bad_request_without_an_error_message_returns_the_generic_error()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{ "title": "Bad Request" }"""));

        Assert.Equal(CartApi.GenericError, await CreateApi(handler).AddAsync(7, 1m));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Other_statuses_return_the_generic_error(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Json(status, """{ "error": "Не се показва." }"""));

        Assert.Equal(CartApi.GenericError, await CreateApi(handler).AddAsync(7, 1m));
    }

    [Fact]
    public async Task Network_failure_returns_the_generic_error()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("Connection refused"));

        Assert.Equal(CartApi.GenericError, await CreateApi(handler).AddAsync(7, 1m));
    }

    [Fact]
    public async Task Bad_request_with_a_body_that_is_not_json_returns_the_generic_error()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("<html>грешка</html>", Encoding.UTF8, "text/html")
        });

        Assert.Equal(CartApi.GenericError, await CreateApi(handler).AddAsync(7, 1m));
    }

    [Fact]
    public void Generic_error_is_bulgarian_and_says_what_to_do()
    {
        Assert.Equal("Неуспешно добавяне в количката. Опитайте отново.", CartApi.GenericError);
    }

    private static CartApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record SeenRequest(HttpMethod Method, string Uri, string? Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SeenRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SeenRequest(request.Method, request.RequestUri!.ToString(), body));
            return respond(request);
        }
    }
}

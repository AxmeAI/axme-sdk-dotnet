using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Axme.Sdk;
using Xunit;

namespace Axme.Sdk.Tests;

public sealed class AxmeClientTests
{
    [Fact]
    public async Task RegisterNickAsync_SendsPayloadAndHeaders()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"owner_agent":"agent://user/1"}"""));
        var client = BuildClient(handler);

        var response = await client.RegisterNickAsync(
            new JsonObject { ["nick"] = "@partner.user", ["display_name"] = "Partner User" },
            new RequestOptions { IdempotencyKey = "register-1" });

        Assert.Equal("/v1/users/register-nick", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("token", handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.Contains("register-1", handler.LastRequest.Headers.GetValues("Idempotency-Key"));

        var body = JsonNode.Parse(await handler.LastRequest.Content!.ReadAsStringAsync())!.AsObject();
        Assert.Equal("@partner.user", body["nick"]!.GetValue<string>());
        Assert.True(response["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task CheckNickAsync_SendsQueryParameter()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"available":true}"""));
        var client = BuildClient(handler);

        var response = await client.CheckNickAsync("@partner.user");

        Assert.Equal("/v1/users/check-nick", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?nick=%40partner.user", handler.LastRequest.RequestUri.Query);
        Assert.True(response["available"]!.GetValue<bool>());
    }

    [Fact]
    public async Task RenameNickAsync_SendsPayloadAndIdempotency()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"nick":"@partner.new"}"""));
        var client = BuildClient(handler);

        var response = await client.RenameNickAsync(
            new JsonObject { ["owner_agent"] = "agent://user/1", ["nick"] = "@partner.new" },
            new RequestOptions { IdempotencyKey = "rename-1" });

        Assert.Equal("/v1/users/rename-nick", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("rename-1", handler.LastRequest.Headers.GetValues("Idempotency-Key"));
        Assert.Equal("@partner.new", response["nick"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetUserProfileAsync_SendsOwnerAgentQuery()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"owner_agent":"agent://user/1"}"""));
        var client = BuildClient(handler);

        var response = await client.GetUserProfileAsync("agent://user/1");

        Assert.Equal("/v1/users/profile", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?owner_agent=agent%3A%2F%2Fuser%2F1", handler.LastRequest.RequestUri.Query);
        Assert.Equal("agent://user/1", response["owner_agent"]!.GetValue<string>());
    }

    [Fact]
    public async Task UpdateUserProfileAsync_SendsPayloadAndIdempotency()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"display_name":"Partner User Updated"}"""));
        var client = BuildClient(handler);

        var response = await client.UpdateUserProfileAsync(
            new JsonObject { ["owner_agent"] = "agent://user/1", ["display_name"] = "Partner User Updated" },
            new RequestOptions { IdempotencyKey = "profile-1" });

        Assert.Equal("/v1/users/profile/update", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("profile-1", handler.LastRequest.Headers.GetValues("Idempotency-Key"));
        Assert.Equal("Partner User Updated", response["display_name"]!.GetValue<string>());
    }

    private static AxmeClient BuildClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new AxmeClient(
            new AxmeClientConfig
            {
                BaseUrl = "https://api.axme.test",
                ApiKey = "token",
            },
            httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }
}

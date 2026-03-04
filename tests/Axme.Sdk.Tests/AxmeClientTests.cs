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

    [Fact]
    public async Task ServiceAccountLifecycleEndpoints_AreReachable()
    {
        var call = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                call += 1;
                return call switch
                {
                    1 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"service_account":{"service_account_id":"sa_123"}}"""),
                    2 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"service_accounts":[{"service_account_id":"sa_123"}]}"""),
                    3 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"service_account":{"service_account_id":"sa_123"}}"""),
                    4 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"key":{"key_id":"sak_123","status":"active"}}"""),
                    _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"key":{"key_id":"sak_123","status":"revoked"}}"""),
                };
            });
        var client = BuildClient(handler);

        await client.CreateServiceAccountAsync(
            new JsonObject
            {
                ["org_id"] = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
                ["workspace_id"] = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
                ["name"] = "sdk-runner",
                ["created_by_actor_id"] = "actor_dotnet",
            });
        Assert.Equal("/v1/service-accounts", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.ListServiceAccountsAsync(
            "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        Assert.Equal("/v1/service-accounts", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.GetServiceAccountAsync("sa_123");
        Assert.Equal("/v1/service-accounts/sa_123", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.CreateServiceAccountKeyAsync("sa_123", new JsonObject { ["created_by_actor_id"] = "actor_dotnet" });
        Assert.Equal("/v1/service-accounts/sa_123/keys", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.RevokeServiceAccountKeyAsync("sa_123", "sak_123");
        Assert.Equal("/v1/service-accounts/sa_123/keys/sak_123/revoke", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task IntentLifecycleAndControlEndpoints_AreReachable()
    {
        var call = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                call += 1;
                return call switch
                {
                    1 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"intent_id":"it_123"}"""),
                    2 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"intent":{"intent_id":"it_123"}}"""),
                    3 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"events":[]}"""),
                    4 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"applied":false,"policy_generation":4}"""),
                    5 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"applied":true}"""),
                    6 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"applied":true,"policy_generation":5}"""),
                    _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"applied":true,"policy_generation":6}"""),
                };
            });
        var client = BuildClient(handler);

        await client.CreateIntentAsync(
            new JsonObject
            {
                ["intent_type"] = "notify.message.v1",
                ["from_agent"] = "agent://self",
                ["to_agent"] = "agent://target",
                ["payload"] = new JsonObject { ["text"] = "hello" },
            });
        Assert.Equal("/v1/intents", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.GetIntentAsync("it_123");
        Assert.Equal("/v1/intents/it_123", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.ListIntentEventsAsync("it_123", since: 2);
        Assert.Equal("/v1/intents/it_123/events", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?since=2", handler.LastRequest!.RequestUri.Query);

        await client.ResolveIntentAsync(
            "it_123",
            new JsonObject
            {
                ["status"] = "COMPLETED",
                ["expected_policy_generation"] = 3,
            },
            new RequestOptions
            {
                OwnerAgent = "agent://owner",
                XOwnerAgent = "agent://owner",
                Authorization = "Bearer scoped-token",
                TraceId = "trace-1",
            });
        Assert.Equal("/v1/intents/it_123/resolve", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?owner_agent=agent%3A%2F%2Fowner", handler.LastRequest!.RequestUri.Query);
        Assert.Contains("Bearer scoped-token", handler.LastRequest.Headers.GetValues("Authorization"));
        Assert.Contains("agent://owner", handler.LastRequest.Headers.GetValues("x-owner-agent"));
        Assert.Contains("trace-1", handler.LastRequest.Headers.GetValues("X-Trace-Id"));

        await client.ResumeIntentAsync(
            "it_123",
            new JsonObject
            {
                ["approve_current_step"] = true,
                ["expected_policy_generation"] = 2,
            },
            new RequestOptions
            {
                OwnerAgent = "agent://owner",
                IdempotencyKey = "resume-1",
            });
        Assert.Equal("/v1/intents/it_123/resume", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?owner_agent=agent%3A%2F%2Fowner", handler.LastRequest!.RequestUri.Query);
        Assert.Contains("resume-1", handler.LastRequest.Headers.GetValues("Idempotency-Key"));

        await client.UpdateIntentControlsAsync(
            "it_123",
            new JsonObject
            {
                ["controls_patch"] = new JsonObject { ["timeout_seconds"] = 120 },
                ["expected_policy_generation"] = 5,
            });
        Assert.Equal("/v1/intents/it_123/controls", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.UpdateIntentPolicyAsync(
            "it_123",
            new JsonObject
            {
                ["grants_patch"] = new JsonObject
                {
                    ["delegate:agent://ops"] = new JsonObject
                    {
                        ["allow"] = new JsonArray("resume", "update_controls"),
                    },
                },
                ["envelope_patch"] = new JsonObject { ["max_retry_count"] = 10 },
                ["expected_policy_generation"] = 5,
            },
            new RequestOptions
            {
                OwnerAgent = "agent://creator",
            });
        Assert.Equal("/v1/intents/it_123/policy", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?owner_agent=agent%3A%2F%2Fcreator", handler.LastRequest!.RequestUri.Query);
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

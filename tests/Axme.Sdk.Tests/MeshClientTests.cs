using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Axme.Sdk;
using Xunit;

namespace Axme.Sdk.Tests;

public sealed class MeshClientTests
{
    // -- Heartbeat -------------------------------------------------------

    [Fact]
    public async Task HeartbeatAsync_PostsToCorrectEndpoint()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"status":"healthy"}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.HeartbeatAsync();

        Assert.Equal("/v1/mesh/heartbeat", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.True(result["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task HeartbeatAsync_IncludesMetricsPayload()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        var metrics = new JsonObject
        {
            ["intents_total"] = 10,
            ["intents_succeeded"] = 8,
        };
        await client.Mesh.HeartbeatAsync(metrics: metrics);

        Assert.NotNull(handler.LastRequest!.Content);
        var body = JsonNode.Parse(await handler.LastRequest.Content!.ReadAsStringAsync())!.AsObject();
        Assert.Equal(10, body["metrics"]!["intents_total"]!.GetValue<int>());
        Assert.Equal(8, body["metrics"]!["intents_succeeded"]!.GetValue<int>());
    }

    [Fact]
    public async Task HeartbeatAsync_WithoutMetrics_SendsNoBody()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        await client.Mesh.HeartbeatAsync();

        Assert.Null(handler.LastRequest!.Content);
    }

    [Fact]
    public async Task HeartbeatAsync_ForwardsRequestOptions()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        await client.Mesh.HeartbeatAsync(
            options: new RequestOptions { TraceId = "trace-mesh-1" });

        Assert.Contains("trace-mesh-1", handler.LastRequest!.Headers.GetValues("X-Trace-Id"));
    }

    // -- ReportMetric + FlushMetrics -------------------------------------

    [Fact]
    public void ReportMetric_BuffersSuccessMetric()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        client.Mesh.ReportMetric(success: true, latencyMs: 100.0, costUsd: 0.01);
        client.Mesh.ReportMetric(success: true, latencyMs: 200.0, costUsd: 0.02);
        client.Mesh.ReportMetric(success: false, latencyMs: 300.0);

        var metrics = client.Mesh.FlushMetrics();

        Assert.NotNull(metrics);
        Assert.Equal(3, metrics["intents_total"]!.GetValue<double>());
        Assert.Equal(2, metrics["intents_succeeded"]!.GetValue<double>());
        Assert.Equal(1, metrics["intents_failed"]!.GetValue<double>());
        Assert.Equal(0.03, metrics["cost_usd"]!.GetValue<double>(), 5);
        // Running average: (100 + 200 + 300) / 3 = 200
        Assert.Equal(200.0, metrics["avg_latency_ms"]!.GetValue<double>(), 1);
    }

    [Fact]
    public void FlushMetrics_ClearsBuffer()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        client.Mesh.ReportMetric(success: true);
        var first = client.Mesh.FlushMetrics();
        Assert.NotNull(first);

        var second = client.Mesh.FlushMetrics();
        Assert.Null(second);
    }

    [Fact]
    public void FlushMetrics_ReturnsNullWhenEmpty()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        var result = client.Mesh.FlushMetrics();
        Assert.Null(result);
    }

    // -- StartHeartbeat / StopHeartbeat ----------------------------------

    [Fact]
    public async Task StartHeartbeat_SendsPeriodicHeartbeats()
    {
        var callCount = 0;
        var handler = new StubHandler(_ =>
        {
            Interlocked.Increment(ref callCount);
            return JsonResponse(HttpStatusCode.OK, """{"ok":true}""");
        });
        var client = BuildClient(handler);

        client.Mesh.StartHeartbeat(intervalSeconds: 0.1, includeMetrics: false);

        // Wait enough for at least 2 heartbeats
        await Task.Delay(350);

        await client.Mesh.StopHeartbeatAsync();

        Assert.True(callCount >= 2, $"Expected at least 2 heartbeats but got {callCount}");
    }

    [Fact]
    public async Task StartHeartbeat_DoubleStartIsNoOp()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        client.Mesh.StartHeartbeat(intervalSeconds: 60);
        client.Mesh.StartHeartbeat(intervalSeconds: 60); // Should not throw or start a second loop

        await client.Mesh.StopHeartbeatAsync();
    }

    [Fact]
    public async Task StopHeartbeat_WhenNotRunning_IsNoOp()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        // Should not throw
        await client.Mesh.StopHeartbeatAsync();
    }

    [Fact]
    public async Task StartHeartbeat_IncludesBufferedMetrics()
    {
        JsonObject? capturedBody = null;
        var handler = new StubHandler(request =>
        {
            if (request.Content is not null)
            {
                var raw = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                capturedBody = JsonNode.Parse(raw)?.AsObject();
            }
            return JsonResponse(HttpStatusCode.OK, """{"ok":true}""");
        });
        var client = BuildClient(handler);

        client.Mesh.ReportMetric(success: true, latencyMs: 42.0);
        client.Mesh.StartHeartbeat(intervalSeconds: 0.1, includeMetrics: true);

        await Task.Delay(250);
        await client.Mesh.StopHeartbeatAsync();

        Assert.NotNull(capturedBody);
        Assert.NotNull(capturedBody!["metrics"]);
        Assert.Equal(1, capturedBody["metrics"]!["intents_total"]!.GetValue<double>());
    }

    // -- ListAgents ------------------------------------------------------

    [Fact]
    public async Task ListAgentsAsync_GetsWithLimitAndHealth()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"agents":[]}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.ListAgentsAsync(limit: 25, health: "healthy");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v1/mesh/agents", handler.LastRequest.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("limit=25", query);
        Assert.Contains("health=healthy", query);
    }

    [Fact]
    public async Task ListAgentsAsync_DefaultParams()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"agents":[]}"""));
        var client = BuildClient(handler);

        await client.Mesh.ListAgentsAsync();

        Assert.Contains("limit=100", handler.LastRequest!.RequestUri!.Query);
        Assert.DoesNotContain("health=", handler.LastRequest.RequestUri.Query);
    }

    // -- GetAgent --------------------------------------------------------

    [Fact]
    public async Task GetAgentAsync_GetsCorrectPath()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"agent":{"address_id":"agent_1"}}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.GetAgentAsync("agent_1");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v1/mesh/agents/agent_1", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAgentAsync_RejectsEmptyAddressId()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.Mesh.GetAgentAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Mesh.GetAgentAsync("   "));
    }

    // -- Kill ------------------------------------------------------------

    [Fact]
    public async Task KillAsync_PostsToCorrectEndpoint()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"killed":true}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.KillAsync("agent_1");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v1/mesh/agents/agent_1/kill", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.True(result["killed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task KillAsync_RejectsEmptyAddressId()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.Mesh.KillAsync(""));
    }

    // -- Resume ----------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_PostsToCorrectEndpoint()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"resumed":true}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.ResumeAsync("agent_1");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v1/mesh/agents/agent_1/resume", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.True(result["resumed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ResumeAsync_RejectsEmptyAddressId()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.Mesh.ResumeAsync(""));
    }

    [Fact]
    public async Task ResumeAsync_ForwardsRequestOptions()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        await client.Mesh.ResumeAsync("agent_1",
            options: new RequestOptions { TraceId = "trace-resume" });

        Assert.Contains("trace-resume", handler.LastRequest!.Headers.GetValues("X-Trace-Id"));
    }

    // -- ListEvents ------------------------------------------------------

    [Fact]
    public async Task ListEventsAsync_GetsWithLimitAndEventType()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"events":[]}"""));
        var client = BuildClient(handler);

        var result = await client.Mesh.ListEventsAsync(limit: 10, eventType: "agent.killed");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v1/mesh/events", handler.LastRequest.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("limit=10", query);
        Assert.Contains("event_type=agent.killed", query);
    }

    [Fact]
    public async Task ListEventsAsync_DefaultParams()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"events":[]}"""));
        var client = BuildClient(handler);

        await client.Mesh.ListEventsAsync();

        Assert.Contains("limit=50", handler.LastRequest!.RequestUri!.Query);
        Assert.DoesNotContain("event_type=", handler.LastRequest.RequestUri.Query);
    }

    // -- Mesh property is lazy singleton ---------------------------------

    [Fact]
    public void MeshProperty_ReturnsSameInstance()
    {
        var client = BuildClient(new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}""")));

        var mesh1 = client.Mesh;
        var mesh2 = client.Mesh;

        Assert.Same(mesh1, mesh2);
    }

    // -- HTTP error propagation ------------------------------------------

    [Fact]
    public async Task HeartbeatAsync_PropagatesHttpError()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.ServiceUnavailable, """{"error":"overloaded"}"""));
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<AxmeHttpException>(
            () => client.Mesh.HeartbeatAsync());

        Assert.Equal(503, ex.StatusCode);
        Assert.Contains("overloaded", ex.ResponseBody);
    }

    [Fact]
    public async Task KillAsync_PropagatesNotFound()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.NotFound, """{"error":"agent not found"}"""));
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<AxmeHttpException>(
            () => client.Mesh.KillAsync("nonexistent"));

        Assert.Equal(404, ex.StatusCode);
    }

    // -- Auth headers are forwarded --------------------------------------

    [Fact]
    public async Task MeshRequests_IncludeApiKeyHeader()
    {
        var handler = new StubHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""));
        var client = BuildClient(handler);

        await client.Mesh.ListAgentsAsync();

        Assert.Contains("test-api-key", handler.LastRequest!.Headers.GetValues("x-api-key"));
    }

    // -- Helpers ---------------------------------------------------------

    private static AxmeClient BuildClient(StubHandler handler)
    {
        return new AxmeClient(
            new AxmeClientConfig
            {
                BaseUrl = "https://api.axme.test",
                ApiKey = "test-api-key",
            },
            new HttpClient(handler));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }
}

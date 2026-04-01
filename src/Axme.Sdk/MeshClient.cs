using System.Text.Json.Nodes;

namespace Axme.Sdk;

/// <summary>
/// Agent Mesh operations - heartbeat, health monitoring, metrics reporting, and agent lifecycle.
/// Access via <see cref="AxmeClient.Mesh"/>.
/// </summary>
public sealed class MeshClient
{
    private readonly AxmeClient _client;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private readonly object _heartbeatLock = new();
    private readonly object _metricsLock = new();
    private Dictionary<string, double> _metricsBuffer = new();

    internal MeshClient(AxmeClient client)
    {
        _client = client;
    }

    // -- Heartbeat -------------------------------------------------------

    /// <summary>
    /// Send a single heartbeat to the mesh. Optionally include metrics.
    /// </summary>
    public Task<JsonObject> HeartbeatAsync(
        JsonObject? metrics = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        JsonObject? payload = null;
        if (metrics is not null)
        {
            payload = new JsonObject { ["metrics"] = metrics.DeepClone() };
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Post,
            "/v1/mesh/heartbeat",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Start a background task that sends heartbeats at regular intervals.
    /// If already running, this is a no-op.
    /// </summary>
    /// <param name="intervalSeconds">Seconds between heartbeats (default 30).</param>
    /// <param name="includeMetrics">Whether to include buffered metrics with each heartbeat.</param>
    public void StartHeartbeat(double intervalSeconds = 30.0, bool includeMetrics = true)
    {
        lock (_heartbeatLock)
        {
            if (_heartbeatCts is not null)
            {
                return; // Already running
            }

            var cts = new CancellationTokenSource();
            _heartbeatCts = cts;
            _heartbeatTask = RunHeartbeatLoopAsync(intervalSeconds, includeMetrics, cts.Token);
        }
    }

    /// <summary>
    /// Stop the background heartbeat task.
    /// </summary>
    public async Task StopHeartbeatAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_heartbeatLock)
        {
            cts = _heartbeatCts;
            task = _heartbeatTask;
            _heartbeatCts = null;
            _heartbeatTask = null;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            if (task is not null)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation
                }
            }
            cts.Dispose();
        }
    }

    private async Task RunHeartbeatLoopAsync(
        double intervalSeconds, bool includeMetrics, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var metrics = includeMetrics ? FlushMetrics() : null;
                await HeartbeatAsync(metrics, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Heartbeat failures are non-fatal; continue loop.
            }
        }
    }

    // -- Metrics ---------------------------------------------------------

    /// <summary>
    /// Buffer a metric observation. Flushed with the next heartbeat.
    /// </summary>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="latencyMs">Optional latency in milliseconds.</param>
    /// <param name="costUsd">Optional cost in USD.</param>
    public void ReportMetric(bool success = true, double? latencyMs = null, double? costUsd = null)
    {
        lock (_metricsLock)
        {
            var buf = _metricsBuffer;
            buf["intents_total"] = buf.GetValueOrDefault("intents_total", 0) + 1;
            if (success)
            {
                buf["intents_succeeded"] = buf.GetValueOrDefault("intents_succeeded", 0) + 1;
            }
            else
            {
                buf["intents_failed"] = buf.GetValueOrDefault("intents_failed", 0) + 1;
            }
            if (latencyMs.HasValue)
            {
                var count = buf["intents_total"];
                var prevAvg = buf.GetValueOrDefault("avg_latency_ms", 0.0);
                buf["avg_latency_ms"] = prevAvg + (latencyMs.Value - prevAvg) / count;
            }
            if (costUsd.HasValue)
            {
                buf["cost_usd"] = buf.GetValueOrDefault("cost_usd", 0.0) + costUsd.Value;
            }
        }
    }

    internal JsonObject? FlushMetrics()
    {
        Dictionary<string, double> snapshot;
        lock (_metricsLock)
        {
            if (_metricsBuffer.Count == 0)
            {
                return null;
            }
            snapshot = _metricsBuffer;
            _metricsBuffer = new Dictionary<string, double>();
        }

        var obj = new JsonObject();
        foreach (var (key, value) in snapshot)
        {
            obj[key] = value;
        }
        return obj;
    }

    // -- Agent management ------------------------------------------------

    /// <summary>
    /// List all agents in workspace with health status.
    /// </summary>
    public Task<JsonObject> ListAgentsAsync(
        int limit = 100,
        string? health = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (!string.IsNullOrWhiteSpace(health))
        {
            query["health"] = health;
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Get,
            "/v1/mesh/agents",
            query: query,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get a single agent detail with metrics and events.
    /// </summary>
    public Task<JsonObject> GetAgentAsync(
        string addressId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addressId))
        {
            throw new ArgumentException("addressId is required", nameof(addressId));
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Get,
            $"/v1/mesh/agents/{addressId}",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Kill an agent - block all intents to and from it.
    /// </summary>
    public Task<JsonObject> KillAsync(
        string addressId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addressId))
        {
            throw new ArgumentException("addressId is required", nameof(addressId));
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Post,
            $"/v1/mesh/agents/{addressId}/kill",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Resume a killed agent.
    /// </summary>
    public Task<JsonObject> ResumeAsync(
        string addressId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addressId))
        {
            throw new ArgumentException("addressId is required", nameof(addressId));
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Post,
            $"/v1/mesh/agents/{addressId}/resume",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// List recent mesh events (kills, resumes, health changes).
    /// </summary>
    public Task<JsonObject> ListEventsAsync(
        int limit = 50,
        string? eventType = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query["event_type"] = eventType;
        }
        return _client.RequestJsonInternalAsync(
            HttpMethod.Get,
            "/v1/mesh/events",
            query: query,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }
}

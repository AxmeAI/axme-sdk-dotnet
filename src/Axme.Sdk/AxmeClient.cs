using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Axme.Sdk;

public sealed class AxmeClient
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public AxmeClient(AxmeClientConfig config, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new ArgumentException("BaseUrl is required", nameof(config));
        }
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new ArgumentException("ApiKey is required", nameof(config));
        }

        _baseUrl = config.BaseUrl.TrimEnd('/');
        _apiKey = config.ApiKey.Trim();
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task<JsonObject> RegisterNickAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/users/register-nick",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> CheckNickAsync(
        string nick,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/users/check-nick",
            query: new Dictionary<string, string> { ["nick"] = nick },
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> RenameNickAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/users/rename-nick",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> GetUserProfileAsync(
        string ownerAgent,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/users/profile",
            query: new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> UpdateUserProfileAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/users/profile/update",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    private async Task<JsonObject> RequestJsonAsync(
        HttpMethod method,
        string path,
        Dictionary<string, string>? query,
        JsonObject? payload,
        RequestOptions? options,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, BuildUrl(path, query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(options?.IdempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", options.IdempotencyKey);
        }
        if (!string.IsNullOrWhiteSpace(options?.TraceId))
        {
            request.Headers.TryAddWithoutValidation("X-Trace-Id", options.TraceId);
        }
        if (payload is not null)
        {
            request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new AxmeHttpException((int)response.StatusCode, raw);
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new JsonObject();
        }

        var parsed = JsonNode.Parse(raw) as JsonObject;
        if (parsed is null)
        {
            throw new InvalidOperationException("response JSON must be an object");
        }

        return parsed;
    }

    private string BuildUrl(string path, Dictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return $"{_baseUrl}{path}";
        }

        var parts = new List<string>();
        foreach (var (key, value) in query)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        if (parts.Count == 0)
        {
            return $"{_baseUrl}{path}";
        }

        return $"{_baseUrl}{path}?{string.Join("&", parts)}";
    }
}

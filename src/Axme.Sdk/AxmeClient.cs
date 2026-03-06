using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Axme.Sdk;

public sealed class AxmeClient
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string? _actorToken;
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
        var actorToken = string.IsNullOrWhiteSpace(config.ActorToken) ? null : config.ActorToken.Trim();
        var bearerToken = string.IsNullOrWhiteSpace(config.BearerToken) ? null : config.BearerToken.Trim();
        if (!string.IsNullOrWhiteSpace(actorToken)
            && !string.IsNullOrWhiteSpace(bearerToken)
            && !string.Equals(actorToken, bearerToken, StringComparison.Ordinal))
        {
            throw new ArgumentException("ActorToken and BearerToken must match when both are provided", nameof(config));
        }

        _baseUrl = config.BaseUrl.TrimEnd('/');
        _apiKey = config.ApiKey.Trim();
        _actorToken = actorToken ?? bearerToken;
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

    public Task<JsonObject> CreateServiceAccountAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/service-accounts",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> ListServiceAccountsAsync(
        string orgId,
        string? workspaceId = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/service-accounts",
            query: new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId ?? string.Empty,
            },
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> GetServiceAccountAsync(
        string serviceAccountId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            $"/v1/service-accounts/{serviceAccountId}",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> CreateServiceAccountKeyAsync(
        string serviceAccountId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/service-accounts/{serviceAccountId}/keys",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> RevokeServiceAccountKeyAsync(
        string serviceAccountId,
        string keyId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/service-accounts/{serviceAccountId}/keys/{keyId}/revoke",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> CreateIntentAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/intents",
            query: null,
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> GetIntentAsync(
        string intentId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            $"/v1/intents/{intentId}",
            query: null,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> ListIntentEventsAsync(
        string intentId,
        int? since = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (since is int sinceValue && sinceValue >= 0)
        {
            query["since"] = sinceValue.ToString();
        }

        return RequestJsonAsync(
            HttpMethod.Get,
            $"/v1/intents/{intentId}/events",
            query: query,
            payload: null,
            options: options,
            cancellationToken: cancellationToken);
    }

    public Task<JsonObject> ResolveIntentAsync(
        string intentId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/intents/{intentId}/resolve",
            query: BuildIntentControlQuery(options),
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> ResumeIntentAsync(
        string intentId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/intents/{intentId}/resume",
            query: BuildIntentControlQuery(options),
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> UpdateIntentControlsAsync(
        string intentId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/intents/{intentId}/controls",
            query: BuildIntentControlQuery(options),
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> UpdateIntentPolicyAsync(
        string intentId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/intents/{intentId}/policy",
            query: BuildIntentControlQuery(options),
            payload: payload,
            options: options,
            cancellationToken: cancellationToken);

    public Task<JsonObject> CreateAccessRequestAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/access-requests", null, payload, options, cancellationToken);

    public Task<JsonObject> ListAccessRequestsAsync(
        string? orgId = null,
        string? workspaceId = null,
        string? state = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(orgId))
        {
            query["org_id"] = orgId;
        }
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            query["workspace_id"] = workspaceId;
        }
        if (!string.IsNullOrWhiteSpace(state))
        {
            query["state"] = state;
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/access-requests", query, null, options, cancellationToken);
    }

    public Task<JsonObject> GetAccessRequestAsync(
        string accessRequestId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/access-requests/{accessRequestId}", null, null, options, cancellationToken);

    public Task<JsonObject> ReviewAccessRequestAsync(
        string accessRequestId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/access-requests/{accessRequestId}/review", null, payload, options, cancellationToken);

    public Task<JsonObject> BindAliasAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/aliases", null, payload, options, cancellationToken);

    public Task<JsonObject> ListAliasesAsync(
        string orgId,
        string workspaceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/aliases",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> RevokeAliasAsync(
        string aliasId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/aliases/{aliasId}/revoke", null, null, options, cancellationToken);

    public Task<JsonObject> ResolveAliasAsync(
        string orgId,
        string workspaceId,
        string alias,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/aliases/resolve",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
                ["alias"] = alias,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> DecideApprovalAsync(
        string approvalId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/approvals/{approvalId}/decision", null, payload, options, cancellationToken);

    public Task<JsonObject> GetCapabilitiesAsync(
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, "/v1/capabilities", null, null, options, cancellationToken);

    public Task<JsonObject> ListInboxAsync(
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/inbox",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> GetInboxThreadAsync(
        string threadId,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            $"/v1/inbox/{threadId}",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> ListInboxChangesAsync(
        string? ownerAgent = null,
        string? cursor = null,
        int? limit = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(ownerAgent))
        {
            query["owner_agent"] = ownerAgent;
        }
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            query["cursor"] = cursor;
        }
        if (limit is int limitValue && limitValue >= 0)
        {
            query["limit"] = limitValue.ToString();
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/inbox/changes", query, null, options, cancellationToken);
    }

    public Task<JsonObject> ReplyInboxThreadAsync(
        string threadId,
        string message,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/inbox/{threadId}/reply",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            new JsonObject { ["message"] = message },
            options,
            cancellationToken);

    public Task<JsonObject> DelegateInboxThreadAsync(
        string threadId,
        JsonObject payload,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/inbox/{threadId}/delegate",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload,
            options,
            cancellationToken);

    public Task<JsonObject> ApproveInboxThreadAsync(
        string threadId,
        JsonObject payload,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/inbox/{threadId}/approve",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload,
            options,
            cancellationToken);

    public Task<JsonObject> RejectInboxThreadAsync(
        string threadId,
        JsonObject payload,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/inbox/{threadId}/reject",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload,
            options,
            cancellationToken);

    public Task<JsonObject> DeleteInboxMessagesAsync(
        string threadId,
        JsonObject payload,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/inbox/{threadId}/messages/delete",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload,
            options,
            cancellationToken);

    public Task<JsonObject> CreateInviteAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/invites/create", null, payload, options, cancellationToken);

    public Task<JsonObject> GetInviteAsync(
        string token,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/invites/{token}", null, null, options, cancellationToken);

    public Task<JsonObject> AcceptInviteAsync(
        string token,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/invites/{token}/accept", null, payload, options, cancellationToken);

    public Task<JsonObject> CreateMediaUploadAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/media/create-upload", null, payload, options, cancellationToken);

    public Task<JsonObject> GetMediaUploadAsync(
        string uploadId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/media/{uploadId}", null, null, options, cancellationToken);

    public Task<JsonObject> FinalizeMediaUploadAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/media/finalize-upload", null, payload, options, cancellationToken);

    public Task<JsonObject> UpsertSchemaAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/schemas", null, payload, options, cancellationToken);

    public Task<JsonObject> GetSchemaAsync(
        string semanticType,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/schemas/{semanticType}", null, null, options, cancellationToken);

    public Task<JsonObject> UpsertWebhookSubscriptionAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/webhooks/subscriptions", null, payload, options, cancellationToken);

    public Task<JsonObject> ListWebhookSubscriptionsAsync(
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/webhooks/subscriptions",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> DeleteWebhookSubscriptionAsync(
        string subscriptionId,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Delete,
            $"/v1/webhooks/subscriptions/{subscriptionId}",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> PublishWebhookEventAsync(
        JsonObject payload,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            "/v1/webhooks/events",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            payload,
            options,
            cancellationToken);

    public Task<JsonObject> ReplayWebhookEventAsync(
        string eventId,
        string? ownerAgent = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Post,
            $"/v1/webhooks/events/{eventId}/replay",
            string.IsNullOrWhiteSpace(ownerAgent) ? null : new Dictionary<string, string> { ["owner_agent"] = ownerAgent },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> CreateOrganizationAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/organizations", null, payload, options, cancellationToken);

    public Task<JsonObject> GetOrganizationAsync(
        string orgId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/organizations/{orgId}", null, null, options, cancellationToken);

    public Task<JsonObject> UpdateOrganizationAsync(
        string orgId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, $"/v1/organizations/{orgId}", null, payload, options, cancellationToken);

    public Task<JsonObject> CreateWorkspaceAsync(
        string orgId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/organizations/{orgId}/workspaces", null, payload, options, cancellationToken);

    public Task<JsonObject> ListWorkspacesAsync(
        string orgId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/organizations/{orgId}/workspaces", null, null, options, cancellationToken);

    public Task<JsonObject> UpdateWorkspaceAsync(
        string orgId,
        string workspaceId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, $"/v1/organizations/{orgId}/workspaces/{workspaceId}", null, payload, options, cancellationToken);

    public Task<JsonObject> ListOrganizationMembersAsync(
        string orgId,
        string? workspaceId = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            $"/v1/organizations/{orgId}/members",
            string.IsNullOrWhiteSpace(workspaceId) ? null : new Dictionary<string, string> { ["workspace_id"] = workspaceId },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> AddOrganizationMemberAsync(
        string orgId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/organizations/{orgId}/members", null, payload, options, cancellationToken);

    public Task<JsonObject> UpdateOrganizationMemberAsync(
        string orgId,
        string memberId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, $"/v1/organizations/{orgId}/members/{memberId}", null, payload, options, cancellationToken);

    public Task<JsonObject> RemoveOrganizationMemberAsync(
        string orgId,
        string memberId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Delete, $"/v1/organizations/{orgId}/members/{memberId}", null, null, options, cancellationToken);

    public Task<JsonObject> UpdateQuotaAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, "/v1/quotas", null, payload, options, cancellationToken);

    public Task<JsonObject> GetQuotaAsync(
        string orgId,
        string workspaceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/quotas",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> GetUsageSummaryAsync(
        string orgId,
        string workspaceId,
        string? window = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["org_id"] = orgId,
            ["workspace_id"] = workspaceId,
        };
        if (!string.IsNullOrWhiteSpace(window))
        {
            query["window"] = window;
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/usage/summary", query, null, options, cancellationToken);
    }

    public Task<JsonObject> GetUsageTimeseriesAsync(
        string orgId,
        string workspaceId,
        int? windowDays = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["org_id"] = orgId,
            ["workspace_id"] = workspaceId,
        };
        if (windowDays is int days && days >= 0)
        {
            query["window_days"] = days.ToString();
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/usage/timeseries", query, null, options, cancellationToken);
    }

    public Task<JsonObject> CreatePrincipalAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/principals", null, payload, options, cancellationToken);

    public Task<JsonObject> GetPrincipalAsync(
        string principalId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/principals/{principalId}", null, null, options, cancellationToken);

    public Task<JsonObject> RegisterRoutingEndpointAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/routing/endpoints", null, payload, options, cancellationToken);

    public Task<JsonObject> ListRoutingEndpointsAsync(
        string orgId,
        string workspaceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/routing/endpoints",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> UpdateRoutingEndpointAsync(
        string routeId,
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, $"/v1/routing/endpoints/{routeId}", null, payload, options, cancellationToken);

    public Task<JsonObject> RemoveRoutingEndpointAsync(
        string routeId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Delete, $"/v1/routing/endpoints/{routeId}", null, null, options, cancellationToken);

    public Task<JsonObject> ResolveRoutingAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/routing/resolve", null, payload, options, cancellationToken);

    public Task<JsonObject> UpsertTransportBindingAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/transports/bindings", null, payload, options, cancellationToken);

    public Task<JsonObject> ListTransportBindingsAsync(
        string orgId,
        string workspaceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/transports/bindings",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> RemoveTransportBindingAsync(
        string bindingId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Delete, $"/v1/transports/bindings/{bindingId}", null, null, options, cancellationToken);

    public Task<JsonObject> SubmitDeliveryAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, "/v1/deliveries", null, payload, options, cancellationToken);

    public Task<JsonObject> ListDeliveriesAsync(
        string orgId,
        string workspaceId,
        string? principalId = null,
        string? status = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["org_id"] = orgId,
            ["workspace_id"] = workspaceId,
        };
        if (!string.IsNullOrWhiteSpace(principalId))
        {
            query["principal_id"] = principalId;
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query["status"] = status;
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/deliveries", query, null, options, cancellationToken);
    }

    public Task<JsonObject> GetDeliveryAsync(
        string deliveryId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/deliveries/{deliveryId}", null, null, options, cancellationToken);

    public Task<JsonObject> ReplayDeliveryAsync(
        string deliveryId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Post, $"/v1/deliveries/{deliveryId}/replay", null, null, options, cancellationToken);

    public Task<JsonObject> UpdateBillingPlanAsync(
        JsonObject payload,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Patch, "/v1/billing/plan", null, payload, options, cancellationToken);

    public Task<JsonObject> GetBillingPlanAsync(
        string orgId,
        string workspaceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            HttpMethod.Get,
            "/v1/billing/plan",
            new Dictionary<string, string>
            {
                ["org_id"] = orgId,
                ["workspace_id"] = workspaceId,
            },
            null,
            options,
            cancellationToken);

    public Task<JsonObject> ListBillingInvoicesAsync(
        string orgId,
        string workspaceId,
        string? billingStatus = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["org_id"] = orgId,
            ["workspace_id"] = workspaceId,
        };
        if (!string.IsNullOrWhiteSpace(billingStatus))
        {
            query["status"] = billingStatus;
        }
        return RequestJsonAsync(HttpMethod.Get, "/v1/billing/invoices", query, null, options, cancellationToken);
    }

    public Task<JsonObject> GetBillingInvoiceAsync(
        string invoiceId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => RequestJsonAsync(HttpMethod.Get, $"/v1/billing/invoices/{invoiceId}", null, null, options, cancellationToken);

    private async Task<JsonObject> RequestJsonAsync(
        HttpMethod method,
        string path,
        Dictionary<string, string>? query,
        JsonObject? payload,
        RequestOptions? options,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, BuildUrl(path, query));
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        if (!string.IsNullOrWhiteSpace(options?.Authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", options.Authorization);
        }
        else if (!string.IsNullOrWhiteSpace(_actorToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _actorToken);
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(options?.IdempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", options.IdempotencyKey);
        }
        if (!string.IsNullOrWhiteSpace(options?.TraceId))
        {
            request.Headers.TryAddWithoutValidation("X-Trace-Id", options.TraceId);
        }
        if (!string.IsNullOrWhiteSpace(options?.XOwnerAgent))
        {
            request.Headers.TryAddWithoutValidation("x-owner-agent", options.XOwnerAgent);
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

    private static Dictionary<string, string>? BuildIntentControlQuery(RequestOptions? options)
    {
        if (string.IsNullOrWhiteSpace(options?.OwnerAgent))
        {
            return null;
        }

        return new Dictionary<string, string> { ["owner_agent"] = options.OwnerAgent! };
    }
}

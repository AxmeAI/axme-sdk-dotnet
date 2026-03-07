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
        Assert.Contains("token", handler.LastRequest.Headers.GetValues("x-api-key"));
        Assert.Contains("register-1", handler.LastRequest.Headers.GetValues("Idempotency-Key"));

        var body = JsonNode.Parse(await handler.LastRequest.Content!.ReadAsStringAsync())!.AsObject();
        Assert.Equal("@partner.user", body["nick"]!.GetValue<string>());
        Assert.True(response["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ClientSendsConfiguredActorToken()
    {
        var handler = new StubHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"available":true}"""));

        var actorClient = new AxmeClient(
            new AxmeClientConfig
            {
                BaseUrl = "https://api.axme.test",
                ApiKey = "platform-token",
                ActorToken = "actor-token",
            },
            new HttpClient(handler));

        await actorClient.CheckNickAsync("@partner.user");

        Assert.Contains("platform-token", handler.LastRequest!.Headers.GetValues("x-api-key"));
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("actor-token", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void ConfigRejectsConflictingActorTokenAliases()
    {
        Assert.Throws<ArgumentException>(() =>
            new AxmeClient(
                new AxmeClientConfig
                {
                    BaseUrl = "https://api.axme.test",
                    ApiKey = "platform-token",
                    ActorToken = "actor-a",
                    BearerToken = "actor-b",
                }));
    }

    [Fact]
    public async Task ConfigUsesDefaultBaseUrlWhenMissing()
    {
        var handler = new StubHttpMessageHandler(
            request =>
            {
                Assert.Equal("https://api.cloud.axme.ai/v1/capabilities", request.RequestUri!.ToString());
                return JsonResponse(HttpStatusCode.OK, """{"runtime":"cloud"}""");
            });

        var client = new AxmeClient(
            new AxmeClientConfig
            {
                ApiKey = "token",
            },
            new HttpClient(handler));

        var response = await client.GetCapabilitiesAsync();
        Assert.Equal("cloud", response["runtime"]!.GetValue<string>());
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

    [Fact]
    public async Task AccessAliasInboxInviteMediaSchemaWebhookEndpoints_AreReachable()
    {
        var call = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                call += 1;
                return call switch
                {
                    1 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"access_request_id":"ar_123"}"""),
                    2 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"items":[]}"""),
                    3 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"access_request":{"access_request_id":"ar_123"}}"""),
                    4 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"state":"approved"}"""),
                    5 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"alias_id":"al_123"}"""),
                    6 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"items":[]}"""),
                    7 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"target":"agent://support"}"""),
                    8 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"revoked":true}"""),
                    9 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"decision":"approve"}"""),
                    10 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"capabilities":["intent.submit"]}"""),
                    11 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"threads":[]}"""),
                    12 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"thread_id":"thread_123"}"""),
                    13 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"changes":[],"next_cursor":"cur-2"}"""),
                    14 => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""),
                    15 => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""),
                    16 => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""),
                    17 => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""),
                    18 => JsonResponse(HttpStatusCode.OK, """{"ok":true}"""),
                    19 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"token":"tok_123"}"""),
                    20 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"token":"tok_123"}"""),
                    21 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"accepted":true}"""),
                    22 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"upload_id":"up_123"}"""),
                    23 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"upload_id":"up_123"}"""),
                    24 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"status":"finalized"}"""),
                    25 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"semantic_type":"notify.message.v1"}"""),
                    26 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"semantic_type":"notify.message.v1"}"""),
                    27 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"subscription_id":"wh_sub_123"}"""),
                    28 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"items":[]}"""),
                    29 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"deleted":true}"""),
                    30 => JsonResponse(HttpStatusCode.OK, """{"ok":true,"event_id":"evt_123"}"""),
                    _ => JsonResponse(HttpStatusCode.OK, """{"ok":true,"replayed":true}"""),
                };
            });
        var client = BuildClient(handler);

        await client.CreateAccessRequestAsync(new JsonObject { ["owner_agent"] = "agent://owner" }, new RequestOptions { IdempotencyKey = "ar-create-1" });
        Assert.Equal("/v1/access-requests", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListAccessRequestsAsync("org_1", "ws_1", "pending");
        Assert.Equal("/v1/access-requests", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetAccessRequestAsync("ar_123");
        Assert.Equal("/v1/access-requests/ar_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ReviewAccessRequestAsync("ar_123", new JsonObject { ["decision"] = "approve" }, new RequestOptions { IdempotencyKey = "ar-review-1" });
        Assert.Equal("/v1/access-requests/ar_123/review", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.BindAliasAsync(new JsonObject { ["alias"] = "@support", ["target_agent"] = "agent://support" });
        Assert.Equal("/v1/aliases", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListAliasesAsync("org_1", "ws_1");
        Assert.Equal("/v1/aliases", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ResolveAliasAsync("org_1", "ws_1", "@support");
        Assert.Equal("/v1/aliases/resolve", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.RevokeAliasAsync("al_123");
        Assert.Equal("/v1/aliases/al_123/revoke", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.DecideApprovalAsync("ap_123", new JsonObject { ["decision"] = "approve" });
        Assert.Equal("/v1/approvals/ap_123/decision", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetCapabilitiesAsync();
        Assert.Equal("/v1/capabilities", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.ListInboxAsync("agent://owner");
        Assert.Equal("/v1/inbox", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetInboxThreadAsync("thread_123", "agent://owner");
        Assert.Equal("/v1/inbox/thread_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListInboxChangesAsync("agent://owner", "cur-1", 50);
        Assert.Equal("/v1/inbox/changes", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ReplyInboxThreadAsync("thread_123", "ack", "agent://owner", new RequestOptions { IdempotencyKey = "reply-1" });
        Assert.Equal("/v1/inbox/thread_123/reply", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.DelegateInboxThreadAsync("thread_123", new JsonObject { ["delegate_to"] = "agent://delegate" }, "agent://owner");
        Assert.Equal("/v1/inbox/thread_123/delegate", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ApproveInboxThreadAsync("thread_123", new JsonObject { ["comment"] = "approved" }, "agent://owner");
        Assert.Equal("/v1/inbox/thread_123/approve", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.RejectInboxThreadAsync("thread_123", new JsonObject { ["comment"] = "reject" }, "agent://owner");
        Assert.Equal("/v1/inbox/thread_123/reject", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.DeleteInboxMessagesAsync("thread_123", new JsonObject { ["message_ids"] = new JsonArray("m1", "m2") }, "agent://owner");
        Assert.Equal("/v1/inbox/thread_123/messages/delete", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.CreateInviteAsync(new JsonObject { ["owner_agent"] = "agent://owner" });
        Assert.Equal("/v1/invites/create", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetInviteAsync("tok_123");
        Assert.Equal("/v1/invites/tok_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.AcceptInviteAsync("tok_123", new JsonObject { ["owner_agent"] = "agent://owner" });
        Assert.Equal("/v1/invites/tok_123/accept", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.CreateMediaUploadAsync(new JsonObject { ["owner_agent"] = "agent://owner" });
        Assert.Equal("/v1/media/create-upload", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetMediaUploadAsync("up_123");
        Assert.Equal("/v1/media/up_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.FinalizeMediaUploadAsync(new JsonObject { ["upload_id"] = "up_123" });
        Assert.Equal("/v1/media/finalize-upload", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.UpsertSchemaAsync(new JsonObject { ["semantic_type"] = "notify.message.v1", ["schema"] = new JsonObject { ["type"] = "object" } });
        Assert.Equal("/v1/schemas", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetSchemaAsync("notify.message.v1");
        Assert.Equal("/v1/schemas/notify.message.v1", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.UpsertWebhookSubscriptionAsync(
            new JsonObject
            {
                ["owner_agent"] = "agent://owner",
                ["callback_url"] = "https://example.com/hook",
                ["event_types"] = new JsonArray("inbox.thread_created"),
            });
        Assert.Equal("/v1/webhooks/subscriptions", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListWebhookSubscriptionsAsync("agent://owner");
        Assert.Equal("/v1/webhooks/subscriptions", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.DeleteWebhookSubscriptionAsync("wh_sub_123", "agent://owner");
        Assert.Equal("/v1/webhooks/subscriptions/wh_sub_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.PublishWebhookEventAsync(
            new JsonObject { ["event_type"] = "inbox.thread_created", ["payload"] = new JsonObject { ["thread_id"] = "thr_1" } },
            "agent://owner");
        Assert.Equal("/v1/webhooks/events", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ReplayWebhookEventAsync("evt_123", "agent://owner");
        Assert.Equal("/v1/webhooks/events/evt_123/replay", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OrganizationRoutingDeliveryAndBillingEndpoints_AreReachable()
    {
        var call = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                call += 1;
                return JsonResponse(HttpStatusCode.OK, """{"ok":true}""");
            });
        var client = BuildClient(handler);

        await client.CreateOrganizationAsync(new JsonObject { ["org_id"] = "org_1", ["name"] = "Acme" });
        Assert.Equal("/v1/organizations", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetOrganizationAsync("org_1");
        Assert.Equal("/v1/organizations/org_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.UpdateOrganizationAsync("org_1", new JsonObject { ["display_name"] = "Acme Inc" });
        Assert.Equal("/v1/organizations/org_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.CreateWorkspaceAsync("org_1", new JsonObject { ["workspace_id"] = "ws_1", ["name"] = "Primary" });
        Assert.Equal("/v1/organizations/org_1/workspaces", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListWorkspacesAsync("org_1");
        Assert.Equal("/v1/organizations/org_1/workspaces", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.UpdateWorkspaceAsync("org_1", "ws_1", new JsonObject { ["name"] = "Primary Updated" });
        Assert.Equal("/v1/organizations/org_1/workspaces/ws_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListOrganizationMembersAsync("org_1", "ws_1");
        Assert.Equal("/v1/organizations/org_1/members", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.AddOrganizationMemberAsync("org_1", new JsonObject { ["owner_agent"] = "agent://owner", ["role"] = "workspace_admin" });
        Assert.Equal("/v1/organizations/org_1/members", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.UpdateOrganizationMemberAsync("org_1", "member_1", new JsonObject { ["role"] = "workspace_viewer" });
        Assert.Equal("/v1/organizations/org_1/members/member_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.RemoveOrganizationMemberAsync("org_1", "member_1");
        Assert.Equal("/v1/organizations/org_1/members/member_1", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.UpdateQuotaAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["hard_enforce"] = true });
        Assert.Equal("/v1/quotas", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetQuotaAsync("org_1", "ws_1");
        Assert.Equal("/v1/quotas", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetUsageSummaryAsync("org_1", "ws_1", "30d");
        Assert.Equal("/v1/usage/summary", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetUsageTimeseriesAsync("org_1", "ws_1", 7);
        Assert.Equal("/v1/usage/timeseries", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.CreatePrincipalAsync(new JsonObject { ["owner_agent"] = "agent://owner", ["kind"] = "service" });
        Assert.Equal("/v1/principals", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetPrincipalAsync("pr_1");
        Assert.Equal("/v1/principals/pr_1", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.RegisterRoutingEndpointAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["transport"] = "http" });
        Assert.Equal("/v1/routing/endpoints", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListRoutingEndpointsAsync("org_1", "ws_1");
        Assert.Equal("/v1/routing/endpoints", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.UpdateRoutingEndpointAsync("route_1", new JsonObject { ["weight"] = 10 });
        Assert.Equal("/v1/routing/endpoints/route_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.RemoveRoutingEndpointAsync("route_1");
        Assert.Equal("/v1/routing/endpoints/route_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ResolveRoutingAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["semantic_type"] = "notify.message.v1" });
        Assert.Equal("/v1/routing/resolve", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.UpsertTransportBindingAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["transport"] = "http" });
        Assert.Equal("/v1/transports/bindings", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListTransportBindingsAsync("org_1", "ws_1");
        Assert.Equal("/v1/transports/bindings", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.RemoveTransportBindingAsync("binding_1");
        Assert.Equal("/v1/transports/bindings/binding_1", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.SubmitDeliveryAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["principal_id"] = "pr_1" });
        Assert.Equal("/v1/deliveries", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListDeliveriesAsync("org_1", "ws_1", "pr_1", "pending");
        Assert.Equal("/v1/deliveries", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetDeliveryAsync("delivery_1");
        Assert.Equal("/v1/deliveries/delivery_1", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ReplayDeliveryAsync("delivery_1");
        Assert.Equal("/v1/deliveries/delivery_1/replay", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.UpdateBillingPlanAsync(new JsonObject { ["org_id"] = "org_1", ["workspace_id"] = "ws_1", ["plan"] = "enterprise" });
        Assert.Equal("/v1/billing/plan", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetBillingPlanAsync("org_1", "ws_1");
        Assert.Equal("/v1/billing/plan", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.ListBillingInvoicesAsync("org_1", "ws_1", "open");
        Assert.Equal("/v1/billing/invoices", handler.LastRequest!.RequestUri!.AbsolutePath);
        await client.GetBillingInvoiceAsync("inv_1");
        Assert.Equal("/v1/billing/invoices/inv_1", handler.LastRequest!.RequestUri!.AbsolutePath);
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

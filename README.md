# axme-sdk-dotnet

**.NET SDK for AXME** - send intents, listen for deliveries, resume workflows. Async/await throughout, targeting .NET 8+.

[![Alpha](https://img.shields.io/badge/status-alpha-orange)](https://cloud.axme.ai/alpha/cli) [![NuGet](https://img.shields.io/nuget/v/Axme)](https://www.nuget.org/packages/Axme/) [![License](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)

**[Quick Start](https://cloud.axme.ai/alpha/cli)** · **[Docs](https://github.com/AxmeAI/axme-docs)** · **[Examples](https://github.com/AxmeAI/axme-examples)**

---

## Install

```bash
dotnet add package Axme
```

Requires .NET 8+.

---

## Quick Start

```csharp
using System.Text.Json.Nodes;
using Axme.Sdk;

var client = new AxmeClient(new AxmeClientConfig { ApiKey = "axme_sa_..." });

// Send an intent - survives crashes, retries, timeouts
var intent = await client.CreateIntentAsync(
    new JsonObject
    {
        ["intent_type"] = "order.fulfillment.v1",
        ["to_agent"] = "agent://myorg/production/fulfillment-service",
        ["payload"] = new JsonObject { ["order_id"] = "ord_123" },
    },
    new RequestOptions { IdempotencyKey = "fulfill-ord-123-001" }
);
Console.WriteLine($"{intent["intent_id"]} {intent["status"]}");
```

---

## Human Approvals

```csharp
var intent = await client.CreateIntentAsync(new CreateIntentParams
{
    IntentType = "intent.budget.approval.v1",
    ToAgent = "agent://myorg/prod/agent_core",
    Payload = new Dictionary<string, object> { ["amount"] = 32000 },
    HumanTask = new HumanTask
    {
        TaskType = "approval",
        NotifyEmail = "approver@example.com",
        AllowedOutcomes = new[] { "approved", "rejected" },
    },
});
var result = await client.WaitForAsync(intent["intent_id"]!.GetValue<string>());
```

8 task types: `approval`, `confirmation`, `review`, `assignment`, `form`, `clarification`, `manual_action`, `override`. Full reference: [axme-docs](https://github.com/AxmeAI/axme-docs).

---

## Observe Lifecycle Events

```csharp
var events = await client.ListIntentEventsAsync(intentId);
```

---

## Agent Mesh - Monitor and Govern

Agent Mesh gives every agent real-time health monitoring, policy enforcement, and a kill switch - all from a single dashboard.

```csharp
client.Mesh.StartHeartbeat();
client.Mesh.ReportMetric(new Metric { Success = true, LatencyMs = 230, CostUsd = 0.02m });
```

Set action policies (allowlist/denylist intent types) and cost policies (intents/day, $/day limits) per agent via dashboard or API. Mesh module coming soon to this SDK - [Python SDK](https://github.com/AxmeAI/axme-sdk-python) available now. [Full overview](https://github.com/AxmeAI/axme#agent-mesh---see-and-control-your-agents).

Open the live dashboard at [mesh.axme.ai](https://mesh.axme.ai) or run `axme mesh dashboard` from the CLI.

---

## Examples

See [`examples/BasicSubmit.cs`](examples/BasicSubmit.cs). More: [axme-examples](https://github.com/AxmeAI/axme-examples)

---

## Development

```bash
dotnet test tests/Axme.Sdk.Tests/Axme.Sdk.Tests.csproj
```

---

## Related

| | |
|---|---|
| [axme-docs](https://github.com/AxmeAI/axme-docs) | API reference and integration guides |
| [axme-examples](https://github.com/AxmeAI/axme-examples) | Runnable examples |
| [axp-spec](https://github.com/AxmeAI/axp-spec) | Protocol specification |
| [axme-cli](https://github.com/AxmeAI/axme-cli) | CLI tool |
| [axme-conformance](https://github.com/AxmeAI/axme-conformance) | Conformance suite |

---

[contact@axme.ai](mailto:contact@axme.ai) · [Security](SECURITY.md) · [License](LICENSE)

# axme-sdk-dotnet

Official .NET SDK for Axme APIs and workflows.

## Status

Beta parity kickoff in progress.

## Quickstart

```csharp
using System.Text.Json.Nodes;
using Axme.Sdk;

var client = new AxmeClient(
    new AxmeClientConfig
    {
        BaseUrl = "https://gateway.example.com",
        ApiKey = "YOUR_API_KEY",
    });

var registered = await client.RegisterNickAsync(
    new JsonObject
    {
        ["nick"] = "@partner.user",
        ["display_name"] = "Partner User",
    },
    new RequestOptions { IdempotencyKey = "nick-register-001" });

var check = await client.CheckNickAsync("@partner.user");

var renamed = await client.RenameNickAsync(
    new JsonObject
    {
        ["owner_agent"] = registered["owner_agent"]!.GetValue<string>(),
        ["nick"] = "@partner.new",
    },
    new RequestOptions { IdempotencyKey = "nick-rename-001" });

var profile = await client.GetUserProfileAsync(registered["owner_agent"]!.GetValue<string>());

var updated = await client.UpdateUserProfileAsync(
    new JsonObject
    {
        ["owner_agent"] = profile["owner_agent"]!.GetValue<string>(),
        ["display_name"] = "Partner User Updated",
    },
    new RequestOptions { IdempotencyKey = "profile-update-001" });
```

## Development

```bash
dotnet test tests/Axme.Sdk.Tests/Axme.Sdk.Tests.csproj
```

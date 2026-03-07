using System;
using System.Text.Json.Nodes;
using Axme.Sdk;

var client = new AxmeClient(new AxmeClientConfig
{
    ApiKey = Environment.GetEnvironmentVariable("AXME_API_KEY") ?? "",
    BaseUrl = Environment.GetEnvironmentVariable("AXME_BASE_URL"),
});

var created = await client.CreateIntentAsync(new JsonObject
{
    ["intent_type"] = "intent.demo.v1",
    ["correlation_id"] = Guid.NewGuid().ToString(),
    ["from_agent"] = "agent://basic/dotnet/source",
    ["to_agent"] = "agent://basic/dotnet/target",
    ["payload"] = new JsonObject { ["task"] = "hello-from-dotnet" },
});

var current = await client.GetIntentAsync(created["intent_id"]?.ToString() ?? "");
var intent = current["intent"]?.AsObject();
var status = intent?["status"]?.ToString()
    ?? intent?["lifecycle_status"]?.ToString()
    ?? current["status"]?.ToString()
    ?? "UNKNOWN";
Console.WriteLine(status);

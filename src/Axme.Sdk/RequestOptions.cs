namespace Axme.Sdk;

public sealed class RequestOptions
{
    public string? IdempotencyKey { get; init; }
    public string? TraceId { get; init; }
}

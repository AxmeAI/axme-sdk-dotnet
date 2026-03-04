namespace Axme.Sdk;

public sealed class RequestOptions
{
    public string? IdempotencyKey { get; init; }
    public string? TraceId { get; init; }
    public string? OwnerAgent { get; init; }
    public string? XOwnerAgent { get; init; }
    public string? Authorization { get; init; }
}

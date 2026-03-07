namespace Axme.Sdk;

public sealed class AxmeClientConfig
{
    public string? BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public string? ActorToken { get; init; }
    public string? BearerToken { get; init; }
}

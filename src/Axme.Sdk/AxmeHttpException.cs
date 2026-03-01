namespace Axme.Sdk;

public sealed class AxmeHttpException : Exception
{
    public AxmeHttpException(int statusCode, string responseBody)
        : base($"axme request failed with status {statusCode}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }

    public string ResponseBody { get; }
}

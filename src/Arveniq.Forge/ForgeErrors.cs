namespace Arveniq.Forge;

/// <summary>Forge returned a non-success HTTP response.</summary>
public sealed class ForgeApiException : Exception
{
    public ForgeApiException(string message, int statusCode, string responseBody, string? requestId)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        RequestId = requestId;
    }

    public int StatusCode { get; }
    public string ResponseBody { get; }
    public string? RequestId { get; }
}

/// <summary>A Forge SSE conversation stream was malformed or unexpectedly interrupted.</summary>
public sealed class ForgeStreamException : Exception
{
    public ForgeStreamException(string message, string code = "stream_interrupted") : base(message) => Code = code;
    public string Code { get; }
}

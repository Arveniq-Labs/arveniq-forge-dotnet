using System.Net.Http;

namespace Arveniq.Forge;

/// <summary>Configuration for a server-side Forge API client.</summary>
public sealed class ForgeClientOptions
{
    public required string BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public HttpClient? HttpClient { get; init; }
    public string? UserAgent { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);
}

/// <summary>Per-request correlation and cancellation controls.</summary>
public sealed class ForgeRequestOptions
{
    public string? RequestId { get; init; }
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Reconnect controls for a Forge conversation event stream.</summary>
public sealed class ForgeChatStreamOptions
{
    public string? RequestId { get; init; }
    public string? AfterEventId { get; init; }
    public int MaxReconnects { get; init; } = 5;
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan? Timeout { get; init; }

    internal void Validate()
    {
        if (MaxReconnects < 0) throw new ArgumentOutOfRangeException(nameof(MaxReconnects));
        if (ReconnectDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ReconnectDelay));
    }
}

/// <summary>Polling controls for an asynchronous developer run.</summary>
public sealed class ForgeRunWaitOptions
{
    public string? RequestId { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public Func<ForgeDeveloperRun, CancellationToken, Task>? OnPoll { get; init; }

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(PollInterval));
        if (Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Timeout));
    }
}

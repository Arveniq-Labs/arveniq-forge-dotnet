using System.Runtime.CompilerServices;

namespace Arveniq.Forge.Maui;

public sealed record ForgeMobileContextAssertion
{
    public string Value { get; }
    private ForgeMobileContextAssertion(string value) => Value = value;

    public static ForgeMobileContextAssertion Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Trim().Length is < 32 or > 16_384) throw new ArgumentOutOfRangeException(nameof(value), "A context assertion must be an opaque value between 32 and 16384 characters.");
        return new ForgeMobileContextAssertion(value);
    }
}

/// <summary>Implement with MAUI SecureStorage in the host app. Store only short-lived gateway tokens.</summary>
public interface IMauiSecureTokenStore
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAccessTokenAsync(string token, CancellationToken cancellationToken = default);
    ValueTask ClearAccessTokenAsync(CancellationToken cancellationToken = default);
}

public interface IMobileGatewayCredentialProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class MauiSecureCredentialProvider(IMauiSecureTokenStore tokenStore) : IMobileGatewayCredentialProvider
{
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => tokenStore.GetAccessTokenAsync(cancellationToken);
}

/// <summary>A customer relay / mobile-gateway transport, deliberately separate from the Developer API.</summary>
public interface IMobileGatewayTransport
{
    ValueTask<MobileGatewayResponse> ExecuteAsync(MobileGatewayRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ForgeMobileEvent> StreamAsync(MobileGatewayRequest request, CancellationToken cancellationToken = default);
}

public sealed record MobileGatewayRequest(string Method, string Path, string BearerToken, IReadOnlyDictionary<string, object?> Body, IReadOnlyDictionary<string, string>? Headers = null);
public sealed record MobileGatewayResponse(int Status, IReadOnlyDictionary<string, object?> Payload, string? RequestId = null);
public sealed record ForgeMobileConversation(string Id, string CreatedAt);
public sealed record ForgeMobileSession(string Id, string ExpiresAt);
public sealed record ForgeUploadPreparation(string AttachmentId, string UploadUrl, IReadOnlyDictionary<string, string> RequiredHeaders, string ExpiresAt);
public sealed record ForgeMobileEvent(string Id, string Type, string ConversationId, string TurnId, string? MessageId, string Sequence, IReadOnlyDictionary<string, object?> Data);
public sealed record ForgeMauiMessageInput(string ClientMessageId, string Message, ForgeMobileContextAssertion ContextAssertion, string? AfterEventId = null);

public sealed class MobileGatewayException(int status, string? requestId, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string? RequestId { get; } = requestId;
}

/// <summary>MAUI-friendly shared client with no Forge Developer key or authoritative resource-ID API.</summary>
public sealed class ForgeMauiClient(IMobileGatewayCredentialProvider credentials, IMobileGatewayTransport transport)
{
    public async ValueTask<ForgeMobileSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync("POST", "/mobile/sessions", [], cancellationToken).ConfigureAwait(false);
        return new ForgeMobileSession(RequiredString(response, "id"), RequiredString(response, "expiresAt"));
    }

    public async ValueTask<ForgeMobileConversation> CreateConversationAsync(ForgeMobileContextAssertion contextAssertion, string? agentHandle = null, CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync("POST", "/conversations", new Dictionary<string, object?>
        {
            ["agentHandle"] = agentHandle, ["contextAssertion"] = AssertionBody(contextAssertion),
        }, cancellationToken).ConfigureAwait(false);
        return new ForgeMobileConversation(RequiredString(response, "id"), RequiredString(response, "createdAt"));
    }

    public async IAsyncEnumerable<ForgeMobileEvent> StreamMessageAsync(string conversationId, ForgeMauiMessageInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Message);
        var request = await CreateRequestAsync("POST", $"/conversations/{Uri.EscapeDataString(conversationId)}/messages:stream", new Dictionary<string, object?>
        {
            ["clientMessageId"] = input.ClientMessageId, ["message"] = input.Message, ["contextAssertion"] = AssertionBody(input.ContextAssertion),
        }, input.AfterEventId is null ? null : new Dictionary<string, string> { ["Last-Event-ID"] = input.AfterEventId }, cancellationToken).ConfigureAwait(false);
        await foreach (var @event in transport.StreamAsync(request, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false)) yield return @event;
    }

    public async ValueTask<ForgeUploadPreparation> PrepareUploadAsync(string filename, string contentType, long byteLength, ForgeMobileContextAssertion contextAssertion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);
        var response = await ExecuteAsync("POST", "/uploads:prepare", new Dictionary<string, object?>
        {
            ["filename"] = filename, ["contentType"] = contentType, ["byteLength"] = byteLength, ["contextAssertion"] = AssertionBody(contextAssertion),
        }, cancellationToken).ConfigureAwait(false);
        var headers = response.Payload.TryGetValue("requiredHeaders", out var value) && value is IReadOnlyDictionary<string, string> typed ? typed : new Dictionary<string, string>();
        return new ForgeUploadPreparation(RequiredString(response, "attachmentId"), RequiredString(response, "uploadUrl"), headers, RequiredString(response, "expiresAt"));
    }

    public async ValueTask RegisterPushTokenAsync(string platform, string token, CancellationToken cancellationToken = default)
    {
        if (platform is not ("android" or "ios")) throw new ArgumentOutOfRangeException(nameof(platform));
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _ = await ExecuteAsync("PUT", "/devices/push-tokens", new Dictionary<string, object?> { ["platform"] = platform, ["token"] = token }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CancelConversationTurnAsync(string conversationId, string turnId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        _ = await ExecuteAsync("POST", $"/conversations/{Uri.EscapeDataString(conversationId)}/turns/{Uri.EscapeDataString(turnId)}:cancel", [], cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<MobileGatewayResponse> ExecuteAsync(string method, string path, IReadOnlyDictionary<string, object?> body, CancellationToken cancellationToken)
    {
        var response = await transport.ExecuteAsync(await CreateRequestAsync(method, path, body, null, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or > 299) throw new MobileGatewayException(response.Status, response.RequestId, $"Mobile gateway request failed with {response.Status}.");
        return response;
    }

    private async ValueTask<MobileGatewayRequest> CreateRequestAsync(string method, string path, IReadOnlyDictionary<string, object?> body, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        var token = await credentials.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("A short-lived mobile gateway access token is required.");
        return new MobileGatewayRequest(method, path, token, body, headers);
    }

    private static IReadOnlyDictionary<string, object?> AssertionBody(ForgeMobileContextAssertion assertion) => new Dictionary<string, object?> { ["value"] = assertion.Value };
    private static string RequiredString(MobileGatewayResponse response, string name) => response.Payload.TryGetValue(name, out var value) && value is string text ? text : throw new MobileGatewayException(response.Status, response.RequestId, $"Gateway response is missing {name}.");
}

/// <summary>Call from MAUI lifecycle events to resume caller-persisted streams after a real app sleep.</summary>
public sealed class ForgeMauiLifecycleCoordinator(Func<CancellationToken, Task> onResumeAfterSleep)
{
    private bool slept;
    public void OnSleeping() => slept = true;
    public async Task OnResumedAsync(CancellationToken cancellationToken = default)
    {
        if (!slept) return;
        slept = false;
        await onResumeAfterSleep(cancellationToken).ConfigureAwait(false);
    }
}

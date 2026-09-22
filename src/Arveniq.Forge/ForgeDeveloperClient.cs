using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Arveniq.Forge;

/// <summary>Server-side client for Forge discovery, asynchronous runs, and streamed conversations.</summary>
public sealed class ForgeDeveloperClient : ForgeServerClient
{
    public ForgeDeveloperClient(ForgeClientOptions options) : base(options) { }

    public async Task<IReadOnlyList<ForgeDeveloperAgent>> ListAgentsAsync(ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => Items(await SendJsonAsync("/developer/v1/agents", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(ForgeDeveloperAgent.From).ToList();

    public async Task<ForgeDeveloperAgent> GetAgentAsync(string agentId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => ForgeDeveloperAgent.From(await SendJsonAsync($"/developer/v1/agents/{RequiredId(agentId, nameof(agentId))}", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false));

    public async Task<IReadOnlyList<ForgeDeveloperWorkflow>> ListWorkflowsAsync(ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => Items(await SendJsonAsync("/developer/v1/workflows", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(ForgeDeveloperWorkflow.From).ToList();

    public async Task<ForgeDeveloperRun> TriggerAgentRunAsync(string agentId, ForgeDeveloperRunInput? input = null, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => ForgeDeveloperRun.From(await SendJsonAsync($"/developer/v1/agents/{RequiredId(agentId, nameof(agentId))}/runs", HttpMethod.Post, input?.ToJson(), options, cancellationToken).ConfigureAwait(false));

    public async Task<ForgeDeveloperRun> TriggerWorkflowRunAsync(string workflowId, ForgeDeveloperRunInput? input = null, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => ForgeDeveloperRun.From(await SendJsonAsync($"/developer/v1/workflows/{RequiredId(workflowId, nameof(workflowId))}/runs", HttpMethod.Post, input?.ToJson(), options, cancellationToken).ConfigureAwait(false));

    public async Task<IReadOnlyList<ForgeDeveloperRun>> ListRunsAsync(ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => Items(await SendJsonAsync("/developer/v1/runs", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(ForgeDeveloperRun.From).ToList();

    public async Task<ForgeDeveloperRun> GetRunAsync(string runId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => ForgeDeveloperRun.From(await SendJsonAsync($"/developer/v1/runs/{RequiredId(runId, nameof(runId))}", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false));

    public Task<JsonObject> GetRunTraceAsync(string runId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => SendJsonAsync($"/developer/v1/runs/{RequiredId(runId, nameof(runId))}/trace", HttpMethod.Get, options: options, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ForgeRateLimitState>> GetRateLimitsAsync(ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => JsonHelpers.Array((await SendJsonAsync("/developer/v1/rate-limit", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false))["limits"]).Select(JsonHelpers.Object).Select(ForgeRateLimitState.From).ToList();

    public async Task<ForgeConversation> CreateConversationAsync(string agentId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var input = new JsonObject { ["agentId"] = RequiredRawId(agentId, nameof(agentId)) };
        return ForgeConversation.From(await SendJsonAsync("/developer/v1/conversations", HttpMethod.Post, input, options, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ForgeConversationSnapshot> GetConversationAsync(string conversationId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => ForgeConversationSnapshot.From(await SendJsonAsync($"/developer/v1/conversations/{RequiredId(conversationId, nameof(conversationId))}", HttpMethod.Get, options: options, cancellationToken: cancellationToken).ConfigureAwait(false));

    public Task<JsonObject> CancelConversationTurnAsync(string conversationId, string turnId, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
        => SendJsonAsync($"/developer/v1/conversations/{RequiredId(conversationId, nameof(conversationId))}/turns/{RequiredId(turnId, nameof(turnId))}/cancel", HttpMethod.Post, options: options, cancellationToken: cancellationToken);

    /// <summary>Receives conversation events immediately and reconnects using the accepted turn and last event ID.</summary>
    public Task StreamMessageAsync(string conversationId, string clientMessageId, string message, Func<ForgeChatEvent, Task> receiver, ForgeChatStreamOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientMessageId)) throw new ArgumentException("clientMessageId is required for safe retries.", nameof(clientMessageId));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("message is required.", nameof(message));
        return StreamAsync(conversationId, null, new JsonObject { ["clientMessageId"] = clientMessageId, ["message"] = message }, receiver, options ?? new ForgeChatStreamOptions(), cancellationToken);
    }

    public Task StreamConversationTurnAsync(string conversationId, string turnId, Func<ForgeChatEvent, Task> receiver, ForgeChatStreamOptions? options = null, CancellationToken cancellationToken = default)
        => StreamAsync(conversationId, RequiredRawId(turnId, nameof(turnId)), null, receiver, options ?? new ForgeChatStreamOptions(), cancellationToken);

    public async Task<ForgeDeveloperRun> WaitForRunAsync(string runId, ForgeRunWaitOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ForgeRunWaitOptions();
        options.Validate();
        var deadline = DateTimeOffset.UtcNow + options.Timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var run = await GetRunAsync(runId, new ForgeRequestOptions { RequestId = options.RequestId }, cancellationToken).ConfigureAwait(false);
            if (options.OnPoll is not null) await options.OnPoll(run, cancellationToken).ConfigureAwait(false);
            if (run.IsTerminal) return run;
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out waiting for Forge run {runId} after {options.Timeout.TotalMilliseconds:0} ms.");
            await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StreamAsync(string conversationId, string? turnId, JsonObject? input, Func<ForgeChatEvent, Task> receiver, ForgeChatStreamOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        options.Validate();
        var rawConversationId = RequiredRawId(conversationId, nameof(conversationId));
        var activeTurnId = turnId;
        var cursor = options.AfterEventId;
        BigInteger? lastSequence = null;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var path = activeTurnId is null
                    ? $"/developer/v1/conversations/{Uri.EscapeDataString(rawConversationId)}/messages/stream"
                    : $"/developer/v1/conversations/{Uri.EscapeDataString(rawConversationId)}/turns/{Uri.EscapeDataString(activeTurnId)}/events/stream";
                using var response = await SendStreamAsync(path, activeTurnId is null ? HttpMethod.Post : HttpMethod.Get, activeTurnId is null ? input : null, cursor, options, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                    throw new ForgeStreamException("Expected an SSE response from Forge.", "stream_content_type_invalid");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var settled = await ReadEventsAsync(stream, async @event =>
                {
                    if (@event.ConversationId != rawConversationId || (activeTurnId is not null && @event.TurnId != activeTurnId))
                        throw new ForgeStreamException("Unexpected stream identity.", "stream_event_invalid");
                    if (@event.Sequence is not null)
                    {
                        if (!BigInteger.TryParse(@event.Sequence, out var incoming)) throw new ForgeStreamException("Invalid event sequence.", "stream_event_invalid");
                        if (lastSequence is not null && incoming <= lastSequence) return;
                        lastSequence = incoming;
                    }
                    activeTurnId = RequiredRawId(@event.TurnId, "turnId");
                    if (@event.EventId is not null) cursor = @event.EventId;
                    await receiver(@event).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
                if (settled) return;
                throw new ForgeStreamException("Stream ended before the turn settled.");
            }
            catch (Exception error) when (IsRetryable(error) && attempt < options.MaxReconnects)
            {
                var multiplier = 1L << Math.Min(attempt, 20);
                var delay = TimeSpan.FromMilliseconds(Math.Min(10000, options.ReconnectDelay.TotalMilliseconds * multiplier));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> ReadEventsAsync(Stream stream, Func<ForgeChatEvent, Task> receiver, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var lines = new List<string>();
        var frameCharacters = 0;
        var settled = false;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            frameCharacters += line.Length;
            if (frameCharacters > 2_000_000) throw new ForgeStreamException("SSE event exceeds the supported size.", "stream_event_too_large");
            if (line.Length > 0) { lines.Add(line); continue; }
            var @event = Dispatch(lines);
            lines.Clear();
            frameCharacters = 0;
            if (@event is null) continue;
            await receiver(@event).ConfigureAwait(false);
            if (@event.IsSettled) settled = true;
        }
        return settled;
    }

    private static ForgeChatEvent? Dispatch(IEnumerable<string> lines)
    {
        var data = string.Join("\n", lines.Where(line => line.StartsWith("data:", StringComparison.Ordinal)).Select(line => line[5..].TrimStart(' ')));
        if (string.IsNullOrWhiteSpace(data)) return null;
        JsonObject raw;
        try { raw = JsonHelpers.ParseObject(data); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw new ForgeStreamException("Invalid SSE JSON.", "stream_event_invalid"); }
        if (JsonHelpers.String(raw, "type") == "error") throw new ForgeStreamException(JsonHelpers.String(raw, "message") ?? "Forge stream interrupted.", JsonHelpers.String(raw, "code") ?? "stream_interrupted");
        return ForgeChatEvent.From(raw);
    }

    private static bool IsRetryable(Exception error) => error switch
    {
        ForgeApiException api => api.StatusCode == 429 || api.StatusCode >= 500,
        ForgeStreamException stream => stream.Code is "stream_interrupted" or "developer_stream_interrupted",
        HttpRequestException or IOException => true,
        _ => false,
    };

    private static IReadOnlyList<JsonObject> Items(JsonObject response) => JsonHelpers.Array(response["items"]).Select(JsonHelpers.Object).ToList();
}

using System.Numerics;
using System.Text.Json.Nodes;

namespace Arveniq.Forge;

/// <summary>A versioned event received from a Forge conversation SSE stream.</summary>
public sealed record ForgeChatEvent(
    string Type, int Version, string ConversationId, string TurnId, string? MessageId,
    string? EventId, string? Sequence, string? CreatedAt, JsonObject Data)
{
    internal static ForgeChatEvent From(JsonObject value)
    {
        var version = value["version"] is JsonValue node && node.TryGetValue<int>(out var result) ? result : -1;
        var eventValue = new ForgeChatEvent(
            JsonHelpers.String(value, "type") ?? string.Empty, version, JsonHelpers.String(value, "conversationId") ?? string.Empty,
            JsonHelpers.String(value, "turnId") ?? string.Empty, JsonHelpers.String(value, "messageId"), JsonHelpers.String(value, "eventId"),
            JsonHelpers.String(value, "sequence"), JsonHelpers.String(value, "createdAt"), JsonHelpers.Object(value["data"]));
        if (eventValue.Version != 1 || string.IsNullOrWhiteSpace(eventValue.Type) || string.IsNullOrWhiteSpace(eventValue.TurnId) || value["data"] is not JsonObject)
            throw new ForgeStreamException("Invalid Forge chat event.", "stream_event_invalid");
        return eventValue;
    }

    public bool IsSettled => Type is "turn.completed" or "turn.failed" or "turn.canceled" or "turn.requires_action";

    public JsonObject ToJson() => new()
    {
        ["type"] = Type, ["version"] = Version, ["conversationId"] = ConversationId, ["turnId"] = TurnId,
        ["messageId"] = MessageId, ["eventId"] = EventId, ["sequence"] = Sequence, ["createdAt"] = CreatedAt,
        ["data"] = Data.DeepClone(),
    };
}

/// <summary>Immutable display state which makes stream replay and replacement safe.</summary>
public sealed record ForgeChatState(
    string Text, string? TurnId = null, string? Status = null, string? Activity = null,
    string? LastEventId = null, string? LastSequence = null)
{
    public static ForgeChatState Empty { get; } = new(string.Empty);
}

public static class ForgeChatEvents
{
    public static ForgeChatState Reduce(ForgeChatState state, ForgeChatEvent @event)
    {
        var previous = state.TurnId is not null && state.TurnId != @event.TurnId ? ForgeChatState.Empty : state;
        if (@event.Sequence is not null && previous.LastSequence is not null && BigInteger.Parse(@event.Sequence) <= BigInteger.Parse(previous.LastSequence)) return previous;
        var text = previous.Text;
        var activity = previous.Activity;
        var status = @event.Data["status"]?.GetValue<string>() ?? previous.Status;
        if (@event.Type == "activity.updated") activity = @event.Data["message"]?.GetValue<string>();
        if (@event.Type == "response.started" && @event.Data["replace"]?.GetValue<bool>() == true) text = string.Empty;
        if (@event.Type == "response.delta")
        {
            var delta = @event.Data["delta"]?.GetValue<string>() ?? string.Empty;
            text = @event.Data["replace"]?.GetValue<bool>() == true ? delta : text + delta;
        }
        if (@event.Type == "response.completed" && @event.Data["text"] is JsonValue finalText) text = finalText.GetValue<string>();
        if (@event.Type.StartsWith("response.", StringComparison.Ordinal) || @event.IsSettled) activity = null;
        return new ForgeChatState(text, @event.TurnId, status, activity, @event.EventId ?? previous.LastEventId, @event.Sequence ?? previous.LastSequence);
    }
}

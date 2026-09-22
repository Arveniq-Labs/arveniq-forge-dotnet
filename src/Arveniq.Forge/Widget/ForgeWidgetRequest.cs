using System.Text.Json.Nodes;

namespace Arveniq.Forge.Widget;

/// <summary>Untrusted browser payload produced by <see cref="ForgeChatWidget"/>.</summary>
public sealed record ForgeWidgetRequest(string Message, string ClientMessageId, JsonObject BrowserContext)
{
    public static ForgeWidgetRequest FromJson(string payload)
    {
        var value = JsonHelpers.ParseObject(payload);
        var message = JsonHelpers.String(value, "message");
        var clientMessageId = JsonHelpers.String(value, "clientMessageId");
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("message is required.", nameof(payload));
        if (string.IsNullOrWhiteSpace(clientMessageId)) throw new ArgumentException("clientMessageId is required.", nameof(payload));
        return new ForgeWidgetRequest(message, clientMessageId, JsonHelpers.Object(value["context"]));
    }
}

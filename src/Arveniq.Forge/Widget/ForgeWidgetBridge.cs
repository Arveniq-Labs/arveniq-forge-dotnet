using System.Text.Json.Nodes;

namespace Arveniq.Forge.Widget;

/// <summary>Builds a run input using context authorized by the host server, never client context.</summary>
public static class ForgeWidgetBridge
{
    public static ForgeDeveloperRunInput TrustedRunInput(ForgeWidgetRequest request, JsonObject? authorizedContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = new JsonObject
        {
            ["prompt"] = request.Message,
            ["context"] = authorizedContext?.DeepClone() ?? new JsonObject(),
        };
        return new ForgeDeveloperRunInput(request.ClientMessageId, input);
    }
}

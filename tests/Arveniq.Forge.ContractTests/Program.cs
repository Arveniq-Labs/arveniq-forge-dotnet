using Arveniq.Forge;
using Arveniq.Forge.Widget;
using System.Text.Json.Nodes;

var handler = ForgeManifests.CreateReadOnlyHandler(new ForgeHandlerOptions
{
    HandlerRef = "tool://contacts/get_summary",
    Slug = "get_summary",
    Name = "Get summary",
    RequiredScopes = ["contacts.read"],
    InputSchema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["question"] = new JsonObject { ["type"] = "string" } } },
    OutputSchema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["summary"] = new JsonObject { ["type"] = "string" } } },
});
var manifest = ForgeManifests.CreateManifest("contact-tools", "contacts", "1.0.0", [handler], runtimeType: ToolPackageRuntimeType.Container, entrypoint: "bin/contact-tools.dll");
Check(ForgeManifests.Validate(manifest).Valid, "valid governed manifest");
Check(handler.ExecutionMode == ToolExecutionMode.ReadOnly && handler.AuditPolicy.AuditOnCall, "read-only policy remains enforced");

var widget = new ForgeChatWidget(new ForgeChatWidgetOptions
{
    Id = "contact-agent",
    ContextLabel = "Contacts",
    Context = new JsonObject { ["contactId"] = "contact_42" },
    Endpoint = "/api/contacts/agent",
    Shortcut = WidgetShortcut.Parse("META+SHIFT+K"),
});
var html = widget.Render();
Check(html.Contains("/api/contacts/agent", StringComparison.Ordinal) && html.Contains("contact_42", StringComparison.Ordinal) && html.Contains("\"meta\":true", StringComparison.Ordinal) && html.Contains("\"shift\":true", StringComparison.Ordinal), "widget embeds endpoint, context, and shortcut");
Check(widget.RenderLauncher().Contains("data-forge-open=\"contact-agent\"", StringComparison.Ordinal), "launcher targets the widget");

var browser = ForgeWidgetRequest.FromJson("{\"message\":\"Summarize this\",\"clientMessageId\":\"turn-1\",\"context\":{\"accountId\":\"forged\"}}");
var runInput = ForgeWidgetBridge.TrustedRunInput(browser, new JsonObject { ["accountId"] = "server-authorized", ["contactId"] = "contact_42" });
var runPayload = runInput.ToJson().ToJsonString();
Check(runPayload.Contains("server-authorized", StringComparison.Ordinal) && !runPayload.Contains("forged", StringComparison.Ordinal), "server context wins over browser context");

var state = ForgeChatEvents.Reduce(ForgeChatState.Empty, new ForgeChatEvent("response.delta", 1, "c1", "t1", "m1", "e1", "1", "now", new JsonObject { ["delta"] = "Hello" }));
state = ForgeChatEvents.Reduce(state, new ForgeChatEvent("response.completed", 1, "c1", "t1", "m1", "e2", "2", "now", new JsonObject { ["text"] = "Hello world" }));
Check(state.Text == "Hello world", "chat state handles a completed answer");
Console.WriteLine("SDK contract checks passed.");

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException($"Failed: {label}");
}

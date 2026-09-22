# Arveniq Forge .NET

.NET 8 SDK for server-side applications integrating with Arveniq Forge O/S, with an embeddable dark AI-agent drawer modeled on the PingLead design.

It is the .NET counterpart to the TypeScript and Java SDKs:

- `ForgeDeveloperClient` discovers agents and workflows, triggers and polls runs, and streams Forge conversation events over SSE.
- `ForgeToolPackagesClient` manages governed Tool Package definitions and deployments.
- `ForgeManifests` creates and validates governed tool manifests.
- `ForgeChatWidget` renders a configurable drawer with a launcher, shortcut, prompt chips, streamed responses, and context support.

The library depends only on the .NET 8 base class library.

## Install

```xml
<PackageReference Include="Arveniq.Forge" Version="0.1.0" />
```

Until the package is published, reference the local project:

```xml
<ProjectReference Include="../arveniq-forge-dotnet/src/Arveniq.Forge/Arveniq.Forge.csproj" />
```

## Call Forge from an application backend

```csharp
using Arveniq.Forge;
using System.Text.Json.Nodes;

var forge = new ForgeDeveloperClient(new ForgeClientOptions
{
    BaseUrl = Environment.GetEnvironmentVariable("FORGE_API_URL") ?? "https://forge-os.io/v1",
    ApiKey = Environment.GetEnvironmentVariable("FORGE_API_KEY")!,
    UserAgent = "contacts-api/1.0",
});

var run = await forge.TriggerAgentRunAsync(
    agentId,
    new ForgeDeveloperRunInput($"contacts-turn:{turnId}", new JsonObject
    {
        ["prompt"] = userMessage,
        ["context"] = authorizedContactContext,
    }));

var complete = await forge.WaitForRunAsync(run.Id!);
Console.WriteLine(complete.FinalAnswer);
```

The client only accepts absolute HTTPS URLs (HTTP is limited to loopback development), attaches `Authorization: Bearer …` and an `x-request-id`, and never exposes a Forge API key to the browser.

## Render the agent widget

Render the fragment once near the end of the document and place its launcher wherever the host application needs an Ask action. The widget opens using the launcher or a configurable shortcut such as `META+K`.

```csharp
using Arveniq.Forge.Widget;
using System.Text.Json.Nodes;

var widget = new ForgeChatWidget(new ForgeChatWidgetOptions
{
    Id = "contacts-ai",
    Title = "PingLead AI",
    AgentName = "Web Research Agent",
    AgentsAvailable = 7,
    ContextLabel = "Contacts",
    Context = new JsonObject { ["contactId"] = contact.Id, ["screen"] = "contacts" },
    Endpoint = "/api/contacts/agent",
    Shortcut = WidgetShortcut.Parse("META+K"),
});

// Razor: @Html.Raw(widget.RenderLauncher()) then @Html.Raw(widget.Render())
```

The widget POSTs this shape to the configured application endpoint and progressively consumes `response.delta` and `response.completed` SSE events:

```json
{
  "message": "Summarize this customer or lead.",
  "clientMessageId": "stable-uuid",
  "context": { "contactId": "contact_42", "screen": "contacts" }
}
```

## Send trusted context to the AI agent

The browser can alter the submitted context, so `ForgeWidgetRequest.BrowserContext` is deliberately untrusted. Resolve all authorization-sensitive user, tenant, account, and contact data from the authenticated server session, then construct the run input with `ForgeWidgetBridge`:

```csharp
using Arveniq.Forge.Widget;
using System.Text.Json.Nodes;

var widgetRequest = ForgeWidgetRequest.FromJson(requestBody);
JsonObject trustedContext = await contacts.GetAuthorizedAgentContextAsync(userId);

var run = await forge.TriggerAgentRunAsync(
    contactsAgentId,
    ForgeWidgetBridge.TrustedRunInput(widgetRequest, trustedContext));
```

`TrustedRunInput` sends the stable message ID as the idempotency key and sends `{ prompt, context }` to Forge while intentionally excluding browser context. This provides the requested contextual AI experience without allowing a caller to substitute a different account or contact ID.

For server-to-browser conversation streaming, forward each received event through your authenticated SSE endpoint:

```csharp
await forge.StreamMessageAsync(conversationId, clientMessageId, message, async forgeEvent =>
{
    await response.WriteAsync($"event: {forgeEvent.Type}\ndata: {forgeEvent.ToJson().ToJsonString()}\n\n");
    await response.Body.FlushAsync();
});
```

## Governed tool manifests

```csharp
using Arveniq.Forge;
using System.Text.Json.Nodes;

var handler = ForgeManifests.CreateReadOnlyHandler(new ForgeHandlerOptions
{
    HandlerRef = "tool://contacts/get_summary",
    Slug = "get_summary",
    Name = "Get contact summary",
    RequiredScopes = ["contacts.read"],
    InputSchema = new JsonObject { ["type"] = "object" },
    OutputSchema = new JsonObject { ["type"] = "object" },
});

var manifest = ForgeManifests.CreateManifest(
    "contact-tools", "contacts", "1.0.0", [handler],
    runtimeType: ToolPackageRuntimeType.Container,
    entrypoint: "bin/contact-tools.dll");

ForgeManifests.AssertValid(manifest);
```

Read-only handlers cannot be converted to a writing or external-action handler, and always preserve audit metadata. Validation checks namespaces, handler references, schemas, risk/approval requirements, supported environments, and governed audit policy.

## Build and verify

```bash
dotnet build Arveniq.Forge.sln --configuration Release
dotnet run --project tests/Arveniq.Forge.ContractTests --configuration Release
```

## CLI

The `forge-tool` project provides the same governed Tool Package commands as the other SDKs:

```bash
dotnet run --project src/Arveniq.Forge.Tool -- validate --manifest tool-package.json
dotnet run --project src/Arveniq.Forge.Tool -- create-version \
  --package-id pkg_123 --manifest tool-package.json --version 1.0.0 --commit-sha abc123
```

## License

Apache-2.0

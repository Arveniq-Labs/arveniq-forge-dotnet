using Arveniq.Forge.Maui;

var transport = new FakeTransport();
var client = new ForgeMauiClient(new StaticCredentials("short-lived-gateway-token"), transport);
var assertion = ForgeMobileContextAssertion.Create("trusted-opaque-context-assertion-value-001");
var conversation = await client.CreateConversationAsync(assertion, "support-agent");
Check(conversation.Id == "conversation-1", "creates a gateway conversation");
Check(transport.LastRequest?.BearerToken == "short-lived-gateway-token", "uses a short-lived gateway token");
Check((transport.LastRequest?.Body["contextAssertion"] as IReadOnlyDictionary<string, object?>)?["value"]?.ToString() == assertion.Value, "passes an opaque context assertion");
var events = new List<ForgeMobileEvent>();
await foreach (var @event in client.StreamMessageAsync(conversation.Id, new ForgeMauiMessageInput("message-1", "Hello", assertion))) events.Add(@event);
Check(events.Single().Type == "response.delta", "streams mobile-gateway events");
Console.WriteLine("MAUI mobile contract checks passed.");

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException($"Failed: {label}");
}

sealed class StaticCredentials(string token) : IMobileGatewayCredentialProvider
{
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(token);
}

sealed class FakeTransport : IMobileGatewayTransport
{
    public MobileGatewayRequest? LastRequest { get; private set; }
    public ValueTask<MobileGatewayResponse> ExecuteAsync(MobileGatewayRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return ValueTask.FromResult(new MobileGatewayResponse(201, new Dictionary<string, object?> { ["id"] = "conversation-1", ["createdAt"] = "2026-09-22T00:00:00Z" }));
    }
    public async IAsyncEnumerable<ForgeMobileEvent> StreamAsync(MobileGatewayRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        yield return new ForgeMobileEvent("evt-1", "response.delta", "conversation-1", "turn-1", "message-1", "1", new Dictionary<string, object?> { ["delta"] = "Hello" });
        await Task.CompletedTask;
    }
}

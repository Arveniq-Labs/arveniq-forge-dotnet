using System.Text.Json.Nodes;

namespace Arveniq.Forge;

public sealed record ForgeDeveloperAgent(
    string? Id, string? Name, string? Slug, string? Description, string? Category, string? Status,
    string? RiskLevel, JsonObject Project, JsonObject? LatestVersion, string? CreatedAt, string? UpdatedAt)
{
    internal static ForgeDeveloperAgent From(JsonObject value) => new(
        JsonHelpers.String(value, "id"), JsonHelpers.String(value, "name"), JsonHelpers.String(value, "slug"),
        JsonHelpers.String(value, "description"), JsonHelpers.String(value, "category"), JsonHelpers.String(value, "status"),
        JsonHelpers.String(value, "riskLevel"), JsonHelpers.Object(value["project"]), value["latestVersion"] as JsonObject,
        JsonHelpers.String(value, "createdAt"), JsonHelpers.String(value, "updatedAt"));
}

public sealed record ForgeDeveloperWorkflow(
    string? Id, string? AgentId, string? AgentName, string? Name, string? Slug, string? Status, long? LatestVersion, string? UpdatedAt)
{
    internal static ForgeDeveloperWorkflow From(JsonObject value) => new(
        JsonHelpers.String(value, "id"), JsonHelpers.String(value, "agentId"), JsonHelpers.String(value, "agentName"),
        JsonHelpers.String(value, "name"), JsonHelpers.String(value, "slug"), JsonHelpers.String(value, "status"),
        JsonHelpers.NullableNumber(value, "latestVersion"), JsonHelpers.String(value, "updatedAt"));
}

public sealed record ForgeDeveloperRunInput(string? IdempotencyKey, JsonNode? Input)
{
    public static ForgeDeveloperRunInput From<T>(T input, string? idempotencyKey = null)
        => new(idempotencyKey, JsonHelpers.ToNode(input));

    public JsonObject ToJson()
    {
        var value = new JsonObject();
        if (!string.IsNullOrWhiteSpace(IdempotencyKey)) value["idempotencyKey"] = IdempotencyKey;
        if (Input is not null) value["input"] = Input.DeepClone();
        return value;
    }
}

public sealed record ForgeDeveloperRun(
    string? Id, string? AgentRunId, string? LegacyExecutionId, JsonObject Agent, string? WorkflowName,
    string? Status, string? Error, string? FinalAnswer, string? CreatedAt, string? StartedAt,
    string? CompletedAt, string? UpdatedAt)
{
    internal static ForgeDeveloperRun From(JsonObject value) => new(
        JsonHelpers.String(value, "id"), JsonHelpers.String(value, "agentRunId"), JsonHelpers.String(value, "legacyExecutionId"),
        JsonHelpers.Object(value["agent"]), JsonHelpers.String(value, "workflowName"), JsonHelpers.String(value, "status"),
        JsonHelpers.String(value, "error"), JsonHelpers.String(value, "finalAnswer"), JsonHelpers.String(value, "createdAt"),
        JsonHelpers.String(value, "startedAt"), JsonHelpers.String(value, "completedAt"), JsonHelpers.String(value, "updatedAt"));

    public bool IsTerminal => Status?.Trim().ToLowerInvariant() is "approval_required" or "canceled" or "cancelled" or "completed" or "failed" or "succeeded" or "timed_out" or "waiting_approval";
}

public sealed record ForgeConversation(string? Id, string? AgentId, string? CreatedAt)
{
    internal static ForgeConversation From(JsonObject value) => new(JsonHelpers.String(value, "id"), JsonHelpers.String(value, "agentId"), JsonHelpers.String(value, "createdAt"));
}

public sealed record ForgeConversationSnapshot(string? Id, string? AgentId, string? CreatedAt, IReadOnlyList<JsonObject> Turns)
{
    internal static ForgeConversationSnapshot From(JsonObject value) => new(
        JsonHelpers.String(value, "id"), JsonHelpers.String(value, "agentId"), JsonHelpers.String(value, "createdAt"),
        JsonHelpers.Array(value["turns"]).Select(JsonHelpers.Object).ToList());
}

public sealed record ForgeRateLimitState(
    string? OrganizationId, string? WorkspaceId, string? ApiKeyId, long WindowSeconds, long RequestLimit,
    long? BurstLimit, long CurrentUsage, long Remaining, string? ResetAt, bool Exceeded)
{
    internal static ForgeRateLimitState From(JsonObject value) => new(
        JsonHelpers.String(value, "organizationId"), JsonHelpers.String(value, "workspaceId"), JsonHelpers.String(value, "apiKeyId"),
        JsonHelpers.Number(value, "windowSeconds"), JsonHelpers.Number(value, "requestLimit"), JsonHelpers.NullableNumber(value, "burstLimit"),
        JsonHelpers.Number(value, "currentUsage"), JsonHelpers.Number(value, "remaining"), JsonHelpers.String(value, "resetAt"), JsonHelpers.Boolean(value, "exceeded"));
}

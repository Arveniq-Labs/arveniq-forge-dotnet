using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Arveniq.Forge;

public enum DeploymentEnvironment { Development, Staging, Production }
public enum ToolExecutionMode { ReadOnly, Write, ExternalAction }
public enum RiskLevel { Low, Medium, High, Restricted }
public enum ApprovalRequirement { None, Always, PolicyBased, HighRiskOnly }
public enum TraceVisibility { Summary, Metadata, FullSafe }
public enum ToolPackageRuntimeType { Node, Container, Serverless, Mcp, Internal }

public sealed record AuditPolicy(
    bool AuditOnCall, bool AuditInputMetadata, bool AuditOutputMetadata,
    IReadOnlyList<string> RedactInputFields, IReadOnlyList<string> RedactOutputFields,
    TraceVisibility TraceVisibility, string? RetentionPolicyId)
{
    internal JsonObject ToJson() => new()
    {
        ["audit_on_call"] = AuditOnCall, ["audit_input_metadata"] = AuditInputMetadata,
        ["audit_output_metadata"] = AuditOutputMetadata, ["redact_input_fields"] = JsonArray.Create(RedactInputFields.ToArray()),
        ["redact_output_fields"] = JsonArray.Create(RedactOutputFields.ToArray()), ["trace_visibility"] = TraceVisibility.ToWire(),
        ["retention_policy_id"] = RetentionPolicyId,
    };
}

public sealed record ToolPackageHandlerManifest(
    string? HandlerRef, string? Slug, string? Name, string? Description, ToolExecutionMode ExecutionMode,
    RiskLevel RiskLevel, IReadOnlyList<string> RequiredScopes, IReadOnlyList<DeploymentEnvironment> SupportedEnvironments,
    ApprovalRequirement ApprovalRequirement, JsonObject InputSchema, JsonObject OutputSchema, AuditPolicy AuditPolicy)
{
    internal JsonObject ToJson() => new()
    {
        ["handler_ref"] = HandlerRef, ["slug"] = Slug, ["name"] = Name, ["description"] = Description ?? string.Empty,
        ["execution_mode"] = ExecutionMode.ToWire(), ["risk_level"] = RiskLevel.ToWire(), ["required_scopes"] = JsonArray.Create(RequiredScopes.ToArray()),
        ["supported_environments"] = JsonArray.Create(SupportedEnvironments.Select(environment => environment.ToWire()).ToArray()),
        ["approval_requirement"] = ApprovalRequirement.ToWire(), ["input_schema"] = InputSchema.DeepClone(),
        ["output_schema"] = OutputSchema.DeepClone(), ["audit_policy"] = AuditPolicy.ToJson(),
    };
}

public sealed record ToolPackageManifest(
    string? Name, string? Namespace, string? Version, string? Description,
    ToolPackageRuntimeType RuntimeType, string? Entrypoint, IReadOnlyList<ToolPackageHandlerManifest> Handlers)
{
    internal JsonObject ToJson() => new()
    {
        ["name"] = Name, ["namespace"] = Namespace, ["version"] = Version, ["description"] = Description ?? string.Empty,
        ["runtime"] = new JsonObject { ["type"] = RuntimeType.ToWire(), ["entrypoint"] = Entrypoint ?? "src/index.ts" },
        ["handlers"] = new JsonArray(Handlers.Select(handler => handler.ToJson()).ToArray()),
    };
}

public sealed record ValidationIssue(string Path, string Message, ValidationSeverity Severity = ValidationSeverity.Error);
public enum ValidationSeverity { Error, Warning }
public sealed record ValidationResult(bool Valid, IReadOnlyList<ValidationIssue> Errors, IReadOnlyList<ValidationIssue> Warnings);

public sealed class ForgeHandlerOptions
{
    public required string HandlerRef { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ToolExecutionMode ExecutionMode { get; init; } = ToolExecutionMode.ReadOnly;
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
    public required IReadOnlyList<string> RequiredScopes { get; init; }
    public IReadOnlyList<DeploymentEnvironment> SupportedEnvironments { get; init; } = [DeploymentEnvironment.Development];
    public ApprovalRequirement ApprovalRequirement { get; init; } = ApprovalRequirement.None;
    public required JsonObject InputSchema { get; init; }
    public required JsonObject OutputSchema { get; init; }
    public AuditPolicy? AuditPolicy { get; init; }
}

public static class ForgeManifests
{
    private static readonly Regex NamespacePattern = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HandlerRefPattern = new("^tool://([a-z0-9][a-z0-9-]*)/([a-zA-Z0-9_.:-]+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static AuditPolicy CreateAuditPolicy(
        IReadOnlyList<string>? redactInputFields = null, IReadOnlyList<string>? redactOutputFields = null,
        TraceVisibility traceVisibility = TraceVisibility.Metadata, string? retentionPolicyId = "standard")
        => new(true, true, true, redactInputFields ?? [], redactOutputFields ?? [], traceVisibility, retentionPolicyId);

    public static ToolPackageHandlerManifest CreateHandler(ForgeHandlerOptions input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new ToolPackageHandlerManifest(input.HandlerRef, input.Slug, input.Name, input.Description, input.ExecutionMode,
            input.RiskLevel, input.RequiredScopes, input.SupportedEnvironments, input.ApprovalRequirement,
            input.InputSchema, input.OutputSchema, input.AuditPolicy ?? CreateAuditPolicy());
    }

    /// <summary>Forces read-only execution and immutable minimum audit collection.</summary>
    public static ToolPackageHandlerManifest CreateReadOnlyHandler(ForgeHandlerOptions input)
    {
        var audit = input.AuditPolicy ?? CreateAuditPolicy();
        return new ToolPackageHandlerManifest(input.HandlerRef, input.Slug, input.Name, input.Description, ToolExecutionMode.ReadOnly,
            input.RiskLevel, input.RequiredScopes, input.SupportedEnvironments, input.ApprovalRequirement, input.InputSchema,
            input.OutputSchema, CreateAuditPolicy(audit.RedactInputFields, audit.RedactOutputFields, audit.TraceVisibility, audit.RetentionPolicyId));
    }

    public static ToolPackageManifest CreateManifest(string name, string @namespace, string version, IReadOnlyList<ToolPackageHandlerManifest> handlers,
        string? description = null, ToolPackageRuntimeType runtimeType = ToolPackageRuntimeType.Node, string entrypoint = "src/index.ts")
        => new(name, @namespace, version, description, runtimeType, entrypoint, handlers);

    public static ValidationResult Validate(ToolPackageManifest? manifest) => ValidateJson(manifest?.ToJson());

    /// <summary>Validates parsed JSON as well as typed manifests, enabling a build-time CLI.</summary>
    public static ValidationResult ValidateJson(JsonNode? candidate)
    {
        var errors = new List<ValidationIssue>();
        var warnings = new List<ValidationIssue>();
        if (candidate is not JsonObject manifest) { Error(errors, "$", "Manifest must be a JSON object."); return Result(errors, warnings); }
        var @namespace = Required(manifest, "namespace", "$.namespace", errors);
        _ = Required(manifest, "name", "$.name", errors); _ = Required(manifest, "version", "$.version", errors);
        OptionalString(manifest, "description", "$.description", errors);
        if (@namespace is not null && !NamespacePattern.IsMatch(@namespace)) Error(errors, "$.namespace", "Namespace must use lowercase letters, numbers, and hyphens.");
        var runtime = manifest["runtime"] as JsonObject;
        if (runtime is null) Error(errors, "$.runtime", "Runtime metadata is required.");
        else { _ = Required(runtime, "entrypoint", "$.runtime.entrypoint", errors); if (!new[] { "node", "container", "serverless", "mcp", "internal" }.Contains(Text(runtime["type"]))) Error(errors, "$.runtime.type", "Runtime type is invalid."); }
        var handlers = manifest["handlers"] as JsonArray;
        if (handlers is null || handlers.Count == 0) { Error(errors, "$.handlers", "At least one handler is required."); return Result(errors, warnings); }
        var slugs = new HashSet<string>(StringComparer.Ordinal); var references = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < handlers.Count; index++)
        {
            var path = $"$.handlers[{index}]";
            if (handlers[index] is not JsonObject handler) { Error(errors, path, "Handler must be an object."); continue; }
            var reference = Required(handler, "handler_ref", path + ".handler_ref", errors); var slug = Required(handler, "slug", path + ".slug", errors);
            _ = Required(handler, "name", path + ".name", errors); OptionalString(handler, "description", path + ".description", errors);
            if (reference is not null)
            {
                var match = HandlerRefPattern.Match(reference);
                if (!match.Success) Error(errors, path + ".handler_ref", "Handler ref must match tool://namespace/action_name.");
                else if (@namespace is not null && match.Groups[1].Value != @namespace) Error(errors, path + ".handler_ref", "Handler ref namespace must match package namespace.");
                if (!references.Add(reference)) Error(errors, path + ".handler_ref", "Handler ref must be unique.");
            }
            if (slug is not null && !slugs.Add(slug)) Error(errors, path + ".slug", "Handler slug must be unique.");
            if (!StringArray(handler["required_scopes"], false)) Error(errors, path + ".required_scopes", "Required scopes must be explicit.");
            if (handler["supported_environments"] is not JsonArray environments || environments.Count == 0) Error(errors, path + ".supported_environments", "At least one supported environment is required.");
            else if (environments.Any(environment => environment is not JsonValue || !new[] { "DEVELOPMENT", "STAGING", "PRODUCTION" }.Contains(Text(environment)))) Error(errors, path + ".supported_environments", "Supported environments must be valid Forge environments.");
            var execution = Text(handler["execution_mode"]); var risk = Text(handler["risk_level"]); var approval = Text(handler["approval_requirement"]);
            if (!new[] { "read_only", "write", "external_action" }.Contains(execution)) Error(errors, path + ".execution_mode", "Execution mode is invalid.");
            if (!new[] { "low", "medium", "high", "restricted" }.Contains(risk)) Error(errors, path + ".risk_level", "Risk level is invalid.");
            if (!new[] { "none", "always", "policy_based", "high_risk_only" }.Contains(approval)) Error(errors, path + ".approval_requirement", "Approval requirement is invalid.");
            if (!Schema(handler["input_schema"])) Error(errors, path + ".input_schema", "Input schema must be a JSON schema object.");
            if (!Schema(handler["output_schema"])) Error(errors, path + ".output_schema", "Output schema must be a JSON schema object.");
            if (execution == "external_action" && environments?.Any(environment => Text(environment) == "PRODUCTION") == true && approval == "none") Error(errors, path + ".approval_requirement", "Production external_action handlers require approval policy.");
            if (risk is "high" or "restricted" && approval == "none") Error(errors, path + ".approval_requirement", "High/restricted risk handlers require approval policy.");
            ValidateAudit(handler["audit_policy"], path + ".audit_policy", errors);
        }
        return Result(errors, warnings);
    }

    public static void AssertValid(ToolPackageManifest manifest)
    {
        var result = Validate(manifest);
        if (!result.Valid) throw new ArgumentException("Invalid tool package manifest:\n" + string.Join("\n", result.Errors.Select(error => $"{error.Path}: {error.Message}")), nameof(manifest));
    }

    private static void ValidateAudit(JsonNode? candidate, string path, List<ValidationIssue> errors)
    {
        if (candidate is not JsonObject policy) { Error(errors, path, "Audit policy is required."); return; }
        foreach (var key in new[] { "audit_on_call", "audit_input_metadata", "audit_output_metadata" }) if (!IsTrue(policy[key])) Error(errors, path + "." + key, $"{key} must be true for governed tool packages.");
        foreach (var key in new[] { "redact_input_fields", "redact_output_fields" }) if (!StringArray(policy[key], true)) Error(errors, path + "." + key, $"{key} must be an array of field names.");
        if (!new[] { "summary", "metadata", "full_safe" }.Contains(Text(policy["trace_visibility"]))) Error(errors, path + ".trace_visibility", "trace_visibility is invalid.");
        if (policy["retention_policy_id"] is not null and not JsonValue) Error(errors, path + ".retention_policy_id", "retention_policy_id must be a string or null.");
    }
    private static string? Required(JsonObject value, string key, string path, List<ValidationIssue> errors) { var text = JsonHelpers.String(value, key); if (string.IsNullOrWhiteSpace(text)) { Error(errors, path, $"{key} is required."); return null; } return text; }
    private static void OptionalString(JsonObject value, string key, string path, List<ValidationIssue> errors) { if (value[key] is not null and not JsonValue) Error(errors, path, $"{key} must be a string."); }
    private static bool StringArray(JsonNode? value, bool permitsEmpty) => value is JsonArray values && (permitsEmpty || values.Count > 0) && values.All(item => item is JsonValue node && node.TryGetValue<string>(out _));
    private static bool Schema(JsonNode? value) => value is JsonObject schema && (schema["type"] is JsonValue || schema["type"] is JsonArray);
    private static bool IsTrue(JsonNode? value) => value is JsonValue node && node.TryGetValue<bool>(out var flag) && flag;
    private static string? Text(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;
    private static void Error(List<ValidationIssue> errors, string path, string message) => errors.Add(new ValidationIssue(path, message));
    private static ValidationResult Result(List<ValidationIssue> errors, List<ValidationIssue> warnings) => new(errors.Count == 0, errors, warnings);
}

internal static class ForgeManifestWireValues
{
    internal static string ToWire(this DeploymentEnvironment value) => value switch { DeploymentEnvironment.Development => "DEVELOPMENT", DeploymentEnvironment.Staging => "STAGING", _ => "PRODUCTION" };
    internal static string ToWire(this ToolExecutionMode value) => value switch { ToolExecutionMode.ReadOnly => "read_only", ToolExecutionMode.Write => "write", _ => "external_action" };
    internal static string ToWire(this RiskLevel value) => value.ToString().ToLowerInvariant();
    internal static string ToWire(this ApprovalRequirement value) => value switch { ApprovalRequirement.None => "none", ApprovalRequirement.Always => "always", ApprovalRequirement.PolicyBased => "policy_based", _ => "high_risk_only" };
    internal static string ToWire(this TraceVisibility value) => value switch { TraceVisibility.Summary => "summary", TraceVisibility.Metadata => "metadata", _ => "full_safe" };
    internal static string ToWire(this ToolPackageRuntimeType value) => value switch { ToolPackageRuntimeType.Node => "node", ToolPackageRuntimeType.Container => "container", ToolPackageRuntimeType.Serverless => "serverless", ToolPackageRuntimeType.Mcp => "mcp", _ => "internal" };
}

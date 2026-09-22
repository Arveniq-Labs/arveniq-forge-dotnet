using System.Net.Http;
using System.Text.Json.Nodes;

namespace Arveniq.Forge;

/// <summary>Forge Tool Package management client. This control-plane API is distinct from the Developer API.</summary>
public sealed class ForgeToolPackagesClient : ForgeServerClient
{
    public ForgeToolPackagesClient(ForgeClientOptions options) : base(options) { }

    public Task<JsonObject> ListPackagesAsync(CancellationToken cancellationToken = default) => SendJsonAsync("/tool-packages", HttpMethod.Get, cancellationToken: cancellationToken);
    public Task<JsonObject> GetPackageAsync(string packageId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}", HttpMethod.Get, cancellationToken: cancellationToken);
    public Task<JsonObject> CreatePackageAsync(JsonObject input, CancellationToken cancellationToken = default) => SendJsonAsync("/tool-packages", HttpMethod.Post, input, cancellationToken: cancellationToken);
    public Task<JsonObject> UpdatePackageAsync(string packageId, JsonObject input, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}", HttpMethod.Patch, input, cancellationToken: cancellationToken);
    public Task<JsonObject> SubmitPackageAsync(string packageId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}/submit", HttpMethod.Post, cancellationToken: cancellationToken);
    public Task<JsonObject> DeprecatePackageAsync(string packageId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}/deprecate", HttpMethod.Post, cancellationToken: cancellationToken);
    public Task<JsonObject> GetVersionAsync(string versionId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-package-versions/{RequiredId(versionId, nameof(versionId))}", HttpMethod.Get, cancellationToken: cancellationToken);
    public Task<JsonObject> ValidateVersionAsync(string versionId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-package-versions/{RequiredId(versionId, nameof(versionId))}/validate", HttpMethod.Post, cancellationToken: cancellationToken);
    public Task<JsonObject> ListValidationRunsAsync(string versionId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-package-versions/{RequiredId(versionId, nameof(versionId))}/validation-runs", HttpMethod.Get, cancellationToken: cancellationToken);
    public Task<JsonObject> ListDeploymentsAsync(CancellationToken cancellationToken = default) => SendJsonAsync("/tool-package-deployments", HttpMethod.Get, cancellationToken: cancellationToken);
    public Task<JsonObject> ListAvailableHandlersAsync(CancellationToken cancellationToken = default) => SendJsonAsync("/tool-package-handlers/available", HttpMethod.Get, cancellationToken: cancellationToken);

    public Task<JsonObject> DeployVersionAsync(string versionId, DeploymentEnvironment environment, string? reason = null, CancellationToken cancellationToken = default)
        => SendJsonAsync($"/tool-package-versions/{RequiredId(versionId, nameof(versionId))}/deploy", HttpMethod.Post, new JsonObject { ["environment"] = environment.ToWire(), ["reason"] = reason }, cancellationToken: cancellationToken);
    public Task<JsonObject> ApproveDeploymentAsync(string deploymentId, string? reason = null, CancellationToken cancellationToken = default) => DeploymentDecisionAsync(deploymentId, "approve", reason, cancellationToken);
    public Task<JsonObject> RejectDeploymentAsync(string deploymentId, string? reason = null, CancellationToken cancellationToken = default) => DeploymentDecisionAsync(deploymentId, "reject", reason, cancellationToken);
    public Task<JsonObject> RollbackDeploymentAsync(string deploymentId, string? reason = null, CancellationToken cancellationToken = default) => DeploymentDecisionAsync(deploymentId, "rollback", reason, cancellationToken);
    public Task<JsonObject> LinkHandlerToToolAsync(string handlerId, string toolId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-package-handlers/{RequiredId(handlerId, nameof(handlerId))}/link-tool", HttpMethod.Post, new JsonObject { ["toolId"] = toolId }, cancellationToken: cancellationToken);
    public Task<JsonObject> CreateToolDefinitionFromHandlerAsync(string handlerId, CancellationToken cancellationToken = default) => SendJsonAsync($"/tool-package-handlers/{RequiredId(handlerId, nameof(handlerId))}/create-tool-definition", HttpMethod.Post, cancellationToken: cancellationToken);

    public Task<JsonObject> CreateVersionAsync(string packageId, string version, ToolPackageManifest manifest, string? commitSha = null, string? artifactRef = null, DeploymentEnvironment environment = DeploymentEnvironment.Development, CancellationToken cancellationToken = default)
    {
        ForgeManifests.AssertValid(manifest);
        return SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}/versions", HttpMethod.Post,
            new JsonObject { ["version"] = version, ["commitSha"] = commitSha, ["artifactRef"] = artifactRef, ["environment"] = environment.ToWire(), ["manifest"] = manifest.ToJson() }, cancellationToken: cancellationToken);
    }

    /// <summary>Creates a version from a prevalidated raw manifest for the package CLI.</summary>
    public Task<JsonObject> CreateVersionJsonAsync(string packageId, JsonObject input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var validation = ForgeManifests.ValidateJson(input["manifest"]);
        if (!validation.Valid) throw new ArgumentException("Invalid tool package manifest: " + string.Join("; ", validation.Errors.Select(error => error.Message)), nameof(input));
        return SendJsonAsync($"/tool-packages/{RequiredId(packageId, nameof(packageId))}/versions", HttpMethod.Post, input, cancellationToken: cancellationToken);
    }

    private Task<JsonObject> DeploymentDecisionAsync(string deploymentId, string action, string? reason, CancellationToken cancellationToken)
        => SendJsonAsync($"/tool-package-deployments/{RequiredId(deploymentId, nameof(deploymentId))}/{action}", HttpMethod.Post, new JsonObject { ["reason"] = reason }, cancellationToken: cancellationToken);
}

using Arveniq.Forge;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Arveniq.Forge.Tool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            await RunAsync(args).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task RunAsync(string[] args)
    {
        var command = args.FirstOrDefault() ?? "help";
        var flags = ParseFlags(args.Skip(1));
        if (command is "help" or "--help" or "-h") { PrintHelp(); return; }
        if (command == "validate")
        {
            var validation = ForgeManifests.ValidateJson(await ReadManifestAsync(flags.GetValueOrDefault("manifest", "tool-package.json")).ConfigureAwait(false));
            Console.WriteLine(JsonSerializer.Serialize(validation, new JsonSerializerOptions { WriteIndented = true }));
            if (!validation.Valid) throw new InvalidOperationException("Manifest validation failed.");
            return;
        }

        var client = new ForgeToolPackagesClient(new ForgeClientOptions
        {
            BaseUrl = flags.GetValueOrDefault("baseUrl") ?? Environment.GetEnvironmentVariable("FORGE_API_URL") ?? "http://localhost:4000/v1",
            ApiKey = flags.GetValueOrDefault("apiKey") ?? Environment.GetEnvironmentVariable("FORGE_API_KEY") ?? throw new ArgumentException("--api-key or FORGE_API_KEY is required."),
        });
        JsonObject response = command switch
        {
            "submit" => await client.SubmitPackageAsync(Required(flags, "packageId", "--package-id")).ConfigureAwait(false),
            "validate-version" => await client.ValidateVersionAsync(Required(flags, "versionId", "--version-id")).ConfigureAwait(false),
            "deploy-version" => await client.DeployVersionAsync(Required(flags, "versionId", "--version-id"), ParseEnvironment(Required(flags, "environment", "--environment")), flags.GetValueOrDefault("reason")).ConfigureAwait(false),
            "create-version" => await CreateVersionAsync(client, flags).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown command: {command}"),
        };
        Console.WriteLine(response.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<JsonObject> CreateVersionAsync(ForgeToolPackagesClient client, IReadOnlyDictionary<string, string> flags)
    {
        var manifest = await ReadManifestAsync(flags.GetValueOrDefault("manifest", "tool-package.json")).ConfigureAwait(false);
        var validation = ForgeManifests.ValidateJson(manifest);
        if (!validation.Valid) throw new ArgumentException("Invalid tool package manifest: " + string.Join("; ", validation.Errors.Select(error => error.Message)));
        var version = flags.GetValueOrDefault("version") ?? manifest["version"]?.GetValue<string>() ?? throw new ArgumentException("--version is required.");
        // The API accepts JSON. Convert the validated raw manifest after preserving its full schema.
        return await client.CreateVersionJsonAsync(Required(flags, "packageId", "--package-id"), new JsonObject
        {
            ["version"] = version,
            ["commitSha"] = flags.GetValueOrDefault("commitSha"),
            ["artifactRef"] = flags.GetValueOrDefault("artifactRef"),
            ["environment"] = flags.GetValueOrDefault("environment") ?? "DEVELOPMENT",
            ["manifest"] = manifest,
        }).ConfigureAwait(false);
    }

    private static async Task<JsonObject> ReadManifestAsync(string path)
        => JsonNode.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false)) as JsonObject ?? throw new InvalidOperationException("Manifest must be a JSON object.");

    private static Dictionary<string, string> ParseFlags(IEnumerable<string> values)
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        var tokens = values.ToArray();
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!tokens[index].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = ToCamelCase(tokens[index][2..]);
            flags[key] = index + 1 < tokens.Length && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal) ? tokens[++index] : "true";
        }
        return flags;
    }

    private static string Required(IReadOnlyDictionary<string, string> flags, string key, string display) => flags.GetValueOrDefault(key) is { Length: > 0 } value ? value : throw new ArgumentException($"{display} is required.");
    private static DeploymentEnvironment ParseEnvironment(string value) => value switch
    {
        "DEVELOPMENT" => DeploymentEnvironment.Development,
        "STAGING" => DeploymentEnvironment.Staging,
        "PRODUCTION" => DeploymentEnvironment.Production,
        _ => throw new ArgumentException($"Unsupported environment: {value}"),
    };
    private static string ToCamelCase(string value) => string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select((part, index) => index == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    private static void PrintHelp() => Console.WriteLine("""
        Arveniq Forge .NET CLI

        Usage:
          forge-tool validate --manifest tool-package.json
          forge-tool submit --package-id pkg_123
          forge-tool create-version --package-id pkg_123 --manifest tool-package.json --version 1.0.0 --commit-sha abc123
          forge-tool validate-version --version-id version_123
          forge-tool deploy-version --version-id version_123 --environment STAGING --reason "Release candidate"
        """);
}

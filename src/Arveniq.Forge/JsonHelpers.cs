using System.Text.Json;
using System.Text.Json.Nodes;

namespace Arveniq.Forge;

internal static class JsonHelpers
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static string? String(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    internal static long Number(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<long>(out var number) ? number : 0;

    internal static long? NullableNumber(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<long>(out var number) ? number : null;

    internal static bool Boolean(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<bool>(out var flag) && flag;

    internal static JsonObject Object(JsonNode? value) => value as JsonObject ?? new JsonObject();
    internal static JsonArray Array(JsonNode? value) => value as JsonArray ?? new JsonArray();

    internal static JsonNode? ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, SerializerOptions);
    internal static JsonObject ParseObject(string source)
        => JsonNode.Parse(source) as JsonObject ?? throw new InvalidOperationException("Expected a JSON object.");
}

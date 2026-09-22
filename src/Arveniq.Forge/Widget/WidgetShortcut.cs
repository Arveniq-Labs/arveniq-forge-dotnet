using System.Text.Json.Nodes;

namespace Arveniq.Forge.Widget;

/// <summary>A keyboard shortcut that opens a <see cref="ForgeChatWidget"/>.</summary>
public sealed record WidgetShortcut(bool Meta, bool Ctrl, bool Alt, bool Shift, string Key)
{
    public static WidgetShortcut MetaKey(string key) => new(true, false, false, false, NormalizeKey(key));
    public static WidgetShortcut ControlKey(string key) => new(false, true, false, false, NormalizeKey(key));

    public static WidgetShortcut Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Shortcut is required.", nameof(value));
        var meta = false; var ctrl = false; var alt = false; var shift = false; string? key = null;
        foreach (var part in value.Trim().ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part)
            {
                case "META": case "CMD": case "COMMAND": meta = true; break;
                case "CTRL": case "CONTROL": ctrl = true; break;
                case "ALT": case "OPTION": alt = true; break;
                case "SHIFT": shift = true; break;
                default:
                    if (key is not null) throw new ArgumentException("Shortcut must contain one key.", nameof(value));
                    key = part;
                    break;
            }
        }
        return new WidgetShortcut(meta, ctrl, alt, shift, NormalizeKey(key));
    }

    public string Display => $"{(Meta ? "⌘ " : string.Empty)}{(Ctrl ? "Ctrl+" : string.Empty)}{(Alt ? "⌥ " : string.Empty)}{(Shift ? "⇧ " : string.Empty)}{(Key == "SPACE" ? "Space" : Key)}";
    internal JsonObject ToJson() => new() { ["meta"] = Meta, ["ctrl"] = Ctrl, ["alt"] = Alt, ["shift"] = Shift, ["key"] = Key };
    private static string NormalizeKey(string? value) => !string.IsNullOrWhiteSpace(value) ? value.Trim().ToUpperInvariant() : throw new ArgumentException("Shortcut key is required.");
}

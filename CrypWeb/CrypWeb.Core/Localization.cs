namespace CrypTool.Web.Core;

public sealed class PortLoc { public string? Caption { get; set; } public string? Tooltip { get; set; } }

public sealed class PluginLoc
{
    public string? Name { get; set; }
    public string? Tooltip { get; set; }
    public Dictionary<string, PortLoc> Ports { get; set; } = new();
}

/// <summary>Language-aware accessors for plugin metadata (English is the fallback).</summary>
public static class PluginMetaLocalization
{
    static PluginLoc? For(PluginMeta m, string lang) => m.Loc.TryGetValue(lang, out var l) ? l : null;

    public static string NameFor(this PluginMeta m, string lang) => For(m, lang)?.Name is { Length: > 0 } n ? n : m.Name;
    public static string TooltipFor(this PluginMeta m, string lang) => For(m, lang)?.Tooltip is { Length: > 0 } t ? t : m.Tooltip;

    public static string PortCaptionFor(this PluginMeta m, string portName, string lang)
    {
        if (For(m, lang)?.Ports.TryGetValue(portName, out var p) == true && !string.IsNullOrWhiteSpace(p.Caption)) return p.Caption!;
        var e = m.Ports.FirstOrDefault(x => x.Name == portName)?.Caption;
        return string.IsNullOrWhiteSpace(e) || e.EndsWith("Caption") ? portName : e;
    }

    public static string PortTooltipFor(this PluginMeta m, string portName, string lang)
    {
        if (For(m, lang)?.Ports.TryGetValue(portName, out var p) == true && !string.IsNullOrWhiteSpace(p.Tooltip)) return p.Tooltip!;
        return m.Ports.FirstOrDefault(x => x.Name == portName)?.Tooltip ?? "";
    }
}

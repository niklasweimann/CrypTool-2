using System.Text.Json;

namespace CrypTool.Web.App.Visualizers;

public static class XamlRegistry
{
    /// <summary>Plugin folder (key of xaml/index.json) for a plugin type name such as "CrypTool.Plugins.T9Code.T9Code".</summary>
    public static string? Folder(string typeName, JsonElement index)
    {
        foreach (var seg in typeName.Split('.').Reverse())
            if (index.TryGetProperty(seg, out _)) return seg;
        return null;
    }

    static HashSet<string>? known;
    public static void SetKnown(IEnumerable<string> folders) => known = folders.ToHashSet();
    public static bool Has(string typeName) => known != null && typeName.Split('.').Any(known.Contains);
}

/// <summary>Per-plugin state for the drawn XAML: what the code-behind would have put into named elements.</summary>
public static class XamlAdapters
{
    static readonly Dictionary<string, Action<XamlRenderer, XamlViz>> byPlugin = new()
    {
        // the stego text the plugin hands to the presentation (capital letters / marked letters / zero-width encoding)
        ["TextSteganography"] = (r, v) =>
        {
            foreach (var c in new[] { "ShowCapitalLetterEncoding", "ShowLettersMarkingEncoding", "ShowZeroWidthSpaceEncoding" })
                if (v.Call(c) is { ValueKind: System.Text.Json.JsonValueKind.String } t) { r.Text["StegoTextBlock"] = t.GetString() ?? ""; break; }
        },
        ["T9Code"] = (r, v) => { if (v.Call("SetNumbersToDisplay") is { } n) r.Text["Display"] = XamlViz.S(n); },
    };

    /// <summary>Everything the plugin wrote into named elements of its presentation ("name.Text=", "name.Visibility=", ...), applied to the drawn XAML.</summary>
    static void Generic(XamlRenderer r, XamlViz v)
    {
        foreach (var (key, json) in v.AllCalls)
        {
            var m = System.Text.RegularExpressions.Regex.Match(key, @"^(\w+)\.(\w+)=$"); if (!m.Success) continue;
            string name = m.Groups[1].Value, prop = m.Groups[2].Value;
            System.Text.Json.JsonElement e; try { e = System.Text.Json.JsonDocument.Parse(json).RootElement; } catch { continue; }
            var val = e.ValueKind == System.Text.Json.JsonValueKind.Array && e.GetArrayLength() == 1 ? e[0] : e;
            var sv = val.ValueKind switch { System.Text.Json.JsonValueKind.String => val.GetString() ?? "", System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => val.ToString(), _ => null };
            if (sv == null) continue;
            switch (prop)
            {
                case "Text": case "Content": case "Header": r.Text[name] = sv; break;
                case "Visibility": if (sv.Contains("Collapsed") || sv.Contains("Hidden") || sv == "2" || sv == "1") { r.Hidden.Add(name); r.Shown.Remove(name); } else { r.Shown.Add(name); r.Hidden.Remove(name); } break;
                case "Fill": case "Background": if (sv.StartsWith('#') || sv.All(char.IsLetter)) r.Fill[name] = sv; break;
                case "Foreground": if (sv.StartsWith('#') || sv.All(char.IsLetter)) { r.Fill["fg:" + name] = sv; r.Fill[name] = sv; } break;
                case "Value": r.Text["val:" + name] = sv; break;
                case "Source": r.Text["img:" + name] = sv; break;
            }
        }
    }

    public static void Apply(string typeName, string file, XamlRenderer r, XamlViz v)
    {
        Generic(r, v);
        foreach (var seg in typeName.Split('.').Reverse())
            if (byPlugin.TryGetValue(seg, out var a)) { a(r, v); return; }
    }
}

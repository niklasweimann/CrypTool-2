using System.Net;
using System.Text;
using CrypTool.Web.Core;
using CrypWeb.App.Engine;
using Microsoft.AspNetCore.Components;

namespace CrypWeb.App.Pages;

/// <summary>Component metadata, labels, tooltips and port bubbles shown on the canvas.</summary>
public partial class Home
{
    private PluginMeta? Meta(PluginNode p)
    {
        return meta.TryGetValue(p.TypeName, out var m) ? m : null;
    }

    /// <summary>Space the header keeps free on the left (toolbar icons) and right (minimize and close).</summary>
    private double HeaderLeft(PluginNode p) => HasPresentation(p) ? 70 : 55;

    private const double HeaderRight = 46;

    /// <summary>Horizontal center of the title: the middle of the space between the toolbar and the window buttons.</summary>
    private double TitleCenter(PluginNode p, double width) => (HeaderLeft(p) + width - HeaderRight) / 2;

    /// <summary>The component name shortened to the free space of the header; empty when not even a few characters fit.</summary>
    private string HeaderTitle(PluginNode p, PluginMeta? m, double w)
    {
        var s = m?.NameFor(Loc.Lang) ?? p.TypeName.Split('.').Last();
        var max = (int)((w - HeaderLeft(p) - HeaderRight) / 6.3);
        if (max < 3) return "";
        return s.Length > max ? s[..Math.Max(1, max - 1)] + "…" : s;
    }

    private string CountText(string? s)
    {
        s ??= "";
        var lines = s.Length == 0 ? 0 : s.Split('\n').Length;
        return $"{s.Length} {(s.Length == 1 ? "character" : "characters")}, {lines} {(lines == 1 ? "line" : "lines")}";
    }

    private void RemoveNode(PluginNode p)
    {
        ed.Checkpoint();
        ed.Selection.Clear();
        ed.Selection.Add(p.Id);
        ed.DeleteSelection();
    }

    private string CatLabel(string c)
    {
        return Loc["cat." + c];
    }

    /// <summary>The name the user gave the component; if there is none (or it is an unresolved <c>$placeholder$</c>), the plugin's name.</summary>
    private string Title(PluginNode p, PluginMeta? m)
    {
        var hasOwnName = !string.IsNullOrWhiteSpace(p.Name) && !p.Name.StartsWith('$');
        return hasOwnName ? p.Name : m?.NameFor(Loc.Lang) ?? p.TypeName.Split('.').Last();
    }

    private string PortTip(ConnectorPort c, PluginMeta? m)
    {
        var cap = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        var tip = m != null && m.PortTooltipFor(c.PropertyName, Loc.Lang) is { Length: > 0 } mt ? mt : c.ToolTip ?? "";
        return $"{cap} ({c.DataTypeName.Split('.').Last()}){(c.IsMandatory ? " – " + Loc["conn.required"] : "")}\n{tip}";
    }

    private string ItemTip(PluginMeta m)
    {
        return m.TooltipFor(Loc.Lang) + (m.Supported ? "" : "  " + Loc["tip.notRunnable"]);
    }

    private string PortLabel(ConnectorPort c, PluginMeta? m)
    {
        var t = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        return t.Length > 24 && hoverPort != c ? t[..23] + "…" : t;
    }

    private IEnumerable<(ConnectorPort, string)> BubblePorts()
    {
        if (connecting != null)
        {
            yield return (connecting, "src");
            foreach (var c in ed.Ws.Connectors)
                if (c.Plugin != null && c != connecting && ed.CanConnect(connecting, c))
                    yield return (c, hoverPort == c ? "okhov" : "ok");
        }
        else if (hoverPort != null)
        {
            yield return (hoverPort, "hover");
        }
    }

    /// <summary>Speech bubble next to a port (outside of the component), scaled so it stays readable when zoomed out.</summary>
    private MarkupString Bubble(ConnectorPort c, double x, double y, string kind, PluginMeta? m)
    {
        var cap = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        var lines = new List<string> { cap };
        var detail = kind is "hover" or "okhov";
        if (detail)
        {
            lines.Add(
                $"{c.DataTypeName.Split('.').Last()} · {Loc[c.Outgoing ? "conn.output" : "conn.input"]}{(c.IsMandatory ? " · " + Loc["conn.required"] : "")}");
            var tip = m != null && m.PortTooltipFor(c.PropertyName, Loc.Lang) is { Length: > 0 } mt ? mt : c.ToolTip ?? "";
            if (kind == "hover" && tip.Length > 0) lines.AddRange(WrapText(tip, 38).Take(3));
        }

        // Layout in the port's own coordinate system (0,0 = port); the bubble sits `gap` away on the port's outer side.
        double lineHeight = detail ? 14 : 12;
        var w = Math.Max(lines.Max(l => l.Length) * 5.9 + 18, 46); // ~5.9 px per character in the 11px font
        var h = lines.Count * lineHeight + (detail ? 8 : 6);
        const double gap = 11;
        // counter-scale so the bubble stays readable when the canvas is zoomed out
        var sc = Math.Clamp(1 / zoom, 1, detail ? 1.5 : 1.3);

        double bx, by; // top-left corner of the bubble
        string arrow; // the little triangle pointing at the port
        switch (SideOf(c))
        {
            case 2: // west
                bx = -gap - w;
                by = -h / 2;
                arrow = $"M {F(-gap)} -6 L {F(-gap + 7)} 0 L {F(-gap)} 6";
                break;
            case 3: // east
                bx = gap;
                by = -h / 2;
                arrow = $"M {F(gap)} -6 L {F(gap - 7)} 0 L {F(gap)} 6";
                break;
            case 0: // north
                bx = -w / 2;
                by = -gap - h;
                arrow = $"M -6 {F(-gap)} L 0 {F(-gap + 7)} L 6 {F(-gap)}";
                break;
            default: // south
                bx = -w / 2;
                by = gap;
                arrow = $"M -6 {F(gap)} L 0 {F(gap - 7)} L 6 {F(gap)}";
                break;
        }

        var cls = kind.StartsWith("ok") ? "ok" : kind == "src" ? "src" : "";
        var sb = new StringBuilder($"<g class=\"bub {cls}\" transform=\"translate({F(x)} {F(y)}) scale({F(sc)})\">");
        sb.Append($"<rect x=\"{F(bx)}\" y=\"{F(by)}\" width=\"{F(w)}\" height=\"{F(h)}\" rx=\"6\"/><path class=\"tail\" d=\"{arrow} Z\"/>");
        for (var i = 0; i < lines.Count; i++)
        {
            var textClass = i == 0 ? "t1" : "t2"; // first line (caption) is emphasized
            var textY = by + lineHeight + i * lineHeight;
            sb.Append($"<text x=\"{F(bx + 9)}\" y=\"{F(textY)}\" class=\"{textClass}\">{WebUtility.HtmlEncode(lines[i])}</text>");
        }

        sb.Append("</g>");
        return new MarkupString(sb.ToString());
    }

    /// <summary>Greedy word wrap at <paramref name="max" /> characters; line breaks in the text count as spaces.</summary>
    private static IEnumerable<string> WrapText(string text, int max)
    {
        var line = "";
        foreach (var word in text.Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (line.Length + word.Length + 1 > max && line.Length > 0)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : line + " " + word;
            }

        if (line.Length > 0) yield return line;
    }

    /// <summary>False for components without a visualization (Presentation is null in the original): no presentation view.</summary>
    private bool HasPresentation(PluginNode p)
    {
        return IsNative(p) || (Meta(p)?.Presentation ?? true);
    }

    /// <summary>The view a component shows: a stored "presentation" view of a component without one shows its data.</summary>
    private int ViewOf(PluginNode p)
    {
        return p.ViewState == 1 && !HasPresentation(p) ? 2 : p.ViewState;
    }

    /// <summary>Whether the engine can run this component (cached per component type).</summary>
    private bool Runnable(PluginNode p)
    {
        if (!runnable.TryGetValue(p.TypeName, out var result))
            runnable[p.TypeName] = result = IsNative(p) || (Meta(p)?.Supported ?? false);
        return result;
    }

    // ---- browser-native input components (text, number, boolean) ----
    // Their value is stored as a setting of the component under a fixed key.

    private static string NativeKey(PluginNode p)
    {
        if (p.TypeName.EndsWith("NumberInput", StringComparison.OrdinalIgnoreCase)) return "Number";
        if (p.TypeName.EndsWith("BooleanInput", StringComparison.OrdinalIgnoreCase)) return "Value";
        return "Text";
    }

    private string NativeText(PluginNode p)
    {
        return p.Settings.GetValueOrDefault(NativeKey(p), "");
    }

    private void SetNativeText(PluginNode p, string value)
    {
        var key = NativeKey(p);
        p.Settings[key] = value;
        p.SettingTypes[key] = key == "Value" ? "System.Int32" : "System.String";
        ed.MarkDirty();
        if (engine.Running) EngineHost.SetText(p.Id, value);
    }

    private void SetSetting(PluginNode p, string key, string value)
    {
        ed.Checkpoint();
        p.Settings[key] = value;
    }

    /// <summary>Background of a component window, as in CT2: the light variant of its category color.</summary>
    private string WindowBackground(PluginNode p) => Theme.CategoryLightColor(Meta(p)?.Category ?? "Undefined");
}

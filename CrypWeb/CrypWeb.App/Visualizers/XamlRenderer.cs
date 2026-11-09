using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace CrypTool.Web.App.Visualizers;

/// <summary>
/// Draws the original WPF presentation XAML of a plugin as HTML/SVG (layout panels -> CSS grid/flex/absolute, shapes/paths -> SVG,
/// transforms -> CSS transforms, brushes -> CSS). Only what the XAML itself declares is drawn; code-behind state is applied through
/// <see cref="Text"/>/<see cref="Hidden"/>/<see cref="Fill"/> overrides keyed by x:Name (supplied by per-plugin adapters).
/// </summary>
public sealed class XamlRenderer
{
    public IReadOnlyDictionary<string, string> Res { get; init; } = new Dictionary<string, string>();
    public Func<string, string?> ImageUrl { get; init; } = _ => null;
    public Dictionary<string, string> Text { get; } = new();
    public HashSet<string> Hidden { get; } = new();
    public HashSet<string> Shown { get; } = new();
    public Dictionary<string, string> Fill { get; } = new();
    public Dictionary<string, string> Html { get; } = new();

    static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    readonly StringBuilder sb = new();

    /// <summary>Design size of the root (Width/Height or d:DesignWidth/DesignHeight), if any.</summary>
    public (double w, double h)? RootSize { get; private set; }

    public string Render(XElement root)
    {
        sb.Clear();
        double? w = Num(root.Attribute("Width")?.Value), h = Num(root.Attribute("Height")?.Value);
        foreach (var a in root.Attributes())
        {
            if (a.Name.LocalName == "DesignWidth") w ??= Num(a.Value);
            if (a.Name.LocalName == "DesignHeight") h ??= Num(a.Value);
        }
        var content = Content(root).FirstOrDefault();
        if (w is null && h is null && content != null) { w = Num(content.Attribute("Width")?.Value); h = Num(content.Attribute("Height")?.Value); }
        if (w is > 0 && h is > 0) RootSize = (w.Value, h.Value);
        sb.Append("<div class=\"xr\" style=\"").Append(RootSize is { } rs ? $"width:{N(rs.w)}px;height:{N(rs.h)}px;" : "").Append("\">");
        if (content != null) Element(content, "grid");
        sb.Append("</div>");
        return sb.ToString();
    }

    // ---------- helpers ----------
    static string N(double d) => d.ToString("0.###", Inv);
    static double? Num(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s[0] == '{' || s.Equals("Auto", StringComparison.OrdinalIgnoreCase) || s.Equals("NaN", StringComparison.OrdinalIgnoreCase)) return null;
        s = s.Trim(); if (s.EndsWith("px")) s = s[..^2];
        return double.TryParse(s, NumberStyles.Float, Inv, out var d) && !double.IsNaN(d) && !double.IsInfinity(d) ? d : null;
    }
    static string E(string s) => WebUtility.HtmlEncode(s);
    static string A(XElement e, string n) => e.Attribute(n)?.Value ?? "";
    static bool Has(XElement e, string n) => e.Attribute(n) != null;
    static string Name(XElement e) => e.Attribute(X + "Name")?.Value ?? e.Attribute("Name")?.Value ?? "";
    static string[] Parts(string s) => s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static bool IsPropElement(XElement e) => e.Name.LocalName.Contains('.');
    static IEnumerable<XElement> Content(XElement e) => e.Elements().Where(c => !IsPropElement(c));
    static XElement? Prop(XElement e, string prop) => e.Elements().FirstOrDefault(c => c.Name.LocalName.EndsWith("." + prop));

    string Resolve(string s)
    {
        s = s.Trim();
        if (s.StartsWith("{Loc ", StringComparison.OrdinalIgnoreCase) || s.StartsWith("{loc:Loc ", StringComparison.OrdinalIgnoreCase))
        {
            var key = s[(s.IndexOf(' ') + 1)..].TrimEnd('}').Trim();
            return Res.TryGetValue(key, out var v) ? v : key;
        }
        if (s.StartsWith("{")) return "";
        return s;
    }

    // ---------- brushes ----------
    static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase) { ["Transparent"] = "transparent", ["LightSlateGray"] = "lightslategray" };

    static string Color(string c)
    {
        c = c.Trim();
        if (c.StartsWith('#'))
        {
            var h = c[1..];
            if (h.Length == 8) { var a = Convert.ToInt32(h[..2], 16) / 255.0; return $"rgba({Convert.ToInt32(h[2..4], 16)},{Convert.ToInt32(h[4..6], 16)},{Convert.ToInt32(h[6..8], 16)},{N(a)})"; }
            return c;
        }
        return Named.TryGetValue(c, out var n) ? n : c;
    }

    /// <summary>CSS value for a brush given as attribute text or as a brush element.</summary>
    string? BrushCss(XElement? holder, string prop, out string? firstColor)
    {
        firstColor = null; if (holder == null) return null;
        var attr = holder.Attribute(prop)?.Value;
        if (attr != null)
        {
            if (attr.StartsWith('{')) return null;
            firstColor = Color(attr); return firstColor;
        }
        var pe = Prop(holder, prop)?.Elements().FirstOrDefault();
        return pe == null ? null : BrushElement(pe, out firstColor);
    }

    string? BrushElement(XElement b, out string? firstColor)
    {
        firstColor = null;
        switch (b.Name.LocalName)
        {
            case "SolidColorBrush": { var c = A(b, "Color"); if (c == "" || c.StartsWith('{')) return null; var o = Num(A(b, "Opacity")); firstColor = Color(c); return firstColor; }
            case "LinearGradientBrush":
            case "RadialGradientBrush":
                {
                    var stops = b.Descendants().Where(x => x.Name.LocalName == "GradientStop").Select(x => (off: Num(A(x, "Offset")) ?? 0, col: Color(A(x, "Color")))).OrderBy(x => x.off).ToList();
                    if (stops.Count == 0) return null; firstColor = stops[0].col;
                    var list = string.Join(",", stops.Select(s => $"{s.col} {N(s.off * 100)}%"));
                    if (b.Name.LocalName == "RadialGradientBrush") return $"radial-gradient(circle at {N((Num(Parts(Has(b, "GradientOrigin") ? A(b, "GradientOrigin") : "0.5,0.5")[0]) ?? .5) * 100)}% {N((Num(Parts(Has(b, "GradientOrigin") ? A(b, "GradientOrigin") : "0.5,0.5")[1]) ?? .5) * 100)}%,{list})";
                    var sp = Parts(Has(b, "StartPoint") ? A(b, "StartPoint") : "0,0"); var ep = Parts(Has(b, "EndPoint") ? A(b, "EndPoint") : "1,1");
                    double dx = (Num(ep[0]) ?? 1) - (Num(sp[0]) ?? 0), dy = (Num(ep[1]) ?? 1) - (Num(sp[1]) ?? 0);
                    var ang = Math.Atan2(dx, -dy) * 180 / Math.PI;
                    return $"linear-gradient({N(ang)}deg,{list})";
                }
        }
        return null;
    }

    // ---------- transforms ----------
    string? TransformCss(XElement e)
    {
        var parts = new List<string>();
        foreach (var pn in new[] { "LayoutTransform", "RenderTransform" })
        {
            var t = Prop(e, pn)?.Elements().FirstOrDefault();
            if (t != null) parts.Add(TransformOne(t));
        }
        var s = string.Join(" ", parts.Where(p => p != ""));
        return s == "" ? null : s;
    }

    string TransformOne(XElement t)
    {
        switch (t.Name.LocalName)
        {
            case "TransformGroup":
                return string.Join(" ", Content(t).Reverse().Select(TransformOne).Where(x => x != "")); // WPF: first applied first; CSS: last in list applies first
            case "TranslateTransform": return $"translate({N(Num(A(t, "X")) ?? 0)}px,{N(Num(A(t, "Y")) ?? 0)}px)";
            case "MatrixTransform":
                { var m = Parts(A(t, "Matrix")); return m.Length == 6 ? $"matrix({string.Join(",", m.Select(x => N(Num(x) ?? 0)))})" : ""; }
        }
        double cx = Num(A(t, "CenterX")) ?? 0, cy = Num(A(t, "CenterY")) ?? 0;
        string core = t.Name.LocalName switch
        {
            "RotateTransform" => $"rotate({N(Num(A(t, "Angle")) ?? 0)}deg)",
            "ScaleTransform" => $"scale({N(Num(A(t, "ScaleX")) ?? 1)},{N(Num(A(t, "ScaleY")) ?? 1)})",
            "SkewTransform" => $"skew({N(Num(A(t, "AngleX")) ?? 0)}deg,{N(Num(A(t, "AngleY")) ?? 0)}deg)",
            _ => "",
        };
        if (core == "") return "";
        return cx == 0 && cy == 0 ? core : $"translate({N(cx)}px,{N(cy)}px) {core} translate({N(-cx)}px,{N(-cy)}px)";
    }

    // ---------- common style ----------
    string Common(XElement e, string parent)
    {
        var s = new StringBuilder();
        double? w = Num(A(e, "Width")), h = Num(A(e, "Height"));
        if (w != null) s.Append($"width:{N(w.Value)}px;"); if (h != null) s.Append($"height:{N(h.Value)}px;");
        if (Num(A(e, "MinWidth")) is { } mw) s.Append($"min-width:{N(mw)}px;"); if (Num(A(e, "MinHeight")) is { } mh) s.Append($"min-height:{N(mh)}px;");
        if (Num(A(e, "MaxWidth")) is { } xw) s.Append($"max-width:{N(xw)}px;"); if (Num(A(e, "MaxHeight")) is { } xh) s.Append($"max-height:{N(xh)}px;");
        var ha = A(e, "HorizontalAlignment"); var va = A(e, "VerticalAlignment");
        string Al(string v, bool sized) => v switch { "Left" or "Top" => "start", "Center" => "center", "Right" or "Bottom" => "end", "Stretch" => "stretch", _ => sized ? "center" : "stretch" };
        if (parent == "canvas")
        {
            s.Append("position:absolute;");
            var l = Num(A(e, "Canvas.Left")); var t = Num(A(e, "Canvas.Top")); var r = Num(A(e, "Canvas.Right")); var b = Num(A(e, "Canvas.Bottom"));
            if (l != null) s.Append($"left:{N(l.Value)}px;"); else if (r == null) s.Append("left:0;");
            if (t != null) s.Append($"top:{N(t.Value)}px;"); else if (b == null) s.Append("top:0;");
            if (r != null) s.Append($"right:{N(r.Value)}px;"); if (b != null) s.Append($"bottom:{N(b.Value)}px;");
        }
        else
        {
            var m = A(e, "Margin");
            if (m != "" && !m.StartsWith('{')) { var p = Parts(m).Select(x => Num(x) ?? 0).ToArray(); s.Append("margin:" + (p.Length switch { 1 => $"{N(p[0])}px", 2 => $"{N(p[1])}px {N(p[0])}px", 4 => $"{N(p[1])}px {N(p[2])}px {N(p[3])}px {N(p[0])}px", _ => "0" }) + ";"); }
            if (parent == "grid")
            {
                int r = (int)(Num(A(e, "Grid.Row")) ?? 0), c = (int)(Num(A(e, "Grid.Column")) ?? 0), rs = (int)(Num(A(e, "Grid.RowSpan")) ?? 1), cs = (int)(Num(A(e, "Grid.ColumnSpan")) ?? 1);
                s.Append($"grid-row:{r + 1}/span {rs};grid-column:{c + 1}/span {cs};justify-self:{Al(ha, w != null)};align-self:{Al(va, h != null)};");
            }
            else if (parent == "stack-v") s.Append($"flex:none;align-self:{Al(ha, w != null)};");
            else if (parent == "stack-h") s.Append($"flex:none;align-self:{Al(va, h != null)};");
            else s.Append("flex:none;");
        }
        if (Num(A(e, "Opacity")) is { } op) s.Append($"opacity:{N(op)};");
        if (Num(A(e, "Panel.ZIndex")) is { } z) s.Append($"z-index:{(int)z};");
        var tr = TransformCss(e);
        if (tr != null)
        {
            var o = Parts(A(e, "RenderTransformOrigin")); s.Append($"transform:{tr};transform-origin:{(o.Length == 2 ? $"{N((Num(o[0]) ?? 0) * 100)}% {N((Num(o[1]) ?? 0) * 100)}%" : "0 0")};");
        }
        var vis = A(e, "Visibility"); var nm = Name(e);
        if (nm != "" && Hidden.Contains(nm)) vis = "Collapsed"; if (nm != "" && Shown.Contains(nm)) vis = "Visible";
        if (vis == "Collapsed") s.Append("display:none;"); else if (vis == "Hidden") s.Append("visibility:hidden;");
        return s.ToString();
    }

    string TextStyle(XElement e)
    {
        var s = new StringBuilder();
        var nm0 = Name(e);
        if (nm0 != "" && Fill.TryGetValue("fg:" + nm0, out var fo0)) s.Append($"color:{Color(fo0)};");
        else if (BrushCss(e, "Foreground", out var fc) != null && fc != null) s.Append($"color:{fc};");
        if (Num(A(e, "FontSize")) is { } fs) s.Append($"font-size:{N(fs)}px;");
        var ff = A(e, "FontFamily"); if (ff != "" && !ff.StartsWith('{')) { var n = ff.Contains('#') ? ff[(ff.LastIndexOf('#') + 1)..] : ff; s.Append($"font-family:'{E(n.Split(',')[0].Trim())}',sans-serif;"); }
        var fw = A(e, "FontWeight"); if (fw is "Bold" or "SemiBold" or "ExtraBold" or "Black" or "Heavy") s.Append("font-weight:bold;");
        if (A(e, "FontStyle") is "Italic" or "Oblique") s.Append("font-style:italic;");
        var ta = A(e, "TextAlignment"); if (ta is "Center" or "Right" or "Left" or "Justify") s.Append($"text-align:{ta.ToLower()};");
        var td = A(e, "TextDecorations"); if (td.Contains("Underline")) s.Append("text-decoration:underline;");
        return s.ToString();
    }

    string Padding(XElement e, double dflt = 0)
    {
        var m = A(e, "Padding"); if (m == "" || m.StartsWith('{')) return dflt == 0 ? "" : $"padding:{N(dflt)}px;";
        var p = Parts(m).Select(x => Num(x) ?? 0).ToArray();
        return "padding:" + (p.Length switch { 1 => $"{N(p[0])}px", 2 => $"{N(p[1])}px {N(p[0])}px", 4 => $"{N(p[1])}px {N(p[2])}px {N(p[3])}px {N(p[0])}px", _ => "0" }) + ";";
    }

    string BorderCss(XElement e)
    {
        var s = new StringBuilder();
        var bg = BrushCss(e, "Background", out _); if (bg != null) s.Append(bg.Contains("gradient") ? $"background:{bg};" : $"background-color:{bg};");
        var bt = A(e, "BorderThickness"); var bb = BrushCss(e, "BorderBrush", out var bc);
        if (bt != "" && !bt.StartsWith('{'))
        {
            var p = Parts(bt).Select(x => Num(x) ?? 0).ToArray();
            var th = p.Length switch { 1 => $"{N(p[0])}px", 2 => $"{N(p[1])}px {N(p[0])}px", 4 => $"{N(p[1])}px {N(p[2])}px {N(p[3])}px {N(p[0])}px", _ => "0" };
            s.Append($"border-width:{th};border-style:solid;border-color:{bc ?? "transparent"};");
        }
        var cr = A(e, "CornerRadius"); if (cr != "" && !cr.StartsWith('{')) s.Append("border-radius:" + string.Join(" ", Parts(cr).Select(x => N(Num(x) ?? 0) + "px")) + ";");
        return s.ToString();
    }

    // ---------- elements ----------
    void Children(XElement e, string ctx) { foreach (var c in Content(e)) Element(c, ctx); }

    void Element(XElement e, string parent)
    {
        var n = e.Name.LocalName; var name = Name(e);
        string cls = name != "" ? $" data-n=\"{E(name)}\"" : "";
        switch (n)
        {
            case "Grid":
                {
                    string Defs(string owner, string tag, string sizeAttr)
                    {
                        var d = Prop(e, owner)?.Elements().Where(x => x.Name.LocalName == tag).ToList();
                        if (d == null || d.Count == 0) return "minmax(0,1fr)";
                        return string.Join(" ", d.Select(x =>
                        {
                            var v = A(x, sizeAttr);
                            if (v == "" ) return "minmax(0,1fr)";
                            if (v.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return "auto";
                            if (v.EndsWith('*')) { var k = v.Length == 1 ? 1 : Num(v[..^1]) ?? 1; return $"minmax(0,{N(k)}fr)"; }
                            var px = Num(v); return px != null ? $"{N(px.Value)}px" : "auto";
                        }));
                    }
                    sb.Append($"<div{cls} class=\"xg\" style=\"{Common(e, parent)}{BorderCss(e)}grid-template-rows:{Defs("RowDefinitions", "RowDefinition", "Height")};grid-template-columns:{Defs("ColumnDefinitions", "ColumnDefinition", "Width")};\">");
                    Children(e, "grid"); sb.Append("</div>"); break;
                }
            case "StackPanel":
            case "WrapPanel":
            case "DockPanel":
            case "UniformGrid":
                {
                    bool horiz = A(e, "Orientation") == "Horizontal";
                    string extra = n switch { "WrapPanel" => "flex-wrap:wrap;", "UniformGrid" => $"display:grid;grid-template-columns:repeat({(int)(Num(A(e, "Columns")) ?? 1)},1fr);", _ => "" };
                    sb.Append($"<div{cls} class=\"xs\" style=\"{Common(e, parent)}{BorderCss(e)}flex-direction:{(horiz ? "row" : "column")};{extra}\">");
                    Children(e, n == "UniformGrid" ? "grid" : horiz ? "stack-h" : "stack-v"); sb.Append("</div>"); break;
                }
            case "Canvas":
                sb.Append($"<div{cls} class=\"xc\" style=\"{Common(e, parent)}{BorderCss(e)}\">"); Children(e, "canvas"); sb.Append("</div>"); break;
            case "Border":
            case "ScrollViewer":
            case "ContentControl":
            case "ContentPresenter":
            case "Grid.Row":
                sb.Append($"<div{cls} class=\"xb\" style=\"{Common(e, parent)}{BorderCss(e)}{Padding(e)}\">"); Children(e, "grid"); sb.Append("</div>"); break;
            case "Viewbox":
                {
                    var inner = Content(e).FirstOrDefault();
                    double? iw = Num(inner?.Attribute("Width")?.Value), ih = Num(inner?.Attribute("Height")?.Value);
                    sb.Append($"<div{cls} class=\"vb\" data-w=\"{(iw != null ? N(iw.Value) : "")}\" data-h=\"{(ih != null ? N(ih.Value) : "")}\" style=\"{Common(e, parent)}\"><div class=\"vbi\">");
                    if (inner != null) Element(inner, "grid");
                    sb.Append("</div></div>"); break;
                }
            case "GroupBox":
            case "Expander":
                sb.Append($"<fieldset{cls} class=\"xb\" style=\"{Common(e, parent)}\"><legend>{E(Resolve(A(e, "Header")))}</legend>"); Children(e, "grid"); sb.Append("</fieldset>"); break;
            case "TabControl":
                {
                    var tabs = Content(e).ToList();
                    sb.Append($"<div{cls} class=\"xtab\" style=\"{Common(e, parent)}\"><div class=\"xth\">");
                    for (int i = 0; i < tabs.Count; i++) sb.Append($"<span class=\"{(i == 0 ? "on" : "")}\">{E(Resolve(A(tabs[i], "Header")))}</span>");
                    sb.Append("</div><div class=\"xb\">"); if (tabs.Count > 0) Children(tabs[0], "grid"); sb.Append("</div></div>"); break;
                }
            case "TabItem": Children(e, parent); break;
            case "TextBlock":
            case "Label":
                {
                    bool label = n == "Label";
                    var wrap = A(e, "TextWrapping") is "Wrap" or "WrapWithOverflow";
                    sb.Append($"<div{cls} class=\"xt\" style=\"{Common(e, parent)}{TextStyle(e)}{BorderCss(e)}{Padding(e, label ? 5 : 0)}white-space:{(wrap ? "pre-wrap" : "pre")};\">");
                    if (name != "" && Html.TryGetValue(name, out var hv)) sb.Append(hv);
                    else if (name != "" && Text.TryGetValue(name, out var tv)) sb.Append(E(tv));
                    else Inlines(e, label);
                    sb.Append("</div>"); break;
                }
            case "Button":
            case "ToggleButton":
            case "CheckBox":
            case "RadioButton":
            case "MenuItem":
                {
                    string tag = n is "CheckBox" or "RadioButton" ? "label" : "button";
                    sb.Append($"<{tag}{cls} class=\"xbtn {n}\" style=\"{Common(e, parent)}{TextStyle(e)}{BorderCss(e)}{Padding(e)}\">");
                    if (n == "CheckBox") sb.Append($"<input type=\"checkbox\" disabled {(A(e, "IsChecked") == "True" ? "checked" : "")}/>");
                    if (n == "RadioButton") sb.Append($"<input type=\"radio\" disabled {(A(e, "IsChecked") == "True" ? "checked" : "")}/>");
                    if (name != "" && Text.TryGetValue(name, out var tv)) sb.Append(E(tv));
                    else if (Has(e, "Content")) sb.Append(E(Resolve(A(e, "Content"))));
                    else Inlines(e, false, true);
                    sb.Append($"</{tag}>"); break;
                }
            case "TextBox":
            case "PasswordBox":
            case "RichTextBox":
            case "ComboBox":
            case "ListBox":
            case "ListView":
            case "DataGrid":
            case "TreeView":
            case "ListBoxItem":
                {
                    var txt = name != "" && Text.TryGetValue(name, out var tv) ? tv : Resolve(A(e, "Text"));
                    sb.Append($"<div{cls} class=\"xbox {n}\" style=\"{Common(e, parent)}{TextStyle(e)}{BorderCss(e)}\">");
                    var cols = e.Descendants().Where(d => d.Name.LocalName is "GridViewColumn" or "DataGridTextColumn" or "DataGridTemplateColumn").ToList();
                    if (cols.Count > 0) sb.Append("<div class=\"xcols\">" + string.Concat(cols.Select(c => $"<span>{E(Resolve(A(c, "Header")))}</span>")) + "</div>");
                    if (n == "RichTextBox") { foreach (var x in e.Descendants().Where(d => d.Name.LocalName is "Paragraph")) { sb.Append("<p>"); Inlines(x, false); sb.Append("</p>"); } }
                    else sb.Append(E(txt));
                    sb.Append("</div>"); break;
                }
            case "Image":
                {
                    var src = A(e, "Source"); string? url = src != "" && !src.StartsWith('{') ? ImageUrl(src) : null;
                    if (name != "" && Text.TryGetValue("img:" + name, out var iu)) url = ImageUrl(iu) ?? url;
                    var fit = A(e, "Stretch") switch { "Fill" => "fill", "UniformToFill" => "cover", "None" => "none", _ => "contain" };
                    sb.Append($"<img{cls} class=\"xi\" style=\"{Common(e, parent)}object-fit:{fit};min-width:0;min-height:0;{(Has(e, "Width") ? "" : "max-width:100%;")}{(Has(e, "Height") ? "" : "max-height:100%;")}{(!Has(e, "Width") && !Has(e, "Height") && parent is "grid" ? "width:100%;height:100%;" : "")}\" {(url != null ? $"src=\"{E(url)}\"" : "")} alt=\"\"/>"); break;
                }
            case "Rectangle":
            case "Ellipse":
                {
                    var f = name != "" && Fill.TryGetValue(name, out var fo) ? Color(fo) : BrushCss(e, "Fill", out _);
                    var st = BrushCss(e, "Stroke", out var sc); var th = Num(A(e, "StrokeThickness")) ?? (st != null ? 1 : 0);
                    var rx = Num(A(e, "RadiusX")) ?? 0;
                    sb.Append($"<div{cls} class=\"xr2\" style=\"{Common(e, parent)}{(f != null ? (f.Contains("gradient") ? $"background:{f};" : $"background-color:{f};") : "")}{(st != null ? $"border:{N(th)}px solid {sc};" : "")}box-sizing:border-box;border-radius:{(n == "Ellipse" ? "50%" : N(rx) + "px")};\"></div>"); break;
                }
            case "Line":
                {
                    double x1 = Num(A(e, "X1")) ?? 0, y1 = Num(A(e, "Y1")) ?? 0, x2 = Num(A(e, "X2")) ?? 0, y2 = Num(A(e, "Y2")) ?? 0;
                    Svg(e, parent, $"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" {Paint(e, false)}/>"); break;
                }
            case "Polyline":
            case "Polygon":
                {
                    var pts = A(e, "Points"); if (pts.StartsWith('{')) break;
                    Svg(e, parent, $"<{n.ToLower()} points=\"{E(pts)}\" {Paint(e, true)}/>"); break;
                }
            case "Path":
                {
                    var d = A(e, "Data"); if (d == "") d = Geometry(Prop(e, "Data")?.Elements().FirstOrDefault()); if (d.StartsWith('{')) d = "";
                    var fr = d.StartsWith("F1") ? "evenodd" : "nonzero"; d = System.Text.RegularExpressions.Regex.Replace(d, @"^F[01]\s*", "");
                    Svg(e, parent, $"<path d=\"{E(d)}\" fill-rule=\"{fr}\" {Paint(e, true, name)}/>"); break;
                }
            case "ProgressBar":
                {
                    var mx = Num(A(e, "Maximum")) ?? 100; var v = Num(A(e, "Value")) ?? 0;
                    if (name != "" && Text.TryGetValue("val:" + name, out var vv) && Num(vv) is { } vn) v = vn;
                    var bar = name != "" && Fill.TryGetValue("fg:" + name, out var bc2) ? $"background:{Color(bc2)};" : "";
                    sb.Append($"<div{cls} class=\"pbar\" style=\"{Common(e, parent)}\"><div style=\"{bar}width:{N(Math.Clamp(v / mx * 100, 0, 100))}%\"></div></div>"); break;
                }
            case "Slider": sb.Append($"<input{cls} type=\"range\" disabled style=\"{Common(e, parent)}\"/>"); break;
            case "Separator": sb.Append($"<hr style=\"{Common(e, parent)}\"/>"); break;
            case "Run": case "LineBreak": case "Bold": case "Italic": case "Span": case "Hyperlink": case "Underline": Inlines(e, false); break;
            case "ResourceDictionary": case "Style": case "DataTemplate": case "ControlTemplate": case "Storyboard": break;
            default:
                // custom / unknown control: draw its content
                sb.Append($"<div{cls} class=\"xb unk\" title=\"{E(n)}\" style=\"{Common(e, parent)}{BorderCss(e)}\">"); Children(e, "grid"); sb.Append("</div>"); break;
        }
    }

    // ---------- text ----------
    void Inlines(XElement e, bool label, bool contentOnly = false)
    {
        var t = A(e, "Text"); if (t == "" && label) t = A(e, "Content");
        if (t != "") { sb.Append(E(Resolve(t))); return; }
        foreach (var node in e.Nodes())
        {
            switch (node)
            {
                case XText x: { var s = System.Text.RegularExpressions.Regex.Replace(x.Value, @"\s+", " "); if (s.Trim() != "" || sb.Length > 0 && s != "") sb.Append(E(s)); break; }
                case XElement c:
                    switch (c.Name.LocalName)
                    {
                        case "Run": sb.Append(E(Resolve(A(c, "Text") != "" ? A(c, "Text") : c.Value))); break;
                        case "LineBreak": sb.Append("<br/>"); break;
                        case "Bold": sb.Append("<b>"); Inlines(c, false); sb.Append("</b>"); break;
                        case "Italic": sb.Append("<i>"); Inlines(c, false); sb.Append("</i>"); break;
                        case "Underline": sb.Append("<u>"); Inlines(c, false); sb.Append("</u>"); break;
                        case "Span": case "Hyperlink": sb.Append($"<span style=\"{TextStyle(c)}\">"); Inlines(c, false); sb.Append("</span>"); break;
                        case var pn when pn.EndsWith(".Inlines") || pn == "TextBlock.Inlines": Inlines(c, false); break;
                        default:
                            if (!c.Name.LocalName.Contains('.')) Element(c, "stack-v");
                            break;
                    }
                    break;
            }
        }
    }

    // ---------- svg ----------
    void Svg(XElement e, string parent, string inner)
    {
        var name = Name(e);
        var style = Common(e, parent == "canvas" ? "canvas" : "other").Replace("flex:none;", "");
        // shapes outside a canvas are positioned from the top-left of the parent
        if (parent != "canvas") style = "position:absolute;left:0;top:0;" + style;
        sb.Append($"<svg{(name != "" ? $" data-n=\"{E(name)}\"" : "")} class=\"xsvg\" style=\"{style}\" width=\"1\" height=\"1\">{inner}</svg>");
    }

    string Paint(XElement e, bool fillable, string name = "")
    {
        var s = new StringBuilder();
        string? fill = name != "" && Fill.TryGetValue(name, out var fo) ? Color(fo) : BrushFirst(e, "Fill");
        s.Append($"fill=\"{(fillable ? fill ?? "none" : "none")}\" ");
        var st = BrushFirst(e, "Stroke"); if (st != null)
        {
            s.Append($"stroke=\"{st}\" stroke-width=\"{N(Num(A(e, "StrokeThickness")) ?? 1)}\" ");
            var da = A(e, "StrokeDashArray"); if (da != "") s.Append($"stroke-dasharray=\"{E(da.Replace(' ', ','))}\" ".Replace(",\"", "\""));
            var lc = A(e, "StrokeStartLineCap"); if (lc == "") lc = A(e, "StrokeEndLineCap"); if (lc == "Round") s.Append("stroke-linecap=\"round\" "); else if (lc == "Square") s.Append("stroke-linecap=\"square\" ");
            if (A(e, "StrokeLineJoin") == "Round") s.Append("stroke-linejoin=\"round\" ");
        }
        return s.ToString();
    }

    string? BrushFirst(XElement e, string prop) { BrushCss(e, prop, out var c); return c; }

    string Geometry(XElement? g)
    {
        if (g == null) return "";
        switch (g.Name.LocalName)
        {
            case "PathGeometry":
                {
                    var f = A(g, "Figures"); if (f != "") return f;
                    return string.Concat(g.Descendants().Where(x => x.Name.LocalName == "PathFigure").Select(Figure));
                }
            case "GeometryGroup": return string.Concat(Content(g).Select(Geometry));
            case "EllipseGeometry":
                { var c = Parts(A(g, "Center")); double cx = c.Length > 1 ? Num(c[0]) ?? 0 : 0, cy = c.Length > 1 ? Num(c[1]) ?? 0 : 0, rx = Num(A(g, "RadiusX")) ?? 0, ry = Num(A(g, "RadiusY")) ?? 0; return $"M{N(cx - rx)},{N(cy)}a{N(rx)},{N(ry)} 0 1,0 {N(2 * rx)},0a{N(rx)},{N(ry)} 0 1,0 {N(-2 * rx)},0z"; }
            case "RectangleGeometry":
                { var r = Parts(A(g, "Rect")).Select(x => Num(x) ?? 0).ToArray(); return r.Length == 4 ? $"M{N(r[0])},{N(r[1])}h{N(r[2])}v{N(r[3])}h{N(-r[2])}z" : ""; }
            case "LineGeometry": { var a = Parts(A(g, "StartPoint")); var b = Parts(A(g, "EndPoint")); return a.Length == 2 && b.Length == 2 ? $"M{a[0]},{a[1]}L{b[0]},{b[1]}" : ""; }
        }
        return "";
    }

    static string Figure(XElement f)
    {
        var sp = Parts(A(f, "StartPoint")); var d = new StringBuilder(sp.Length == 2 ? $"M{sp[0]},{sp[1]}" : "M0,0");
        foreach (var s in f.Descendants())
        {
            switch (s.Name.LocalName)
            {
                case "LineSegment": d.Append($"L{A(s, "Point")} "); break;
                case "PolyLineSegment": d.Append($"L{A(s, "Points")} "); break;
                case "BezierSegment": d.Append($"C{A(s, "Point1")} {A(s, "Point2")} {A(s, "Point3")} "); break;
                case "PolyBezierSegment": d.Append($"C{A(s, "Points")} "); break;
                case "QuadraticBezierSegment": d.Append($"Q{A(s, "Point1")} {A(s, "Point2")} "); break;
                case "ArcSegment":
                    { var sz = Parts(A(s, "Size")); d.Append($"A{(sz.Length == 2 ? $"{sz[0]},{sz[1]}" : "0,0")} {A(s, "RotationAngle").DefaultIfEmpty("0")} {(A(s, "IsLargeArc") == "True" ? 1 : 0)} {(A(s, "SweepDirection") == "Clockwise" ? 1 : 0)} {A(s, "Point")} "); break; }
            }
        }
        if (A(f, "IsClosed") == "True") d.Append('z');
        return d.ToString();
    }
}

static class StrExt { public static string DefaultIfEmpty(this string s, string d) => s == "" ? d : s; }

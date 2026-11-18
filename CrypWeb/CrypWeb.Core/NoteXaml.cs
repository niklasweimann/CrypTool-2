using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CrypTool.Web.Core;

/// <summary>
///     The rich text of a note. CT2 keeps a note as the XAML of a WPF FlowDocument section (Section, Paragraph, List,
///     Run, Span, Bold/Italic/Underline, LineBreak, Table) inside a XamlPackage. The web app shows and edits it as HTML:
///     <see cref="ToHtml"/> turns the XAML into HTML made only of tags and style values written here (so a workspace
///     file cannot inject markup), and <see cref="FromBlocks"/> builds the XAML again from what the editor produced
///     (wwwroot/noteeditor.js describes its content as blocks of styled runs).
/// </summary>
public static class NoteXaml
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>Defaults of a new note (the attributes CT2 writes on the section).</summary>
    public const string DefaultFontFamily = "Segoe UI";

    public const double DefaultFontSize = 12;

    // ---- XAML -> HTML ------------------------------------------------------------------------------------------

    /// <summary>The character formatting that is inherited from the section down to the runs.</summary>
    private sealed record Format(bool Bold, bool Italic, bool Underline, double Size, string Family, string? Color)
    {
        public Format With(XElement e)
        {
            var f = this;
            var weight = (string?)e.Attribute("FontWeight");
            if (weight != null) f = f with { Bold = IsBold(weight) };
            var style = (string?)e.Attribute("FontStyle");
            if (style != null) f = f with { Italic = style is "Italic" or "Oblique" };
            var decorations = (string?)e.Attribute("TextDecorations");
            if (decorations != null) f = f with { Underline = decorations.Contains("Underline", StringComparison.OrdinalIgnoreCase) };
            if (double.TryParse((string?)e.Attribute("FontSize"), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0)
                f = f with { Size = size };
            var family = SafeFamily((string?)e.Attribute("FontFamily"));
            if (family != null) f = f with { Family = family };
            var color = SafeColor((string?)e.Attribute("Foreground"));
            if (color != null) f = f with { Color = color };
            return e.Name.LocalName switch
            {
                "Bold" => f with { Bold = true },
                "Italic" => f with { Italic = true },
                "Underline" => f with { Underline = true },
                _ => f,
            };
        }
    }

    private static bool IsBold(string weight) =>
        weight is "Bold" or "SemiBold" or "DemiBold" or "ExtraBold" or "UltraBold" or "Black" or "Heavy" or "ExtraBlack" or "UltraBlack"
        || (int.TryParse(weight, out var w) && w >= 600);

    /// <summary>
    ///     HTML for the XAML of a note (a Section, or any FlowDocument fragment). Returns null when the text is not
    ///     XAML; the caller then shows the plain text.
    /// </summary>
    public static string? ToHtml(string? xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return null;
        XElement root;
        try
        {
            root = XElement.Parse(xaml, LoadOptions.PreserveWhitespace);
        }
        catch (Exception)
        {
            return null;
        }

        var format = new Format(false, false, false, DefaultFontSize, DefaultFontFamily, null).With(root);
        var html = new StringBuilder();
        html.Append("<div class=\"rt\" style=\"").Append(CharacterStyle(format, null)).Append("\">");
        Blocks(root, format, html);
        html.Append("</div>");
        return html.ToString();
    }

    private static void Blocks(XElement parent, Format format, StringBuilder html)
    {
        foreach (var e in parent.Elements())
        {
            var f = format.With(e);
            switch (e.Name.LocalName)
            {
                case "Paragraph":
                    html.Append("<p style=\"").Append(ParagraphStyle(e)).Append("\">");
                    Inlines(e, f, html);
                    html.Append("</p>");
                    break;
                case "List":
                    var ordered = (string?)e.Attribute("MarkerStyle") is "Decimal" or "LowerLatin" or "UpperLatin" or "LowerRoman" or "UpperRoman";
                    html.Append(ordered ? "<ol>" : "<ul>");
                    foreach (var item in e.Elements().Where(i => i.Name.LocalName == "ListItem"))
                    {
                        html.Append("<li>");
                        Blocks(item, f.With(item), html);
                        html.Append("</li>");
                    }

                    html.Append(ordered ? "</ol>" : "</ul>");
                    break;
                case "Table":
                    // a table becomes one line per row, the cells separated by a gap
                    foreach (var row in e.Descendants().Where(r => r.Name.LocalName == "TableRow"))
                    {
                        html.Append("<p>");
                        var first = true;
                        foreach (var cell in row.Elements().Where(c => c.Name.LocalName == "TableCell"))
                        {
                            if (!first) html.Append("&emsp;");
                            first = false;
                            foreach (var p in cell.Descendants().Where(p => p.Name.LocalName == "Paragraph"))
                                Inlines(p, f.With(cell).With(p), html);
                        }

                        html.Append("</p>");
                    }

                    break;
                case "Section":
                case "BlockUIContainer":
                    Blocks(e, f, html);
                    break;
                default:
                    // inline content directly in a section: a paragraph of its own
                    if (IsInline(e))
                    {
                        html.Append("<p>");
                        Inline(e, f, html);
                        html.Append("</p>");
                    }

                    break;
            }
        }
    }

    private static bool IsInline(XElement e) =>
        e.Name.LocalName is "Run" or "Span" or "Bold" or "Italic" or "Underline" or "Hyperlink" or "LineBreak";

    private static void Inlines(XElement parent, Format format, StringBuilder html)
    {
        var any = false;
        foreach (var node in parent.Nodes())
        {
            if (node is XElement e)
            {
                Inline(e, format.With(e), html);
                any = true;
            }
            else if (node is XText t && t.Value.Trim().Length > 0)
            {
                // text directly in a paragraph is an implicit run (white space between elements is layout only)
                Text(t.Value, format, html);
                any = true;
            }
        }

        if (!any) html.Append("<br>"); // an empty paragraph keeps its line
    }

    private static void Inline(XElement e, Format format, StringBuilder html)
    {
        switch (e.Name.LocalName)
        {
            case "Run":
                Text((string?)e.Attribute("Text") ?? e.Value, format, html);
                break;
            case "LineBreak":
                html.Append("<br>");
                break;
            case "Span":
            case "Bold":
            case "Italic":
            case "Underline":
            case "Hyperlink":
                foreach (var node in e.Nodes())
                    if (node is XElement child) Inline(child, format.With(child), html);
                    else if (node is XText t) Text(t.Value, format, html);
                break;
        }
    }

    private static void Text(string text, Format format, StringBuilder html)
    {
        if (text.Length == 0) return;
        // a line break inside a run (e.g. a template text put in for a placeholder) stays a line break
        var encoded = WebUtility.HtmlEncode(text.Replace("\r\n", "\n")).Replace("\n", "<br>");
        html.Append("<span style=\"").Append(CharacterStyle(format, null)).Append("\">").Append(encoded).Append("</span>");
    }

    private static string CharacterStyle(Format f, string? extra)
    {
        var css = new StringBuilder();
        css.Append("font-family:'").Append(f.Family).Append("',sans-serif;");
        css.Append("font-size:").Append(f.Size.ToString("0.##", CultureInfo.InvariantCulture)).Append("px;");
        css.Append("font-weight:").Append(f.Bold ? "bold" : "normal").Append(';');
        css.Append("font-style:").Append(f.Italic ? "italic" : "normal").Append(';');
        css.Append("text-decoration:").Append(f.Underline ? "underline" : "none").Append(';');
        if (f.Color != null) css.Append("color:").Append(f.Color).Append(';');
        if (extra != null) css.Append(extra);
        return css.ToString();
    }

    private static string ParagraphStyle(XElement p)
    {
        var css = new StringBuilder();
        var align = (string?)p.Attribute("TextAlignment") ?? (string?)p.Parent?.Attribute("TextAlignment");
        if (align is "Center" or "Right" or "Justify") css.Append("text-align:").Append(align.ToLowerInvariant()).Append(';');
        var margin = ((string?)p.Attribute("Margin"))?.Split(',', ' ');
        if (margin != null && margin.Length > 0 &&
            double.TryParse(margin[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) && left > 0)
            css.Append("margin-left:").Append(left.ToString("0.##", CultureInfo.InvariantCulture)).Append("px;");
        return css.ToString();
    }

    /// <summary>A font family name that is safe inside a CSS string (letters, digits, blanks, - and _).</summary>
    private static string? SafeFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return null;
        var first = family.Split(',')[0].Trim().Trim('\'', '"');
        return Regex.IsMatch(first, @"^[\p{L}\p{N} _\-]{1,60}$") ? first : null;
    }

    /// <summary>#AARRGGBB or #RRGGBB as CSS (rgba); null for anything else (named brushes stay the default color).</summary>
    private static string? SafeColor(string? brush)
    {
        if (brush == null) return null;
        var m = Regex.Match(brush.Trim(), "^#([0-9A-Fa-f]{2})?([0-9A-Fa-f]{6})$");
        if (!m.Success) return null;
        var rgb = m.Groups[2].Value;
        var alpha = m.Groups[1].Success ? Convert.ToInt32(m.Groups[1].Value, 16) / 255.0 : 1;
        return string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3:0.###})",
            Convert.ToInt32(rgb[..2], 16), Convert.ToInt32(rgb[2..4], 16), Convert.ToInt32(rgb[4..], 16), alpha);
    }

    // ---- editor blocks -> XAML ---------------------------------------------------------------------------------

    /// <summary>A paragraph of the editor: its runs, alignment, indentation and list (none, "ul" or "ol").</summary>
    public sealed class Block
    {
        public List<Run> Runs { get; set; } = new();
        public string? Align { get; set; }
        public double Indent { get; set; }
        public string? List { get; set; }
    }

    /// <summary>Text with one formatting; Break is a line break inside the paragraph.</summary>
    public sealed class Run
    {
        public string Text { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public double Size { get; set; }
        public string? Family { get; set; }

        /// <summary>#RRGGBB, or null for the default color.</summary>
        public string? Color { get; set; }

        public bool Break { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The blocks the editor sent (JSON), or null when they cannot be read.</summary>
    public static List<Block>? ParseBlocks(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Block>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The XAML section of a note for the editor's blocks (as CT2 writes it: Section with Paragraphs and Lists).</summary>
    public static string FromBlocks(IReadOnlyList<Block> blocks)
    {
        var section = new XElement(Presentation + "Section",
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            new XAttribute("TextAlignment", "Left"),
            new XAttribute("FontFamily", DefaultFontFamily),
            new XAttribute("FontSize", Num(DefaultFontSize)),
            new XAttribute("Foreground", "#FF000000"));
        XElement? list = null;
        foreach (var block in blocks)
        {
            var paragraph = new XElement(Presentation + "Paragraph", new XAttribute("Margin", Num(block.List == null ? block.Indent : 0) + ",0,0,0"));
            if (block.Align is "center" or "right" or "justify")
                paragraph.Add(new XAttribute("TextAlignment", char.ToUpperInvariant(block.Align[0]) + block.Align[1..]));
            foreach (var run in block.Runs) paragraph.Add(RunElement(run));

            if (block.List is "ul" or "ol")
            {
                var marker = block.List == "ol" ? "Decimal" : "Disc";
                if (list == null || (string?)list.Attribute("MarkerStyle") != marker)
                {
                    list = new XElement(Presentation + "List", new XAttribute("MarkerStyle", marker));
                    section.Add(list);
                }

                list.Add(new XElement(Presentation + "ListItem", paragraph));
            }
            else
            {
                list = null;
                section.Add(paragraph);
            }
        }

        return section.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement RunElement(Run run)
    {
        if (run.Break) return new XElement(Presentation + "LineBreak");
        var e = new XElement(Presentation + "Run", run.Text);
        if (run.Bold) e.Add(new XAttribute("FontWeight", "Bold"));
        if (run.Italic) e.Add(new XAttribute("FontStyle", "Italic"));
        if (run.Underline) e.Add(new XAttribute("TextDecorations", "Underline"));
        if (run.Size > 0 && Math.Abs(run.Size - DefaultFontSize) > 0.01) e.Add(new XAttribute("FontSize", Num(run.Size)));
        var family = SafeFamily(run.Family);
        if (family != null && family != DefaultFontFamily) e.Add(new XAttribute("FontFamily", family));
        if (run.Color != null && Regex.IsMatch(run.Color, "^#[0-9A-Fa-f]{6}$") && !run.Color.Equals("#000000", StringComparison.OrdinalIgnoreCase))
            e.Add(new XAttribute("Foreground", "#FF" + run.Color[1..].ToUpperInvariant()));
        return e;
    }

    /// <summary>The XAML section of a plain note: one paragraph per line, default formatting.</summary>
    public static string FromText(string text) =>
        FromBlocks((text ?? "").Replace("\r\n", "\n").Split('\n')
            .Select(line => new Block { Runs = line.Length == 0 ? new List<Run>() : new List<Run> { new() { Text = line } } })
            .ToList());

    /// <summary>The plain text of the blocks: one line per paragraph (search, the minimap, older readers).</summary>
    public static string PlainText(IReadOnlyList<Block> blocks) =>
        string.Join("\n", blocks.Select(b => string.Concat(b.Runs.Select(r => r.Break ? "\n" : r.Text))));

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

using System.Text;

namespace CrypWeb.App;

/// <summary>
///     A link that opens a template with given settings (made with the link manager), for course pages, slides or the
///     CrypTool book. Format of the query string:
///     <code>?t=Cryptography/Classic/Caesar.cwm&amp;12.ShiftKey=3&amp;run=1&amp;lang=de</code>
///     <list type="bullet">
///         <item><c>t</c>: the template, relative to <c>templates/</c>.</item>
///         <item><c>&lt;component&gt;.&lt;setting&gt;</c>: a setting of a component, the component given by its id in the template (the link
///         manager writes ids, they do not depend on the language) or by its name. The text of a text input is the setting <c>Text</c>.</item>
///         <item><c>run=1</c>: starts the workspace at once; <c>lang</c>: language of the user interface and of the template texts.</item>
///     </list>
/// </summary>
public sealed class TemplateLink
{
    public string Template { get; set; } = "";
    public bool Run { get; set; }
    public string? Lang { get; set; }

    /// <summary>Setting values: (component id or name, setting name) -> value, in the order of the link.</summary>
    public List<(string Component, string Setting, string Value)> Settings { get; } = new();

    /// <summary>Reads a link (the page URL or just its query); null when it names no template.</summary>
    public static TemplateLink? Parse(string url)
    {
        var start = url.IndexOf('?');
        if (start < 0) return null;
        var query = url[(start + 1)..];
        var hash = query.IndexOf('#');
        if (hash >= 0) query = query[..hash];

        var link = new TemplateLink();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Decode(eq < 0 ? part : part[..eq]);
            var value = eq < 0 ? "" : Decode(part[(eq + 1)..]);
            switch (key)
            {
                case "t":
                    link.Template = value;
                    break;
                case "run":
                    link.Run = value is "1" or "true";
                    break;
                case "lang":
                    link.Lang = value;
                    break;
                default:
                    // the setting name follows the last dot (component names may contain dots, setting names do not)
                    var dot = key.LastIndexOf('.');
                    if (dot > 0 && dot < key.Length - 1) link.Settings.Add((key[..dot], key[(dot + 1)..], value));
                    break;
            }
        }

        return link.Template.Length == 0 ? null : link;
    }

    /// <summary>The link below <paramref name="baseUri" /> (the app's address).</summary>
    public string ToUrl(string baseUri)
    {
        var query = new StringBuilder("?t=").Append(EncodeTemplate(Template));
        foreach (var (component, setting, value) in Settings)
            query.Append('&').Append(Uri.EscapeDataString(component + "." + setting)).Append('=').Append(Uri.EscapeDataString(value));
        if (Run) query.Append("&run=1");
        if (!string.IsNullOrEmpty(Lang)) query.Append("&lang=").Append(Uri.EscapeDataString(Lang));
        return baseUri + query;
    }

    /// <summary>Slashes stay readable in the template path.</summary>
    private static string EncodeTemplate(string template)
    {
        return string.Join("/", template.Split('/').Select(Uri.EscapeDataString));
    }

    private static string Decode(string s)
    {
        return Uri.UnescapeDataString(s.Replace('+', ' '));
    }
}

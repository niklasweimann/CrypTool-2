namespace CrypWeb.App;

// The documentation files written by tools/gen/docs.py (wwwroot/docs). Property names are the JSON keys (camelCase).

/// <summary>Texts per language code ("en", "de"); <see cref="DocsText.Pick" /> falls back to English.</summary>
public static class DocsText
{
    public static string Pick(IReadOnlyDictionary<string, string>? texts, string lang)
    {
        if (texts == null) return "";
        if (texts.TryGetValue(lang, out var text) && text.Length > 0) return text;
        if (texts.TryGetValue("en", out text)) return text;
        return texts.Values.FirstOrDefault() ?? "";
    }
}

/// <summary>docs/index.json: everything the navigation and the search need.</summary>
public sealed class DocsIndex
{
    public List<DocsComponent> Components { get; set; } = new();
    public List<DocsTemplate> Templates { get; set; } = new();
    public List<DocsArticleRef> Articles { get; set; } = new();
}

public sealed class DocsComponent
{
    /// <summary>The full type name, as in plugins.json.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Icon { get; set; }
    public string Tooltip { get; set; } = "";
    public bool Experimental { get; set; }

    /// <summary>Whether docs/c/&lt;id&gt;.json exists (a documentation file or templates that use the component).</summary>
    public bool Article { get; set; }

    public Dictionary<string, DocsComponentLoc> Loc { get; set; } = new();

    public string NameFor(string lang) => Loc.TryGetValue(lang, out var l) && l.Name.Length > 0 ? l.Name : Name;
    public string TooltipFor(string lang) => Loc.TryGetValue(lang, out var l) && l.Tooltip.Length > 0 ? l.Tooltip : Tooltip;
}

public sealed class DocsComponentLoc
{
    public string Name { get; set; } = "";
    public string Tooltip { get; set; } = "";
}

public sealed class DocsTemplate
{
    /// <summary>The template path below templates/ ("Cryptography/Classic/Caesar.cwm").</summary>
    public string Id { get; set; } = "";

    public Dictionary<string, string> Title { get; set; } = new();

    /// <summary>HTML (only b, i, u).</summary>
    public Dictionary<string, string> Summary { get; set; } = new();

    public Dictionary<string, string> Description { get; set; } = new();
    public Dictionary<string, string> Keywords { get; set; } = new();

    /// <summary>The types of the components the template names as relevant.</summary>
    public List<string> Plugins { get; set; } = new();

    public bool Experimental => Id.StartsWith("Experimental/", StringComparison.Ordinal);
    public string Folder => Id.Contains('/') ? Id[..Id.LastIndexOf('/')] : "";
}

public sealed class DocsArticleRef
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Title { get; set; } = new();
}

/// <summary>docs/c/&lt;type&gt;.json and docs/a/&lt;id&gt;.json: the text of one component or general article.</summary>
public sealed class DocsArticle
{
    /// <summary>Per language: "introduction", "usage", "presentation" (components) or "description" (articles); HTML.</summary>
    public Dictionary<string, Dictionary<string, string>> Text { get; set; } = new();

    public List<DocsReference> References { get; set; } = new();

    /// <summary>Templates that use the component.</summary>
    public List<string> Templates { get; set; } = new();

    public string Section(string lang, string name)
    {
        if (Text.TryGetValue(lang, out var sections) && sections.TryGetValue(name, out var html)) return html;
        return Text.TryGetValue("en", out sections) && sections.TryGetValue(name, out html) ? html : "";
    }
}

public sealed class DocsReference
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Html { get; set; } = new();
}

/// <summary>
///     Links to documentation pages: <c>?doc=component/&lt;type&gt;</c>, <c>?doc=template/&lt;path&gt;</c>,
///     <c>?doc=article/&lt;id&gt;</c>. The generated articles (tools/gen/docs.py) use the same form.
/// </summary>
public static class DocLink
{
    public static string Href(string kind, string id) => "?doc=" + kind + "/" + Uri.EscapeDataString(id);

    /// <summary>The "kind/id" of the <c>doc</c> parameter of a page URL, or null when it has none.</summary>
    public static string? Parse(string url)
    {
        var start = url.IndexOf('?');
        if (start < 0) return null;
        var query = url[(start + 1)..];
        var hash = query.IndexOf('#');
        if (hash >= 0) query = query[..hash];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            if (part.StartsWith("doc=", StringComparison.Ordinal))
                return Uri.UnescapeDataString(part[4..].Replace('+', ' '));
        return null;
    }

    /// <summary>Splits "kind/id" ("template/Cryptography/Classic/Caesar.cwm" → ("template", "Cryptography/Classic/Caesar.cwm")).</summary>
    public static (string Kind, string Id) Split(string target)
    {
        var slash = target.IndexOf('/');
        return slash < 0 ? (target, "") : (target[..slash], target[(slash + 1)..]);
    }
}

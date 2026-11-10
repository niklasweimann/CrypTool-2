using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace CrypTool.Web.App.Visualizers;

/// <summary>
/// Marks a component as the visualization of one or more plugins, matched by the plugin's *type name* (short name or full name).
/// Visualizers live only in the web app and read nothing but the calls a plugin already makes against its presentation
/// (name → last JSON arguments), so plugin sources stay untouched and can be synced from upstream at any time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class VisualizerAttribute : Attribute
{
    public string[] Plugins { get; }
    public VisualizerAttribute(params string[] plugins) => Plugins = plugins;
}

public abstract class VizBase : ComponentBase
{
    /// <summary>Last arguments of each presentation call the plugin made, as JSON.</summary>
    [Parameter] public IReadOnlyDictionary<string, string> Pres { get; set; } = new Dictionary<string, string>();

    /// <summary>True when drawn inside the component on the canvas (fill the space), false in the dialog.</summary>
    [Parameter] public bool Inline { get; set; }

    /// <summary>Plugin type name the visualization is drawn for.</summary>
    [Parameter] public string TypeName { get; set; } = "";

    /// <summary>Id of the workspace component (input on a drawn presentation is sent back for this id).</summary>
    [Parameter] public int NodeId { get; set; }

    protected JsonElement? Get(string call)
    {
        if (!Pres.TryGetValue(call, out var s)) return null;
        try { return JsonDocument.Parse(s).RootElement.Clone(); } catch { return null; }
    }

    protected IEnumerable<JsonElement> Items(string call) => Get(call) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

    protected static string Str(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => e.GetString() ?? "",
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        _ => e.ToString(),
    };
}

/// <summary>Finds the visualizer component for a plugin type.</summary>
public static class VisualizerRegistry
{
    static Dictionary<string, Type>? map;

    static Dictionary<string, Type> Map => map ??= typeof(VisualizerRegistry).Assembly.GetTypes()
        .Where(t => typeof(VizBase).IsAssignableFrom(t) && !t.IsAbstract)
        .SelectMany(t => t.GetCustomAttributes(typeof(VisualizerAttribute), false).Cast<VisualizerAttribute>().SelectMany(a => a.Plugins).Select(p => (p, t)))
        .ToDictionary(x => x.p, x => x.t);

    /// <summary>
    /// The view for a component: its real WPF presentation when the engine renders one (<paramref name="pres"/>
    /// contains the HTML), otherwise a custom visualizer or the static drawing of the XAML.
    /// </summary>
    public static Type? Find(string pluginTypeName, IReadOnlyDictionary<string, string>? pres)
        => pres != null && pres.ContainsKey(WpfViz.HtmlKey) ? typeof(WpfViz) : Find(pluginTypeName);

    /// <summary>Custom visualizer, else the generic drawing of the plugin's original XAML when one was exported.</summary>
    public static Type? Find(string pluginTypeName)
        => FindCustom(pluginTypeName) ?? (XamlRegistry.Has(pluginTypeName) ? typeof(XamlViz) : null);

    public static Type? FindCustom(string pluginTypeName)
        => Map.TryGetValue(pluginTypeName, out var t) ? t : Map.TryGetValue(pluginTypeName.Split('.').Last(), out t) ? t : null;
}

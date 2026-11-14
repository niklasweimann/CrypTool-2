using System.Text.Json;
using CrypTool.Web.Core;
using CrypWeb.App.Engine;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>
///     Links that open a template with given settings (see <see cref="TemplateLink" />): opening such a link at
///     startup, and the link manager (Home › Extras) that makes and keeps them.
/// </summary>
public partial class Home
{
    private bool linkManagerOpen;

    private void OpenLinkManager()
    {
        linkManagerOpen = true;
    }

    private async Task SaveLinks()
    {
        await settings.SaveAsync(Js);
    }

    /// <summary>Opens the template of a link in a new tab with the link's settings; starts it when the link says so.</summary>
    private async Task OpenLink(TemplateLink link)
    {
        if (!string.IsNullOrEmpty(link.Lang) && link.Lang != Loc.Lang && Localizer.Languages.Any(l => l.Code == link.Lang))
            await SetLanguage(link.Lang);
        await OpenTemplate(link.Template, adjust: ws => ApplyLinkSettings(ws, link));
        if (!link.Run || error != null || active.Kind != DocKind.Workspace || current != link.Template) return;
        // at startup the engine is connected to the page after the first render (OnAfterRenderAsync)
        if (engineReady) await Play();
        else runWhenReady = active;
    }

    /// <summary>True once the engine reports to this page; a link with run=1 opened before that starts then.</summary>
    private bool engineReady;

    private Doc? runWhenReady;

    private async Task RunPendingLink()
    {
        if (runWhenReady == null) return;
        var doc = runWhenReady;
        runWhenReady = null;
        if (doc == active) await Play();
    }

    /// <summary>The template as the link manager shows it: read and with its texts, but not opened in a tab.</summary>
    private async Task<Workspace?> LoadTemplateForLink(string template)
    {
        try
        {
            var workspace = CwmReader.Read(await Http.GetByteArrayAsync(TemplateUrl(template)));
            await TryApplyTemplateStrings(workspace, template);
            return workspace;
        }
        catch (Exception e)
        {
            AddLog(Loc["links.title"], LogLevel.Error, template + ": " + e.Message);
            return null;
        }
    }

    /// <summary>The component a link means: its id in the template, or else its name.</summary>
    internal static PluginNode? LinkTarget(Workspace ws, string component)
    {
        return int.TryParse(component, out var id)
            ? ws.Plugins.FirstOrDefault(p => p.Id == id)
            : ws.Plugins.FirstOrDefault(p => string.Equals(p.Name, component, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyLinkSettings(Workspace ws, TemplateLink link)
    {
        var fieldTypes = new Dictionary<PluginNode, Dictionary<string, string>>();
        foreach (var (component, setting, value) in link.Settings)
        {
            var plugin = LinkTarget(ws, component);
            if (plugin == null)
            {
                AddLog(Loc["links.title"], LogLevel.Warning, Loc.T("links.unknownComponent", component));
                continue;
            }

            plugin.Settings[setting] = value;
            if (plugin.SettingTypes.ContainsKey(setting)) continue;
            // a setting the template does not store: its .NET type comes from the settings class (needed to save it)
            if (!fieldTypes.TryGetValue(plugin, out var types)) fieldTypes[plugin] = types = SettingTypesOf(plugin);
            plugin.SettingTypes[setting] = types.GetValueOrDefault(setting, "System.String");
        }
    }

    private static Dictionary<string, string> SettingTypesOf(PluginNode plugin)
    {
        try
        {
            using var doc = JsonDocument.Parse(EngineHost.Schema(plugin.TypeName, JsonSerializer.Serialize(plugin.Settings)));
            if (!doc.RootElement.TryGetProperty("fields", out var fields)) return new Dictionary<string, string>();
            return fields.EnumerateArray().ToDictionary(f => f.GetProperty("Name").GetString() ?? "", f => f.GetProperty("TypeName").GetString() ?? "System.String");
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }
}

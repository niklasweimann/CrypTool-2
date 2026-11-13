using CrypTool.Web.Core;
using CrypWeb.App.Engine;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>Per-component UI state: settings dialog, view (presentation/data/log/...), settings changes.</summary>
public partial class Home
{
    private void OpenSettings(PluginNode p)
    {
        settingsFor = p;
        AddLog("UI", LogLevel.Debug, "Opening settings of " + p.TypeName);
        StateHasChanged();
    }

    private void CloseSettings()
    {
        settingsFor = null;
    }

    private void OpenView(PluginNode p)
    {
        viewFor = p;
    }

    /// <summary>
    ///     Switches the component to a view; choosing the current one again goes back to the default.
    ///     Values follow CT2's PluginViewState: 0 Min, 1 Presentation, 2 Data, 3 Log, 4 Setting, 5 Description, 7 Default.
    /// </summary>
    private void SetView(PluginNode p, int view)
    {
        ed.Checkpoint();
        if (p.Minimized) p.Minimized = false;
        if (p.ViewState == view)
        {
            p.ViewState = 7;
            return;
        }

        var (w, h) = Size(p);
        p.Width = Math.Max(w, 260);
        p.Height = Math.Max(h, 190);
        p.ViewState = view;
    }

    private IEnumerable<LogEntry> NodeLog(PluginNode p)
    {
        return log.Where(l => l.NodeId == p.Id).TakeLast(60);
    }

    private string PortCap(PluginNode p, string name)
    {
        return Meta(p) is { } m ? m.PortCaptionFor(name, Loc.Lang) : name;
    }

    /// <summary>A setting was edited in the settings dialog: store it, and pass it on to a running component.</summary>
    private async Task OnSettingChanged((string Name, string Value, string Type) change)
    {
        if (settingsFor == null) return;
        ed.Checkpoint();
        settingsFor.Settings[change.Name] = change.Value;
        if (!string.IsNullOrEmpty(change.Type)) settingsFor.SettingTypes[change.Name] = change.Type;
        if (engine.Running) EngineHost.SetSetting(settingsFor.Id, change.Name, change.Value);
        await Task.CompletedTask;
    }
}

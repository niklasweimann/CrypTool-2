using System.Text.Json;
using System.Text.Json.Serialization;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App;

/// <summary>Global (per browser) settings, the counterpart of the "Settings" tab of CrypTool 2. Stored in localStorage.</summary>
public sealed class AppSettings
{
    private const string Key = "ct2web.settings.v1";

    /// <summary>0 = decided by the component, 1 = always show the presentation, 2 = always the icon view.</summary>
    public int DefaultView { get; set; }

    /// <summary>The design in use: the id of a ready-made one (<see cref="Designs.Presets" />) or <see cref="Designs.Custom" />.</summary>
    public string DesignId { get; set; } = Designs.DefaultId;

    /// <summary>The user's own design; made from the design in use when something is changed (see SettingsPage).</summary>
    public Design Custom { get; set; } = Designs.Default.Design.Clone();

    /// <summary>Whether the interface is light or dark as the design in use says (<see cref="Design.DarkUi" />).</summary>
    public bool UiFollowsDesign { get; set; } = true;

    /// <summary>"light", "dark" or "system" (the setting of the device); used while the interface does not follow the design.</summary>
    public string UiMode { get; set; } = "system";

    /// <summary>The mode of the interface now: "light", "dark" or "system".</summary>
    [JsonIgnore]
    public string EffectiveUiMode => UiFollowsDesign ? (Current.DarkUi ? "dark" : "light") : UiMode;

    /// <summary>The tab of the settings page; kept while the page is closed and opened again (not stored).</summary>
    [JsonIgnore]
    public string SettingsTab { get; set; } = "general";

    /// <summary>The design in use.</summary>
    [JsonIgnore]
    public Design Current => DesignId == Designs.Custom ? Custom : (Designs.Find(DesignId) ?? Designs.Default).Design;

    // settings of earlier versions: the colors and the look were stored here (see Migrate)
    public string? ComponentStyle { get; set; }
    public Dictionary<string, string>? TypeColors { get; set; }
    public Dictionary<string, string>? CategoryColors { get; set; }

    public int GuiUpdateMs { get; set; } = 80;
    public int SleepMs { get; set; } = 1;
    public bool ReplacePlaceholders { get; set; } = true;
    public bool ShowGlobalProgress { get; set; } = true;

    /// <summary>Shows the experimental components and templates (View tab); off by default, as in the CT2 release.</summary>
    public bool ShowExperimental { get; set; }

    /// <summary>View tab: whether the log pane is shown (off by default).</summary>
    public bool ShowLog { get; set; }

    /// <summary>View tab: executions and time per component below each component.</summary>
    public bool ShowTimings { get; set; }

    /// <summary>View tab: overview map of the workspace in the bottom right corner of the canvas.</summary>
    public bool ShowMinimap { get; set; } = true;

    /// <summary>The links made with the link manager (name and URL), newest first.</summary>
    public List<SavedLink> Links { get; set; } = new();

    /// <summary>View tab: snap components to the grid.</summary>
    public bool Snap { get; set; } = true;

    /// <summary>Gives the colors of the design in use to <see cref="Theme" />.</summary>
    public void ApplyTheme()
    {
        Theme.TypeOverrides.Clear();
        foreach (var kv in Current.TypeColors) Theme.TypeOverrides[kv.Key] = kv.Value;
        Theme.CategoryOverrides.Clear();
        foreach (var kv in Current.CategoryColors) Theme.CategoryOverrides[kv.Key] = kv.Value;
    }

    /// <summary>Settings of earlier versions had colors and a look of their own: they become the custom design.</summary>
    private void Migrate()
    {
        var colors = (TypeColors?.Count ?? 0) + (CategoryColors?.Count ?? 0) > 0;
        if (colors)
        {
            Custom = Designs.Default.Design.Clone();
            foreach (var kv in TypeColors ?? new()) Custom.TypeColors[kv.Key] = kv.Value;
            foreach (var kv in CategoryColors ?? new()) Custom.CategoryColors[kv.Key] = kv.Value;
            if (ComponentStyle != null && Designs.Looks.Contains(ComponentStyle)) Custom.Look = ComponentStyle;
            DesignId = Designs.Custom;
        }
        else if (ComponentStyle == "classic")
        {
            DesignId = "classic";
        }

        TypeColors = null;
        CategoryColors = null;
        ComponentStyle = null;
    }

    public static async Task<AppSettings> LoadAsync(IJSRuntime js)
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("ct2.store.get", Key);
            if (!string.IsNullOrEmpty(raw) && JsonSerializer.Deserialize<AppSettings>(raw) is { } s)
            {
                s.Migrate();
                s.ApplyTheme();
                return s;
            }
        }
        catch (Exception)
        {
            /* unreadable stored settings: use the defaults */
        }

        return new AppSettings();
    }

    public async Task SaveAsync(IJSRuntime js)
    {
        ApplyTheme();
        try
        {
            await js.InvokeVoidAsync("ct2.store.set", Key, JsonSerializer.Serialize(this));
        }
        catch (Exception)
        {
            /* storage unavailable: the settings are not remembered */
        }
    }
}

/// <summary>A link of the link manager: opens a template with the given settings.</summary>
public sealed class SavedLink
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

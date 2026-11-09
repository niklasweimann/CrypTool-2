using System.Text.Json;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App;

/// <summary>Global (per browser) settings, the counterpart of the "Settings" tab of CrypTool 2. Stored in localStorage.</summary>
public sealed class AppSettings
{
    const string Key = "ct2web.settings.v1";
    /// <summary>0 = decided by the component, 1 = always show the presentation, 2 = always the icon view.</summary>
    public int DefaultView { get; set; }
    public int GuiUpdateMs { get; set; } = 80;
    public int SleepMs { get; set; } = 1;
    public bool ReplacePlaceholders { get; set; } = true;
    public bool ShowGlobalProgress { get; set; } = true;
    public Dictionary<string, string> TypeColors { get; set; } = new();
    public Dictionary<string, string> CategoryColors { get; set; } = new();

    public void ApplyTheme()
    {
        Theme.TypeOverrides.Clear(); foreach (var kv in TypeColors) Theme.TypeOverrides[kv.Key] = kv.Value;
        Theme.CategoryOverrides.Clear(); foreach (var kv in CategoryColors) Theme.CategoryOverrides[kv.Key] = kv.Value;
    }

    public static async Task<AppSettings> LoadAsync(IJSRuntime js)
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("ct2.store.get", Key);
            if (!string.IsNullOrEmpty(raw) && JsonSerializer.Deserialize<AppSettings>(raw) is { } s) { s.ApplyTheme(); return s; }
        }
        catch { }
        return new AppSettings();
    }

    public async Task SaveAsync(IJSRuntime js) { ApplyTheme(); try { await js.InvokeVoidAsync("ct2.store.set", Key, JsonSerializer.Serialize(this)); } catch { } }
}

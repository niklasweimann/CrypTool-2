using System.Text.Json;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

// Autosave of the current canvas into the browser's localStorage (survives reloads and rebuilds).
public partial class Home
{
    const string AutosaveKey = "ct2web.canvas.v1";
    Timer? autosaveTimer;
    string? lastAutosave;

    sealed record Autosaved(string? Name, double PanX, double PanY, double Zoom, string Ws);

    async Task RestoreAutosave()
    {
        try
        {
            var raw = await Js.InvokeAsync<string?>("ct2.store.get", AutosaveKey);
            if (string.IsNullOrEmpty(raw)) return;
            var a = JsonSerializer.Deserialize<Autosaved>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (a == null) return;
            ed.Load(WorkspaceEditor.FromJson(a.Ws));
            current = a.Name; panX = a.PanX; panY = a.PanY; zoom = a.Zoom <= 0 ? 1 : a.Zoom;
            ed.MarkDirty(); // restored work counts as modified: a language switch must not reload the original template over it
            lastAutosave = raw;
            AddLog("Workspace", CrypTool.Web.Core.LogLevel.Info, "Restored the last session from this browser.");
        }
        catch (Exception e)
        {
            // a broken or outdated autosave must never keep the app from starting
            AddLog("Workspace", CrypTool.Web.Core.LogLevel.Warning, "Could not restore the last session: " + e.Message);
            try { await Js.InvokeVoidAsync("ct2.store.remove", AutosaveKey); } catch { }
        }
    }

    void StartAutosave() => autosaveTimer = new Timer(_ => _ = InvokeAsync(SaveAutosave), null, 2000, 2000);

    async Task SaveAutosave()
    {
        try
        {
            var raw = JsonSerializer.Serialize(new Autosaved(current, panX, panY, zoom, ed.ToJson()));
            if (raw == lastAutosave) return;
            await Js.InvokeVoidAsync("ct2.store.set", AutosaveKey, raw);
            lastAutosave = raw;
        }
        catch { }
    }

    void StopAutosave() => autosaveTimer?.Dispose();
}

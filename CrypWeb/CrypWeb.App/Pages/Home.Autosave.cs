using System.Text.Json;
using CrypTool.Web.Core;
using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

// Autosave of the current canvas into the browser's localStorage (survives reloads and rebuilds).
public partial class Home
{
    private const string AutosaveKey = "ct2web.docs.v1";
    public Timer? AutosaveTimer { get; private set; }
    public string? LastAutosave { get; private set; }

    private async Task RestoreAutosave()
    {
        try
        {
            var raw = await Js.InvokeAsync<string?>("ct2.store.get", AutosaveKey);
            if (string.IsNullOrEmpty(raw)) return;
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var all = JsonSerializer.Deserialize<AutosavedAll>(raw, opt);
            if (all?.Docs is { Count: > 0 })
            {
                docs.Clear();
                foreach (var a in all.Docs)
                {
                    var d = new Doc
                    {
                        Kind = Enum.TryParse<DocKind>(a.Kind, out var k) ? k : DocKind.Workspace,
                        Name = a.Name,
                        PanX = a.PanX,
                        PanY = a.PanY,
                        Zoom = a.Zoom <= 0 ? 1 : a.Zoom
                    };
                    if (d.Kind == DocKind.Workspace && a.Ws != null)
                    {
                        d.Ed.Load(WorkspaceEditor.FromJson(a.Ws));
                        d.Ed.MarkDirty();
                    }

                    docs.Add(d);
                }

                active = docs[Math.Clamp(all.Active, 0, docs.Count - 1)];
            }

            LastAutosave = raw;
            AddLog("Workspace", LogLevel.Info, "Restored the last session from this browser.");
        }
        catch (Exception e)
        {
            // a broken or outdated autosave must never keep the app from starting
            if (docs.Count == 0)
            {
                active = new Doc();
                docs.Add(active);
            }

            AddLog("Workspace", LogLevel.Warning, "Could not restore the last session: " + e.Message);
            try
            {
                await Js.InvokeVoidAsync("ct2.store.remove", AutosaveKey);
            }
            catch (Exception)
            {
                /* storage unavailable: nothing to clear */
            }
        }
    }

    private void StartAutosave()
    {
        AutosaveTimer = new Timer(_ => _ = InvokeAsync(SaveAutosave), null, 2000, 2000);
    }

    private async Task SaveAutosave()
    {
        try
        {
            var saved = docs.Select(d => new AutosavedDoc(
                d.Kind.ToString(), d.Name, d.PanX, d.PanY, d.Zoom,
                d.Kind == DocKind.Workspace ? d.Ed.ToJson() : null)).ToList();
            var raw = JsonSerializer.Serialize(new AutosavedAll(docs.IndexOf(active), saved));
            if (raw == LastAutosave) return;
            await Js.InvokeVoidAsync("ct2.store.set", AutosaveKey, raw);
            LastAutosave = raw;
        }
        catch (Exception)
        {
            /* storage unavailable or full: try again with the next change */
        }
    }

    private void StopAutosave()
    {
        AutosaveTimer?.Dispose();
    }

    private sealed record Autosaved(string? Name, double PanX, double PanY, double Zoom, string Ws);

    private sealed record AutosavedDoc(string Kind, string? Name, double PanX, double PanY, double Zoom, string? Ws);

    private sealed record AutosavedAll(int Active, List<AutosavedDoc> Docs);
}

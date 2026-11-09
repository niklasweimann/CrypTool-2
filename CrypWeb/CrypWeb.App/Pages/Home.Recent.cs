using System.Net.Http.Json;
using System.Text.Json;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

// Startcenter data: recently opened workspaces (kept in localStorage) and the YouTube video list (snapshot in wwwroot/youtube.json).
public partial class Home
{
    const string RecentKey = "ct2web.recent.v1";
    sealed record Recent(string Name, string Kind, string? Data);
    sealed record Video(string Id, string Title, string Published);
    List<Recent> recents = new();
    List<Video> videos = new();

    async Task LoadStartData()
    {
        try { videos = await Http.GetFromJsonAsync<List<Video>>("youtube.json", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); } catch { }
        try
        {
            var raw = await Js.InvokeAsync<string?>("ct2.store.get", RecentKey);
            if (!string.IsNullOrEmpty(raw)) recents = JsonSerializer.Deserialize<List<Recent>>(raw) ?? new();
        }
        catch { }
    }

    /// <summary>Remembers a workspace: templates by path, files by content (small ones only, the browser cannot reopen a file by path).</summary>
    async Task AddRecent(string name, string kind, byte[]? data = null)
    {
        recents.RemoveAll(r => r.Name == name && r.Kind == kind);
        recents.Insert(0, new Recent(name, kind, data is { Length: > 0 and < 300_000 } ? Convert.ToBase64String(data) : null));
        if (recents.Count > 15) recents.RemoveRange(15, recents.Count - 15);
        try { await Js.InvokeVoidAsync("ct2.store.set", RecentKey, JsonSerializer.Serialize(recents)); } catch { }
    }

    async Task ClearRecents() { recents.Clear(); try { await Js.InvokeVoidAsync("ct2.store.remove", RecentKey); } catch { } }

    async Task OpenRecent(Recent r)
    {
        if (r.Kind == "template") { await OpenTemplate(r.Name); return; }
        if (r.Data == null) { error = Loc["start.noRecentData"]; return; }
        try
        {
            var ws = CwmReader.Read(Convert.FromBase64String(r.Data));
            EnsureTarget(); ed.Load(ws); current = r.Name; error = null; await FitAsync();
        }
        catch (Exception ex) { error = ex.Message; }
    }
}

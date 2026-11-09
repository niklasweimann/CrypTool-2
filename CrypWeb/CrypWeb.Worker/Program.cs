using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using CrypTool.Web.Core;

// Entry point only boots the runtime; all work is driven through the [JSExport] methods below.
Console.WriteLine("CrypWeb worker runtime ready");

public static partial class WorkerApi
{
    static ExecutionEngine? engine;
    static Workspace? ws;
    static bool dirty;
    static long lastSent;

    [JSImport("post", "worker")]
    static partial void Post(string json);

    /// <summary>Starts the workspace (JSON from WorkspaceEditor.ToJson). Returns immediately; the engine runs cooperatively.</summary>
    [JSExport]
    public static void Start(string json, string culture)
    {
        Stop(); sentIo.Clear();
        try { var c = new System.Globalization.CultureInfo(string.IsNullOrEmpty(culture) ? "en" : culture); System.Globalization.CultureInfo.CurrentUICulture = c; System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = c; } catch { }
        ws = WorkspaceEditor.FromJson(json);
        var eng = engine = new ExecutionEngine();
        eng.Changed += () => Flush(false);
        eng.Log += (n, l, m) => Post(JsonSerializer.Serialize(new { type = "log", id = n.Model.Id, level = l.ToString(), message = m }));
        _ = Run(eng);
    }

    static async Task Run(ExecutionEngine eng)
    {
        try { await eng.RunAsync(ws!); }
        catch (Exception e) { Post(JsonSerializer.Serialize(new { type = "log", id = 0, level = "Error", message = e.ToString() })); }
        Flush(true);
        Post("{\"type\":\"stopped\"}");
    }

    [JSExport] public static void Stop() => engine?.Stop();

    /// <summary>Settings form description (JSON list of SettingField) for a plugin type with the given stored values.</summary>
    [JSExport]
    public static string Schema(string typeName, string valuesJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(valuesJson) ?? new();
            return JsonSerializer.Serialize(new { fields = SettingsSchema.Build(typeName, values) });
        }
        catch (Exception e) { return JsonSerializer.Serialize(new { error = e.InnerException?.Message ?? e.Message }); }
    }

    /// <summary>Applies one changed setting to a running component (for settings that may change while executing).</summary>
    [JSExport]
    public static void SetSetting(int pluginId, string name, string value)
    {
        if (engine != null && engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp != null)
            n.Comp.ApplySettings(new Dictionary<string, string> { [name] = value });
    }

    [JSExport]
    public static void SetText(int pluginId, string text)
    {
        if (engine != null && engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp is NativeComponent nc) { nc.Text = text; engine.Retrigger(pluginId); }
    }

    static readonly Dictionary<int, int> sentImage = new();
    static string? TakeImage(RunNode n)
    {
        if (n.ImageVersion == 0 || sentImage.GetValueOrDefault(n.Model.Id) == n.ImageVersion) return null;
        sentImage[n.Model.Id] = n.ImageVersion; return n.ImageUri;
    }

    static readonly Dictionary<int, string> sentIo = new();
    static string Fmt(object? v)
    {
        string t = v switch
        {
            null => "",
            string str => str,
            byte[] b => $"[{b.Length} bytes] " + Convert.ToHexString(b.Take(48).ToArray()),
            System.Collections.IEnumerable e => "[" + string.Join(", ", e.Cast<object?>().Take(24).Select(x => x?.ToString())) + "]",
            _ => v.ToString() ?? ""
        };
        return t.Length > 400 ? t[..400] + "…" : t;
    }
    static object[]? TakeIo(RunNode n)
    {
        object[] r;
        try { r = n.Last.Select(kv => (object)new { name = kv.Key.PropertyName, @out = kv.Key.Outgoing, value = Fmt(kv.Value) }).ToArray(); } catch { return null; }
        var key = JsonSerializer.Serialize(r);
        if (sentIo.GetValueOrDefault(n.Model.Id) == key) return null;
        sentIo[n.Model.Id] = key; return r;
    }

    static object[]? TakePres(RunNode n)
    {
        lock (n.Pres)
        {
            if (n.PresDirty.Count == 0) return null;
            var r = n.PresDirty.Select(k => (object)new { name = k, value = n.Pres[k] }).ToArray();
            n.PresDirty.Clear(); return r;
        }
    }

    static void Flush(bool force)
    {
        var eng = engine; if (eng == null) return;
        long now = Environment.TickCount64;
        if (!force && now - lastSent < 80) return;
        lastSent = now;
        var nodes = eng.Nodes.Values.Select(n => new { id = n.Model.Id, state = n.State.ToString(), progress = n.Progress, display = n.Display, ms = n.LastMs, err = n.Logs.LastOrDefault(l => l.Level == LogLevel.Error).Message, pres = TakePres(n), image = TakeImage(n), io = TakeIo(n) });
        Post(JsonSerializer.Serialize(new { type = "state", running = eng.Running, count = eng.ExecutionCount, wires = eng.ActiveWires.ToArray(), nodes }));
    }
}

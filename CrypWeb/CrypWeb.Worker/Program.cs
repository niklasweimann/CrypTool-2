using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using CrypTool.Web.Core;

// Entry point only boots the runtime; all work is driven through the [JSExport] methods below.
Console.WriteLine("CrypWeb worker runtime ready");

public static partial class WorkerApi
{
    static ExecutionEngine? engine;
    static Workspace? ws;
    static string? engineDoc;
    static bool dirty;
    static bool presentationErrorsHooked;
    static long lastSent; static int guiInterval = 80;

    [JSImport("post", "worker")]
    static partial void Post(string json);

    /// <summary>
    /// Opens a workspace without running it: creates the components so that their presentations are shown and
    /// can be used before the first run, as in CT2. Called again whenever the workspace changes; components of
    /// unchanged plugins are kept (ExecutionEngine.Prepare).
    /// </summary>
    [JSExport]
    public static void Open(string json, string doc, string culture)
    {
        if (engine?.Running == true) return;
        SetCulture(culture);
        ws = WorkspaceEditor.FromJson(json);
        EnsureEngine(doc).Prepare(ws);
        Flush(true);
    }

    /// <summary>Starts the workspace (JSON from WorkspaceEditor.ToJson). Returns immediately; the engine runs cooperatively.</summary>
    [JSExport]
    public static void Start(string json, string doc, string culture, int guiMs, int sleepMs)
    {
        if (engine?.Running == true) return;
        sentIo.Clear();
        SetCulture(culture);
        ws = WorkspaceEditor.FromJson(json);
        var eng = EnsureEngine(doc);
        eng.SleepMs = sleepMs; guiInterval = Math.Max(10, guiMs);
        _ = Run(eng);
    }

    static void SetCulture(string culture)
    {
        try
        {
            var c = new System.Globalization.CultureInfo(string.IsNullOrEmpty(culture) ? "en" : culture);
            System.Globalization.CultureInfo.CurrentUICulture = c;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = c;
        }
        catch { }
    }

    /// <summary>
    /// The engine of the workspace document <paramref name="doc"/>; it keeps the components (and presentations)
    /// between runs. Another document gets a fresh engine, so no component of one workspace shows up in another.
    /// </summary>
    static ExecutionEngine EnsureEngine(string doc)
    {
        if (doc != engineDoc) { engine = null; engineDoc = doc; sentIo.Clear(); sentImage.Clear(); }
        if (!presentationErrorsHooked)
        {
            presentationErrorsHooked = true;
            System.Windows.Hosting.WpfRuntime.UnhandledException += e => Post(JsonSerializer.Serialize(new { type = "log", id = 0, level = "Warning", message = "Presentation: " + e.GetType().Name + ": " + e.Message + " " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim() }));
        }
        if (engine != null) return engine;
        var eng = engine = new ExecutionEngine();
        eng.Changed += () => Flush(false);
        eng.Log += (n, l, m) => Post(JsonSerializer.Serialize(new { type = "log", id = n.Model.Id, level = l.ToString(), message = m }));
        return eng;
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

    /// <summary>Renders the presentation of a component if it changed and queues the HTML like a presentation value.</summary>
    static void RenderPresentation(RunNode n)
    {
        if (n.Comp is not PluginComponent { Host: { } host }) return;
        try
        {
            var html = host.RenderIfChanged();
            if (html != null) lock (n.Pres) { n.Pres[System.Windows.Hosting.PresentationHost.HtmlKey] = html; n.PresDirty.Add(System.Windows.Hosting.PresentationHost.HtmlKey); }
        }
        catch (Exception e) { Post(JsonSerializer.Serialize(new { type = "log", id = n.Model.Id, level = "Warning", message = "Presentation: " + e.Message })); }
    }

    /// <summary>An input event (click, text, ...) on an element of a component's presentation.</summary>
    [JSExport]
    public static void PresEvent(int pluginId, int elementId, string kind, string value, double x, double y)
    {
        if (engine == null || !engine.Nodes.TryGetValue(pluginId, out var n) || n.Comp is not PluginComponent { Host: { } host }) return;
        try { host.Dispatch(elementId, kind, value, x, y); }
        catch (Exception e) { Post(JsonSerializer.Serialize(new { type = "log", id = pluginId, level = "Warning", message = "Presentation: " + (e.InnerException?.Message ?? e.Message) })); }
        if (kind != "size") Flush(true);      // size reports come in bursts; the next tick draws
    }

    /// <summary>Called regularly by the worker script: advances animations/timers and reports redrawn presentations.</summary>
    [JSExport]
    public static void Tick()
    {
        if (engine == null) return;
        try { System.Windows.Hosting.WpfRuntime.Tick(); } catch (Exception e) { Post(JsonSerializer.Serialize(new { type = "log", id = 0, level = "Warning", message = "Animation: " + e.Message })); }
        Flush(false);
    }

    /// <summary>Settings a component changed itself (from its presentation), for the app to store in the workspace.</summary>
    static object[]? TakeSettings(RunNode n) =>
        n.Comp?.TakeChangedSettings()?.Select(kv => (object)new { name = kv.Key, value = kv.Value.Value, type = kv.Value.Type }).ToArray();

    static object[]? TakePres(RunNode n)
    {
        RenderPresentation(n);
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
        if (!force && now - lastSent < guiInterval) return;
        lastSent = now;
        var nodes = eng.Nodes.Values.Select(n => new { id = n.Model.Id, state = n.State.ToString(), progress = n.Progress, display = n.Display, ms = n.LastMs, err = n.Logs.LastOrDefault(l => l.Level == LogLevel.Error).Message, pres = TakePres(n), image = TakeImage(n), io = TakeIo(n), settings = TakeSettings(n) });
        Post(JsonSerializer.Serialize(new { type = "state", running = eng.Running, count = eng.ExecutionCount, wires = eng.ActiveWires.ToArray(), nodes }));
    }
}

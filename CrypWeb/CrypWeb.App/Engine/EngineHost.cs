using System.Text.Json;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App.Engine;

/// <summary>
/// Runs workspaces in the app itself. The runtime is multi-threaded (WasmEnableThreads): the engine and the
/// presentations live on the UI thread, plugin code (Execute) runs on background threads, as on the desktop.
///
/// State changes go to the page as JSON messages (<see cref="Message"/>, handled by Home.OnWorkerMessage) — the same
/// messages the engine worker used to post, so the page did not have to change. Everything that is raised on a
/// background thread (outputs, logs, progress) is moved to the UI thread first.
/// </summary>
public static class EngineHost
{
    static ExecutionEngine? engine;
    static Workspace? ws;
    static string? engineDoc;
    static bool presentationErrorsHooked;
    static long lastSent;
    static int guiInterval = 80;
    static SynchronizationContext? ui;

    /// <summary>A state, log or "stopped" message for the page (raised on the UI thread).</summary>
    public static event Action<string>? Message;

    /// <summary>Called once on the UI thread at startup: presentations and the engine belong to this thread.</summary>
    public static void Initialize()
    {
        ui = SynchronizationContext.Current;
        System.Windows.Threading.Dispatcher.AttachUiThread(ui!);
    }

    static void Post(string json)
    {
        if (ui == null || SynchronizationContext.Current == ui) Message?.Invoke(json);
        else ui.Post(_ => Message?.Invoke(json), null);
    }

    static void OnUiThread(Action action)
    {
        if (ui == null || SynchronizationContext.Current == ui) action();
        else ui.Post(_ => action(), null);
    }

    /// <summary>Language of plugin texts (resources), for the UI thread and the background threads.</summary>
    public static void SetCulture(string language)
    {
        try
        {
            var culture = new System.Globalization.CultureInfo(string.IsNullOrEmpty(language) ? "en" : language);
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (System.Globalization.CultureNotFoundException) { }
    }

    /// <summary>
    /// Opens a workspace without running it: creates the components so that their presentations are shown and
    /// can be used before the first run, as in CT2. Called again whenever the workspace changes; components of
    /// unchanged plugins are kept (ExecutionEngine.Prepare).
    /// </summary>
    public static void Open(string json, string doc)
    {
        if (engine?.Running == true) return;
        ws = WorkspaceEditor.FromJson(json);
        EnsureEngine(doc).Prepare(ws);
        Flush(true);
    }

    /// <summary>Starts the workspace; returns at once, the run continues asynchronously.</summary>
    public static void Start(string json, string doc, int guiMs, int sleepMs)
    {
        // a plugin that ignored Stop may still compute in the background: that run is left behind
        if (engine?.Running == true) engine = null;
        sentIo.Clear();
        sentHtml.Clear();   // the app starts with empty node views: send every presentation again
        ws = WorkspaceEditor.FromJson(json);
        var eng = EnsureEngine(doc);
        eng.SleepMs = sleepMs; guiInterval = Math.Max(10, guiMs);
        _ = Run(eng);
    }

    public static void Stop() => engine?.Stop();

    /// <summary>
    /// The engine of the workspace document <paramref name="doc"/>; it keeps the components (and presentations)
    /// between runs. Another document gets a fresh engine, so no component of one workspace shows up in another.
    /// </summary>
    static ExecutionEngine EnsureEngine(string doc)
    {
        if (doc != engineDoc) { engine = null; engineDoc = doc; sentIo.Clear(); sentImage.Clear(); sentHtml.Clear(); }
        if (!presentationErrorsHooked)
        {
            presentationErrorsHooked = true;
            System.Windows.Hosting.WpfRuntime.UnhandledException += e => Log(0, "Warning", "Presentation: " + e.GetType().Name + ": " + e.Message + " " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim());
            // a {Loc Key} in a presentation whose text cannot be found (the key is shown instead)
            CrypTool.PluginBase.Miscellaneous.LocExtension.OnGuiLogMessageOccured += (message, level) => Log(0, "Warning", "Presentation: " + message);
        }
        if (engine != null) return engine;
        var eng = engine = new ExecutionEngine { ExecuteInBackground = true };
        eng.Changed += () => OnUiThread(() => { if (ReferenceEquals(engine, eng)) Flush(false); });
        eng.Log += (n, l, m) => Log(n.Model.Id, l.ToString(), m);
        return eng;
    }

    static void Log(int id, string level, string message) =>
        Post(JsonSerializer.Serialize(new { type = "log", id, level, message }));

    static async Task Run(ExecutionEngine eng)
    {
        try { await eng.RunAsync(ws!); }
        catch (Exception e) { Log(0, "Error", e.ToString()); }
        if (!ReferenceEquals(engine, eng)) return;      // replaced by a newer run
        Flush(true);
        Post("{\"type\":\"stopped\"}");
    }

    /// <summary>Settings form description (JSON list of SettingField) for a plugin type with the given stored values.</summary>
    public static string Schema(string typeName, string valuesJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(valuesJson) ?? new();
            return JsonSerializer.Serialize(new { fields = SettingsSchema.Build(typeName, values) });
        }
        catch (Exception e) { return JsonSerializer.Serialize(new { error = e.InnerException?.Message ?? e.Message }); }
    }

    /// <summary>Applies one changed setting to a component (for settings that may change while executing).</summary>
    public static void SetSetting(int pluginId, string name, string value)
    {
        if (engine != null && engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp != null)
            n.Comp.ApplySettings(new Dictionary<string, string> { [name] = value });
    }

    public static void SetText(int pluginId, string text)
    {
        if (engine != null && engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp is NativeComponent nc) { nc.Text = text; engine.Retrigger(pluginId); }
    }

    /// <summary>An input event (click, text, size, drag, ...) on an element of a component's presentation (wpf.js).</summary>
    [JSInvokable]
    public static void PresEvent(int pluginId, int elementId, string kind, string value, double x, double y)
    {
        if (engine == null || !engine.Nodes.TryGetValue(pluginId, out var n) || n.Comp is not PluginComponent { Host: { } host }) return;
        try { host.Dispatch(elementId, kind, value, x, y); }
        catch (Exception e) { Log(pluginId, "Warning", "Presentation: " + (e.InnerException?.Message ?? e.Message)); }
        if (kind != "size") Flush(true);      // size reports come in bursts; the next tick draws
    }

    /// <summary>Called regularly by the page: advances animations/timers and reports redrawn presentations.</summary>
    [JSInvokable]
    public static void Tick()
    {
        if (engine == null) return;
        try { System.Windows.Hosting.WpfRuntime.Tick(); }
        catch (Exception e) { Log(0, "Warning", "Animation: " + e.Message); }
        Flush(false);
    }

    // ---- state messages -------------------------------------------------------------------------------------

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

    /// <summary>Last HTML sent per component, so a presentation is only sent again when it looks different.</summary>
    static readonly Dictionary<int, string> sentHtml = new();

    /// <summary>The presentation of a component rendered as HTML, or null when it has not changed since the last call.</summary>
    static string? TakeHtml(RunNode n)
    {
        if (n.Comp is not PluginComponent { Host: { } host }) return null;
        try
        {
            // RenderIfChanged skips the work while nothing in the presentation changed; the first time after a
            // (re)start everything is rendered, because the app has forgotten the earlier HTML
            bool sentBefore = sentHtml.TryGetValue(n.Model.Id, out var previous);
            string? html = sentBefore ? host.RenderIfChanged() : host.Render();
            if (html == null || html == previous) return null;
            sentHtml[n.Model.Id] = html;
            return html;
        }
        catch (Exception e)
        {
            Log(n.Model.Id, "Warning", "Presentation: " + e.Message);
            return null;
        }
    }

    /// <summary>Settings a component changed itself (from its presentation), for the app to store in the workspace.</summary>
    static object[]? TakeSettings(RunNode n) =>
        n.Comp?.TakeChangedSettings()?.Select(kv => (object)new { name = kv.Key, value = kv.Value.Value, type = kv.Value.Type }).ToArray();

    /// <summary>Sends the state of all components (on the UI thread; the gate keeps component threads out meanwhile).</summary>
    static void Flush(bool force)
    {
        var eng = engine; if (eng == null) return;
        long now = Environment.TickCount64;
        if (!force && now - lastSent < guiInterval) return;
        lastSent = now;
        string json;
        lock (eng.Gate)
        {
            var nodes = eng.Nodes.Values.Select(n => new
            {
                id = n.Model.Id,
                state = n.State.ToString(),
                progress = n.Progress,
                display = n.Display,
                ms = n.LastMs,
                err = n.Logs.LastOrDefault(l => l.Level == CrypTool.Web.Core.LogLevel.Error).Message,
                html = TakeHtml(n),
                image = TakeImage(n),
                io = TakeIo(n),
                settings = TakeSettings(n),
            }).ToList();
            json = JsonSerializer.Serialize(new { type = "state", running = eng.Running, count = eng.ExecutionCount, wires = eng.ActiveWires.ToArray(), nodes });
        }
        Post(json);
    }
}

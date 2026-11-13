using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Windows.Hosting;
using System.Windows.Threading;
using CrypTool.PluginBase.Miscellaneous;
using CrypTool.Web.Core;
using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Engine;

/// <summary>
///     Runs workspaces in the app itself. The runtime is multi-threaded (WasmEnableThreads): the engine and the
///     presentations live on the UI thread, plugin code (Execute) runs on background threads, as on the desktop.
///     State changes go to the page as JSON messages (<see cref="Message" />, handled by Home.OnWorkerMessage) — the same
///     messages the engine worker used to post, so the page did not have to change. Everything that is raised on a
///     background thread (outputs, logs, progress) is moved to the UI thread first.
/// </summary>
public static class EngineHost
{
    public static ExecutionEngine? Engine { get; private set; }
    private static Workspace? Ws { get; set; }
    private static string? EngineDoc { get; set; }
    private static bool PresentationErrorsHooked { get; set; }
    private static long LastSent { get; set; }
    private static int GuiInterval { get; set; } = 80;
    private static SynchronizationContext? Ui { get; set; }

    // ---- state messages -------------------------------------------------------------------------------------

    private static readonly Dictionary<int, int> SentImage = new();

    private static readonly Dictionary<int, string> SentIo = new();

    /// <summary>Last HTML sent per component, so a presentation is only sent again when it looks different.</summary>
    private static readonly Dictionary<int, string> SentHtml = new();

    /// <summary>A state, log or "stopped" message for the page (raised on the UI thread).</summary>
    public static event Action<string>? Message;

    /// <summary>Called once on the UI thread at startup: presentations and the engine belong to this thread.</summary>
    public static void Initialize()
    {
        Ui = SynchronizationContext.Current;
        Dispatcher.AttachUiThread(Ui!);
    }

    private static void Post(string json)
    {
        if (Ui == null || SynchronizationContext.Current == Ui) Message?.Invoke(json);
        else Ui.Post(_ => Message?.Invoke(json), null);
    }

    private static void OnUiThread(Action action)
    {
        if (Ui == null || SynchronizationContext.Current == Ui) action();
        else Ui.Post(_ => action(), null);
    }

    /// <summary>Language of plugin texts (resources), for the UI thread and the background threads.</summary>
    public static void SetCulture(string language)
    {
        try
        {
            var culture = new CultureInfo(string.IsNullOrEmpty(language) ? "en" : language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            /* unknown language code: keep the current culture */
        }
    }

    /// <summary>
    ///     Opens a workspace without running it: creates the components so that their presentations are shown and
    ///     can be used before the first run, as in CT2. Called again whenever the workspace changes; components of
    ///     unchanged plugins are kept (ExecutionEngine.Prepare).
    /// </summary>
    public static void Open(string json, string doc)
    {
        if (Engine?.Running == true) return;
        Ws = WorkspaceEditor.FromJson(json);
        EnsureEngine(doc).Prepare(Ws);
        Flush(true);
    }

    /// <summary>Starts the workspace; returns at once, the run continues asynchronously.</summary>
    public static void Start(string json, string doc, int guiMs, int sleepMs)
    {
        // a plugin that ignored Stop may still compute in the background: that run is left behind
        if (Engine?.Running == true) Engine = null;
        SentIo.Clear();
        SentHtml.Clear(); // the app starts with empty node views: send every presentation again
        Ws = WorkspaceEditor.FromJson(json);
        var eng = EnsureEngine(doc);
        eng.SleepMs = sleepMs;
        GuiInterval = Math.Max(10, guiMs);
        _ = Run(eng);
    }

    public static void Stop()
    {
        Engine?.Stop();
    }

    /// <summary>
    ///     The engine of the workspace document <paramref name="doc" />; it keeps the components (and presentations)
    ///     between runs. Another document gets a fresh engine, so no component of one workspace shows up in another.
    /// </summary>
    private static ExecutionEngine EnsureEngine(string doc)
    {
        if (doc != EngineDoc)
        {
            Engine = null;
            EngineDoc = doc;
            SentIo.Clear();
            SentImage.Clear();
            SentHtml.Clear();
        }

        if (!PresentationErrorsHooked)
        {
            PresentationErrorsHooked = true;
            WpfRuntime.UnhandledException += e => Log(0,
                "Warning",
                "Presentation: " + e.GetType().Name + ": " + e.Message + " " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim());
            // a {Loc Key} in a presentation whose text cannot be found (the key is shown instead)
            LocExtension.OnGuiLogMessageOccured += (message, _) => Log(0, "Warning", "Presentation: " + message);
        }

        if (Engine != null) return Engine;
        var eng = Engine = new ExecutionEngine { ExecuteInBackground = true };
        eng.Changed += () => OnUiThread(() =>
        {
            if (ReferenceEquals(Engine, eng)) Flush(false);
        });
        eng.Log += (n, l, m) => Log(n.Model.Id, l.ToString(), m);
        return eng;
    }

    private static void Log(int id, string level, string message)
    {
        Post(JsonSerializer.Serialize(new { type = "log", id, level, message }));
    }

    private static async Task Run(ExecutionEngine eng)
    {
        try
        {
            await eng.RunAsync(Ws!);
        }
        catch (Exception e)
        {
            Log(0, "Error", e.ToString());
        }

        if (!ReferenceEquals(Engine, eng)) return; // replaced by a newer run
        Flush(true);
        Post("{\"type\":\"stopped\"}");
    }

    /// <summary>Settings form description (JSON list of SettingField) for a plugin type with the given stored values.</summary>
    public static string Schema(string typeName, string valuesJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(valuesJson) ?? new Dictionary<string, string>();
            return JsonSerializer.Serialize(new { fields = SettingsSchema.Build(typeName, values) });
        }
        catch (Exception e)
        {
            return JsonSerializer.Serialize(new { error = e.InnerException?.Message ?? e.Message });
        }
    }

    /// <summary>Applies one changed setting to a component (for settings that may change while executing).</summary>
    public static void SetSetting(int pluginId, string name, string value)
    {
        if (Engine != null && Engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp != null)
            n.Comp.ApplySettings(new Dictionary<string, string> { [name] = value });
    }

    public static void SetText(int pluginId, string text)
    {
        if (Engine != null && Engine.Nodes.TryGetValue(pluginId, out var n) && n.Comp is NativeComponent nc)
        {
            nc.Text = text;
            Engine.Retrigger(pluginId);
        }
    }

    /// <summary>An input event (click, text, size, drag, ...) on an element of a component's presentation (wpf.js).</summary>
    [JSInvokable]
    public static void PresEvent(int pluginId, int elementId, string kind, string value, double x, double y)
    {
        if (Engine == null || !Engine.Nodes.TryGetValue(pluginId, out var n) || n.Comp is not PluginComponent { Host: { } host }) return;
        try
        {
            host.Dispatch(elementId, kind, value, x, y);
        }
        catch (Exception e)
        {
            Log(pluginId, "Warning", "Presentation: " + (e.InnerException?.Message ?? e.Message));
        }

        if (kind != "size") Flush(true); // size reports come in bursts; the next tick draws
    }

    /// <summary>Called regularly by the page: advances animations/timers and reports redrawn presentations.</summary>
    [JSInvokable]
    public static void Tick()
    {
        if (Engine == null) return;
        try
        {
            WpfRuntime.Tick();
        }
        catch (Exception e)
        {
            Log(0, "Warning", "Animation: " + e.Message);
        }

        Flush(false);
    }

    private static string? TakeImage(RunNode n)
    {
        if (n.ImageVersion == 0 || SentImage.GetValueOrDefault(n.Model.Id) == n.ImageVersion) return null;
        SentImage[n.Model.Id] = n.ImageVersion;
        return n.ImageUri;
    }

    private static string Fmt(object? v)
    {
        var t = v switch
        {
            null => "",
            string str => str,
            byte[] b => $"[{b.Length} bytes] " + Convert.ToHexString(b.Take(48).ToArray()),
            IEnumerable e => "[" + string.Join(", ", e.Cast<object?>().Take(24).Select(x => x?.ToString())) + "]",
            _ => v.ToString() ?? ""
        };
        return t.Length > 400 ? t[..400] + "…" : t;
    }

    private static object[]? TakeIo(RunNode n)
    {
        object[] r;
        try
        {
            r = n.Last.Select(kv => (object)new { name = kv.Key.PropertyName, @out = kv.Key.Outgoing, value = Fmt(kv.Value) }).ToArray();
        }
        catch
        {
            return null;
        }

        var key = JsonSerializer.Serialize(r);
        if (SentIo.GetValueOrDefault(n.Model.Id) == key) return null;
        SentIo[n.Model.Id] = key;
        return r;
    }

    /// <summary>The presentation of a component rendered as HTML, or null when it has not changed since the last call.</summary>
    private static string? TakeHtml(RunNode n)
    {
        if (n.Comp is not PluginComponent { Host: { } host }) return null;
        try
        {
            // RenderIfChanged skips the work while nothing in the presentation changed; the first time after a
            // (re)start everything is rendered, because the app has forgotten the earlier HTML
            var sentBefore = SentHtml.TryGetValue(n.Model.Id, out var previous);
            var html = sentBefore ? host.RenderIfChanged() : host.Render();
            if (html == null || html == previous) return null;
            SentHtml[n.Model.Id] = html;
            return html;
        }
        catch (Exception e)
        {
            Log(n.Model.Id, "Warning", "Presentation: " + e.Message);
            return null;
        }
    }

    /// <summary>Settings a component changed itself (from its presentation), for the app to store in the workspace.</summary>
    private static object[]? TakeSettings(RunNode n)
    {
        return n.Comp?.TakeChangedSettings()?.Select(kv => (object)new { name = kv.Key, value = kv.Value.Value, type = kv.Value.Type }).ToArray();
    }

    /// <summary>Sends the state of all components (on the UI thread; the gate keeps component threads out meanwhile).</summary>
    private static void Flush(bool force)
    {
        var eng = Engine;
        if (eng == null) return;
        var now = Environment.TickCount64;
        if (!force && now - LastSent < GuiInterval) return;
        LastSent = now;
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
                err = n.Logs.LastOrDefault(l => l.Level == LogLevel.Error).Message,
                html = TakeHtml(n),
                image = TakeImage(n),
                io = TakeIo(n),
                settings = TakeSettings(n)
            }).ToList();
            json = JsonSerializer.Serialize(new
                { type = "state", running = eng.Running, count = eng.ExecutionCount, wires = eng.ActiveWires.ToArray(), nodes });
        }

        Post(json);
    }
}

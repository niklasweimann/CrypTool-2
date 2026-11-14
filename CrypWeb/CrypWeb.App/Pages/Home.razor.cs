using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CrypTool.Web.Core;
using CrypWeb.App.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>
///     The workspace page: ribbon, palette, canvas, log. One component split into partial classes by concern (Home.&lt;Concern&gt;.cs); the shared state is
///     declared here.
/// </summary>
public partial class Home : IDisposable, IHandleEvent
{
    private const int MaxLogEntries = 500;

    // Zoom slider: position 0..100 maps logarithmically to MinZoom..MaxZoom (5 % .. 400 %, a factor of 80).
    private static readonly double ZoomRange = MaxZoom / MinZoom;

    private readonly List<Doc> docs = new();

    private readonly HashSet<LogLevel> hiddenLevels = new() { LogLevel.Debug };
    private readonly Dictionary<string, PluginMeta> meta = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> runnable = new(StringComparer.OrdinalIgnoreCase);
    private bool aboutOpen;
    private Doc active = new();
    private ConnectorPort? connecting;
    private DateTime downAt;
    private bool downHeader;
    private VisualNode? downNode;
    private PaletteEntry? dragEntry;
    private bool dragMoved, spaceDown;
    private Dictionary<int, (double x, double y)> dragStart = new();
    private double editX, editY, editW, editH;
    private VisualNode? editing;
    private string? error;
    private string filter = "", editText = "";
    private ConnectorPort? hoverPort;
    private double lx, ly, cx, cy, mx0, my0, dragWx, dragWy;
    private Mode mode;

    private string? openCat;

    // The engine worker holds the components of one workspace, also when it is not running, so that presentations
    // are shown and usable before the first run and after a stop (as in CT2). previewDoc is that workspace;
    // previewSignature describes what the worker was last given, to send it again only after a change.
    private Doc? previewDoc;
    private string? previewSignature;
    private VisualNode? resizing;

    private double rl, rt, rw = 1000, rh = 650;
    private Doc? runDoc;
    private DotNetObjectReference<Home>? self;
    private AppSettings settings = new();
    private PluginNode? settingsFor, viewFor;
    /// <summary>
    ///     The views are an HTML layer above the whole SVG, so a view would cover the window frame of a component that
    ///     lies in front of it. This cuts out the areas of such components (clip-path, relative to the view's corner).
    /// </summary>
    private string ViewClip(PluginNode p)
    {
        var (vw, vh) = Size(p);
        double left = p.X + 6, top = p.Y + 24;
        var holes = new System.Text.StringBuilder();
        foreach (var o in ed.Ws.Plugins)
        {
            if (o == p || o.Minimized || o.ZIndex <= p.ZIndex) continue;
            var (ow, oh) = Size(o);
            if (o.X >= p.X + vw || o.X + ow <= left || o.Y >= p.Y + vh || o.Y + oh <= top) continue;
            holes.Append(FormattableString.Invariant(
                $"M{o.X - left:0.#} {o.Y - top:0.#}h{ow:0.#}v{oh:0.#}h{-ow:0.#}Z"));
        }

        return holes.Length == 0
            ? ""
            : "clip-path:path(evenodd,'M-9999 -9999H9999V9999H-9999Z" + holes + "')";
    }

    /// <summary>
    ///     Set by an event handler that changed nothing visible. Blazor re-renders after every event, and each
    ///     render gives the lambda handlers new ids, so a fast sequence of events (pointer moves, a double click)
    ///     would hit handlers that are already gone ("no event handler associated with this event").
    /// </summary>
    private bool skipRender;
    private bool hideMenu, focusMode;
    private Doc? shownTab;

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        skipRender = false;
        var task = callback.InvokeAsync(arg);
        var pending = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;
        if (!skipRender) StateHasChanged();
        return pending ? RenderAfter(task) : Task.CompletedTask;
    }

    private async Task RenderAfter(Task task)
    {
        try
        {
            await task;
        }
        catch when (task.IsCanceled)
        {
            return;
        }

        StateHasChanged();
    }

    private bool snap
    {
        get => settings.Snap;
        set { settings.Snap = value; _ = settings.SaveAsync(Js); }
    }

    private bool logOpen
    {
        get => settings.ShowLog;
        set { settings.ShowLog = value; _ = settings.SaveAsync(Js); }
    }

    private int tab = 1;
    private List<string> templates = new();

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    private WorkspaceEditor ed => active.Ed;
    private RunView engine => active.Engine;
    private List<LogEntry> log => active.Log;

    private string? current
    {
        get => active.Name;
        set => active.Name = value;
    }

    private double panX
    {
        get => active.PanX;
        set => active.PanX = value;
    }

    private double panY
    {
        get => active.PanY;
        set => active.PanY = value;
    }

    private double zoom
    {
        get => active.Zoom;
        set => active.Zoom = value;
    }

    private int ZoomSlider => (int)Math.Round(100 * Math.Log(zoom / MinZoom) / Math.Log(ZoomRange));

    /// <summary>The expanded palette category; the first one (alphabetically by label) until the user picks one.</summary>
    private string OpenCat
    {
        get => openCat ?? VisibleMeta.Select(m => m.Category).Distinct().OrderBy(CatLabel).FirstOrDefault() ?? "";
        set => openCat = value;
    }

    private bool HasSelection => ed.Selection.Count > 0 || ed.SelectedWire != null;
    private PluginNode? SelectedPlugin => ed.Selection.Count == 1 ? ed.Ws.Plugins.FirstOrDefault(p => ed.Selection.Contains(p.Id)) : null;
    /// <summary>Folder of the experimental templates (copied from Experimental/Templates by tools-copy-templates.sh).</summary>
    private const string ExperimentalTemplateFolder = "Experimental/";

    /// <summary>The templates of the list: the experimental ones only when they are switched on.</summary>
    private IEnumerable<string> VisibleTemplates =>
        templates.Where(t => settings.ShowExperimental || !t.StartsWith(ExperimentalTemplateFolder, StringComparison.Ordinal));

    /// <summary>Templates that have an icon (<c>name.png</c> next to <c>name.cwm</c>), see templates/icons.json.</summary>
    private HashSet<string> templateIcons = new();

    private static string TemplateIcon(string template) => "templates/" + Uri.EscapeDataString(template[..^4]).Replace("%2F", "/") + ".png";

    /// <summary>Top level folder of a template: its category in the list below the templates.</summary>
    private static string TemplateCategory(string template) => template.Split('/')[0];

    private IEnumerable<string> TemplateCategories => VisibleTemplates.Select(TemplateCategory).Distinct().OrderBy(c => c);

    /// <summary>The expanded template category ("*" = all); the first one until the user picks one.</summary>
    private string OpenTemplateCat
    {
        get => openTemplateCat ?? TemplateCategories.FirstOrDefault() ?? "*";
        set => openTemplateCat = value;
    }

    private string? openTemplateCat;

    /// <summary>The templates of the list: those of the open category, or all that match the search text.</summary>
    private IEnumerable<string> FilteredTemplates => VisibleTemplates.Where(t =>
        filter.Length > 0
            ? t.Contains(filter, StringComparison.OrdinalIgnoreCase)
            : OpenTemplateCat == "*" || TemplateCategory(t) == OpenTemplateCat);

    /// <summary>
    /// The components of the palette: experimental ones only when they are switched on (View tab) and the browser
    /// build can run them.
    /// </summary>
    private IEnumerable<PluginMeta> VisibleMeta => meta.Values.Where(m => !m.Experimental || (settings.ShowExperimental && m.Supported));

    /// <summary>The experimental switch of the View tab: shows or hides experimental components and templates.</summary>
    private async Task ToggleExperimental(ChangeEventArgs e)
    {
        settings.ShowExperimental = e.Value is true;
        if (!settings.ShowExperimental)
        {
            openCat = null;     // the open category may have held only experimental components
        }
        await settings.SaveAsync(Js);
    }

    private IEnumerable<PluginMeta> FilteredMeta => VisibleMeta.Where(m =>
        m.NameFor(Loc.Lang).Contains(filter, StringComparison.OrdinalIgnoreCase)
        || m.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        StopAutosave();
        DetachLocalizer();
        StopRun();
        self?.Dispose();
    }

    private static string PreviewSignature(Doc d)
    {
        return d.Key + "|" + string.Join(";", d.Ed.Ws.Plugins.Select(p =>
            p.Id + ":" + p.TypeName + "{" + string.Join(",", p.Settings.OrderBy(s => s.Key).Select(s => s.Key + "=" + s.Value)) + "}"));
    }

    /// <summary>Gives the active workspace to the engine worker when it changed (not while a run is going on).</summary>
    private async Task SyncPreviewAsync()
    {
        if (active.Kind != DocKind.Workspace || runDoc != null) return;
        var signature = PreviewSignature(active);
        if (ReferenceEquals(previewDoc, active) && signature == previewSignature) return;
        previewDoc = active;
        previewSignature = signature;
        EngineHost.SetCulture(Loc.Lang);
        EngineHost.Open(ed.ToJson(), active.Key);
        await Task.CompletedTask;
    }

    private async Task SettingsChanged()
    {
        await settings.SaveAsync(Js);
    }

    private void ToggleLevel(LogLevel l)
    {
        if (!hiddenLevels.Remove(l)) hiddenLevels.Add(l);
    }

    private void SetZoomSlider(string? position)
    {
        if (!double.TryParse(position, out var t)) return;
        var targetZoom = MinZoom * Math.Pow(ZoomRange, t / 100);
        ZoomAt(targetZoom / zoom, rw / 2, rh / 2);
    }

    /// <summary>Exports the whole workspace (not just the visible part) as a PNG download.</summary>
    private async Task ExportImage()
    {
        var ws = ed.Ws;
        const double margin = 20;
        const double titleHeight = 20; // the component title is drawn below the component
        var boxes = ws.Plugins.Select(p => (left: p.X, top: p.Y, right: p.X + Size(p).w, bottom: p.Y + Size(p).h + titleHeight))
            .Concat(ws.Texts.Select(t => (left: t.X, top: t.Y, right: t.X + t.Width, bottom: t.Y + t.Height)))
            .Concat(ws.Images.Select(i => (left: i.X, top: i.Y, right: i.X + i.Width, bottom: i.Y + i.Height)))
            .ToList();
        if (boxes.Count == 0) return;

        double x0 = boxes.Min(b => b.left) - margin, y0 = boxes.Min(b => b.top) - margin;
        double x1 = boxes.Max(b => b.right) + margin, y1 = boxes.Max(b => b.bottom) + margin;
        var fileName = (current == null ? "workspace" : Path.GetFileNameWithoutExtension(current)) + ".png";
        try
        {
            // the SVG root group is translated/scaled by pan/zoom: exportPng re-bases it, so pass world bounds
            await Js.InvokeVoidAsync("ct2.exportPng", "main svg.canvas", fileName, x0, y0, x1 - x0, y1 - y0);
        }
        catch (Exception e)
        {
            AddLog("Workspace", LogLevel.Error, "Could not export image: " + e.Message);
        }
    }

    private void SetZoomPct(string? percent)
    {
        if (!double.TryParse(percent, out var p)) return;
        var targetZoom = Math.Clamp(p, MinZoom * 100, MaxZoom * 100) / 100.0;
        ZoomAt(targetZoom / zoom, rw / 2, rh / 2);
    }

    private void PickAll()
    {
        PickCat("*");
    }

    private void PickAllTemplates()
    {
        PickTemplateCat("*");
    }

    private void PickTemplateCat(string k)
    {
        openTemplateCat = k;
        filter = "";
    }

    private void PickCat(string k)
    {
        openCat = k;
        filter = "";
    }

    protected override async Task OnInitializedAsync()
    {
        docs.Add(active);
        _ = LoadStartData();
        settings = await AppSettings.LoadAsync(Js);
        // the first requests of the page are sometimes aborted while the runtime starts its threads: try again
        for (var attempt = 1; attempt <= 3 && templates.Count == 0; attempt++)
            try
            {
                templates = await Http.GetFromJsonAsync<List<string>>("templates/index.json") ?? new List<string>();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Template list (attempt " + attempt + "): " + e.Message);
                if (attempt == 3)
                    AddLog("Startup",
                        LogLevel.Error,
                        "Template list could not be loaded (templates/index.json): " + e.Message + " — generate it with CrypWeb/tools-copy-templates.sh");
                else await Task.Delay(300);
            }

        try
        {
            // optional: without the file the templates simply have no icons
            templateIcons = (await Http.GetFromJsonAsync<List<string>>("templates/icons.json") ?? new List<string>()).ToHashSet();
        }
        catch (Exception)
        {
            templateIcons = new HashSet<string>();
        }

        try
        {
            var list = await Http.GetFromJsonAsync<List<PluginMeta>>("plugins.json") ?? new List<PluginMeta>();
            foreach (var m in list) meta[m.Type] = m;
        }
        catch (Exception)
        {
            /* no plugin metadata: the palette stays empty */
        }

        var q = Regex.Match(new Uri(Nav.Uri).Query, "t=([^&]+)");
        if (q.Success) await OpenTemplate(Uri.UnescapeDataString(q.Groups[1].Value));
        else
            try
            {
                await RestoreAutosave();
            }
            catch (Exception e)
            {
                AddLog("Startup", LogLevel.Warning, "The last session could not be restored: " + e.Message);
            }
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first)
        {
            if (ctxFresh)
            {
                ctxFresh = false;
                try
                {
                    await Js.InvokeVoidAsync("ct2.clampCtx");
                }
                catch (Exception)
                {
                    /* the menu is already closed */
                }
            }

            if (active != shownTab)
            {
                // with many tabs the newly active one may be scrolled out of sight
                shownTab = active;
                try
                {
                    await Js.InvokeVoidAsync("eval",
                        "document.querySelector('.doctab.on')?.scrollIntoView({block: 'nearest', inline: 'nearest'})");
                }
                catch (JSException)
                {
                    /* page is going away */
                }
            }

            await SyncPreviewAsync();
            return;
        }

        self = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("ct2.init", self, "main svg.canvas");
        // the engine runs in this app: plugin code on background threads, presentations on this (UI) thread
        EngineHost.Initialize();
        MediaBridge.Initialize(Js);
        EngineHost.Message += OnWorkerMessage;
        await Js.InvokeVoidAsync("ct2engine.start");
        StartAutosave();
    }

    /// <summary>Appends to the log of <paramref name="doc" /> (default: the active document); the oldest entries are dropped.</summary>
    private void AddLog(string source, LogLevel level, string message, int nodeId = 0, Doc? doc = null)
    {
        var entries = (doc ?? active).Log;
        entries.Add(new LogEntry(DateTime.Now, nodeId, source, level, message));
        if (entries.Count > MaxLogEntries) entries.RemoveAt(0);
    }

    private void ClearLog()
    {
        log.Clear();
    }

    /// <summary>Called from JavaScript when the canvas moved or was resized (position and size in screen pixels).</summary>
    [JSInvokable]
    public void OnRect(double left, double top, double width, double height)
    {
        (rl, rt, rw, rh) = (left, top, width, height);
    }

    /// <summary>Space is held: a left-button drag pans the canvas.</summary>
    [JSInvokable]
    public void OnSpace(bool down)
    {
        spaceDown = down;
    }

    /// <summary>
    ///     A pointer button was released or the gesture was cancelled anywhere in the window. Blazor can lose the
    ///     pointerup of the canvas (stale event handler after a re-render); without this the gesture would never end.
    /// </summary>
    [JSInvokable]
    public void OnPointerRelease()
    {
        if (mode == Mode.None) return;
        mode = Mode.None;
        connecting = null;
        resizing = null;
        segWire = null;
        StateHasChanged();
    }

    /// <summary>Pointer position over the canvas (coalesced in canvas.js, see there).</summary>
    [JSInvokable]
    public void OnPointerMove(double clientX, double clientY, long buttons, bool altKey)
    {
        if (mode == Mode.None) return;
        Move(new Microsoft.AspNetCore.Components.Web.PointerEventArgs
            { ClientX = clientX, ClientY = clientY, Buttons = buttons, AltKey = altKey });
        StateHasChanged();
    }

    [JSInvokable]
    public void OnKey(string key, bool mod, bool shift)
    {
        if (editing != null) return;
        ctx = Ctx.None;
        var k = key.ToLowerInvariant();
        if (k == "f12") ToggleMenu();
        else if (k == "f11") ToggleFocus();
        if (mod)
        {
            switch (k)
            {
                case "z":
                    if (shift) Redo();
                    else Undo();
                    break;
                case "y":
                    if (shift) Spread(false);
                    else Redo();
                    break;
                case "w":
                    SizeAll(true, shift);
                    break;
                case "h":
                    SizeAll(false, shift);
                    break;
                case "r":
                    Rearrange();
                    break;
                case "arrowleft":
                    if (shift) MoveSel(-10, 0);
                    else Align("left");
                    break;
                case "arrowright":
                    if (shift) MoveSel(10, 0);
                    else Align("right");
                    break;
                case "arrowup":
                    if (shift) MoveSel(0, -10);
                    else Align("top");
                    break;
                case "arrowdown":
                    if (shift) MoveSel(0, 10);
                    else Align("bottom");
                    break;
                case "c":
                    ed.Copy();
                    break;
                case "x":
                    if (shift) Spread(true);
                    else CutSel();
                    break;
                case "v":
                    PasteClip();
                    break;
                case "a":
                    ed.SelectAll();
                    break;
                case "s":
                    Save();
                    break;
            }
        }
        else if (k is "delete" or "backspace")
        {
            DeleteSel();
        }
        else if (k == "escape")
        {
            ed.Selection.Clear();
            ed.SelectedWire = null;
            connecting = null;
            mode = Mode.None;
        }
        else if (k.StartsWith("arrow") && ed.Selection.Count > 0)
        {
            // arrow keys nudge the selection by 1 unit (10 with Shift)
            double step = shift ? 10 : 1;
            var dx = k == "arrowleft" ? -step : k == "arrowright" ? step : 0;
            var dy = k == "arrowup" ? -step : k == "arrowdown" ? step : 0;
            ed.Checkpoint();
            foreach (var node in ed.Movable.Where(n => ed.Selection.Contains(n.Id)))
            {
                node.X += dx;
                node.Y += dy;
            }
        }

        StateHasChanged();
    }

    private enum Mode
    {
        None,
        Pan,
        Drag,
        Marquee,
        Connect,
        Resize,
        WireSeg
    }

    private record LogEntry(DateTime Time, int NodeId, string Source, LogLevel Level, string Message);

    private enum DocKind
    {
        Workspace,
        Start,
        Settings
    }

    private sealed class Doc
    {
        public readonly WorkspaceEditor Ed = new();
        public readonly RunView Engine = new();

        /// <summary>Identifies the document in the engine worker (it keeps the components of one document).</summary>
        public readonly string Key = Guid.NewGuid().ToString("N");

        public readonly List<LogEntry> Log = new();
        public DocKind Kind;
        public string? Name;
        public double PanX = 40, PanY = 40, Zoom = 1;
    }
}

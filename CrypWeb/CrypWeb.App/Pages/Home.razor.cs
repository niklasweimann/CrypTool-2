using System.Net.Http.Json;
using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

public partial class Home : IDisposable
{
    enum Mode { None, Pan, Drag, Marquee, Connect, Resize }
    record LogEntry(DateTime Time, int NodeId, string Source, CrypTool.Web.Core.LogLevel Level, string Message);

    [Inject] HttpClient Http { get; set; } = default!;
    [Inject] IJSRuntime Js { get; set; } = default!;
    [Inject] NavigationManager Nav { get; set; } = default!;

    enum DocKind { Workspace, Start, Settings }
    sealed class Doc
    {
        public DocKind Kind; public WorkspaceEditor Ed = new(); public RunView Engine = new(); public List<LogEntry> Log = new();
        public string? Name; public double PanX = 40, PanY = 40, Zoom = 1;
    }
    readonly List<Doc> docs = new();
    Doc active = new();
    Doc? runDoc;
    WorkspaceEditor ed => active.Ed;
    RunView engine => active.Engine;
    List<LogEntry> log => active.Log;
    string? current { get => active.Name; set => active.Name = value; }
    double panX { get => active.PanX; set => active.PanX = value; }
    double panY { get => active.PanY; set => active.PanY = value; }
    double zoom { get => active.Zoom; set => active.Zoom = value; }
    List<string> templates = new();
    Dictionary<string, PluginMeta> meta = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, bool> runnable = new(StringComparer.OrdinalIgnoreCase);
    PaletteEntry? dragEntry;
    string filter = "", editText = "";
    AppSettings settings = new();
    async Task SettingsChanged() { await settings.SaveAsync(Js); }
    readonly HashSet<CrypTool.Web.Core.LogLevel> hiddenLevels = new() { CrypTool.Web.Core.LogLevel.Debug };
    void ToggleLevel(CrypTool.Web.Core.LogLevel l) { if (!hiddenLevels.Remove(l)) hiddenLevels.Add(l); }
    // zoom slider: logarithmic, 5% .. 400%
    int ZoomSlider => (int)Math.Round(100 * Math.Log(zoom / 0.05) / Math.Log(80));
    void SetZoomSlider(string? v) { if (!double.TryParse(v, out var t)) return; ZoomAt(0.05 * Math.Pow(80, t / 100) / zoom, rw / 2, rh / 2); }
    async Task ExportImage()
    {
        var ws = ed.Ws; var nodes = ws.Plugins.Select(p => (p.X, p.Y, p.X + Size(p).w, p.Y + Size(p).h + 20)).Concat(ws.Texts.Select(t => (t.X, t.Y, t.X + t.Width, t.Y + t.Height))).Concat(ws.Images.Select(t => (t.X, t.Y, t.X + t.Width, t.Y + t.Height))).ToList();
        if (nodes.Count == 0) return;
        double x0 = nodes.Min(n => n.Item1) - 20, y0 = nodes.Min(n => n.Item2) - 20, x1 = nodes.Max(n => n.Item3) + 20, y1 = nodes.Max(n => n.Item4) + 20;
        // the SVG root group is translated/scaled by pan/zoom: exportPng re-bases it, so pass world bounds and reset the scale
        try { await Js.InvokeVoidAsync("ct2.exportPng", "main svg", (current == null ? "workspace" : Path.GetFileNameWithoutExtension(current)) + ".png", x0, y0, x1 - x0, y1 - y0); }
        catch (Exception e) { AddLog("Workspace", CrypTool.Web.Core.LogLevel.Error, "Could not export image: " + e.Message); }
    }
    void SetZoomPct(string? v) { if (double.TryParse(v, out var p)) ZoomAt(Math.Clamp(p, 5, 400) / 100.0 / zoom, rw / 2, rh / 2); }
    void PickAll() => PickCat("*");
    string? openCat;
    void PickCat(string k) { openCat = k; filter = ""; }
    string OpenCat { get => openCat ?? (meta.Values.Select(m => m.Category).Distinct().OrderBy(CatLabel).FirstOrDefault() ?? ""); set => openCat = value; }
    int tab = 1;
    string? error;
    bool snap = true, logOpen = true;
    ConnectorPort? hoverPort;
    PluginNode? settingsFor, viewFor;
    VisualNode? downNode; bool downHeader; DateTime downAt;

    double rl, rt, rw = 1000, rh = 650;
    Mode mode;
    double lx, ly, cx, cy, mx0, my0, dragWx, dragWy;
    bool dragMoved, spaceDown;
    ConnectorPort? connecting;
    VisualNode? resizing;
    Dictionary<int, (double x, double y)> dragStart = new();
    VisualNode? editing; double editX, editY, editW;
    DotNetObjectReference<Home>? self;

    bool HasSelection => ed.Selection.Count > 0 || ed.SelectedWire != null;
    PluginNode? SelectedPlugin => ed.Selection.Count == 1 ? ed.Ws.Plugins.FirstOrDefault(p => ed.Selection.Contains(p.Id)) : null;
    IEnumerable<string> FilteredTemplates => templates.Where(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase));
    IEnumerable<PluginMeta> FilteredMeta => meta.Values.Where(m => m.NameFor(Loc.Lang).Contains(filter, StringComparison.OrdinalIgnoreCase) || m.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        docs.Add(active);
        settings = await AppSettings.LoadAsync(Js);
        try { templates = await Http.GetFromJsonAsync<List<string>>("templates/index.json") ?? new(); }
        catch (Exception e) { AddLog("Startup", CrypTool.Web.Core.LogLevel.Error, "Template list could not be loaded (templates/index.json): " + e.Message + " — generate it with CrypWeb/tools-copy-templates.sh"); }
        try
        {
            var list = await Http.GetFromJsonAsync<List<PluginMeta>>("plugins.json") ?? new();
            foreach (var m in list) meta[m.Type] = m;
        }
        catch { }
        var q = System.Text.RegularExpressions.Regex.Match(new Uri(Nav.Uri).Query, "t=([^&]+)");
        if (q.Success) await OpenTemplate(Uri.UnescapeDataString(q.Groups[1].Value));
        else { try { await RestoreAutosave(); } catch (Exception e) { AddLog("Startup", CrypTool.Web.Core.LogLevel.Warning, "The last session could not be restored: " + e.Message); } }
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) { if (ctxFresh) { ctxFresh = false; try { await Js.InvokeVoidAsync("ct2.clampCtx"); } catch { } } return; }
        self = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("ct2.init", self, "main svg");
        await Js.InvokeVoidAsync("ct2engine.create", self);
        await Js.InvokeVoidAsync("ct2engine.prewarm");
        StartAutosave();
    }

    void AddLog(string src, CrypTool.Web.Core.LogLevel l, string msg, int nodeId = 0, Doc? d = null) { var lg = (d ?? active).Log; lg.Add(new(DateTime.Now, nodeId, src, l, msg)); if (lg.Count > 500) lg.RemoveAt(0); }
    void ClearLog() => log.Clear();

    [JSInvokable] public void OnRect(double l, double t, double w, double h) { rl = l; rt = t; rw = w; rh = h; }
    [JSInvokable] public void OnSpace(bool down) => spaceDown = down;

    [JSInvokable]
    public void OnKey(string key, bool mod, bool shift)
    {
        if (editing != null) return;
        ctx = Ctx.None;
        var k = key.ToLowerInvariant();
        if (mod)
        {
            switch (k)
            {
                case "z": if (shift) Redo(); else Undo(); break;
                case "y": if (shift) Spread(false); else Redo(); break;
                case "w": SizeAll(true, shift); break;
                case "h": SizeAll(false, shift); break;
                case "r": Rearrange(); break;
                case "arrowleft": if (shift) MoveSel(-10, 0); else Align("left"); break;
                case "arrowright": if (shift) MoveSel(10, 0); else Align("right"); break;
                case "arrowup": if (shift) MoveSel(0, -10); else Align("top"); break;
                case "arrowdown": if (shift) MoveSel(0, 10); else Align("bottom"); break;
                case "c": ed.Copy(); break;
                case "x": if (shift) Spread(true); else CutSel(); break;
                case "v": PasteClip(); break;
                case "a": ed.SelectAll(); break;
                case "s": Save(); break;
            }
        }
        else if (k is "delete" or "backspace") DeleteSel();
        else if (k == "escape") { ed.Selection.Clear(); ed.SelectedWire = null; connecting = null; mode = Mode.None; }
        else if (k.StartsWith("arrow") && ed.Selection.Count > 0)
        {
            double s = shift ? 10 : 1, dx = k == "arrowleft" ? -s : k == "arrowright" ? s : 0, dy = k == "arrowup" ? -s : k == "arrowdown" ? s : 0;
            ed.Checkpoint();
            foreach (var n in ed.Movable.Where(n => ed.Selection.Contains(n.Id))) { n.X += dx; n.Y += dy; }
        }
        StateHasChanged();
    }

    // ---------- metadata helpers ----------
    PluginMeta? Meta(PluginNode p) => meta.TryGetValue(p.TypeName, out var m) ? m : null;
    string HeaderTitle(PluginNode p, PluginMeta? m, double w)
    {
        var s = m?.NameFor(Loc.Lang) ?? p.TypeName.Split('.').Last(); int max = Math.Max(3, (int)((w - 100) / 6.3));
        return s.Length > max ? s[..Math.Max(1, max - 1)] + "…" : s;
    }
    string CountText(string? s)
    {
        s ??= ""; int lines = s.Length == 0 ? 0 : s.Split('\n').Length;
        return $"{s.Length} {(s.Length == 1 ? "character" : "characters")}, {lines} {(lines == 1 ? "line" : "lines")}";
    }
    void RemoveNode(PluginNode p) { ed.Checkpoint(); ed.Selection.Clear(); ed.Selection.Add(p.Id); ed.DeleteSelection(); }
    string CatLabel(string c) => Loc["cat." + c];
    string Title(PluginNode p, PluginMeta? m) => !string.IsNullOrWhiteSpace(p.Name) && !p.Name.StartsWith('$') ? p.Name : (m?.NameFor(Loc.Lang) ?? p.TypeName.Split('.').Last());
    string PortTip(ConnectorPort c, PluginMeta? m)
    {
        var cap = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        var tip = m != null && m.PortTooltipFor(c.PropertyName, Loc.Lang) is { Length: > 0 } mt ? mt : c.ToolTip ?? "";
        return $"{cap} ({c.DataTypeName.Split('.').Last()}){(c.IsMandatory ? " – " + Loc["conn.required"] : "")}\n{tip}";
    }
    string ItemTip(PluginMeta m) => m.TooltipFor(Loc.Lang) + (m.Supported ? "" : "  " + Loc["tip.notRunnable"]);
    string PortLabel(ConnectorPort c, PluginMeta? m)
    {
        var t = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        return t.Length > 24 && hoverPort != c ? t[..23] + "…" : t;
    }

    IEnumerable<(ConnectorPort, string)> BubblePorts()
    {
        if (connecting != null)
        {
            yield return (connecting, "src");
            foreach (var c in ed.Ws.Connectors)
                if (c.Plugin != null && c != connecting && ed.CanConnect(connecting, c)) yield return (c, hoverPort == c ? "okhov" : "ok");
        }
        else if (hoverPort != null) yield return (hoverPort, "hover");
    }

    /// <summary>Speech bubble next to a port (outside of the component), scaled so it stays readable when zoomed out.</summary>
    MarkupString Bubble(ConnectorPort c, double x, double y, string kind, PluginMeta? m)
    {
        var cap = m != null ? m.PortCaptionFor(c.PropertyName, Loc.Lang) : c.PropertyName;
        var lines = new List<string> { cap };
        bool detail = kind is "hover" or "okhov";
        if (detail)
        {
            lines.Add($"{c.DataTypeName.Split('.').Last()} · {Loc[c.Outgoing ? "conn.output" : "conn.input"]}{(c.IsMandatory ? " · " + Loc["conn.required"] : "")}");
            var tip = m != null && m.PortTooltipFor(c.PropertyName, Loc.Lang) is { Length: > 0 } mt ? mt : c.ToolTip ?? "";
            if (kind == "hover" && tip.Length > 0) lines.AddRange(WrapText(tip, 38).Take(3));
        }
        double lh = detail ? 14 : 12, w = Math.Max(lines.Max(l => l.Length) * 5.9 + 18, 46), h = lines.Count * lh + (detail ? 8 : 6), gap = 11;
        double sc = Math.Clamp(1 / zoom, 1, detail ? 1.5 : 1.3);
        int side = SideOf(c);
        double bx, by; string arrow;
        switch (side)
        {
            case 2: bx = -gap - w; by = -h / 2; arrow = $"M {F(-gap)} -6 L {F(-gap + 7)} 0 L {F(-gap)} 6"; break;
            case 3: bx = gap; by = -h / 2; arrow = $"M {F(gap)} -6 L {F(gap - 7)} 0 L {F(gap)} 6"; break;
            case 0: bx = -w / 2; by = -gap - h; arrow = $"M -6 {F(-gap)} L 0 {F(-gap + 7)} L 6 {F(-gap)}"; break;
            default: bx = -w / 2; by = gap; arrow = $"M -6 {F(gap)} L 0 {F(gap - 7)} L 6 {F(gap)}"; break;
        }
        var cls = kind.StartsWith("ok") ? "ok" : kind == "src" ? "src" : "";
        var sb = new System.Text.StringBuilder($"<g class=\"bub {cls}\" transform=\"translate({F(x)} {F(y)}) scale({F(sc)})\">");
        sb.Append($"<rect x=\"{F(bx)}\" y=\"{F(by)}\" width=\"{F(w)}\" height=\"{F(h)}\" rx=\"6\"/><path class=\"tail\" d=\"{arrow} Z\"/>");
        for (int i = 0; i < lines.Count; i++)
            sb.Append($"<text x=\"{F(bx + 9)}\" y=\"{F(by + (detail ? 14 : 12) + i * lh)}\" class=\"{(i == 0 ? "t1" : "t2")}\">{System.Net.WebUtility.HtmlEncode(lines[i])}</text>");
        sb.Append("</g>");
        return new MarkupString(sb.ToString());
    }

    static IEnumerable<string> WrapText(string text, int max)
    {
        var line = "";
        foreach (var word in text.Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > max && line.Length > 0) { yield return line; line = word; }
            else line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
    }

    bool Runnable(PluginNode p) { if (!runnable.TryGetValue(p.TypeName, out var r)) runnable[p.TypeName] = r = IsNative(p) || (Meta(p)?.Supported ?? false); return r; }

    static string NativeKey(PluginNode p) => p.TypeName.EndsWith("NumberInput", StringComparison.OrdinalIgnoreCase) ? "Number" : p.TypeName.EndsWith("BooleanInput", StringComparison.OrdinalIgnoreCase) ? "Value" : "Text";
    string NativeText(PluginNode p) => p.Settings.GetValueOrDefault(NativeKey(p), "");
    void SetNativeText(PluginNode p, string v)
    {
        p.Settings[NativeKey(p)] = v; p.SettingTypes[NativeKey(p)] = NativeKey(p) == "Value" ? "System.Int32" : "System.String"; ed.MarkDirty();
        if (engine.Running) _ = Js.InvokeVoidAsync("ct2engine.text", p.Id, v);
    }
    void SetSetting(PluginNode p, string key, string v) { ed.Checkpoint(); p.Settings[key] = v; }

    // ---------- execution (runs in a Web Worker) ----------
    async Task Play()
    {
        if (engine.Running || active.Kind != DocKind.Workspace) return;
        if (runDoc != null && runDoc.Engine.Running) StopRun();
        CommitEdit(); logOpen = true; engine.Reset(); engine.Running = true; runDoc = active;
        AddLog(Loc["log.workspace"], CrypTool.Web.Core.LogLevel.Info, Loc["log.started"]);
        await Js.InvokeVoidAsync("ct2engine.start", ed.ToJson(), settings.GuiUpdateMs, settings.SleepMs);
    }

    void StopRun()
    {
        var d = runDoc; if (d == null || !d.Engine.Running) return;
        _ = Js.InvokeVoidAsync("ct2engine.stop");
        d.Engine.Running = false; d.Engine.ActiveWires.Clear();
        foreach (var n in d.Engine.Nodes.Values) if (n.State == RunState.Running) n.State = RunState.Idle;
        AddLog(Loc["log.workspace"], CrypTool.Web.Core.LogLevel.Info, Loc["log.stopped"], 0, d);
        runDoc = null;
        _ = Js.InvokeVoidAsync("ct2engine.prewarm");
    }

    [JSInvokable]
    public void OnWorkerMessage(string json)
    {
        if (runDoc == null) return;
        var engine = runDoc.Engine; var ed = runDoc.Ed; var rd = runDoc;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var r = doc.RootElement; var type = r.GetProperty("type").GetString();
        if (type == "state")
        {
            if (!engine.Running) return;
            engine.ExecutionCount = r.GetProperty("count").GetInt32();
            engine.ActiveWires.Clear();
            foreach (var w in r.GetProperty("wires").EnumerateArray()) engine.ActiveWires.Add(w.GetInt32());
            foreach (var n in r.GetProperty("nodes").EnumerateArray())
            {
                var id = n.GetProperty("id").GetInt32();
                if (!engine.Nodes.TryGetValue(id, out var v)) engine.Nodes[id] = v = new NodeView();
                v.State = Enum.Parse<RunState>(n.GetProperty("state").GetString()!);
                v.Progress = n.GetProperty("progress").GetDouble();
                v.Display = n.TryGetProperty("display", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String ? d.GetString() : null;
                v.Ms = n.GetProperty("ms").GetInt64();
                if (n.TryGetProperty("image", out var im) && im.ValueKind == System.Text.Json.JsonValueKind.String) v.Image = im.GetString();
                v.LastError = n.TryGetProperty("err", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String ? e.GetString() : null;
                if (n.TryGetProperty("io", out var io) && io.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var c in io.EnumerateArray()) v.Io[(c.GetProperty("name").GetString()!, c.GetProperty("out").GetBoolean())] = c.GetProperty("value").GetString() ?? "";
                if (n.TryGetProperty("pres", out var pr) && pr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var c in pr.EnumerateArray()) v.Pres[c.GetProperty("name").GetString()!] = c.GetProperty("value").GetString() ?? "";
            }
        }
        else if (type == "log")
        {
            var id = r.GetProperty("id").GetInt32(); var p = ed.Ws.Plugins.FirstOrDefault(x => x.Id == id);
            Enum.TryParse<CrypTool.Web.Core.LogLevel>(r.GetProperty("level").GetString(), out var lvl);
            AddLog(p == null ? Loc["log.engine"] : Title(p, Meta(p)), lvl, r.GetProperty("message").GetString() ?? "", id, rd);
        }
        else if (type == "stopped") { engine.Running = false; engine.ActiveWires.Clear(); AddLog(Loc["log.workspace"], CrypTool.Web.Core.LogLevel.Info, Loc["log.finished"], 0, rd); }
        StateHasChanged();
    }

    // ---------- loading / saving ----------
    bool ActiveIsBlank => active.Kind == DocKind.Workspace && current == null && !ed.Dirty && ed.Ws.Plugins.Count + ed.Ws.Texts.Count + ed.Ws.Images.Count == 0;

    /// <summary>Makes sure there is a workspace tab to load into: reuses an empty untitled one, otherwise opens a new tab.</summary>
    void EnsureTarget()
    {
        if (ActiveIsBlank) return;
        var d = new Doc(); docs.Add(d); active = d; ed.Load(new Workspace());
    }

    async Task OpenTemplate(string t, bool inPlace = false)
    {
        if (!inPlace)
        {
            var open = docs.FirstOrDefault(d => d.Kind == DocKind.Workspace && d.Name == t && !d.Ed.Dirty);
            if (open != null) { await Activate(open); return; }
            EnsureTarget();
        }
        current = t; error = null; editing = null; if (runDoc == active) StopRun();
        try
        {
            var bytes = await Http.GetByteArrayAsync("templates/" + string.Join("/", t.Split('/').Select(Uri.EscapeDataString)));
            var ws = CwmReader.Read(bytes);
            try
            {
                var xml = await Http.GetByteArrayAsync("templates/" + string.Join("/", (t[..^4] + ".xml").Split('/').Select(Uri.EscapeDataString)));
                if (settings.ReplacePlaceholders) TemplateStrings.Apply(ws, TemplateStrings.Load(xml, Loc.Lang));
            }
            catch { }
            ed.Load(ws);
            await FitAsync();
        }
        catch (Exception e) { error = e.Message; }
    }

    async Task OpenFile(InputFileChangeEventArgs e)
    {
        try
        {
            using var ms = new MemoryStream();
            await e.File.OpenReadStream(50_000_000).CopyToAsync(ms);
            var b = ms.ToArray();
            var ws = e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? WorkspaceEditor.FromJson(System.Text.Encoding.UTF8.GetString(b)) : CwmReader.Read(b);
            EnsureTarget();
            ed.Load(ws); current = e.File.Name; error = null;
            await FitAsync();
        }
        catch (Exception ex) { error = "Could not open file: " + ex.Message; }
    }

    async Task AddImageFile(InputFileChangeEventArgs e)
    {
        try
        {
            using var ms = new MemoryStream();
            await e.File.OpenReadStream(20_000_000).CopyToAsync(ms);
            var data = ms.ToArray();
            var size = await Js.InvokeAsync<double[]>("ct2.imageSize", "data:" + e.File.ContentType + ";base64," + Convert.ToBase64String(data));
            double w = Math.Min(size[0], 400), h = size[0] > 0 ? size[1] * w / size[0] : 200; var (cx0, cy0) = Center();
            ed.Checkpoint(); var im = ed.AddImage(data, cx0 - w / 2, cy0 - h / 2, w, h); ed.Selection.Clear(); ed.Selection.Add(im.Id);
        }
        catch (Exception ex) { error = "Could not add image: " + ex.Message; }
    }

    static string ImageUri(ImageNode im)
    {
        var d = im.Data; string mime = d.Length > 3 && d[0] == 0x89 ? "image/png" : d.Length > 2 && d[0] == 0xFF ? "image/jpeg" : d.Length > 2 && d[0] == 0x47 ? "image/gif" : d.Length > 1 && d[0] == 0x42 ? "image/bmp" : "image/png";
        return $"data:{mime};base64,{Convert.ToBase64String(d)}";
    }

    // ---------- documents (tabs) ----------
    async Task Activate(Doc d) { active = d; editing = null; error = null; await Task.Delay(20); try { var r = await Js.InvokeAsync<double[]>("ct2.rect", "main svg"); rl = r[0]; rt = r[1]; rw = r[2]; rh = r[3]; } catch { } }
    void NewDoc() { var d = new Doc(); docs.Add(d); active = d; }
    void ShowSingleton(DocKind k) { var d = docs.FirstOrDefault(x => x.Kind == k); if (d == null) { d = new Doc { Kind = k }; docs.Add(d); } active = d; }
    async Task CloseDoc(Doc d)
    {
        if (d.Kind == DocKind.Workspace && d.Ed.Dirty && d.Ed.Ws.Plugins.Count + d.Ed.Ws.Texts.Count > 0
            && !await Js.InvokeAsync<bool>("confirm", Loc.T("doc.confirmClose", DocTitle(d)))) return;
        if (runDoc == d) { var keep = active; active = d; StopRun(); active = keep; }
        int i = docs.IndexOf(d); docs.Remove(d);
        if (docs.Count == 0) docs.Add(new Doc());
        if (active == d) active = docs[Math.Min(i, docs.Count - 1)];
    }
    string DocTitle(Doc d) => d.Kind switch { DocKind.Start => Loc["doc.start"], DocKind.Settings => Loc["doc.settings"], _ => d.Name == null ? Loc["doc.untitled"] : Path.GetFileNameWithoutExtension(d.Name) } + (d.Kind == DocKind.Workspace && d.Ed.Dirty ? " *" : "");

    void New() => NewDoc();
    /// <summary>Saves a CrypTool 2 compatible .cwm file (opens in the desktop version).</summary>
    async void Save()
    {
        if (active.Kind != DocKind.Workspace) return;
        var name = current == null ? "workspace" : Path.GetFileNameWithoutExtension(current);
        try { await Js.InvokeVoidAsync("ct2.downloadBytes", name + ".cwm", Convert.ToBase64String(CwmWriter.Write(ed.Ws))); }
        catch (Exception e) { AddLog("Workspace", CrypTool.Web.Core.LogLevel.Error, "Could not save: " + e.Message); }
    }

    void Undo() => ed.Undo();
    void Redo() => ed.Redo();
    void DeleteSel() { if (!HasSelection) return; ed.Checkpoint(); ed.DeleteSelection(); }
    void CopySel() => ed.Copy();
    void CutSel() { ed.Copy(); DeleteSel(); }
    void PasteClip() { ed.Checkpoint(); ed.Paste(); }
    void AddNote() { ed.Checkpoint(); var (x, y) = Center(); var t = ed.AddText(x - 110, y - 40); ed.Selection.Clear(); ed.Selection.Add(t.Id); }
    void AddAtCenter(PaletteEntry e) { var (x, y) = Center(); AddAt(e, x - 60, y - 40); }
    void AddAt(PaletteEntry e, double x, double y) { ed.Checkpoint(); var p = ed.AddPlugin(e, Snap(x), Snap(y)); if (settings.DefaultView == 2) p.ViewState = 7; else if (settings.DefaultView == 1) SetView(p, 1); ed.Selection.Clear(); ed.Selection.Add(p.Id); }
    (double, double) Center() => ((rw / 2 - panX) / zoom, (rh / 2 - panY) / zoom);
    void Drop(DragEventArgs e) { if (dragEntry == null) return; var (wx, wy) = ToWorld(e.ClientX, e.ClientY); AddAt(dragEntry, wx, wy); dragEntry = null; }

    // ---------- geometry ----------
    (double x, double y) ToWorld(double clientX, double clientY) => ((clientX - rl - panX) / zoom, (clientY - rt - panY) / zoom);
    double Snap(double v) => snap ? Math.Round(v / 10) * 10 : v;
    static string F(double d) => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    static MarkupString Txt(double x, double y, string cls, string anchor, string text) =>
        new($"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"{cls}\" text-anchor=\"{anchor}\">{System.Net.WebUtility.HtmlEncode(text)}</text>");

    static int SideOf(ConnectorPort c) => c.Orientation is >= 0 and <= 3 ? c.Orientation : (c.Outgoing ? 3 : 2); // 0 N, 1 S, 2 W, 3 E
    static IEnumerable<ConnectorPort> OnSide(PluginNode p, int side) => p.Inputs.Concat(p.Outputs).Where(c => SideOf(c) == side);

    static (double w, double h) Size(PluginNode p)
    {
        if (p.Minimized) return (p.ViewState != 0 && p.Width >= 60 ? p.Width : AutoW(p), 22);
        if (p.ViewState != 0 && p.Width >= 60 && p.Height >= 40) return (p.Width, p.Height);
        return (AutoW(p), AutoH(p));
    }
    static bool IsNative(PluginNode p) => NativeComponent.TryCreate(p.TypeName) != null;
    static double AutoW(PluginNode p) => Math.Max(IsNative(p) ? 220 : 104, 30 + Math.Max(OnSide(p, 0).Count(), OnSide(p, 1).Count()) * 26);
    static double AutoH(PluginNode p) => Math.Max(IsNative(p) ? 110 : 92, 48 + Math.Max(OnSide(p, 2).Count(), OnSide(p, 3).Count()) * 26);

    static (double x, double y) PortPos(ConnectorPort c)
    {
        var p = c.Plugin!; var (w, h) = Size(p); int side = SideOf(c);
        var list = OnSide(p, side).ToList(); int n = list.Count, i = Math.Max(0, list.IndexOf(c));
        double off = (i - (n - 1) / 2.0) * 26;
        double top = p.Y + 22, bodyH = Math.Max(0, h - 40);
        return side switch
        {
            0 => (p.X + w / 2 + off, p.Y),
            1 => (p.X + w / 2 + off, p.Y + h),
            2 => (p.X, p.Minimized ? p.Y + 11 : top + bodyH / 2 + off),
            _ => (p.X + w, p.Minimized ? p.Y + 11 : top + bodyH / 2 + off),
        };
    }

    static (double dx, double dy) Away(int side) => side switch { 0 => (0, -1), 1 => (0, 1), 2 => (-1, 0), _ => (1, 0) };

    static string Tri(ConnectorPort c, double x, double y, bool big)
    {
        double r = big ? 8 : 6.5; var (ax, ay) = Away(SideOf(c));
        var (dx, dy) = c.Outgoing ? (ax, ay) : (-ax, -ay); // direction the triangle points
        double px = -dy, py = dx; // perpendicular
        string P(double a, double b) => $"{F(x + dx * a + px * b)} {F(y + dy * a + py * b)}";
        return $"M {P(-r, -r)} L {P(r, 0)} L {P(-r, r)} Z";
    }

    static string WarnPath(double x, double y) => $"M {F(x)} {F(y - 8)} L {F(x + 9)} {F(y + 7)} L {F(x - 9)} {F(y + 7)} Z";

    static string WirePath(Wire w)
    {
        var a = PortPos(w.From!); var b = PortPos(w.To!);
        var (d0x, d0y) = Away(SideOf(w.From!)); var (d1x, d1y) = Away(SideOf(w.To!));
        const double stub = 18;
        var s1 = (x: a.x + d0x * stub, y: a.y + d0y * stub); var e1 = (x: b.x + d1x * stub, y: b.y + d1y * stub);
        var pts = new List<(double x, double y)> { a, s1 };
        bool h0 = d0x != 0, h1 = d1x != 0;
        if (h0 && h1) { var mx = (s1.x + e1.x) / 2; pts.Add((mx, s1.y)); pts.Add((mx, e1.y)); }
        else if (!h0 && !h1) { var my = (s1.y + e1.y) / 2; pts.Add((s1.x, my)); pts.Add((e1.x, my)); }
        else if (h0) pts.Add((e1.x, s1.y));
        else pts.Add((s1.x, e1.y));
        pts.Add(e1); pts.Add(b);
        // drop zero-length segments
        for (int i = pts.Count - 1; i > 0; i--) if (Math.Abs(pts[i].x - pts[i - 1].x) < .01 && Math.Abs(pts[i].y - pts[i - 1].y) < .01) pts.RemoveAt(i);
        var sb = new System.Text.StringBuilder($"M {F(pts[0].x)} {F(pts[0].y)}");
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var p0 = pts[i - 1]; var p1 = pts[i]; var p2 = pts[i + 1];
            double l1 = Math.Sqrt((p1.x - p0.x) * (p1.x - p0.x) + (p1.y - p0.y) * (p1.y - p0.y)), l2 = Math.Sqrt((p2.x - p1.x) * (p2.x - p1.x) + (p2.y - p1.y) * (p2.y - p1.y));
            double r = Math.Min(8, Math.Min(l1, l2) / 2);
            double ax = p1.x - (p1.x - p0.x) / l1 * r, ay = p1.y - (p1.y - p0.y) / l1 * r, bx = p1.x + (p2.x - p1.x) / l2 * r, by = p1.y + (p2.y - p1.y) / l2 * r;
            sb.Append($" L {F(ax)} {F(ay)} Q {F(p1.x)} {F(p1.y)} {F(bx)} {F(by)}");
        }
        sb.Append($" L {F(pts[^1].x)} {F(pts[^1].y)}");
        return sb.ToString();
    }

    static IEnumerable<string> Wrap(string text, double width)
    {
        int max = Math.Max(8, (int)(width / 6.3));
        foreach (var para in (text ?? "").Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
            {
                if (line.Length + word.Length + 1 > max && line.Length > 0) { yield return line; line = word; }
                else line = line.Length == 0 ? word : line + " " + word;
            }
            yield return line;
        }
    }

    // ---------- view ----------
    async Task FitAsync()
    {
        try { var d = await Js.InvokeAsync<double[]>("ct2.rect", "main svg"); rl = d[0]; rt = d[1]; rw = d[2]; rh = d[3]; } catch { }
        var ws = ed.Ws;
        if (ws.Plugins.Count + ws.Texts.Count == 0) { panX = panY = 40; zoom = 1; return; }
        var xs = ws.Plugins.Select(p => p.X).Concat(ws.Texts.Select(t => t.X)).Min();
        var ys = ws.Plugins.Select(p => p.Y).Concat(ws.Texts.Select(t => t.Y)).Min();
        var xe = ws.Plugins.Select(p => p.X + Size(p).w).Concat(ws.Texts.Select(t => t.X + t.Width)).Max();
        var ye = ws.Plugins.Select(p => p.Y + Size(p).h).Concat(ws.Texts.Select(t => t.Y + t.Height)).Max();
        double w = xe - xs, h = ye - ys;
        zoom = Math.Clamp(Math.Min(1.2, Math.Min((rw - 80) / w, (rh - 60) / h)), 0.05, 4);
        panX = (rw - w * zoom) / 2 - xs * zoom; panY = (rh - h * zoom) / 2 - ys * zoom;
    }

    void ZoomAt(double f, double px, double py) { var nz = Math.Clamp(zoom * f, 0.05, 4); panX = px - (px - panX) * nz / zoom; panY = py - (py - panY) * nz / zoom; zoom = nz; }
    void ZoomBy(double f) => ZoomAt(f, rw / 2, rh / 2);
    void Zoom100() => ZoomAt(1 / zoom, rw / 2, rh / 2);
    void Wheel(WheelEventArgs e) => ZoomAt(e.DeltaY < 0 ? 1.1 : 1 / 1.1, e.ClientX - rl, e.ClientY - rt);

    // ---------- pointer interaction ----------
    void BgDown(PointerEventArgs e)
    {
        downNode = null; panDist = 0; ctx = Ctx.None;
        CommitEdit();
        lx = e.ClientX; ly = e.ClientY;
        if (e.Button is 1 or 2 || (e.Button == 0 && spaceDown)) { mode = Mode.Pan; return; }
        if (e.Button != 0) return;
        if (!e.ShiftKey && !e.CtrlKey) { ed.Selection.Clear(); ed.SelectedWire = null; }
        (mx0, my0) = ToWorld(e.ClientX, e.ClientY); cx = mx0; cy = my0; mode = Mode.Marquee;
    }

    void NodeDown(PointerEventArgs e, VisualNode n, bool header = false)
    {
        downNode = n; downHeader = header; downAt = DateTime.UtcNow; panDist = 0; ctx = Ctx.None;
        CommitEdit();
        if (e.Button is 1 or 2) { lx = e.ClientX; ly = e.ClientY; mode = Mode.Pan; return; }
        if (e.Button != 0) return;
        ed.SelectedWire = null;
        if (e.ShiftKey || e.CtrlKey) { if (!ed.Selection.Remove(n.Id)) ed.Selection.Add(n.Id); }
        else if (!ed.Selection.Contains(n.Id)) { ed.Selection.Clear(); ed.Selection.Add(n.Id); }
        ed.BringToFront(n);
        dragStart = ed.Movable.Where(m => ed.Selection.Contains(m.Id)).ToDictionary(m => m.Id, m => (m.X, m.Y));
        (dragWx, dragWy) = ToWorld(e.ClientX, e.ClientY); dragMoved = false; mode = Mode.Drag;
    }

    void ResizeDown(PointerEventArgs e, VisualNode n)
    {
        ed.Checkpoint(); resizing = n; lx = e.ClientX; ly = e.ClientY; mode = Mode.Resize;
        if (n is PluginNode p) { var (w, h) = Size(p); p.Width = w; p.Height = h; p.ViewState = Math.Max(1, p.ViewState); }
    }

    void WireDown(PointerEventArgs e, Wire w) { ed.Selection.Clear(); ed.SelectedWire = w.Id; }

    void PortDown(PointerEventArgs e, ConnectorPort c)
    {
        if (e.Button != 0) return;
        if (!c.Outgoing && c.Wires.Count == 1) { var w = c.Wires[0]; var from = w.From!; ed.Checkpoint(); ed.RemoveWire(w); connecting = from; }
        else connecting = c;
        (cx, cy) = ToWorld(e.ClientX, e.ClientY); mode = Mode.Connect;
    }

    void PortUp(ConnectorPort c)
    {
        if (mode != Mode.Connect || connecting == null) return;
        if (ed.CanConnect(connecting, c)) { ed.Checkpoint(); ed.Connect(connecting, c); }
        connecting = null; mode = Mode.None;
    }

    void Move(PointerEventArgs e)
    {
        if (mode != Mode.None && e.Buttons == 0) { Up(e); return; }
        switch (mode)
        {
            case Mode.Pan: panDist += Math.Abs(e.ClientX - lx) + Math.Abs(e.ClientY - ly); panX += e.ClientX - lx; panY += e.ClientY - ly; lx = e.ClientX; ly = e.ClientY; break;
            case Mode.Drag:
                {
                    var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
                    double dx = wx - dragWx, dy = wy - dragWy;
                    if (!dragMoved && Math.Abs(dx) + Math.Abs(dy) < 3 / zoom) break;
                    if (!dragMoved) { dragMoved = true; ed.Checkpoint(); }
                    foreach (var n in ed.Movable.Where(m => dragStart.ContainsKey(m.Id)))
                    {
                        var (sx, sy) = dragStart[n.Id];
                        n.X = e.AltKey ? sx + dx : Snap(sx + dx); n.Y = e.AltKey ? sy + dy : Snap(sy + dy);
                    }
                    break;
                }
            case Mode.Resize:
                {
                    double dx = (e.ClientX - lx) / zoom, dy = (e.ClientY - ly) / zoom; lx = e.ClientX; ly = e.ClientY;
                    resizing!.Width = Math.Max(resizing is PluginNode ? 70 : 60, resizing.Width + dx);
                    resizing.Height = Math.Max(resizing is PluginNode ? 60 : 30, resizing.Height + dy);
                    break;
                }
            case Mode.Marquee:
                {
                    (cx, cy) = ToWorld(e.ClientX, e.ClientY);
                    double x0 = Math.Min(mx0, cx), x1 = Math.Max(mx0, cx), y0 = Math.Min(my0, cy), y1 = Math.Max(my0, cy);
                    foreach (var p in ed.Ws.Plugins) { var (w, h) = Size(p); if (p.X < x1 && p.X + w > x0 && p.Y < y1 && p.Y + h > y0) ed.Selection.Add(p.Id); }
                    foreach (var t in ed.Ws.Texts.Cast<VisualNode>().Concat(ed.Ws.Images)) if (t.X < x1 && t.X + t.Width > x0 && t.Y < y1 && t.Y + t.Height > y0) ed.Selection.Add(t.Id);
                    break;
                }
            case Mode.Connect: (cx, cy) = ToWorld(e.ClientX, e.ClientY); break;
        }
    }

    void Up(PointerEventArgs e)
    {
        mode = Mode.None; connecting = null; resizing = null;
    }

    /// <summary>A plain click (no drag) on the body of a component opens its settings dialog.</summary>
    void SvgClick(MouseEventArgs e)
    {
        var n = downNode; downNode = null;
        if (n is PluginNode pn && !downHeader && !dragMoved && !e.ShiftKey && !e.CtrlKey && !e.MetaKey && e.Button == 0 && (DateTime.UtcNow - downAt).TotalMilliseconds < 1500)
            OpenSettings(pn);
    }

    void OpenSettings(PluginNode p) { settingsFor = p; AddLog("UI", CrypTool.Web.Core.LogLevel.Debug, "Opening settings of " + p.TypeName); StateHasChanged(); }

    void CloseSettings() => settingsFor = null;
    static System.Text.Json.JsonElement ParseJson(string s) { try { return System.Text.Json.JsonDocument.Parse(s).RootElement.Clone(); } catch { return System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(s)).RootElement.Clone(); } }
    void OpenView(PluginNode p) { viewFor = p; }
    // PluginViewState (CT2): 0 Min, 1 Presentation, 2 Data, 3 Log, 4 Setting, 5 Description, 7 Default
    void SetView(PluginNode p, int v)
    {
        ed.Checkpoint();
        if (p.Minimized) p.Minimized = false;
        if (p.ViewState == v) { p.ViewState = 7; return; }
        var (w, h) = Size(p); p.Width = Math.Max(w, 260); p.Height = Math.Max(h, 190); p.ViewState = v;
    }
    IEnumerable<LogEntry> NodeLog(PluginNode p) => log.Where(l => l.NodeId == p.Id).TakeLast(60);
    string PortCap(PluginNode p, string name) => Meta(p) is { } m ? m.PortCaptionFor(name, Loc.Lang) : name;
    async Task OnSettingChanged((string Name, string Value, string Type) c)
    {
        if (settingsFor == null) return;
        ed.Checkpoint(); settingsFor.Settings[c.Name] = c.Value; if (!string.IsNullOrEmpty(c.Type)) settingsFor.SettingTypes[c.Name] = c.Type;
        if (engine.Running) await Js.InvokeVoidAsync("ct2engine.setting", settingsFor.Id, c.Name, c.Value);
    }

    // ---------- inline editing ----------
    void EditName(PluginNode p) { editing = p; editText = Title(p, Meta(p)); editX = p.X * zoom + panX; editY = (p.Y + Size(p).h + 2) * zoom + panY; editW = Math.Max(120, (Size(p).w - 24) * zoom); FocusEdit(); }
    void EditText(TextNode t) { editing = t; editText = t.Text; editX = t.X * zoom + panX; editY = t.Y * zoom + panY; editW = Math.Max(160, t.Width * zoom); FocusEdit(); }
    async void FocusEdit() { await Task.Delay(30); try { await Js.InvokeVoidAsync("eval", "(()=>{const i=document.getElementById('inline-edit'); if(i){i.focus(); i.select();}})()"); } catch { } }
    void EditKey(KeyboardEventArgs e) { if (e.Key == "Enter") CommitEdit(); else if (e.Key == "Escape") editing = null; }
    void CommitEdit()
    {
        if (editing == null) return;
        ed.Checkpoint();
        if (editing is PluginNode p) p.Name = editText; else if (editing is TextNode t) t.Text = editText;
        editing = null;
    }
    void ToggleMin(PluginNode p) { ed.Checkpoint(); p.Minimized = !p.Minimized; }

    public void Dispose() { StopAutosave(); DetachLocalizer(); StopRun(); self?.Dispose(); }
}

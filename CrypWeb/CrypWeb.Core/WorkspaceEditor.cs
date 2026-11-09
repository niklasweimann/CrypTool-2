using System.Text.Json;

namespace CrypTool.Web.Core;

/// <summary>Editing operations of the canvas (selection, connect, delete, copy/paste, undo/redo, persistence).</summary>
public sealed class WorkspaceEditor
{
    public Workspace Ws { get; private set; } = new();
    public HashSet<int> Selection { get; } = new();
    public int? SelectedWire { get; set; }
    readonly Stack<Workspace> undo = new(), redo = new();
    Workspace? clipboard;
    int pasteCount;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool Dirty { get; private set; }
    public void MarkDirty() => Dirty = true;

    public void Load(Workspace ws) { Ws = ws; Selection.Clear(); SelectedWire = null; undo.Clear(); redo.Clear(); Dirty = false; }

    /// <summary>Call BEFORE a mutating operation.</summary>
    public void Checkpoint() { undo.Push(Ws.Clone()); redo.Clear(); if (undo.Count > 200) { var l = undo.Reverse().Skip(1).ToArray(); undo.Clear(); foreach (var x in l) undo.Push(x); } Dirty = true; }
    public void Undo() { if (undo.Count == 0) return; redo.Push(Ws.Clone()); Ws = undo.Pop(); Prune(); }
    public void Redo() { if (redo.Count == 0) return; undo.Push(Ws.Clone()); Ws = redo.Pop(); Prune(); }
    void Prune() { var ids = Ws.Plugins.Select(p => p.Id).Concat(Ws.Texts.Select(t => t.Id)).Concat(Ws.Images.Select(t => t.Id)).ToHashSet(); Selection.IntersectWith(ids); SelectedWire = null; }

    public IEnumerable<VisualNode> Movable => Ws.Plugins.Cast<VisualNode>().Concat(Ws.Texts).Concat(Ws.Images);
    public VisualNode? Find(int id) => Movable.FirstOrDefault(n => n.Id == id);

    public void BringToFront(VisualNode n) { n.ZIndex = Movable.Max(m => m.ZIndex) + 1; }

    public void SendToBack(VisualNode n) { n.ZIndex = Movable.Min(m => m.ZIndex) - 1; }

    /// <summary>Moves a node one step up (+1) or down (-1) in the z-order.</summary>
    public void Elevate(VisualNode n, int dir)
    {
        var l = Movable.OrderBy(m => m.ZIndex).ToList();
        for (int i = 0; i < l.Count; i++) l[i].ZIndex = i + 1;
        int idx = l.IndexOf(n), j = idx + dir; if (idx < 0 || j < 0 || j >= l.Count) return;
        (l[idx].ZIndex, l[j].ZIndex) = (l[j].ZIndex, l[idx].ZIndex);
    }

    public PluginNode AddPlugin(PaletteEntry e, double x, double y)
    {
        var p = new PluginNode { Id = Ws.NextId(), X = x, Y = y, Width = e.Width, Height = e.Height, Name = e.Name, TypeName = e.TypeName, AssemblyName = e.AssemblyName };
        void add(List<PalettePort> src, bool outgoing)
        {
            int i = 0;
            foreach (var s in src)
            {
                var c = new ConnectorPort { Id = Ws.NextId(), Plugin = p, Outgoing = outgoing, Orientation = outgoing ? 3 : 2, Index = i++, PropertyName = s.PropertyName, Name = s.PropertyName,
                    DataTypeName = s.DataTypeName, ToolTip = s.ToolTip, IsMandatory = s.IsMandatory };
                (outgoing ? p.Outputs : p.Inputs).Add(c); Ws.Connectors.Add(c);
            }
        }
        Ws.Plugins.Add(p);
        add(e.Inputs, false); add(e.Outputs, true);
        BringToFront(p);
        return p;
    }

    public TextNode AddText(double x, double y)
    {
        var t = new TextNode { Id = Ws.NextId(), X = x, Y = y, Width = 220, Height = 80, Text = "Note" };
        Ws.Texts.Add(t); BringToFront(t); return t;
    }

    public ImageNode AddImage(byte[] data, double x, double y, double w, double h)
    {
        var im = new ImageNode { Id = Ws.NextId(), X = x, Y = y, Width = w, Height = h, Data = data };
        Ws.Images.Add(im); BringToFront(im); return im;
    }

    public bool CanConnect(ConnectorPort a, ConnectorPort b)
    {
        if (a == b || a.Outgoing == b.Outgoing || a.Plugin == b.Plugin) return false;
        var from = a.Outgoing ? a : b; var to = a.Outgoing ? b : a;
        return Compatible(from.DataTypeName, to.DataTypeName);
    }

    static readonly Dictionary<string, Type?> typeCache = new();
    static Type? Resolve(string name)
    {
        if (string.IsNullOrEmpty(name)) return typeof(object);
        if (typeCache.TryGetValue(name, out var t)) return t;
        t = Type.GetType(name) ?? Type.GetType(name + ", CrypPluginBase") ?? Type.GetType(name + ", System.Runtime.Numerics");
        return typeCache[name] = t;
    }

    /// <summary>Same rules as the original WorkspaceModel.CheckConnection: green (assignable) or yellow (convertible).</summary>
    static bool Compatible(string from, string to)
    {
        if (from == to || from == "System.Object" || to == "System.Object" || from == "" || to == "") return true;
        var a = Resolve(from); var b = Resolve(to);
        if (a != null && b != null && (a == b || a.IsSubclassOf(b) || a.GetInterfaces().Contains(b))) return true;
        bool intLike(string t) => t is "System.Int32" or "System.Int64";
        const string big = "System.Numerics.BigInteger", str = "System.String", bytes = "System.Byte[]", stream = "CrypTool.PluginBase.IO.ICrypToolStream";
        return (intLike(from) && to == big) || (intLike(to) && from == big)
            || (to == str && from == bytes) || (to == bytes && from == str)
            || (to == str && (intLike(from) || from == big || from == "System.Boolean" || from == stream))
            || (to == stream && from == str);
    }

    static bool StreamLike(string t) => t.EndsWith("ICrypToolStream") || t == "System.Byte[]" || t == "System.String";

    public Wire? Connect(ConnectorPort a, ConnectorPort b)
    {
        if (!CanConnect(a, b)) return null;
        var from = a.Outgoing ? a : b; var to = a.Outgoing ? b : a;
        if (from.Wires.Any(w => w.To == to)) return null;
        foreach (var old in to.Wires.ToList()) RemoveWire(old); // an input takes one connection
        var w = new Wire { Id = Ws.NextId(), From = from, To = to };
        from.Wires.Add(w); to.Wires.Add(w); Ws.Wires.Add(w);
        return w;
    }

    public void RemoveWire(Wire w) { w.From?.Wires.Remove(w); w.To?.Wires.Remove(w); Ws.Wires.Remove(w); if (SelectedWire == w.Id) SelectedWire = null; }

    public void DeleteSelection()
    {
        if (SelectedWire is int wid && Ws.Wires.FirstOrDefault(w => w.Id == wid) is { } w) { RemoveWire(w); return; }
        foreach (var p in Ws.Plugins.Where(p => Selection.Contains(p.Id)).ToList())
        {
            foreach (var c in p.Inputs.Concat(p.Outputs)) { foreach (var wi in c.Wires.ToList()) RemoveWire(wi); Ws.Connectors.Remove(c); }
            Ws.Plugins.Remove(p);
        }
        Ws.Texts.RemoveAll(t => Selection.Contains(t.Id));
        Ws.Images.RemoveAll(t => Selection.Contains(t.Id));
        Selection.Clear();
    }

    public void Copy() => clipboard = Ws.Clone(n => Selection.Contains(n.Id));
    public bool HasClipboard => clipboard != null && (clipboard.Plugins.Count + clipboard.Texts.Count + clipboard.Images.Count) > 0;

    public void Paste()
    {
        if (!HasClipboard) return;
        pasteCount++;
        int baseId = Ws.NextId();
        var c = clipboard!.Clone();
        var map = new Dictionary<int, int>();
        int Next(int old) => map.TryGetValue(old, out var v) ? v : map[old] = baseId++;
        foreach (var p in c.Plugins) { p.Id = Next(p.Id); p.X += 30 * pasteCount; p.Y += 30 * pasteCount; }
        foreach (var t in c.Texts) { t.Id = Next(t.Id); t.X += 30 * pasteCount; t.Y += 30 * pasteCount; }
        foreach (var t in c.Images) { t.Id = Next(t.Id); t.X += 30 * pasteCount; t.Y += 30 * pasteCount; }
        foreach (var k in c.Connectors) k.Id = Next(k.Id);
        foreach (var w in c.Wires) w.Id = Next(w.Id);
        Ws.Plugins.AddRange(c.Plugins); Ws.Connectors.AddRange(c.Connectors); Ws.Wires.AddRange(c.Wires); Ws.Texts.AddRange(c.Texts); Ws.Images.AddRange(c.Images);
        Selection.Clear(); foreach (var n in c.Plugins.Cast<VisualNode>().Concat(c.Texts).Concat(c.Images)) { Selection.Add(n.Id); BringToFront(n); }
    }

    public void SelectAll() { Selection.Clear(); foreach (var n in Movable) Selection.Add(n.Id); }

    // ---- persistence (own JSON format; the .cwm reader is the import path) ----
    sealed record Dto(List<PluginDto> Plugins, List<TextNode> Texts, List<WireDto> Wires, List<ImageNode>? Images = null, double Zoom = 1, Dictionary<string, string>? WsExtra = null);
    sealed record PortDto(int Id, string Name, string DataType, string ToolTip, bool Mandatory, int Orient = 4, string Asm = "", Dictionary<string, string>? Extra = null);
    sealed record PluginDto(int Id, string Name, string Type, string Assembly, double X, double Y, double W, double H, int Z, bool Min,
        List<PortDto> In, List<PortDto> Out, Dictionary<string, string> Settings, int View = 0, Dictionary<string, string>? Extra = null, Dictionary<string, string>? SettingTypes = null);
    sealed record WireDto(int Id, int From, int To, string Sig = "", List<double[]>? Pts = null, Dictionary<string, string>? Extra = null);

    public string ToJson() => JsonSerializer.Serialize(new Dto(
        Ws.Plugins.Select(p => new PluginDto(p.Id, p.Name, p.TypeName, p.AssemblyName, p.X, p.Y, p.Width, p.Height, p.ZIndex, p.Minimized,
            p.Inputs.Select(c => new PortDto(c.Id, c.PropertyName, c.DataTypeName, c.ToolTip, c.IsMandatory, c.Orientation, c.DataTypeAssembly, c.Extra)).ToList(),
            p.Outputs.Select(c => new PortDto(c.Id, c.PropertyName, c.DataTypeName, c.ToolTip, c.IsMandatory, c.Orientation, c.DataTypeAssembly, c.Extra)).ToList(), p.Settings, p.ViewState, p.Extra, p.SettingTypes)).ToList(),
        Ws.Texts.ToList(),
        Ws.Wires.Where(w => w.From != null && w.To != null).Select(w => new WireDto(w.Id, w.From!.Id, w.To!.Id, w.RouteSig, w.Points.Select(q => new[] { q.X, q.Y }).ToList(), w.Extra)).ToList(), Ws.Images.ToList(), Ws.Zoom, Ws.Extra),
        new JsonSerializerOptions { WriteIndented = true });

    public static Workspace FromJson(string json)
    {
        var d = JsonSerializer.Deserialize<Dto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var ws = new Workspace(); var ports = new Dictionary<int, ConnectorPort>();
        foreach (var p in d.Plugins)
        {
            var n = new PluginNode { Id = p.Id, Name = p.Name, TypeName = p.Type, AssemblyName = p.Assembly, X = p.X, Y = p.Y, Width = p.W, Height = p.H, ZIndex = p.Z, Minimized = p.Min, ViewState = p.View, Extra = p.Extra ?? new() };
            foreach (var kv in p.Settings ?? new()) n.Settings[kv.Key] = kv.Value;
            foreach (var kv in p.SettingTypes ?? new()) n.SettingTypes[kv.Key] = kv.Value;
            ws.Plugins.Add(n);
            void add(List<PortDto> l, bool o) { int i = 0; foreach (var q in l) { var c = new ConnectorPort { Id = q.Id, Plugin = n, Outgoing = o, Index = i++, PropertyName = q.Name, Name = q.Extra != null && q.Extra.TryGetValue("Name", out var nm) ? nm : q.Name, DataTypeName = q.DataType, DataTypeAssembly = q.Asm, ToolTip = q.ToolTip, IsMandatory = q.Mandatory, Orientation = q.Orient, Extra = q.Extra ?? new() }; (o ? n.Outputs : n.Inputs).Add(c); ws.Connectors.Add(c); ports[c.Id] = c; } }
            add(p.In, false); add(p.Out, true);
        }
        ws.Zoom = d.Zoom <= 0 ? 1 : d.Zoom;
        ws.Extra = d.WsExtra ?? new();
        ws.Texts.AddRange(d.Texts);
        foreach (var t in d.Texts) t.Extra ??= new();
        foreach (var im in d.Images ?? new()) { im.Extra ??= new(); ws.Images.Add(im); }
        foreach (var w in d.Wires) if (ports.TryGetValue(w.From, out var f) && ports.TryGetValue(w.To, out var t)) { var x = new Wire { Id = w.Id != 0 ? w.Id : ws.NextId(), From = f, To = t, RouteSig = w.Sig, Extra = w.Extra ?? new() }; foreach (var q in w.Pts ?? new()) x.Points.Add((q[0], q[1])); f.Wires.Add(x); t.Wires.Add(x); ws.Wires.Add(x); }
        return ws;
    }
}

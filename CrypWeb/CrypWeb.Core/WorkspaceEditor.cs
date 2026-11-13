namespace CrypTool.Web.Core;

/// <summary>Editing operations of the canvas (selection, connect, delete, copy/paste, undo/redo) on one workspace.</summary>
/// <remarks>Persistence (own JSON format) is in <c>WorkspaceEditor.Json.cs</c>, the connection rules in <see cref="ConnectionRules"/>.</remarks>
public sealed partial class WorkspaceEditor
{
    const int MaxUndoSteps = 200;
    const double PasteOffset = 30; // each paste lands this much further down-right, so copies do not hide each other

    public Workspace Ws { get; private set; } = new();
    public HashSet<int> Selection { get; } = new();
    public int? SelectedWire { get; set; }

    readonly Stack<Workspace> undo = new();
    readonly Stack<Workspace> redo = new();
    Workspace? clipboard;
    int pasteCount;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    /// <summary>True when there are changes that were not saved.</summary>
    public bool Dirty { get; private set; }

    public void MarkDirty() => Dirty = true;

    /// <summary>Replaces the workspace (new/open); clears selection and undo history.</summary>
    public void Load(Workspace workspace)
    {
        Ws = workspace;
        Selection.Clear();
        SelectedWire = null;
        undo.Clear();
        redo.Clear();
        Dirty = false;
    }

    // ---- undo / redo ----

    /// <summary>Call BEFORE a mutating operation: remembers the current state as an undo step.</summary>
    public void Checkpoint()
    {
        undo.Push(Ws.Clone());
        redo.Clear();
        if (undo.Count > MaxUndoSteps) DropOldestUndoStep();
        Dirty = true;
    }

    void DropOldestUndoStep()
    {
        var newestFirst = undo.ToArray();
        undo.Clear();
        foreach (var step in newestFirst.Take(newestFirst.Length - 1).Reverse()) undo.Push(step);
    }

    public void Undo()
    {
        if (undo.Count == 0) return;
        redo.Push(Ws.Clone());
        Ws = undo.Pop();
        PruneSelection();
    }

    public void Redo()
    {
        if (redo.Count == 0) return;
        undo.Push(Ws.Clone());
        Ws = redo.Pop();
        PruneSelection();
    }

    /// <summary>After undo/redo the selected nodes may no longer exist.</summary>
    void PruneSelection()
    {
        var existing = Movable.Select(n => n.Id).ToHashSet();
        Selection.IntersectWith(existing);
        SelectedWire = null;
    }

    // ---- nodes and z-order ----

    /// <summary>Everything that can be moved, selected and deleted (components, notes, pictures).</summary>
    public IEnumerable<VisualNode> Movable => Ws.Plugins.Cast<VisualNode>().Concat(Ws.Texts).Concat(Ws.Images);

    public VisualNode? Find(int id) => Movable.FirstOrDefault(n => n.Id == id);

    public void BringToFront(VisualNode node) => node.ZIndex = Movable.Max(m => m.ZIndex) + 1;

    public void SendToBack(VisualNode node) => node.ZIndex = Movable.Min(m => m.ZIndex) - 1;

    /// <summary>Moves a node one step up (+1) or down (-1) in the z-order.</summary>
    public void Elevate(VisualNode node, int direction)
    {
        var ordered = Movable.OrderBy(m => m.ZIndex).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].ZIndex = i + 1; // make the z-indices distinct first

        int index = ordered.IndexOf(node);
        int neighbour = index + direction;
        if (index < 0 || neighbour < 0 || neighbour >= ordered.Count) return;
        (ordered[index].ZIndex, ordered[neighbour].ZIndex) = (ordered[neighbour].ZIndex, ordered[index].ZIndex);
    }

    public PluginNode AddPlugin(PaletteEntry entry, double x, double y)
    {
        var plugin = new PluginNode
        {
            Id = Ws.NextId(),
            X = x,
            Y = y,
            Width = entry.Width,
            Height = entry.Height,
            Name = entry.Name,
            TypeName = entry.TypeName,
            AssemblyName = entry.AssemblyName,
        };
        Ws.Plugins.Add(plugin);
        AddPorts(plugin, entry.Inputs, outgoing: false);
        AddPorts(plugin, entry.Outputs, outgoing: true);
        BringToFront(plugin);
        return plugin;
    }

    /// <summary>Inputs sit on the west side, outputs on the east side (orientation 2 / 3).</summary>
    void AddPorts(PluginNode plugin, List<PalettePort> ports, bool outgoing)
    {
        int index = 0;
        foreach (var port in ports)
        {
            var connector = new ConnectorPort
            {
                Id = Ws.NextId(),
                Plugin = plugin,
                Outgoing = outgoing,
                Orientation = outgoing ? 3 : 2,
                Index = index++,
                PropertyName = port.PropertyName,
                Name = port.PropertyName,
                DataTypeName = port.DataTypeName,
                ToolTip = port.ToolTip,
                IsMandatory = port.IsMandatory,
            };
            (outgoing ? plugin.Outputs : plugin.Inputs).Add(connector);
            Ws.Connectors.Add(connector);
        }
    }

    public TextNode AddText(double x, double y)
    {
        var note = new TextNode { Id = Ws.NextId(), X = x, Y = y, Width = 220, Height = 80, Text = "Note" };
        Ws.Texts.Add(note);
        BringToFront(note);
        return note;
    }

    public ImageNode AddImage(byte[] data, double x, double y, double width, double height)
    {
        var image = new ImageNode { Id = Ws.NextId(), X = x, Y = y, Width = width, Height = height, Data = data };
        Ws.Images.Add(image);
        BringToFront(image);
        return image;
    }

    // ---- wires ----

    /// <summary>True if an output and an input of different components can be connected (see <see cref="ConnectionRules"/>).</summary>
    public bool CanConnect(ConnectorPort a, ConnectorPort b)
    {
        if (a == b || a.Outgoing == b.Outgoing || a.Plugin == b.Plugin) return false;
        var (from, to) = a.Outgoing ? (a, b) : (b, a);
        return ConnectionRules.Compatible(from.DataTypeName, to.DataTypeName);
    }

    /// <summary>Connects two ports (in either order). An input takes one connection: an existing wire to it is replaced.</summary>
    public Wire? Connect(ConnectorPort a, ConnectorPort b)
    {
        if (!CanConnect(a, b)) return null;
        var (from, to) = a.Outgoing ? (a, b) : (b, a);
        if (from.Wires.Any(w => w.To == to)) return null;

        foreach (var old in to.Wires.ToList()) RemoveWire(old);
        var wire = new Wire { Id = Ws.NextId(), From = from, To = to };
        from.Wires.Add(wire);
        to.Wires.Add(wire);
        Ws.Wires.Add(wire);
        return wire;
    }

    public void RemoveWire(Wire wire)
    {
        wire.From?.Wires.Remove(wire);
        wire.To?.Wires.Remove(wire);
        Ws.Wires.Remove(wire);
        if (SelectedWire == wire.Id) SelectedWire = null;
    }

    /// <summary>Deletes the selected wire, or else the selected components (with their wires), notes and pictures.</summary>
    public void DeleteSelection()
    {
        if (SelectedWire is int wireId && Ws.Wires.FirstOrDefault(w => w.Id == wireId) is { } selectedWire)
        {
            RemoveWire(selectedWire);
            return;
        }

        foreach (var plugin in Ws.Plugins.Where(p => Selection.Contains(p.Id)).ToList())
        {
            foreach (var connector in plugin.Inputs.Concat(plugin.Outputs))
            {
                foreach (var wire in connector.Wires.ToList()) RemoveWire(wire);
                Ws.Connectors.Remove(connector);
            }
            Ws.Plugins.Remove(plugin);
        }
        Ws.Texts.RemoveAll(t => Selection.Contains(t.Id));
        Ws.Images.RemoveAll(i => Selection.Contains(i.Id));
        Selection.Clear();
    }

    // ---- clipboard ----

    public bool HasClipboard => clipboard != null && clipboard.Plugins.Count + clipboard.Texts.Count + clipboard.Images.Count > 0;

    public void Copy() => clipboard = Ws.Clone(n => Selection.Contains(n.Id));

    /// <summary>Inserts a copy of the clipboard with fresh ids, offset a bit, and selects it.</summary>
    public void Paste()
    {
        if (!HasClipboard) return;
        pasteCount++;
        double offset = PasteOffset * pasteCount;
        var pasted = clipboard!.Clone();

        int nextId = Ws.NextId();
        var newIds = new Dictionary<int, int>();
        int NewId(int oldId) => newIds.TryGetValue(oldId, out var id) ? id : newIds[oldId] = nextId++;

        var nodes = pasted.Plugins.Cast<VisualNode>().Concat(pasted.Texts).Concat(pasted.Images).ToList();
        foreach (var node in nodes)
        {
            node.Id = NewId(node.Id);
            node.X += offset;
            node.Y += offset;
        }
        foreach (var connector in pasted.Connectors) connector.Id = NewId(connector.Id);
        foreach (var wire in pasted.Wires) wire.Id = NewId(wire.Id);

        Ws.Plugins.AddRange(pasted.Plugins);
        Ws.Connectors.AddRange(pasted.Connectors);
        Ws.Wires.AddRange(pasted.Wires);
        Ws.Texts.AddRange(pasted.Texts);
        Ws.Images.AddRange(pasted.Images);

        Selection.Clear();
        foreach (var node in nodes)
        {
            Selection.Add(node.Id);
            BringToFront(node);
        }
    }

    public void SelectAll()
    {
        Selection.Clear();
        foreach (var node in Movable) Selection.Add(node.Id);
    }
}

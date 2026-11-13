namespace CrypTool.Web.Core;

/// <summary>Platform independent view of a CrypTool 2 workspace (.cwm).</summary>
public sealed class Workspace
{
    public List<PluginNode> Plugins { get; } = new();
    public List<ConnectorPort> Connectors { get; } = new();
    public List<Wire> Wires { get; } = new();
    public List<TextNode> Texts { get; } = new();
    public List<ImageNode> Images { get; } = new();
    public double Zoom { get; set; } = 1;

    /// <summary>Raw members of the original WorkspaceModel (size of the workspace view etc.).</summary>
    public Dictionary<string, string> Extra { get; set; } = new();

    /// <summary>The next free id (ids are shared by all kinds of objects of a workspace).</summary>
    public int NextId()
    {
        var ids = Plugins.Select(p => p.Id)
            .Concat(Connectors.Select(c => c.Id))
            .Concat(Wires.Select(w => w.Id))
            .Concat(Texts.Select(t => t.Id))
            .Concat(Images.Select(i => i.Id));
        return 1 + new[] { 0 }.Concat(ids).Max();
    }

    /// <summary>
    /// Deep copy (ids preserved). Used for undo snapshots, copy/paste and persistence.
    /// With <paramref name="include"/> only the selected nodes are copied; connectors and wires follow
    /// their components, a wire is kept only if both of its ends are copied.
    /// </summary>
    public Workspace Clone(Func<VisualNode, bool>? include = null)
    {
        bool Wanted(VisualNode node) => include?.Invoke(node) ?? true;

        var copy = new Workspace { Zoom = Zoom, Extra = new(Extra) };
        var pluginCopies = new Dictionary<PluginNode, PluginNode>();
        var connectorCopies = new Dictionary<ConnectorPort, ConnectorPort>();

        foreach (var plugin in Plugins.Where(Wanted))
        {
            var clone = plugin.CloneWithoutConnectors();
            pluginCopies[plugin] = clone;
            copy.Plugins.Add(clone);
        }

        foreach (var connector in Connectors.Where(c => c.Plugin != null && pluginCopies.ContainsKey(c.Plugin)))
        {
            var clone = connector.CloneFor(pluginCopies[connector.Plugin!]);
            connectorCopies[connector] = clone;
            copy.Connectors.Add(clone);
        }

        foreach (var wire in Wires.Where(w => w.From != null && w.To != null
                                              && connectorCopies.ContainsKey(w.From) && connectorCopies.ContainsKey(w.To)))
        {
            copy.Wires.Add(wire.CloneBetween(connectorCopies[wire.From!], connectorCopies[wire.To!]));
        }

        copy.Texts.AddRange(Texts.Where(Wanted).Select(t => t.Clone()));
        copy.Images.AddRange(Images.Where(Wanted).Select(i => i.Clone()));
        return copy;
    }
}

/// <summary>Common part of everything that has a position on the canvas.</summary>
public abstract class VisualNode
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; }

    /// <summary>Raw serialized members of the original .cwm object (kept so a save can reproduce fields the editor does not model).</summary>
    public Dictionary<string, string> Extra { get; set; } = new();

    /// <summary>Copies id, geometry and raw members to <paramref name="target"/>.</summary>
    protected T CopyBaseTo<T>(T target) where T : VisualNode
    {
        target.Id = Id;
        target.X = X;
        target.Y = Y;
        target.Width = Width;
        target.Height = Height;
        target.ZIndex = ZIndex;
        target.Extra = new(Extra);
        return target;
    }
}

/// <summary>A component on the canvas, with its connectors and settings.</summary>
public sealed class PluginNode : VisualNode
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string AssemblyName { get; set; } = "";
    public bool Startable { get; set; }
    public bool Minimized { get; set; }
    public int ImageIndex { get; set; }

    /// <summary>0 = default (icon), 1 = presentation, others = data/log/settings views.</summary>
    public int ViewState { get; set; }

    public List<ConnectorPort> Inputs { get; } = new();
    public List<ConnectorPort> Outputs { get; } = new();

    /// <summary>Setting values by property name, as strings (as stored in the .cwm).</summary>
    public Dictionary<string, string> Settings { get; } = new();

    /// <summary>The .NET type name of each setting (needed to write it back).</summary>
    public Dictionary<string, string> SettingTypes { get; } = new();

    internal PluginNode CloneWithoutConnectors()
    {
        var clone = CopyBaseTo(new PluginNode
        {
            Name = Name,
            TypeName = TypeName,
            AssemblyName = AssemblyName,
            Startable = Startable,
            Minimized = Minimized,
            ImageIndex = ImageIndex,
            ViewState = ViewState,
        });
        foreach (var (key, value) in Settings) clone.Settings[key] = value;
        foreach (var (key, value) in SettingTypes) clone.SettingTypes[key] = value;
        return clone;
    }
}

/// <summary>An input or output of a component.</summary>
public sealed class ConnectorPort : VisualNode
{
    public PluginNode? Plugin { get; set; }
    public string Name { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string ToolTip { get; set; } = "";
    public string DataTypeName { get; set; } = "";
    public string DataTypeAssembly { get; set; } = "";
    public bool Outgoing { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>0 = North, 1 = South, 2 = West, 3 = East, 4 = unset.</summary>
    public int Orientation { get; set; } = 4;

    public int Index { get; set; }
    public List<Wire> Wires { get; } = new();

    /// <summary>Copy that belongs to <paramref name="owner"/> (and is registered in its inputs/outputs).</summary>
    internal ConnectorPort CloneFor(PluginNode owner)
    {
        var clone = CopyBaseTo(new ConnectorPort
        {
            Name = Name,
            PropertyName = PropertyName,
            ToolTip = ToolTip,
            DataTypeName = DataTypeName,
            DataTypeAssembly = DataTypeAssembly,
            Outgoing = Outgoing,
            IsMandatory = IsMandatory,
            Orientation = Orientation,
            Index = Index,
            Plugin = owner,
        });
        (clone.Outgoing ? owner.Outputs : owner.Inputs).Add(clone);
        return clone;
    }
}

/// <summary>A connection from an output to an input.</summary>
public sealed class Wire : VisualNode
{
    public ConnectorPort? From { get; set; }
    public ConnectorPort? To { get; set; }

    /// <summary>The route the user dragged (empty: automatic route).</summary>
    public List<(double X, double Y)> Points { get; } = new();

    /// <summary>Signature of the endpoints' geometry when Points were valid; if it changes the stored route is dropped on save.</summary>
    public string RouteSig { get; set; } = "";

    /// <summary>Copy that connects the (already copied) ports <paramref name="from"/> and <paramref name="to"/>.</summary>
    internal Wire CloneBetween(ConnectorPort from, ConnectorPort to)
    {
        var clone = CopyBaseTo(new Wire { From = from, To = to, RouteSig = RouteSig });
        clone.Points.AddRange(Points);
        from.Wires.Add(clone);
        to.Wires.Add(clone);
        return clone;
    }
}

/// <summary>A note on the canvas.</summary>
public sealed class TextNode : VisualNode
{
    /// <summary>Plain text extracted from the RTF/XAML payload.</summary>
    public string Text { get; set; } = "";

    public string? BackgroundColor { get; set; }

    /// <summary>The original XamlPackage bytes (formatting) and the text extracted from them; reused on save while the text is unchanged.</summary>
    public byte[]? OriginalData { get; set; }

    public string? OriginalText { get; set; }

    internal TextNode Clone() => CopyBaseTo(new TextNode
    {
        Text = Text,
        BackgroundColor = BackgroundColor,
        OriginalData = OriginalData,
        OriginalText = OriginalText,
    });
}

/// <summary>A picture on the canvas.</summary>
public sealed class ImageNode : VisualNode
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    internal ImageNode Clone() => CopyBaseTo(new ImageNode { Data = Data });
}

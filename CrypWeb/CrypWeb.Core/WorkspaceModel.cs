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

    public int NextId() => 1 + new[] { 0 }.Concat(Plugins.Select(p => p.Id)).Concat(Connectors.Select(p => p.Id))
        .Concat(Wires.Select(p => p.Id)).Concat(Texts.Select(p => p.Id)).Concat(Images.Select(p => p.Id)).Max();

    /// <summary>Deep copy (ids preserved). Used for undo snapshots, copy/paste and persistence.</summary>
    public Workspace Clone(Func<VisualNode, bool>? include = null)
    {
        var c = new Workspace { Zoom = Zoom, Extra = new(Extra) };
        var pm = new Dictionary<PluginNode, PluginNode>();
        var cm = new Dictionary<ConnectorPort, ConnectorPort>();
        foreach (var p in Plugins.Where(p => include?.Invoke(p) ?? true))
        {
            var n = new PluginNode { Id = p.Id, X = p.X, Y = p.Y, Width = p.Width, Height = p.Height, ZIndex = p.ZIndex, Name = p.Name,
                TypeName = p.TypeName, AssemblyName = p.AssemblyName, Startable = p.Startable, Minimized = p.Minimized, ImageIndex = p.ImageIndex, ViewState = p.ViewState, Extra = new(p.Extra) };
            foreach (var kv in p.Settings) n.Settings[kv.Key] = kv.Value;
            foreach (var kv in p.SettingTypes) n.SettingTypes[kv.Key] = kv.Value;
            pm[p] = n; c.Plugins.Add(n);
        }
        foreach (var k in Connectors.Where(k => k.Plugin != null && pm.ContainsKey(k.Plugin)))
        {
            var n = new ConnectorPort { Id = k.Id, X = k.X, Y = k.Y, Width = k.Width, Height = k.Height, Name = k.Name, PropertyName = k.PropertyName,
                ToolTip = k.ToolTip, DataTypeName = k.DataTypeName, Outgoing = k.Outgoing, IsMandatory = k.IsMandatory, Orientation = k.Orientation, Index = k.Index, Plugin = pm[k.Plugin!], DataTypeAssembly = k.DataTypeAssembly, Extra = new(k.Extra) };
            (n.Outgoing ? n.Plugin.Outputs : n.Plugin.Inputs).Add(n);
            cm[k] = n; c.Connectors.Add(n);
        }
        foreach (var w in Wires.Where(w => w.From != null && w.To != null && cm.ContainsKey(w.From) && cm.ContainsKey(w.To)))
        {
            var n = new Wire { Id = w.Id, From = cm[w.From!], To = cm[w.To!], RouteSig = w.RouteSig, Extra = new(w.Extra) };
            n.Points.AddRange(w.Points);
            n.From.Wires.Add(n); n.To.Wires.Add(n); c.Wires.Add(n);
        }
        foreach (var t in Texts.Where(t => include?.Invoke(t) ?? true))
            c.Texts.Add(new TextNode { Id = t.Id, X = t.X, Y = t.Y, Width = t.Width, Height = t.Height, ZIndex = t.ZIndex, Text = t.Text, BackgroundColor = t.BackgroundColor, OriginalData = t.OriginalData, OriginalText = t.OriginalText, Extra = new(t.Extra) });
        foreach (var im in Images.Where(i => include?.Invoke(i) ?? true)) c.Images.Add(new ImageNode { Id = im.Id, X = im.X, Y = im.Y, Width = im.Width, Height = im.Height, ZIndex = im.ZIndex, Data = im.Data, Extra = new(im.Extra) });
        return c;
    }
}

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
}

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
    public Dictionary<string, string> Settings { get; } = new();
    public Dictionary<string, string> SettingTypes { get; } = new();
}

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
}

public sealed class Wire : VisualNode
{
    public ConnectorPort? From { get; set; }
    public ConnectorPort? To { get; set; }
    public List<(double X, double Y)> Points { get; } = new();
    /// <summary>Signature of the endpoints' geometry when Points were valid; if it changes the stored route is dropped on save.</summary>
    public string RouteSig { get; set; } = "";
}

public sealed class TextNode : VisualNode
{
    /// <summary>Plain text extracted from the RTF/XAML payload.</summary>
    public string Text { get; set; } = "";
    public string? BackgroundColor { get; set; }
    /// <summary>The original XamlPackage bytes (formatting) and the text extracted from them; reused on save while the text is unchanged.</summary>
    public byte[]? OriginalData { get; set; }
    public string? OriginalText { get; set; }
}

public sealed class ImageNode : VisualNode
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public sealed record PalettePort(string PropertyName, string DataTypeName, string ToolTip, bool IsMandatory);

/// <summary>A component that can be dropped on the canvas.</summary>
public sealed class PaletteEntry
{
    public string TypeName { get; set; } = "";
    public string AssemblyName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
    public List<PalettePort> Inputs { get; set; } = new();
    public List<PalettePort> Outputs { get; set; } = new();
}

public sealed class PortMeta
{
    public string Name { get; set; } = ""; public string Dir { get; set; } = "in"; public string Type { get; set; } = "";
    public string Caption { get; set; } = ""; public string Tooltip { get; set; } = ""; public bool Mandatory { get; set; } public bool Control { get; set; }
}

/// <summary>Component metadata extracted from the plugin sources (plugins.json).</summary>
public sealed class PluginMeta
{
    public string Type { get; set; } = ""; public string Dir { get; set; } = ""; public string Assembly { get; set; } = ""; public string Name { get; set; } = "";
    public string Tooltip { get; set; } = ""; public string Category { get; set; } = "Undefined"; public string? Icon { get; set; }
    public List<PortMeta> Ports { get; set; } = new(); public bool Supported { get; set; }
    /// <summary>Localized overrides per language code ("de"); anything missing falls back to the English values above.</summary>
    public Dictionary<string, PluginLoc> Loc { get; set; } = new();

    static string Full(string t)
    {
        t = t.Replace("?", "");
        return t switch
        {
            "string" => "System.String", "int" => "System.Int32", "long" => "System.Int64", "double" => "System.Double", "bool" => "System.Boolean",
            "byte[]" => "System.Byte[]", "BigInteger" => "System.Numerics.BigInteger", "ICrypToolStream" => "CrypTool.PluginBase.IO.ICrypToolStream",
            "object" => "System.Object", "Array" => "System.Array", "string[]" => "System.String[]", "int[]" => "System.Int32[]", "double[]" => "System.Double[]",
            "byte" => "System.Byte", "float" => "System.Single", "char" => "System.Char", _ => t
        };
    }

    public PaletteEntry ToPalette()
    {
        PalettePort P(PortMeta m) => new(m.Name, Full(m.Type), string.IsNullOrEmpty(m.Tooltip) ? m.Caption : m.Tooltip, m.Mandatory);
        return new PaletteEntry { TypeName = Type, AssemblyName = string.IsNullOrEmpty(Assembly) ? Dir : Assembly, Name = Name, Category = Category,
            Inputs = Ports.Where(p => p.Dir == "in" && !p.Control).Select(P).ToList(), Outputs = Ports.Where(p => p.Dir == "out" && !p.Control).Select(P).ToList() };
    }
}

/// <summary>Colors taken from the original CT2 workspace (WorkspaceManagerModel/Properties/Settings.settings).</summary>
public static class Theme
{
    public static string CategoryColor(string category) => category switch
    {
        "CiphersClassic" => "#b8c881", "CiphersModernSymmetric" => "#d49090", "CiphersModernAsymmetric" => "#6789a2",
        "Steganography" => "#9acd32", "HashFunctions" => "#8abbbc", "CryptanalysisSpecific" => "#bc8aac", "CryptanalysisGeneric" => "#8c8abc",
        "Protocols" => "#ff8c00", "ToolsBoolean" or "ToolsDataflow" or "ToolsDataInputOutput" or "ToolsRandomNumbers" or "ToolsCodes" or "ToolsMisc" => "#8abc94",
        _ => "#a3d090"
    };

    public static string TypeColor(string dataType)
    {
        var t = dataType.Replace("[]", "");
        return t switch
        {
            "System.Int32" => "#00ffff", "System.Byte" => "#228b22", "System.Double" => "#0000ff", "System.Boolean" => "#800000",
            "System.String" => "#808080", "System.Object" => "#9370db", "System.Numerics.BigInteger" => "#00ced1",
            _ when t.EndsWith("ICrypToolStream") || t.EndsWith("Stream") => "#ff8c00",
            _ => "#a3d090"
        };
    }
}

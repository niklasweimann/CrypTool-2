namespace CrypTool.Web.Core;

/// <summary>A connector of a palette entry (a component that can be added to the canvas).</summary>
/// <remarks><paramref name="IsControl"/>: a control connector (IControl), placed on the top (slave) or bottom (master) side.</remarks>
public sealed record PalettePort(string PropertyName, string DataTypeName, string ToolTip, bool IsMandatory, bool IsControl = false);

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

/// <summary>One connector (or control connector) of a plugin, as found in plugins.json.</summary>
public sealed class PortMeta
{
    public string Name { get; set; } = "";

    /// <summary>"in" or "out".</summary>
    public string Dir { get; set; } = "in";

    /// <summary>The C# type name as written in the plugin source (e.g. "int", "byte[]").</summary>
    public string Type { get; set; } = "";

    public string Caption { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public bool Mandatory { get; set; }

    /// <summary>A control (IControl) connector: the master gets the slave's control object when a run starts.</summary>
    public bool Control { get; set; }
}

/// <summary>Component metadata extracted from the plugin sources (plugins.json, written by tools-gen-plugins.py).</summary>
public sealed class PluginMeta
{
    public string Type { get; set; } = "";
    public string Dir { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public string Category { get; set; } = "Undefined";
    public string? Icon { get; set; }
    public List<PortMeta> Ports { get; set; } = new();

    /// <summary>Whether the engine can run the component.</summary>
    public bool Supported { get; set; }

    /// <summary>
    /// An experimental component (Experimental/CrypPlugins, not part of the CT2 release); the app shows it only when
    /// experimental plugins are switched on under View.
    /// </summary>
    public bool Experimental { get; set; }

    /// <summary>False for components without a visualization (their Presentation is null): no presentation view.</summary>
    public bool Presentation { get; set; } = true;

    /// <summary>Localized overrides per language code ("de"); anything missing falls back to the English values above.</summary>
    public Dictionary<string, PluginLoc> Loc { get; set; } = new();

    /// <summary>Maps the C# keyword/type names used in plugin sources to the full .NET type names used in .cwm files.</summary>
    static string FullTypeName(string type)
    {
        type = type.Replace("?", "");
        return type switch
        {
            "string" => "System.String",
            "int" => "System.Int32",
            "long" => "System.Int64",
            "double" => "System.Double",
            "bool" => "System.Boolean",
            "byte" => "System.Byte",
            "float" => "System.Single",
            "char" => "System.Char",
            "object" => "System.Object",
            "Array" => "System.Array",
            "string[]" => "System.String[]",
            "int[]" => "System.Int32[]",
            "double[]" => "System.Double[]",
            "byte[]" => "System.Byte[]",
            "BigInteger" => "System.Numerics.BigInteger",
            "ICrypToolStream" => "CrypTool.PluginBase.IO.ICrypToolStream",
            _ => type,
        };
    }

    public PaletteEntry ToPalette()
    {
        PalettePort ToPort(PortMeta port) => new(
            port.Name,
            FullTypeName(port.Type),
            string.IsNullOrEmpty(port.Tooltip) ? port.Caption : port.Tooltip,
            port.Mandatory,
            port.Control);

        return new PaletteEntry
        {
            TypeName = Type,
            AssemblyName = string.IsNullOrEmpty(Assembly) ? Dir : Assembly,
            Name = Name,
            Category = Category,
            Inputs = Ports.Where(p => p.Dir == "in").Select(ToPort).ToList(),
            Outputs = Ports.Where(p => p.Dir == "out").Select(ToPort).ToList(),
        };
    }
}

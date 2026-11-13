using System.Text.Json;

namespace CrypTool.Web.Core;

// Persistence in CrypWeb's own JSON format (autosave, "Save as json", worker hand-over).
// The .cwm reader/writer (CwmReader, CwmWriter) is the exchange format with CrypTool 2.
/// <summary>JSON persistence part of the editor.</summary>
public sealed partial class WorkspaceEditor
{
    // Data transfer objects of the JSON format. Optional members keep older files readable.
    sealed record Dto(
        List<PluginDto> Plugins,
        List<TextNode> Texts,
        List<WireDto> Wires,
        List<ImageNode>? Images = null,
        double Zoom = 1,
        Dictionary<string, string>? WsExtra = null);

    sealed record PortDto(
        int Id, string Name, string DataType, string ToolTip, bool Mandatory,
        int Orient = 4, string Asm = "", Dictionary<string, string>? Extra = null);

    sealed record PluginDto(
        int Id, string Name, string Type, string Assembly, double X, double Y, double W, double H, int Z, bool Min,
        List<PortDto> In, List<PortDto> Out, Dictionary<string, string> Settings,
        int View = 0, Dictionary<string, string>? Extra = null, Dictionary<string, string>? SettingTypes = null);

    sealed record WireDto(
        int Id, int From, int To, string Sig = "", List<double[]>? Pts = null, Dictionary<string, string>? Extra = null);

    public string ToJson()
    {
        var plugins = Ws.Plugins.Select(ToDto).ToList();
        var wires = Ws.Wires.Where(w => w.From != null && w.To != null).Select(ToDto).ToList();
        var dto = new Dto(plugins, Ws.Texts.ToList(), wires, Ws.Images.ToList(), Ws.Zoom, Ws.Extra);
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    static PortDto ToDto(ConnectorPort c) =>
        new(c.Id, c.PropertyName, c.DataTypeName, c.ToolTip, c.IsMandatory, c.Orientation, c.DataTypeAssembly, c.Extra);

    static PluginDto ToDto(PluginNode p) => new(
        p.Id, p.Name, p.TypeName, p.AssemblyName, p.X, p.Y, p.Width, p.Height, p.ZIndex, p.Minimized,
        p.Inputs.Select(ToDto).ToList(), p.Outputs.Select(ToDto).ToList(),
        p.Settings, p.ViewState, p.Extra, p.SettingTypes);

    static WireDto ToDto(Wire w) => new(
        w.Id, w.From!.Id, w.To!.Id, w.RouteSig, w.Points.Select(q => new[] { q.X, q.Y }).ToList(), w.Extra);

    public static Workspace FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var workspace = new Workspace();
        var portsById = new Dictionary<int, ConnectorPort>();

        foreach (var pluginDto in dto.Plugins)
        {
            var plugin = FromDto(pluginDto);
            workspace.Plugins.Add(plugin);
            AddPortsFromDto(workspace, plugin, pluginDto.In, outgoing: false, portsById);
            AddPortsFromDto(workspace, plugin, pluginDto.Out, outgoing: true, portsById);
        }

        workspace.Zoom = dto.Zoom <= 0 ? 1 : dto.Zoom;
        workspace.Extra = dto.WsExtra ?? new();

        workspace.Texts.AddRange(dto.Texts);
        foreach (var text in dto.Texts) text.Extra ??= new();
        foreach (var image in dto.Images ?? new())
        {
            image.Extra ??= new();
            workspace.Images.Add(image);
        }

        foreach (var wireDto in dto.Wires)
        {
            if (!portsById.TryGetValue(wireDto.From, out var from) || !portsById.TryGetValue(wireDto.To, out var to)) continue;
            var wire = new Wire
            {
                Id = wireDto.Id != 0 ? wireDto.Id : workspace.NextId(),
                From = from,
                To = to,
                RouteSig = wireDto.Sig,
                Extra = wireDto.Extra ?? new(),
            };
            foreach (var point in wireDto.Pts ?? new()) wire.Points.Add((point[0], point[1]));
            from.Wires.Add(wire);
            to.Wires.Add(wire);
            workspace.Wires.Add(wire);
        }
        return workspace;
    }

    static PluginNode FromDto(PluginDto dto)
    {
        var plugin = new PluginNode
        {
            Id = dto.Id,
            Name = dto.Name,
            TypeName = dto.Type,
            AssemblyName = dto.Assembly,
            X = dto.X,
            Y = dto.Y,
            Width = dto.W,
            Height = dto.H,
            ZIndex = dto.Z,
            Minimized = dto.Min,
            ViewState = dto.View,
            Extra = dto.Extra ?? new(),
        };
        foreach (var (key, value) in dto.Settings ?? new()) plugin.Settings[key] = value;
        foreach (var (key, value) in dto.SettingTypes ?? new()) plugin.SettingTypes[key] = value;
        return plugin;
    }

    static void AddPortsFromDto(
        Workspace workspace, PluginNode plugin, List<PortDto> ports, bool outgoing, Dictionary<int, ConnectorPort> portsById)
    {
        int index = 0;
        foreach (var dto in ports)
        {
            var connector = new ConnectorPort
            {
                Id = dto.Id,
                Plugin = plugin,
                Outgoing = outgoing,
                Index = index++,
                PropertyName = dto.Name,
                // the original connector name is kept in the raw members when it differs from the property name
                Name = dto.Extra != null && dto.Extra.TryGetValue("Name", out var name) ? name : dto.Name,
                DataTypeName = dto.DataType,
                DataTypeAssembly = dto.Asm,
                ToolTip = dto.ToolTip,
                IsMandatory = dto.Mandatory,
                Orientation = dto.Orient,
                Extra = dto.Extra ?? new(),
            };
            (outgoing ? plugin.Outputs : plugin.Inputs).Add(connector);
            workspace.Connectors.Add(connector);
            portsById[connector.Id] = connector;
        }
    }
}

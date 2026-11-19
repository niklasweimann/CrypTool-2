using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>
/// Makes a template from an existing one by changing component settings (used by tools-gen-derived-templates.py for
/// variants such as Beaufort = the Vigenère template with another mode).
/// </summary>
public static class DeriveCommand
{
    /// <summary>
    /// <c>derive base.cwm out.cwm edits.json</c>; edits.json is a list of
    /// <c>{"match": "type name part", "where": {"Setting": "value starts with"}, "set": {"Setting": "value"}}</c>.
    /// Every component whose type contains <c>match</c> (and whose settings start with the <c>where</c> values) gets the
    /// settings of <c>set</c>. An edit that matches nothing, or a setting the component does not have, is an error.
    /// Instead of <c>set</c> an edit can have <c>retype</c>: <c>{"type": "...", "assembly": "...", "name": "...", "settings": {...}}</c>
    /// replaces the component by another one that has the same connectors (e.g. Camellia by BlockCiphers); its settings
    /// are exactly the listed ones, with the value type of the setting of the same name (or of <c>typeLike</c>) in the old component.
    /// Other edits change the structure: <c>{"add": {"type": "...", "x": 0, "y": 0, "w": 100, "h": 100, "viewState": 1, "settings": {...}}}</c> places a
    /// component (palette entry from plugins.json), <c>{"connect": {"from": "Type#n.Port", "to": "Type#n.Port"}}</c> and
    /// <c>{"disconnect": {...}}</c> add and remove a wire, <c>{"remove": "type part"}</c> deletes components, an "add" can give a <c>"name"</c>, <c>{"move": {"match": "...", "dx": 0, "dy": 0}}</c> shifts components (an "add" can also give <c>"at": {"match", "dx", "dy"}</c>
    /// instead of x and y); "Type#n" is the n-th (from 0) component whose type contains "Type".
    /// </summary>
    public static bool Run(string[] args)
    {
        var workspace = CwmReader.Read(File.ReadAllBytes(args[2]));
        using var edits = JsonDocument.Parse(File.ReadAllText(args[4]));
        var ok = true;
        var editor = new WorkspaceEditor();
        editor.Load(workspace);
        foreach (var edit in edits.RootElement.EnumerateArray())
        {
            if (edit.TryGetProperty("add", out var add))
            {
                ok &= Add(editor, add);
                continue;
            }
            if (edit.TryGetProperty("remove", out var remove))
            {
                // {"remove": "type part"}: deletes the matching components with their wires
                foreach (var node in workspace.Plugins.Where(p => p.TypeName.Contains(remove.GetString()!, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    editor.Selection.Clear();
                    editor.Selection.Add(node.Id);
                    editor.DeleteSelection();
                }
                continue;
            }
            if (edit.TryGetProperty("move", out var move))
            {
                // {"move": {"match": "type part", "dx": 0, "dy": 0}}: shifts the matching components (and so the wires attached to them)
                var shift = workspace.Plugins.Where(p => p.TypeName.Contains(move.GetProperty("match").GetString()!, StringComparison.OrdinalIgnoreCase)).ToList();
                if (shift.Count == 0) ok &= Report($"no component matches {move}");
                foreach (var node in shift)
                {
                    node.X += move.TryGetProperty("dx", out var dx) ? dx.GetDouble() : 0;
                    node.Y += move.TryGetProperty("dy", out var dy) ? dy.GetDouble() : 0;
                }
                continue;
            }
            if (edit.TryGetProperty("connect", out var connect))
            {
                ok &= Wire(editor, connect, add: true);
                continue;
            }
            if (edit.TryGetProperty("disconnect", out var disconnect))
            {
                ok &= Wire(editor, disconnect, add: false);
                continue;
            }
            var match = edit.GetProperty("match").GetString()!;
            var where = edit.TryGetProperty("where", out var w)
                ? w.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
                : new Dictionary<string, string>();
            var nodes = workspace.Plugins
                .Where(p => p.TypeName.Contains(match, StringComparison.OrdinalIgnoreCase))
                .Where(p => where.All(c => p.Settings.TryGetValue(c.Key, out var v) && v.StartsWith(c.Value, StringComparison.Ordinal)))
                .ToList();
            if (nodes.Count == 0)
            {
                Console.WriteLine($"no component matches '{match}'");
                ok = false;
            }
            foreach (var node in nodes)
            {
                if (edit.TryGetProperty("retype", out var retype))
                {
                    Retype(node, retype);
                    continue;
                }
                foreach (var setting in edit.GetProperty("set").EnumerateObject())
                {
                    if (!node.Settings.ContainsKey(setting.Name))
                    {
                        Console.WriteLine($"{node.TypeName} has no setting '{setting.Name}'");
                        ok = false;
                        continue;
                    }
                    node.Settings[setting.Name] = setting.Value.GetString()!;
                }
            }
        }
        if (ok) File.WriteAllBytes(args[3], CwmWriter.Write(workspace));
        return ok;
    }

    static void Retype(PluginNode node, JsonElement retype)
    {
        var oldTypes = new Dictionary<string, string>(node.SettingTypes);
        var typeLike = retype.TryGetProperty("typeLike", out var like) ? like.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!) : new();
        node.TypeName = retype.GetProperty("type").GetString()!;
        node.AssemblyName = retype.GetProperty("assembly").GetString()!;
        if (retype.TryGetProperty("name", out var name)) node.Name = name.GetString()!;
        node.Settings.Clear();
        node.SettingTypes.Clear();
        foreach (var setting in retype.GetProperty("settings").EnumerateObject())
        {
            node.Settings[setting.Name] = setting.Value.GetString()!;
            var source = typeLike.GetValueOrDefault(setting.Name, setting.Name);
            node.SettingTypes[setting.Name] = oldTypes.GetValueOrDefault(source, "System.Int32");
        }
    }

    static string SettingType(string value) =>
        int.TryParse(value, out _) ? "System.Int32" : value is "True" or "False" ? "System.Boolean" : "System.String";

    /// <summary>Places a component from the palette metadata (plugins.json) and sets the given settings.</summary>
    static bool Add(WorkspaceEditor editor, JsonElement add)
    {
        var type = add.GetProperty("type").GetString()!;
        var metaFile = Path.Combine(AppContext.BaseDirectory, "../../../../CrypWeb.App/wwwroot/plugins.json");
        var meta = JsonSerializer.Deserialize<List<PluginMeta>>(File.ReadAllText(metaFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
            .FirstOrDefault(p => p.Type == type);
        if (meta == null)
        {
            Console.WriteLine($"no component '{type}' in plugins.json");
            return false;
        }
        var entry = meta.ToPalette();
        entry.Width = add.TryGetProperty("w", out var w) ? w.GetDouble() : 100;
        entry.Height = add.TryGetProperty("h", out var h) ? h.GetDouble() : 100;
        double x = add.TryGetProperty("x", out var ax) ? ax.GetDouble() : 0, y = add.TryGetProperty("y", out var ay) ? ay.GetDouble() : 0;
        if (add.TryGetProperty("at", out var at))   // relative to the first component whose type contains "at.match"
        {
            var anchor = editor.Ws.Plugins.FirstOrDefault(p => p.TypeName.Contains(at.GetProperty("match").GetString()!, StringComparison.OrdinalIgnoreCase));
            if (anchor == null) return Report($"no component matches {at}");
            x = anchor.X + (at.TryGetProperty("dx", out var dx) ? dx.GetDouble() : 0);
            y = anchor.Y + (at.TryGetProperty("dy", out var dy) ? dy.GetDouble() : 0);
        }
        if (add.TryGetProperty("name", out var title)) entry.Name = title.GetString()!;   // the title shown on the component
        var node = editor.AddPlugin(entry, x, y);
        if (add.TryGetProperty("viewState", out var viewState)) node.ViewState = viewState.GetInt32();   // 1 shows the presentation of the component
        if (add.TryGetProperty("settings", out var settings))
        {
            foreach (var setting in settings.EnumerateObject())
            {
                node.Settings[setting.Name] = setting.Value.GetString()!;
                node.SettingTypes[setting.Name] = SettingType(setting.Value.GetString()!);
            }
        }
        return true;
    }

    static ConnectorPort? Port(Workspace workspace, string selector)
    {
        var (typePart, property) = (selector[..selector.LastIndexOf('.')], selector[(selector.LastIndexOf('.') + 1)..]);
        var index = 0;
        if (typePart.Contains('#'))
        {
            index = int.Parse(typePart[(typePart.IndexOf('#') + 1)..]);
            typePart = typePart[..typePart.IndexOf('#')];
        }
        var node = workspace.Plugins.Where(p => p.TypeName.Contains(typePart, StringComparison.OrdinalIgnoreCase)).Skip(index).FirstOrDefault();
        return node?.Inputs.Concat(node.Outputs).FirstOrDefault(c => c.PropertyName == property);
    }

    static bool Wire(WorkspaceEditor editor, JsonElement spec, bool add)
    {
        var from = Port(editor.Ws, spec.GetProperty("from").GetString()!);
        var to = Port(editor.Ws, spec.GetProperty("to").GetString()!);
        if (from == null || to == null)
        {
            Console.WriteLine($"connector not found: {spec}");
            return false;
        }
        // old templates spell some type names differently ("Cryptool.PluginBase.IO.ICryptoolStream"): the new connector takes the spelling of the existing one
        if (add && from.DataTypeName != to.DataTypeName && string.Equals(from.DataTypeName, to.DataTypeName, StringComparison.OrdinalIgnoreCase))
            from.DataTypeName = to.DataTypeName;
        if (add) return editor.Connect(from, to) != null || Report($"cannot connect {spec} ({from.Plugin?.TypeName}.{from.PropertyName}: {from.DataTypeName}, outgoing {from.Outgoing} -> {to.Plugin?.TypeName}.{to.PropertyName}: {to.DataTypeName}, outgoing {to.Outgoing})");
        var wire = from.Wires.FirstOrDefault(x => x.To == to);
        if (wire == null) return Report($"no wire to remove: {spec}");
        editor.RemoveWire(wire);
        return true;
    }

    static bool Report(string message)
    {
        Console.WriteLine(message);
        return false;
    }
}

using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Shortcuts on the connectors. A double click on an input adds a matching input component (text, number or boolean)
// next to it and connects it; a double click on an output adds a matching output component (text, boolean or picture).
// Settings › Workspace: add the best fitting component, ask which one when several fit, or do nothing.
public partial class Home
{
    /// <summary>Distance between a component and the one added next to its connector.</summary>
    private const double AddedGap = 70;

    private ConnectorPort? ctxPort;
    private List<PaletteEntry> ctxChoices = new();

    private void PortDoubleClick(ConnectorPort port, MouseEventArgs e)
    {
        if (settings.PortDoubleClick == "off" || port.Plugin == null || port.IsControl) return;
        if (!port.Outgoing && port.Wires.Count > 0) return; // an input takes one wire: it is taken

        var fitting = FittingComponents(port);
        if (fitting.Count == 0) return;
        if (settings.PortDoubleClick == "ask" && fitting.Count > 1)
        {
            ctxPort = port;
            ctxChoices = fitting;
            panDist = 0;
            OpenCtx(e, Ctx.Port, null);
            return;
        }

        AddMatching(port, fitting[0]);
    }

    /// <summary>The components that can be connected to the connector, best first.</summary>
    private List<PaletteEntry> FittingComponents(ConnectorPort port)
    {
        var result = new List<PaletteEntry>();
        foreach (var kind in MatchingKinds(port))
        {
            var entry = meta.Values.FirstOrDefault(m => m.Supported && m.Type.Split('.').Last().Equals(kind, StringComparison.OrdinalIgnoreCase))?.ToPalette();
            var facing = FacingPort(port, entry);
            if (entry == null || facing == null) continue;
            var (from, to) = port.Outgoing ? (port.DataTypeName, facing.DataTypeName) : (facing.DataTypeName, port.DataTypeName);
            if (ConnectionRules.Compatible(from, to)) result.Add(entry);
        }

        return result;
    }

    /// <summary>The connector of the new component that faces the double-clicked one.</summary>
    private static PalettePort? FacingPort(ConnectorPort port, PaletteEntry? entry)
    {
        return (port.Outgoing ? entry?.Inputs : entry?.Outputs)?.FirstOrDefault(p => !p.IsControl);
    }

    /// <summary>Adds the component beside the connector and connects it (one undo step).</summary>
    private void AddMatching(ConnectorPort port, PaletteEntry entry)
    {
        var facing = FacingPort(port, entry);
        if (facing == null) return;
        var at = PortPos(port);
        var plugin = AddAt(entry, at.x, at.y);
        // placed by the size the component is drawn with: beside the connector, facing away from its component
        var (w, h) = Size(plugin);
        var x = port.Outgoing ? at.x + AddedGap : at.x - AddedGap - w;
        plugin.X = Snap(x);
        plugin.Y = Snap(FreeY(plugin, plugin.X, at.y - h / 2));
        var added = (port.Outgoing ? plugin.Inputs : plugin.Outputs).FirstOrDefault(c => !c.IsControl && c.PropertyName == facing.PropertyName);
        if (added != null) ed.Connect(added, port);
    }

    /// <summary>The components that fit a connector, best first (names as in plugins.json).</summary>
    private static IEnumerable<string> MatchingKinds(ConnectorPort port)
    {
        var type = port.DataTypeName;
        if (port.Outgoing)
        {
            var picture = port.PropertyName.Contains("picture", StringComparison.OrdinalIgnoreCase)
                          || port.PropertyName.Contains("image", StringComparison.OrdinalIgnoreCase);
            if (type == "System.Boolean") return new[] { "BooleanOutput", "TextOutput" };
            if (picture) return new[] { "PictureOutput", "TextOutput" };
            return new[] { "TextOutput" };
        }

        if (type == "System.Boolean") return new[] { "BooleanInput" };
        if (type is "System.Int32" or "System.Int64" or "System.Numerics.BigInteger") return new[] { "NumberInput" };
        return new[] { "TextInput", "NumberInput", "BooleanInput" };
    }

    /// <summary>The first y at or below <paramref name="y" /> where the component, put at x, touches no other component.</summary>
    private double FreeY(PluginNode plugin, double x, double y)
    {
        var (width, height) = Size(plugin);
        for (var attempt = 0; attempt < 60; attempt++, y += 20)
        {
            var free = ed.Ws.Plugins.Where(p => p != plugin).All(p =>
            {
                var (w, h) = Size(p);
                return x + width + 10 <= p.X || p.X + w + 10 <= x || y + height + 10 <= p.Y || p.Y + h + 10 <= y;
            });
            if (free) break;
        }

        return y;
    }
}

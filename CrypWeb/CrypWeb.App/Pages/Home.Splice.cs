using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

/// <summary>
///     Dropping a component onto a wire puts it in between: the wire's output is connected to a matching input of the
///     component and a matching output of the component to the wire's input. Works for a component dragged from the
///     palette and for an unconnected component moved on the canvas. The wire is highlighted while the drop would
///     do this. Not in CT2.
/// </summary>
public partial class Home
{
    /// <summary>Distance (in screen pixels) from a wire within which a drop counts as "onto the wire".</summary>
    private const double SpliceDistance = 9;

    /// <summary>The wire the dragged component would be put into, if dropped now.</summary>
    private Wire? spliceWire;

    private readonly record struct PortType(string Property, string Type, bool Mandatory);

    /// <summary>The input and output of a component that fit into <paramref name="wire" />, or null.</summary>
    private static (string Input, string Output)? SplicePorts(IEnumerable<PortType> inputs, IEnumerable<PortType> outputs, Wire wire)
    {
        var fromType = wire.From!.DataTypeName;
        var toType = wire.To!.DataTypeName;
        // the same type before a converted one ("yellow" connection), then the mandatory input
        var input = inputs.Where(i => ConnectionRules.Compatible(fromType, i.Type))
            .OrderBy(i => i.Type == fromType ? 0 : 1).ThenBy(i => i.Mandatory ? 0 : 1).FirstOrDefault();
        var output = outputs.Where(o => ConnectionRules.Compatible(o.Type, toType))
            .OrderBy(o => o.Type == toType ? 0 : 1).FirstOrDefault();
        return input.Property == null || output.Property == null ? null : (input.Property, output.Property);
    }

    private static (string Input, string Output)? SplicePorts(PluginNode p, Wire wire)
    {
        return SplicePorts(
            p.Inputs.Where(c => !c.IsControl).Select(c => new PortType(c.PropertyName, c.DataTypeName, c.IsMandatory)),
            p.Outputs.Where(c => !c.IsControl).Select(c => new PortType(c.PropertyName, c.DataTypeName, c.IsMandatory)),
            wire);
    }

    private static (string Input, string Output)? SplicePorts(PaletteEntry entry, Wire wire)
    {
        return SplicePorts(
            entry.Inputs.Where(c => !c.IsControl).Select(c => new PortType(c.PropertyName, c.DataTypeName, c.IsMandatory)),
            entry.Outputs.Where(c => !c.IsControl).Select(c => new PortType(c.PropertyName, c.DataTypeName, c.IsMandatory)),
            wire);
    }

    /// <summary>The data wire nearest to a world position, if one is within <see cref="SpliceDistance" />.</summary>
    private Wire? WireAt(double x, double y)
    {
        var limit = SpliceDistance / zoom;
        Wire? best = null;
        var bestDistance = double.MaxValue;
        foreach (var wire in ed.Ws.Wires)
        {
            if (wire.From == null || wire.To == null || wire.From.IsControl || wire.To.IsControl) continue;
            var pts = WirePts(wire);
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var distance = DistanceToSegment(x, y, pts[i], pts[i + 1]);
                if (distance < limit && distance < bestDistance)
                {
                    best = wire;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static double DistanceToSegment(double x, double y, (double x, double y) a, (double x, double y) b)
    {
        double dx = b.x - a.x, dy = b.y - a.y;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((x - a.x) * dx + (y - a.y) * dy) / lengthSquared, 0, 1);
        double px = a.x + t * dx - x, py = a.y + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }

    /// <summary>The component being moved, if it is a single component without any wire (only such one can be put into a wire).</summary>
    private PluginNode? DraggedLoosePlugin()
    {
        if (mode != Mode.Drag || !dragMoved || dragStart.Count != 1) return null;
        var plugin = ed.Ws.Plugins.FirstOrDefault(p => dragStart.ContainsKey(p.Id));
        return plugin != null && plugin.Inputs.Concat(plugin.Outputs).All(c => c.Wires.Count == 0) ? plugin : null;
    }

    /// <summary>While a component is moved: the wire under the pointer that it would be put into.</summary>
    private void UpdateSpliceWhileMoving(double wx, double wy)
    {
        var plugin = DraggedLoosePlugin();
        var wire = plugin == null ? null : WireAt(wx, wy);
        spliceWire = wire != null && SplicePorts(plugin!, wire) != null ? wire : null;
    }

    /// <summary>A palette entry is dragged over the canvas (HTML drag and drop).</summary>
    private void DragOver(DragEventArgs e)
    {
        Wire? wire = null;
        if (dragEntry != null)
        {
            var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
            wire = WireAt(wx, wy);
            if (wire != null && SplicePorts(dragEntry, wire) == null) wire = null;
        }

        if (wire == spliceWire) skipRender = true;
        spliceWire = wire;
    }

    /// <summary>The move of a component ends: put it into the highlighted wire.</summary>
    private void FinishDragSplice()
    {
        var plugin = DraggedLoosePlugin();
        var wire = spliceWire;
        spliceWire = null;
        if (plugin != null && wire != null) Splice(plugin, wire);
    }

    /// <summary>Replaces <paramref name="wire" /> by two wires through <paramref name="plugin" /> (part of the current undo step).</summary>
    private void Splice(PluginNode plugin, Wire wire)
    {
        if (SplicePorts(plugin, wire) is not { } ports || !ed.Ws.Wires.Contains(wire)) return;
        var from = wire.From!;
        var to = wire.To!;
        var input = plugin.Inputs.First(c => c.PropertyName == ports.Input);
        var output = plugin.Outputs.First(c => c.PropertyName == ports.Output);
        ed.RemoveWire(wire);
        ed.Connect(from, input);
        ed.Connect(output, to);
    }
}

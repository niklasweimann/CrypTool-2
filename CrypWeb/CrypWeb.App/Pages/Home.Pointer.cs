using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

/// <summary>
///     Pointer interaction on the canvas. <see cref="mode" /> says what the current mouse gesture does;
///     the Down handlers start a gesture, <see cref="Move" /> continues it and <see cref="Up" /> ends it.
/// </summary>
public partial class Home
{
    /// <summary>Distance (in screen pixels) the pointer must travel before a press on a component counts as a drag.</summary>
    private const double DragThreshold = 3;

    private static readonly (double Width, double Height) MinNoteSize = (60, 30);

    /// <summary>Press on the empty canvas: pan (middle/right button or Space), or start a selection rectangle.</summary>
    private void BgDown(PointerEventArgs e)
    {
        downNode = null;
        panDist = 0;
        ctx = Ctx.None;
        CommitEdit();
        lx = e.ClientX;
        ly = e.ClientY;

        var panGesture = e.Button is 1 or 2 || (e.Button == 0 && spaceDown);
        if (panGesture)
        {
            mode = Mode.Pan;
            return;
        }

        if (e.Button != 0) return;

        var extendSelection = e.ShiftKey || e.CtrlKey;
        if (!extendSelection)
        {
            ed.Selection.Clear();
            ed.SelectedWire = null;
        }

        (mx0, my0) = ToWorld(e.ClientX, e.ClientY);
        cx = mx0;
        cy = my0;
        mode = Mode.Marquee;
    }

    /// <summary>Press on a component, note or picture: select it (Shift/Ctrl toggles) and prepare a drag.</summary>
    private void NodeDown(PointerEventArgs e, VisualNode node, bool header = false)
    {
        downNode = node;
        downHeader = header;
        downAt = DateTime.UtcNow;
        panDist = 0;
        ctx = Ctx.None;
        CommitEdit();

        if (e.Button is 1 or 2)
        {
            lx = e.ClientX;
            ly = e.ClientY;
            mode = Mode.Pan;
            return;
        }

        if (e.Button != 0) return;

        ed.SelectedWire = null;
        if (e.ShiftKey || e.CtrlKey)
        {
            if (!ed.Selection.Remove(node.Id)) ed.Selection.Add(node.Id);
        }
        else if (!ed.Selection.Contains(node.Id))
        {
            SelectOnly(node.Id);
        }

        ed.BringToFront(node);
        dragStart = ed.Movable.Where(m => ed.Selection.Contains(m.Id)).ToDictionary(m => m.Id, m => (m.X, m.Y));
        (dragWx, dragWy) = ToWorld(e.ClientX, e.ClientY);
        dragMoved = false;
        mode = Mode.Drag;
    }

    /// <summary>Press on the resize handle in the bottom right corner.</summary>
    private void ResizeDown(PointerEventArgs e, VisualNode node)
    {
        if (node is PluginNode fixedSize && !CanResize(fixedSize)) return;
        ed.Checkpoint();
        resizing = node;
        lx = e.ClientX;
        ly = e.ClientY;
        mode = Mode.Resize;
        if (node is PluginNode plugin)
        {
            // an automatically sized component gets its current size as the explicit starting size
            var (w, h) = Size(plugin);
            plugin.Width = w;
            plugin.Height = h;
            plugin.ViewState = Math.Max(1, plugin.ViewState);
        }
    }

    private void WireDown(PointerEventArgs e, Wire wire)
    {
        ed.Selection.Clear();
        ed.SelectedWire = wire.Id;
    }

    /// <summary>
    ///     Press on a connector: start a new wire. Pressing a connected input detaches its wire and
    ///     continues dragging it from the original output (re-routing).
    /// </summary>
    private void PortDown(PointerEventArgs e, ConnectorPort port)
    {
        if (e.Button != 0) return;
        if (!port.Outgoing && port.Wires.Count == 1)
        {
            var wire = port.Wires[0];
            var source = wire.From!;
            ed.Checkpoint();
            ed.RemoveWire(wire);
            connecting = source;
        }
        else
        {
            connecting = port;
        }

        (cx, cy) = ToWorld(e.ClientX, e.ClientY);
        mode = Mode.Connect;
    }

    /// <summary>Released over a connector while dragging a wire: connect if the types match.</summary>
    private void PortUp(ConnectorPort port)
    {
        if (mode != Mode.Connect || connecting == null) return;
        if (ed.CanConnect(connecting, port))
        {
            ed.Checkpoint();
            ed.Connect(connecting, port);
        }

        connecting = null;
        mode = Mode.None;
    }

    private void Move(PointerEventArgs e)
    {
        if (mode == Mode.None)
        {
            skipRender = true; // plain hovering: nothing to update
            return;
        }

        // the button was released outside of the canvas
        if (mode != Mode.None && e.Buttons == 0)
        {
            Up(e);
            return;
        }

        switch (mode)
        {
            case Mode.WireSeg:
                WireSegMove(e);
                break;
            case Mode.Pan:
                MovePan(e);
                break;
            case Mode.Drag:
                MoveDrag(e);
                break;
            case Mode.Resize:
                MoveResize(e);
                break;
            case Mode.Marquee:
                MoveMarquee(e);
                break;
            case Mode.Connect:
                (cx, cy) = ToWorld(e.ClientX, e.ClientY);
                break;
            case Mode.Minimap:
                MinimapPan(e);
                break;
        }
    }

    private void MovePan(PointerEventArgs e)
    {
        panDist += Math.Abs(e.ClientX - lx) + Math.Abs(e.ClientY - ly);
        panX += e.ClientX - lx;
        panY += e.ClientY - ly;
        lx = e.ClientX;
        ly = e.ClientY;
    }

    /// <summary>Moves the selection; snaps to the 10 unit grid unless Alt is held.</summary>
    private void MoveDrag(PointerEventArgs e)
    {
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        double dx = wx - dragWx, dy = wy - dragWy;
        if (!dragMoved && Math.Abs(dx) + Math.Abs(dy) < DragThreshold / zoom) return;
        if (!dragMoved)
        {
            dragMoved = true;
            ed.Checkpoint(); // one undo step for the whole drag
        }

        foreach (var node in ed.Movable.Where(m => dragStart.ContainsKey(m.Id)))
        {
            var (startX, startY) = dragStart[node.Id];
            node.X = e.AltKey ? startX + dx : Snap(startX + dx);
            node.Y = e.AltKey ? startY + dy : Snap(startY + dy);
        }

        UpdateSpliceWhileMoving(wx, wy);
    }

    private void MoveResize(PointerEventArgs e)
    {
        double dx = (e.ClientX - lx) / zoom, dy = (e.ClientY - ly) / zoom;
        lx = e.ClientX;
        ly = e.ClientY;
        var min = resizing is PluginNode plugin ? MinSize(plugin) : MinNoteSize;
        resizing!.Width = Math.Max(min.Width, resizing.Width + dx);
        resizing.Height = Math.Max(min.Height, resizing.Height + dy);
    }

    /// <summary>Extends the selection rectangle and selects everything it touches.</summary>
    private void MoveMarquee(PointerEventArgs e)
    {
        (cx, cy) = ToWorld(e.ClientX, e.ClientY);
        double left = Math.Min(mx0, cx), right = Math.Max(mx0, cx);
        double top = Math.Min(my0, cy), bottom = Math.Max(my0, cy);

        bool Touches(double x, double y, double w, double h)
        {
            return x < right && x + w > left && y < bottom && y + h > top;
        }

        foreach (var plugin in ed.Ws.Plugins)
        {
            var (w, h) = Size(plugin);
            if (Touches(plugin.X, plugin.Y, w, h)) ed.Selection.Add(plugin.Id);
        }

        foreach (var node in ed.Ws.Texts.Cast<VisualNode>().Concat(ed.Ws.Images))
            if (Touches(node.X, node.Y, node.Width, node.Height))
                ed.Selection.Add(node.Id);
    }

    private void Up(PointerEventArgs e)
    {
        if (mode == Mode.None && connecting == null && resizing == null && segWire == null)
        {
            skipRender = true;
            return;
        }

        if (mode == Mode.Drag) FinishDragSplice();
        mode = Mode.None;
        connecting = null;
        resizing = null;
        segWire = null;
    }

    /// <summary>A plain click (no drag) on the body of a component opens its settings dialog.</summary>
    private void SvgClick(MouseEventArgs e)
    {
        var node = downNode;
        downNode = null;

        var plainClick = !downHeader && !dragMoved && !e.ShiftKey && !e.CtrlKey && !e.MetaKey && e.Button == 0;
        var quick = (DateTime.UtcNow - downAt).TotalMilliseconds < 1500;
        if (node is PluginNode plugin && plainClick && quick) OpenSettings(plugin);
    }
}

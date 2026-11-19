using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Context menus of the original CrypTool 2 canvas: component menu (views + z-order) and workspace menu (align / move / spread / size).
public partial class Home
{
    private Ctx ctx;

    private bool ctxFresh;
    private PluginNode? ctxNode;
    private Doc? ctxDoc;
    private double ctxX, ctxY, panDist;

    private List<VisualNode> SelNodes => ed.Movable.Where(n => ed.Selection.Contains(n.Id)).ToList();

    private static (double w, double h) Dim(VisualNode n)
    {
        return n is PluginNode p ? Size(p) : (n.Width, n.Height);
    }

    private void OpenCtx(MouseEventArgs e, Ctx kind, PluginNode? node)
    {
        if (panDist > 5) return; // right-drag was a pan, not a click
        CommitEdit();
        ctx = kind;
        ctxNode = node;
        // viewport coordinates (position:fixed): independent of the cached canvas rectangle; ct2.clampCtx keeps the menu on screen
        ctxX = e.ClientX;
        ctxY = e.ClientY;
        ctxFresh = true;
    }

    private void NodeMenu(MouseEventArgs e, PluginNode p)
    {
        if (!ed.Selection.Contains(p.Id))
        {
            ed.Selection.Clear();
            ed.Selection.Add(p.Id);
        }

        ed.SelectedWire = null;
        OpenCtx(e, Ctx.Node, p);
    }

    /// <summary>Right click on a document tab.</summary>
    private void TabMenu(MouseEventArgs e, Doc doc)
    {
        panDist = 0;
        ctxDoc = doc;
        OpenCtx(e, Ctx.Tab, null);
    }

    private void BgMenu(MouseEventArgs e)
    {
        OpenCtx(e, Ctx.Bg, null);
    }

    private void CloseCtx()
    {
        ctx = Ctx.None;
    }

    private void Run(Action a)
    {
        a();
        ctx = Ctx.None;
    }

    // ----- component menu -----
    private void ZFront(PluginNode p)
    {
        ed.Checkpoint();
        ed.BringToFront(p);
    }

    private void ZBack(PluginNode p)
    {
        ed.Checkpoint();
        ed.SendToBack(p);
    }

    private void ZUp(PluginNode p)
    {
        ed.Checkpoint();
        ed.Elevate(p, 1);
    }

    private void ZDown(PluginNode p)
    {
        ed.Checkpoint();
        ed.Elevate(p, -1);
    }

    // ----- workspace menu -----
    private void Align(string how)
    {
        var s = SelNodes;
        if (s.Count < 2) return;
        ed.Checkpoint();
        double l = s.Min(n => n.X), t = s.Min(n => n.Y), r = s.Max(n => n.X + Dim(n).w), b = s.Max(n => n.Y + Dim(n).h);
        foreach (var n in s)
        {
            var (w, h) = Dim(n);
            switch (how)
            {
                case "left":
                    n.X = l;
                    break;
                case "right":
                    n.X = r - w;
                    break;
                case "top":
                    n.Y = t;
                    break;
                case "bottom":
                    n.Y = b - h;
                    break;
            }
        }
    }

    private void MoveSel(double dx, double dy)
    {
        var selected = SelNodes;
        if (selected.Count == 0) return;
        ed.Checkpoint();
        foreach (var node in selected)
        {
            node.X += dx;
            node.Y += dy;
        }
    }

    private void Spread(bool horizontal)
    {
        var s = SelNodes;
        if (s.Count < 3) return;
        ed.Checkpoint();
        s = horizontal ? s.OrderBy(n => n.X).ToList() : s.OrderBy(n => n.Y).ToList();
        double start = horizontal ? s[0].X : s[0].Y, end = horizontal ? s[^1].X + Dim(s[^1]).w : s[^1].Y + Dim(s[^1]).h;
        double total = s.Sum(n => horizontal ? Dim(n).w : Dim(n).h), gap = (end - start - total) / (s.Count - 1), pos = start;
        foreach (var n in s)
        {
            var (w, h) = Dim(n);
            if (horizontal)
            {
                n.X = pos;
                pos += w + gap;
            }
            else
            {
                n.Y = pos;
                pos += h + gap;
            }
        }
    }

    private void SizeAll(bool width, bool biggest)
    {
        var s = SelNodes;
        if (s.Count < 2) return;
        ed.Checkpoint();
        var vals = s.Select(n => width ? Dim(n).w : Dim(n).h).ToList();
        var v = biggest ? vals.Max() : vals.Min();
        foreach (var n in s)
            if (n is PluginNode p)
            {
                if (p.Minimized || IconOnly(p)) continue; // icon-only components keep their fixed size
                var (w, h) = Size(p);
                p.Width = width ? v : w;
                p.Height = width ? h : v;
                p.ViewState = Math.Max(1, p.ViewState);
            }
            else if (width)
            {
                n.Width = v;
            }
            else
            {
                n.Height = v;
            }
    }

    /// <summary>
    ///     Routes all wires anew around the components (see <see cref="RouteAround" />). The wires are routed one
    ///     after the other and avoid running on top of the ones already placed.
    /// </summary>
    private void Rearrange()
    {
        ed.Checkpoint();
        const double margin = 10;
        var boxes = ed.Ws.Plugins.Where(p => !p.Minimized).Select(p =>
        {
            var (w, h) = Size(p);
            return new Box(p.X - margin, p.Y - margin, p.X + w + margin, p.Y + h + margin);
        }).ToList();

        var placed = new List<List<(double x, double y)>>();
        foreach (var wire in ed.Ws.Wires.Where(w => w.From != null && w.To != null))
        {
            var route = RouteAround(wire, boxes, placed);
            if (route == null || !IsValidRoute(wire, route)) route = AutoRoute(wire);
            wire.Points.Clear();
            wire.Points.AddRange(route);
            wire.RouteSig = CwmWriter.RouteSig(wire);
            placed.Add(route);
        }
    }

    private enum Ctx
    {
        None,
        Node,
        Bg,
        Tab
    }
}

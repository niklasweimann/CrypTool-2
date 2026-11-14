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
    private void ShowHelp(PluginNode p)
    {
        ed.Selection.Clear();
        ed.Selection.Add(p.Id);
    }

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
    ///     Routes all wires anew: for every wire the candidate route (see <see cref="RouteCandidates" />) that crosses
    ///     the fewest components, runs on top of the fewest other wires and is the shortest wins.
    /// </summary>
    private void Rearrange()
    {
        ed.Checkpoint();
        const double margin = 8;
        var boxes = ed.Ws.Plugins.Where(p => !p.Minimized).Select(p =>
        {
            var (w, h) = Size(p);
            return (plugin: p, x0: p.X - margin, y0: p.Y - margin, x1: p.X + w + margin, y1: p.Y + h + margin);
        }).ToList();

        var placed = new List<List<(double x, double y)>>();
        foreach (var wire in ed.Ws.Wires.Where(w => w.From != null && w.To != null))
        {
            var best = AutoRoute(wire);
            var bestCost = double.MaxValue;
            foreach (var route in RouteCandidates(wire, boxes.Select(b => (b.x0, b.y0, b.x1, b.y1)).ToList()))
            {
                var cost = RouteCost(route, boxes, placed, wire);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = route;
            }

            wire.Points.Clear();
            wire.Points.AddRange(best);
            wire.RouteSig = CwmWriter.RouteSig(wire);
            placed.Add(best);
        }
    }

    /// <summary>
    ///     Orthogonal routes between the two ports: the automatic one, the same with the vertical run moved next to
    ///     a component edge, and routes with a horizontal lane above/below the components.
    /// </summary>
    private static IEnumerable<List<(double x, double y)>> RouteCandidates(Wire wire,
        List<(double x0, double y0, double x1, double y1)> boxes)
    {
        yield return AutoRoute(wire);

        var start = PortPos(wire.From!);
        var end = PortPos(wire.To!);
        var (sdx, sdy) = Away(SideOf(wire.From!));
        var (edx, edy) = Away(SideOf(wire.To!));
        if (sdx == 0 || edx == 0) yield break; // only left/right ports are re-routed beyond the automatic route

        const double stub = 18;
        var a = (x: start.x + sdx * stub, y: start.y);
        var b = (x: end.x + edx * stub, y: end.y);

        // vertical run between the stubs, next to a component edge (or in the middle)
        var xs = new List<double> { (a.x + b.x) / 2 };
        foreach (var box in boxes)
        {
            xs.Add(box.x0);
            xs.Add(box.x1);
        }

        foreach (var x in xs.Distinct())
            yield return new List<(double, double)> { start, a, (x, a.y), (x, b.y), b, end };

        // horizontal lane above/below: leave the stub vertically, run along the lane, come back down/up
        var lanes = new List<double>();
        foreach (var box in boxes)
        {
            lanes.Add(box.y0);
            lanes.Add(box.y1);
        }

        foreach (var y in lanes.Distinct())
            yield return new List<(double, double)> { start, a, (a.x, y), (b.x, y), b, end };
    }

    /// <summary>Cost of a route: crossed components (heavy), shared stretches with already placed wires, length, bends.</summary>
    private static double RouteCost(List<(double x, double y)> route,
        List<(PluginNode plugin, double x0, double y0, double x1, double y1)> boxes,
        List<List<(double x, double y)>> placed, Wire wire)
    {
        var cost = 0.0;
        for (var i = 0; i + 1 < route.Count; i++)
        {
            var (p, q) = (route[i], route[i + 1]);
            var length = Math.Abs(q.x - p.x) + Math.Abs(q.y - p.y);
            cost += length + (length > 0 ? 20 : 0); // every bend costs a little
            var (lx, hx) = (Math.Min(p.x, q.x), Math.Max(p.x, q.x));
            var (ly, hy) = (Math.Min(p.y, q.y), Math.Max(p.y, q.y));
            foreach (var box in boxes)
            {
                // the first and last stretch leave the own components
                var own = box.plugin == wire.From!.Plugin || box.plugin == wire.To!.Plugin;
                if (own && (i == 0 || i + 2 == route.Count)) continue;
                if (hx > box.x0 && lx < box.x1 && hy > box.y0 && ly < box.y1) cost += 5000;
            }

            foreach (var other in placed)
                for (var j = 0; j + 1 < other.Count; j++)
                {
                    var (r, t) = (other[j], other[j + 1]);
                    var sameVertical = Math.Abs(p.x - q.x) < .5 && Math.Abs(r.x - t.x) < .5 && Math.Abs(p.x - r.x) < 4;
                    var sameHorizontal = Math.Abs(p.y - q.y) < .5 && Math.Abs(r.y - t.y) < .5 && Math.Abs(p.y - r.y) < 4;
                    if (sameVertical && Math.Min(hy, Math.Max(r.y, t.y)) > Math.Max(ly, Math.Min(r.y, t.y))) cost += 300;
                    if (sameHorizontal && Math.Min(hx, Math.Max(r.x, t.x)) > Math.Max(lx, Math.Min(r.x, t.x))) cost += 300;
                }
        }

        return cost;
    }

    private enum Ctx
    {
        None,
        Node,
        Bg,
        Tab
    }
}

using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Context menus of the original CrypTool 2 canvas: component menu (views + z-order) and workspace menu (align / move / spread / size).
public partial class Home
{
    enum Ctx { None, Node, Bg }
    Ctx ctx; double ctxX, ctxY, panDist; PluginNode? ctxNode;

    List<VisualNode> SelNodes => ed.Movable.Where(n => ed.Selection.Contains(n.Id)).ToList();
    static (double w, double h) Dim(VisualNode n) => n is PluginNode p ? Size(p) : (n.Width, n.Height);

    void OpenCtx(MouseEventArgs e, Ctx kind, PluginNode? node)
    {
        if (panDist > 5) return; // right-drag was a pan, not a click
        CommitEdit();
        ctx = kind; ctxNode = node;
        ctxX = Math.Max(0, Math.Min(e.ClientX - rl, rw - 300));
        ctxY = Math.Max(0, Math.Min(e.ClientY - rt, rh - (kind == Ctx.Bg ? 420 : 260)));
    }
    void NodeMenu(MouseEventArgs e, PluginNode p)
    {
        if (!ed.Selection.Contains(p.Id)) { ed.Selection.Clear(); ed.Selection.Add(p.Id); }
        ed.SelectedWire = null; OpenCtx(e, Ctx.Node, p);
    }
    void BgMenu(MouseEventArgs e) => OpenCtx(e, Ctx.Bg, null);
    void CloseCtx() => ctx = Ctx.None;
    void Run(Action a) { a(); ctx = Ctx.None; }

    // ----- component menu -----
    void ShowHelp(PluginNode p) { ed.Selection.Clear(); ed.Selection.Add(p.Id); }
    void ZFront(PluginNode p) { ed.Checkpoint(); ed.BringToFront(p); }
    void ZBack(PluginNode p) { ed.Checkpoint(); ed.SendToBack(p); }
    void ZUp(PluginNode p) { ed.Checkpoint(); ed.Elevate(p, 1); }
    void ZDown(PluginNode p) { ed.Checkpoint(); ed.Elevate(p, -1); }

    // ----- workspace menu -----
    void Align(string how)
    {
        var s = SelNodes; if (s.Count < 2) return; ed.Checkpoint();
        double l = s.Min(n => n.X), t = s.Min(n => n.Y), r = s.Max(n => n.X + Dim(n).w), b = s.Max(n => n.Y + Dim(n).h);
        foreach (var n in s)
        {
            var (w, h) = Dim(n);
            switch (how) { case "left": n.X = l; break; case "right": n.X = r - w; break; case "top": n.Y = t; break; case "bottom": n.Y = b - h; break; }
        }
    }

    void MoveSel(double dx, double dy) { var s = SelNodes; if (s.Count == 0) return; ed.Checkpoint(); foreach (var n in s) { n.X += dx; n.Y += dy; } }

    void Spread(bool horizontal)
    {
        var s = SelNodes; if (s.Count < 3) return; ed.Checkpoint();
        s = horizontal ? s.OrderBy(n => n.X).ToList() : s.OrderBy(n => n.Y).ToList();
        double start = horizontal ? s[0].X : s[0].Y, end = horizontal ? s[^1].X + Dim(s[^1]).w : s[^1].Y + Dim(s[^1]).h;
        double total = s.Sum(n => horizontal ? Dim(n).w : Dim(n).h), gap = (end - start - total) / (s.Count - 1), pos = start;
        foreach (var n in s)
        {
            var (w, h) = Dim(n);
            if (horizontal) { n.X = pos; pos += w + gap; } else { n.Y = pos; pos += h + gap; }
        }
    }

    void SizeAll(bool width, bool biggest)
    {
        var s = SelNodes; if (s.Count < 2) return; ed.Checkpoint();
        var vals = s.Select(n => width ? Dim(n).w : Dim(n).h).ToList(); double v = biggest ? vals.Max() : vals.Min();
        foreach (var n in s)
        {
            if (n is PluginNode p)
            {
                if (p.Minimized) continue;
                var (w, h) = Size(p); p.Width = width ? v : w; p.Height = width ? h : v; p.ViewState = Math.Max(1, p.ViewState);
            }
            else if (width) n.Width = v; else n.Height = v;
        }
    }

    void Rearrange() { ed.Checkpoint(); foreach (var w in ed.Ws.Wires) w.Points.Clear(); }
}

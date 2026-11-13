using System.Text;
using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Wire routing: orthogonal routes the user can drag segment-wise, hops where wires cross, hover focus.
public partial class Home
{
    private Wire? hoverWire;
    private bool segHoriz, segMoved;
    private int segIdx;
    private List<(double x, double y)> segPts = new();
    private double segStart, segOrig;

    // drag state of a wire segment
    private Wire? segWire;

    /// <summary>The wires that stay fully visible while a port or wire is hovered (null = no focus, nothing is faded).</summary>
    private HashSet<Wire>? FocusWires()
    {
        if (mode != Mode.None && mode != Mode.WireSeg) return null;
        if (hoverPort != null && hoverPort.Wires.Count > 0) return hoverPort.Wires.Where(w => w.From != null && w.To != null).ToHashSet();
        if (hoverWire != null) return new HashSet<Wire> { hoverWire };
        return null;
    }

    /// <summary>The hovered port and the ports at the other end of its wires (or both ends of a hovered wire).</summary>
    private bool PortFocused(ConnectorPort c)
    {
        var f = FocusWires();
        return f != null && (c == hoverPort || f.Any(w => w.From == c || w.To == c));
    }

    private static bool NearP((double x, double y) p, (double x, double y) q)
    {
        return Math.Abs(p.x - q.x) < 1 && Math.Abs(p.y - q.y) < 1;
    }

    /// <summary>The full polyline of a wire: the route the user dragged (while still valid), otherwise the automatic one.</summary>
    private static List<(double x, double y)> WirePts(Wire w)
    {
        var a = PortPos(w.From!);
        var b = PortPos(w.To!);
        var dragged = w.Points.Count >= 4 && w.RouteSig != "" && w.RouteSig == CwmWriter.RouteSig(w);
        if (dragged && NearP(w.Points[0], a) && NearP(w.Points[^1], b))
            return w.Points.ToList();
        return AutoRoute(w);
    }

    private static List<VSeg> VerticalSegs(Dictionary<Wire, List<(double x, double y)>> routes)
    {
        var l = new List<VSeg>();
        foreach (var (w, pts) in routes)
            for (var i = 0; i + 1 < pts.Count; i++)
                if (Math.Abs(pts[i].x - pts[i + 1].x) < .01 && Math.Abs(pts[i].y - pts[i + 1].y) > .5)
                    l.Add(new VSeg(w, pts[i].x, Math.Min(pts[i].y, pts[i + 1].y), Math.Max(pts[i].y, pts[i + 1].y)));
        return l;
    }

    /// <summary>SVG path of a route with rounded corners; horizontal runs hop with a small arc over vertical runs of other wires.</summary>
    private static string WirePath(Wire w, List<(double x, double y)> raw, List<VSeg> verticals)
    {
        var pts = raw.ToList();
        RemoveZeroLengthSegments(pts);
        if (pts.Count < 2) return "";
        const double R = 5, corner = 8;
        var sb = new StringBuilder($"M {F(pts[0].x)} {F(pts[0].y)}");
        var cur = pts[0];

        void Line((double x, double y) to)
        {
            if (Math.Abs(cur.y - to.y) < .01 && Math.Abs(to.x - cur.x) > 2 * R + 2)
            {
                double dir = Math.Sign(to.x - cur.x), lo = Math.Min(cur.x, to.x), hi = Math.Max(cur.x, to.x);
                var xs = verticals.Where(v => v.W != w && v.X > lo + R + 1 && v.X < hi - R - 1 && cur.y > v.Y0 + .5 && cur.y < v.Y1 - .5)
                    .Select(v => v.X).Distinct().OrderBy(x => dir * x).ToList();
                var last = double.NegativeInfinity;
                foreach (var x in xs)
                {
                    if (dir * x - last < 2 * R + 1) continue; // overlapping hops: skip
                    sb.Append($" L {F(x - dir * R)} {F(cur.y)} A {F(R)} {F(R)} 0 0 {(dir > 0 ? 1 : 0)} {F(x + dir * R)} {F(cur.y)}");
                    last = dir * x;
                }
            }

            sb.Append($" L {F(to.x)} {F(to.y)}");
            cur = to;
        }

        for (var i = 1; i < pts.Count - 1; i++)
        {
            var p0 = pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            double l1 = Distance(p0, p1), l2 = Distance(p1, p2);
            var r = Math.Min(corner, Math.Min(l1, l2) / 2);
            var a = (x: p1.x - (p1.x - p0.x) / l1 * r, y: p1.y - (p1.y - p0.y) / l1 * r);
            var b = (x: p1.x + (p2.x - p1.x) / l2 * r, y: p1.y + (p2.y - p1.y) / l2 * r);
            Line(a);
            sb.Append($" Q {F(p1.x)} {F(p1.y)} {F(b.x)} {F(b.y)}");
            cur = b;
        }

        Line(pts[^1]);
        return sb.ToString();
    }

    private static bool IsHoriz((double x, double y) p, (double x, double y) q)
    {
        return Math.Abs(p.y - q.y) < .01;
    }

    private void WireSegDown(PointerEventArgs e, Wire w, List<(double x, double y)> pts, int i)
    {
        ed.Selection.Clear();
        ed.SelectedWire = w.Id;
        if (e.Button != 0) return;
        segWire = w;
        segPts = pts.ToList();
        segIdx = i;
        segMoved = false;
        segHoriz = IsHoriz(pts[i], pts[i + 1]);
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        segStart = segHoriz ? wy : wx;
        segOrig = segHoriz ? pts[i].y : pts[i].x;
        mode = Mode.WireSeg;
    }

    private void WireSegMove(PointerEventArgs e)
    {
        if (segWire == null) return;
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        var d = (segHoriz ? wy : wx) - segStart;
        if (!segMoved)
        {
            if (Math.Abs(d) < 3 / zoom) return;
            segMoved = true;
            ed.Checkpoint();
            // a segment collinear with a port stub would turn the stub diagonal: keep the stub's end fixed and add a jog instead
            int n = segPts.Count, i = segIdx;
            if (i + 1 == n - 2 && IsHoriz(segPts[n - 2], segPts[n - 1]) == segHoriz) segPts.Insert(i + 1, segPts[i + 1]);
            if (i == 1 && IsHoriz(segPts[0], segPts[1]) == segHoriz)
            {
                segPts.Insert(1, segPts[1]);
                segIdx = 2;
            }
        }

        var v = e.AltKey ? segOrig + d : Snap(segOrig + d);
        var p = segPts[segIdx];
        var q = segPts[segIdx + 1];
        segPts[segIdx] = segHoriz ? (p.x, v) : (v, p.y);
        segPts[segIdx + 1] = segHoriz ? (q.x, v) : (v, q.y);
        segWire.Points.Clear();
        segWire.Points.AddRange(segPts);
        segWire.RouteSig = CwmWriter.RouteSig(segWire);
    }

    private record struct VSeg(Wire W, double X, double Y0, double Y1);
}

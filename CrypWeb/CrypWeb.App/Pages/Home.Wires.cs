using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Wire routing: orthogonal routes the user can drag segment-wise, hops where wires cross, hover focus.
public partial class Home
{
    record struct VSeg(Wire W, double X, double Y0, double Y1);

    // drag state of a wire segment
    Wire? segWire; List<(double x, double y)> segPts = new(); int segIdx; bool segHoriz, segMoved; double segStart, segOrig;
    Wire? hoverWire;

    /// <summary>The wires that stay fully visible while a port or wire is hovered (null = no focus, nothing is faded).</summary>
    HashSet<Wire>? FocusWires()
    {
        if (mode != Mode.None && mode != Mode.WireSeg) return null;
        if (hoverPort != null && hoverPort.Wires.Count > 0) return hoverPort.Wires.Where(w => w.From != null && w.To != null).ToHashSet();
        if (hoverWire != null) return new HashSet<Wire> { hoverWire };
        return null;
    }

    /// <summary>The hovered port and the ports at the other end of its wires (or both ends of a hovered wire).</summary>
    bool PortFocused(ConnectorPort c)
    {
        var f = FocusWires();
        return f != null && (c == hoverPort || f.Any(w => w.From == c || w.To == c));
    }

    static bool NearP((double x, double y) p, (double x, double y) q) => Math.Abs(p.x - q.x) < 1 && Math.Abs(p.y - q.y) < 1;

    /// <summary>The full polyline of a wire: the route the user dragged (while still valid), otherwise the automatic one.</summary>
    static List<(double x, double y)> WirePts(Wire w)
    {
        var a = PortPos(w.From!); var b = PortPos(w.To!);
        if (w.Points.Count >= 4 && w.RouteSig != "" && w.RouteSig == CwmWriter.RouteSig(w) && NearP(w.Points[0], a) && NearP(w.Points[^1], b))
            return w.Points.ToList();
        var (d0x, d0y) = Away(SideOf(w.From!)); var (d1x, d1y) = Away(SideOf(w.To!));
        const double stub = 18;
        var s1 = (x: a.x + d0x * stub, y: a.y + d0y * stub); var e1 = (x: b.x + d1x * stub, y: b.y + d1y * stub);
        var pts = new List<(double x, double y)> { a, s1 };
        bool h0 = d0x != 0, h1 = d1x != 0;
        if (h0 && h1) { var mx = (s1.x + e1.x) / 2; pts.Add((mx, s1.y)); pts.Add((mx, e1.y)); }
        else if (!h0 && !h1) { var my = (s1.y + e1.y) / 2; pts.Add((s1.x, my)); pts.Add((e1.x, my)); }
        else if (h0) pts.Add((e1.x, s1.y));
        else pts.Add((s1.x, e1.y));
        pts.Add(e1); pts.Add(b);
        return pts;
    }

    static List<VSeg> VerticalSegs(Dictionary<Wire, List<(double x, double y)>> routes)
    {
        var l = new List<VSeg>();
        foreach (var (w, pts) in routes)
            for (int i = 0; i + 1 < pts.Count; i++)
                if (Math.Abs(pts[i].x - pts[i + 1].x) < .01 && Math.Abs(pts[i].y - pts[i + 1].y) > .5)
                    l.Add(new VSeg(w, pts[i].x, Math.Min(pts[i].y, pts[i + 1].y), Math.Max(pts[i].y, pts[i + 1].y)));
        return l;
    }

    /// <summary>SVG path of a route with rounded corners; horizontal runs hop with a small arc over vertical runs of other wires.</summary>
    static string WirePath(Wire w, List<(double x, double y)> raw, List<VSeg> verticals)
    {
        var pts = raw.ToList();
        for (int i = pts.Count - 1; i > 0; i--) if (Math.Abs(pts[i].x - pts[i - 1].x) < .01 && Math.Abs(pts[i].y - pts[i - 1].y) < .01) pts.RemoveAt(i);
        if (pts.Count < 2) return "";
        const double R = 5, corner = 8;
        var sb = new System.Text.StringBuilder($"M {F(pts[0].x)} {F(pts[0].y)}");
        var cur = pts[0];

        void Line((double x, double y) to)
        {
            if (Math.Abs(cur.y - to.y) < .01 && Math.Abs(to.x - cur.x) > 2 * R + 2)
            {
                double dir = Math.Sign(to.x - cur.x), lo = Math.Min(cur.x, to.x), hi = Math.Max(cur.x, to.x);
                var xs = verticals.Where(v => v.W != w && v.X > lo + R + 1 && v.X < hi - R - 1 && cur.y > v.Y0 + .5 && cur.y < v.Y1 - .5)
                                  .Select(v => v.X).Distinct().OrderBy(x => dir * x).ToList();
                double last = double.NegativeInfinity;
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

        for (int i = 1; i < pts.Count - 1; i++)
        {
            var p0 = pts[i - 1]; var p1 = pts[i]; var p2 = pts[i + 1];
            double l1 = Math.Sqrt((p1.x - p0.x) * (p1.x - p0.x) + (p1.y - p0.y) * (p1.y - p0.y)), l2 = Math.Sqrt((p2.x - p1.x) * (p2.x - p1.x) + (p2.y - p1.y) * (p2.y - p1.y));
            double r = Math.Min(corner, Math.Min(l1, l2) / 2);
            var a = (x: p1.x - (p1.x - p0.x) / l1 * r, y: p1.y - (p1.y - p0.y) / l1 * r);
            var b = (x: p1.x + (p2.x - p1.x) / l2 * r, y: p1.y + (p2.y - p1.y) / l2 * r);
            Line(a);
            sb.Append($" Q {F(p1.x)} {F(p1.y)} {F(b.x)} {F(b.y)}"); cur = b;
        }
        Line(pts[^1]);
        return sb.ToString();
    }

    static bool IsHoriz((double x, double y) p, (double x, double y) q) => Math.Abs(p.y - q.y) < .01;

    void WireSegDown(PointerEventArgs e, Wire w, List<(double x, double y)> pts, int i)
    {
        ed.Selection.Clear(); ed.SelectedWire = w.Id;
        if (e.Button != 0) return;
        segWire = w; segPts = pts.ToList(); segIdx = i; segMoved = false;
        segHoriz = IsHoriz(pts[i], pts[i + 1]);
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        segStart = segHoriz ? wy : wx; segOrig = segHoriz ? pts[i].y : pts[i].x;
        mode = Mode.WireSeg;
    }

    void WireSegMove(PointerEventArgs e)
    {
        if (segWire == null) return;
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        double d = (segHoriz ? wy : wx) - segStart;
        if (!segMoved)
        {
            if (Math.Abs(d) < 3 / zoom) return;
            segMoved = true; ed.Checkpoint();
            // a segment collinear with a port stub would turn the stub diagonal: keep the stub's end fixed and add a jog instead
            int n = segPts.Count, i = segIdx;
            if (i + 1 == n - 2 && IsHoriz(segPts[n - 2], segPts[n - 1]) == segHoriz) segPts.Insert(i + 1, segPts[i + 1]);
            if (i == 1 && IsHoriz(segPts[0], segPts[1]) == segHoriz) { segPts.Insert(1, segPts[1]); segIdx = 2; }
        }
        double v = e.AltKey ? segOrig + d : Snap(segOrig + d);
        var p = segPts[segIdx]; var q = segPts[segIdx + 1];
        segPts[segIdx] = segHoriz ? (p.x, v) : (v, p.y);
        segPts[segIdx + 1] = segHoriz ? (q.x, v) : (v, q.y);
        segWire.Points.Clear(); segWire.Points.AddRange(segPts);
        segWire.RouteSig = CwmWriter.RouteSig(segWire);
    }
}

using CrypTool.Web.Core;

namespace CrypWeb.App.Pages;

// Automatic wire routing around the components: orthogonal shortest path on the "Hanan grid" formed by the
// interesting coordinates (connector stubs, component edges and the middle of the gaps between them).
public partial class Home
{
    private readonly record struct Box(double X0, double Y0, double X1, double Y1);

    private static readonly (int dx, int dy)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

    private const double BendCost = 40;
    private const double OverlapCost = 150;
    private const double CrossingCost = 12;

    /// <summary>
    ///     The route of a wire that avoids the boxes: few bends, short, and not on top of the already
    ///     <paramref name="placed" /> wires. Null when there is no way (the caller falls back to the automatic route).
    /// </summary>
    private static List<(double x, double y)>? RouteAround(Wire wire, List<Box> boxes, List<List<(double x, double y)>> placed)
    {
        var start = PortPos(wire.From!);
        var end = PortPos(wire.To!);
        var (startDx, startDy) = Away(SideOf(wire.From!));
        var (endDx, endDy) = Away(SideOf(wire.To!));
        const double stub = 18;
        // rounded like the grid lines, so that the stubs can be found among them
        var stubStart = (x: Math.Round(start.x + startDx * stub, 2), y: Math.Round(start.y + startDy * stub, 2));
        var stubEnd = (x: Math.Round(end.x + endDx * stub, 2), y: Math.Round(end.y + endDy * stub, 2));

        var xs = GridLines(boxes.SelectMany(b => new[] { b.X0, b.X1 }).Append(stubStart.x).Append(stubEnd.x));
        var ys = GridLines(boxes.SelectMany(b => new[] { b.Y0, b.Y1 }).Append(stubStart.y).Append(stubEnd.y));
        int nx = xs.Count, ny = ys.Count;
        int startX = xs.IndexOf(stubStart.x), startY = ys.IndexOf(stubStart.y);
        int goalX = xs.IndexOf(stubEnd.x), goalY = ys.IndexOf(stubEnd.y);
        if (startX < 0 || startY < 0 || goalX < 0 || goalY < 0) return null;

        int State(int ix, int iy, int dir) => (iy * nx + ix) * 4 + dir;
        var cost = new double[nx * ny * 4];
        Array.Fill(cost, double.MaxValue);
        var from = new int[nx * ny * 4];
        Array.Fill(from, -1);

        // direction the wire leaves the first stub, and the one it must arrive with at the last stub
        var firstDir = DirectionIndex(startDx, startDy);
        var lastDir = DirectionIndex(-endDx, -endDy);
        var queue = new PriorityQueue<int, double>();
        var first = State(startX, startY, firstDir);
        cost[first] = 0;
        queue.Enqueue(first, Heuristic(startX, startY));

        double Heuristic(int ix, int iy) => Math.Abs(xs[ix] - stubEnd.x) + Math.Abs(ys[iy] - stubEnd.y);

        var goalState = -1;
        while (queue.TryDequeue(out var current, out _))
        {
            int dir = current % 4, cell = current / 4, ix = cell % nx, iy = cell / nx;
            if (ix == goalX && iy == goalY)
            {
                goalState = current; // the arrival bend is part of the cost, see below
                break;
            }

            for (var d = 0; d < 4; d++)
            {
                if (d == (dir + 2) % 4) continue; // no U-turn
                int jx = ix + Steps[d].dx, jy = iy + Steps[d].dy;
                if (jx < 0 || jy < 0 || jx >= nx || jy >= ny) continue;
                double ax = xs[ix], ay = ys[iy], bx = xs[jx], by = ys[jy];
                if (Blocked(boxes, ax, ay, bx, by)) continue;

                var step = Math.Abs(bx - ax) + Math.Abs(by - ay);
                var next = cost[current] + step + (d != dir ? BendCost : 0) + Crowding(placed, ax, ay, bx, by);
                if (jx == goalX && jy == goalY && d != lastDir) next += BendCost;
                var s = State(jx, jy, d);
                if (next >= cost[s]) continue;
                cost[s] = next;
                from[s] = current;
                queue.Enqueue(s, next + Heuristic(jx, jy));
            }
        }

        if (goalState < 0) return null;

        var points = new List<(double x, double y)> { end };
        for (var s = goalState; s >= 0; s = from[s])
        {
            var cell = s / 4;
            points.Add((xs[cell % nx], ys[cell / nx]));
        }

        points.Add(start);
        points.Reverse();
        RemoveZeroLengthSegments(points);
        RemoveCollinearPoints(points);
        return points;
    }

    /// <summary>A route must start and end at the connectors and consist of horizontal and vertical segments only.</summary>
    private static bool IsValidRoute(Wire wire, List<(double x, double y)> route)
    {
        if (route.Count < 2 || !NearP(route[0], PortPos(wire.From!)) || !NearP(route[^1], PortPos(wire.To!))) return false;
        for (var i = 0; i + 1 < route.Count; i++)
            if (Math.Abs(route[i].x - route[i + 1].x) > .01 && Math.Abs(route[i].y - route[i + 1].y) > .01)
                return false;
        return true;
    }

    private static int DirectionIndex(double dx, double dy)
    {
        for (var i = 0; i < 4; i++)
            if (Steps[i].dx == Math.Sign(dx) && Steps[i].dy == Math.Sign(dy))
                return i;
        return 0;
    }

    /// <summary>Sorted distinct coordinates plus the middle of every gap wider than 30 (room for a wire between components).</summary>
    private static List<double> GridLines(IEnumerable<double> values)
    {
        var sorted = values.Select(v => Math.Round(v, 2)).Distinct().OrderBy(v => v).ToList();
        var result = new List<double>(sorted);
        for (var i = 0; i + 1 < sorted.Count; i++)
            if (sorted[i + 1] - sorted[i] > 30)
                result.Add(Math.Round((sorted[i] + sorted[i + 1]) / 2, 2));
        result.Sort();
        return result;
    }

    /// <summary>Whether the straight segment runs through the inside of a box.</summary>
    private static bool Blocked(List<Box> boxes, double ax, double ay, double bx, double by)
    {
        double lx = Math.Min(ax, bx), hx = Math.Max(ax, bx), ly = Math.Min(ay, by), hy = Math.Max(ay, by);
        foreach (var b in boxes)
        {
            if (hx > b.X0 && lx < b.X1 && hy > b.Y0 && ly < b.Y1)
            {
                // a segment lying exactly on an edge does not enter the box
                var onEdgeX = Math.Abs(ax - bx) < .01 && (Math.Abs(ax - b.X0) < .01 || Math.Abs(ax - b.X1) < .01);
                var onEdgeY = Math.Abs(ay - by) < .01 && (Math.Abs(ay - b.Y0) < .01 || Math.Abs(ay - b.Y1) < .01);
                if (!onEdgeX && !onEdgeY) return true;
            }
        }

        return false;
    }

    /// <summary>Extra cost of a segment for running on top of (overlap) or across (crossing) wires that are already placed.</summary>
    private static double Crowding(List<List<(double x, double y)>> placed, double ax, double ay, double bx, double by)
    {
        var extra = 0.0;
        var vertical = Math.Abs(ax - bx) < .01;
        double lx = Math.Min(ax, bx), hx = Math.Max(ax, bx), ly = Math.Min(ay, by), hy = Math.Max(ay, by);
        foreach (var route in placed)
            for (var i = 0; i + 1 < route.Count; i++)
            {
                var (p, q) = (route[i], route[i + 1]);
                var otherVertical = Math.Abs(p.x - q.x) < .01;
                double plx = Math.Min(p.x, q.x), phx = Math.Max(p.x, q.x), ply = Math.Min(p.y, q.y), phy = Math.Max(p.y, q.y);
                if (vertical == otherVertical)
                {
                    var sameLine = vertical ? Math.Abs(ax - p.x) < 4 : Math.Abs(ay - p.y) < 4;
                    var overlap = vertical ? Math.Min(hy, phy) - Math.Max(ly, ply) : Math.Min(hx, phx) - Math.Max(lx, plx);
                    if (sameLine && overlap > 1) extra += OverlapCost;
                }
                else if (hx > plx && lx < phx && hy > ply && ly < phy)
                {
                    extra += CrossingCost;
                }
            }

        return extra;
    }

    private static void RemoveCollinearPoints(List<(double x, double y)> points)
    {
        for (var i = points.Count - 2; i > 0; i--)
        {
            var sameX = Math.Abs(points[i - 1].x - points[i].x) < .01 && Math.Abs(points[i].x - points[i + 1].x) < .01;
            var sameY = Math.Abs(points[i - 1].y - points[i].y) < .01 && Math.Abs(points[i].y - points[i + 1].y) < .01;
            if (sameX || sameY) points.RemoveAt(i);
        }
    }
}

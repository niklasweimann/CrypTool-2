using System.Globalization;
using System.Net;
using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components;

namespace CrypWeb.App.Pages;

/// <summary>Canvas geometry: sizes, port positions, SVG path helpers.</summary>
public partial class Home
{
    private (double x, double y) ToWorld(double clientX, double clientY)
    {
        return ((clientX - rl - panX) / zoom, (clientY - rt - panY) / zoom);
    }

    private double Snap(double v)
    {
        return snap ? Math.Round(v / 10) * 10 : v;
    }

    private static string F(double d)
    {
        return d.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static MarkupString Txt(double x, double y, string cls, string anchor, string text)
    {
        return new MarkupString($"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"{cls}\" text-anchor=\"{anchor}\">{WebUtility.HtmlEncode(text)}</text>");
    }

    private static int SideOf(ConnectorPort c)
    {
        return c.Orientation is >= 0 and <= 3 ? c.Orientation : (c.Outgoing ? 3 : 2);
        // 0 N, 1 S, 2 W, 3 E
    }

    private static IEnumerable<ConnectorPort> OnSide(PluginNode p, int side)
    {
        return p.Inputs.Concat(p.Outputs).Where(c => SideOf(c) == side);
    }

    private static (double w, double h) Size(PluginNode p)
    {
        if (p.Minimized) return (p.ViewState != 0 && p.Width >= 60 ? p.Width : AutoW(p), 22);
        if (p.ViewState != 0 && p.Width >= 60 && p.Height >= 40)
        {
            var min = MinSize(p);
            return (Math.Max(p.Width, min.Width), Math.Max(p.Height, min.Height));
        }

        return (AutoW(p), AutoH(p));
    }

    /// <summary>A component showing only its icon has a fixed size, as in CT2 (80x80 there; the header needs a bit more here).</summary>
    private static bool IconOnly(PluginNode p)
    {
        return !IsNative(p) && p.ViewState is 0 or 7;
    }

    private static bool CanResize(PluginNode p)
    {
        return !IconOnly(p);
    }

    /// <summary>
    ///     Smallest size of a component. CT2 (ComponentVisual.xaml) allows 200x150 for a component with an open view;
    ///     the room for the toolbar and the connectors is a lower limit in any case. Limits that a presentation sets
    ///     itself (MinWidth/MaxWidth of the WPF control) are not known in the browser.
    /// </summary>
    private static (double Width, double Height) MinSize(PluginNode p)
    {
        var (w, h) = IsNative(p) ? (200d, 120d) : IconOnly(p) ? (104d, 80d) : (200d, 150d);
        return (Math.Max(w, 30 + Math.Max(OnSide(p, 0).Count(), OnSide(p, 1).Count()) * 26),
            Math.Max(h, 48 + Math.Max(OnSide(p, 2).Count(), OnSide(p, 3).Count()) * 26));
    }

    private static bool IsNative(PluginNode p)
    {
        return NativeComponent.TryCreate(p.TypeName) != null;
    }

    private static double AutoW(PluginNode p)
    {
        return Math.Max(IsNative(p) ? 220 : 104, 30 + Math.Max(OnSide(p, 0).Count(), OnSide(p, 1).Count()) * 26);
    }

    private static double AutoH(PluginNode p)
    {
        return Math.Max(IsNative(p) ? 110 : 92, 48 + Math.Max(OnSide(p, 2).Count(), OnSide(p, 3).Count()) * 26);
    }

    private static (double x, double y) PortPos(ConnectorPort c)
    {
        var p = c.Plugin!;
        var (w, h) = Size(p);
        var side = SideOf(c);
        var list = OnSide(p, side).ToList();
        int n = list.Count, i = Math.Max(0, list.IndexOf(c));
        var off = (i - (n - 1) / 2.0) * 26;
        double top = p.Y + 22, bodyH = Math.Max(0, h - 40);
        return side switch
        {
            0 => (p.X + w / 2 + off, p.Y),
            1 => (p.X + w / 2 + off, p.Y + h),
            2 => (p.X, p.Minimized ? p.Y + 11 : top + bodyH / 2 + off),
            _ => (p.X + w, p.Minimized ? p.Y + 11 : top + bodyH / 2 + off)
        };
    }

    private static (double dx, double dy) Away(int side)
    {
        return side switch { 0 => (0, -1), 1 => (0, 1), 2 => (-1, 0), _ => (1, 0) };
    }

    private static string Tri(ConnectorPort c, double x, double y, bool big)
    {
        var r = big ? 8 : 6.5;
        var (ax, ay) = Away(SideOf(c));
        var (dx, dy) = c.Outgoing ? (ax, ay) : (-ax, -ay); // direction the triangle points
        double px = -dy, py = dx; // perpendicular

        string P(double a, double b)
        {
            return $"{F(x + dx * a + px * b)} {F(y + dy * a + py * b)}";
        }

        return $"M {P(-r, -r)} L {P(r, 0)} L {P(-r, r)} Z";
    }

    private static string WarnPath(double x, double y)
    {
        return $"M {F(x)} {F(y - 8)} L {F(x + 9)} {F(y + 7)} L {F(x - 9)} {F(y + 7)} Z";
    }

    /// <summary>
    ///     The automatic route of a wire as a polyline: leaves each port straight for a short stub, then runs
    ///     orthogonally (horizontal/vertical depending on the port sides) to the other stub.
    /// </summary>
    private static List<(double x, double y)> AutoRoute(Wire wire)
    {
        var start = PortPos(wire.From!);
        var end = PortPos(wire.To!);
        var (startDx, startDy) = Away(SideOf(wire.From!));
        var (endDx, endDy) = Away(SideOf(wire.To!));

        const double stub = 18;
        var afterStart = (x: start.x + startDx * stub, y: start.y + startDy * stub);
        var beforeEnd = (x: end.x + endDx * stub, y: end.y + endDy * stub);

        var points = new List<(double x, double y)> { start, afterStart };
        bool startHorizontal = startDx != 0, endHorizontal = endDx != 0;
        if (startHorizontal && endHorizontal)
        {
            var midX = (afterStart.x + beforeEnd.x) / 2;
            points.Add((midX, afterStart.y));
            points.Add((midX, beforeEnd.y));
        }
        else if (!startHorizontal && !endHorizontal)
        {
            var midY = (afterStart.y + beforeEnd.y) / 2;
            points.Add((afterStart.x, midY));
            points.Add((beforeEnd.x, midY));
        }
        else if (startHorizontal)
        {
            points.Add((beforeEnd.x, afterStart.y));
        }
        else
        {
            points.Add((afterStart.x, beforeEnd.y));
        }

        points.Add(beforeEnd);
        points.Add(end);
        return points;
    }

    private static void RemoveZeroLengthSegments(List<(double x, double y)> points)
    {
        for (var i = points.Count - 1; i > 0; i--)
            if (Math.Abs(points[i].x - points[i - 1].x) < .01 && Math.Abs(points[i].y - points[i - 1].y) < .01)
                points.RemoveAt(i);
    }

    private static double Distance((double x, double y) a, (double x, double y) b)
    {
        return Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
    }

    /// <summary>Word wrap by an estimated character width (~6.3 px per character); keeps empty lines.</summary>
    private static IEnumerable<string> Wrap(string text, double width)
    {
        var max = Math.Max(8, (int)(width / 6.3));
        foreach (var para in (text ?? "").Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
                if (line.Length + word.Length + 1 > max && line.Length > 0)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = line.Length == 0 ? word : line + " " + word;
                }

            yield return line;
        }
    }
}

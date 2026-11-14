using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

/// <summary>
///     Overview map in the bottom right corner of the canvas (View › Minimap): the whole workspace with the visible
///     part as a frame. Clicking or dragging in it moves the view there.
/// </summary>
public partial class Home
{
    private const double MinimapWidth = 200, MinimapHeight = 140, MinimapMargin = 10, MinimapPadding = 40;

    private bool showMinimap
    {
        get => settings.ShowMinimap;
        set { settings.ShowMinimap = value; _ = settings.SaveAsync(Js); }
    }

    private bool MinimapVisible => showMinimap && active.Kind == DocKind.Workspace && ed.Ws.Plugins.Count + ed.Ws.Texts.Count + ed.Ws.Images.Count > 0;

    /// <summary>The visible part of the workspace in world coordinates.</summary>
    private (double X, double Y, double W, double H) Viewport => (-panX / zoom, -panY / zoom, rw / zoom, rh / zoom);

    /// <summary>What the map shows (world coordinates): the content and the visible part, with some room around.</summary>
    private (double X, double Y, double W, double H) MinimapBounds()
    {
        var boxes = ed.Ws.Plugins.Select(p => (p.X, p.Y, Size(p).w, Size(p).h))
            .Concat(ed.Ws.Texts.Select(t => (t.X, t.Y, t.Width, t.Height)))
            .Concat(ed.Ws.Images.Select(i => (i.X, i.Y, i.Width, i.Height)))
            .Append(Viewport)
            .ToList();
        var left = boxes.Min(b => b.Item1) - MinimapPadding;
        var top = boxes.Min(b => b.Item2) - MinimapPadding;
        var right = boxes.Max(b => b.Item1 + b.Item3) + MinimapPadding;
        var bottom = boxes.Max(b => b.Item2 + b.Item4) + MinimapPadding;
        // the map keeps the aspect ratio of its box: widen the shorter side
        double width = right - left, height = bottom - top;
        var ratio = MinimapWidth / MinimapHeight;
        if (width / height < ratio)
        {
            var extra = height * ratio - width;
            left -= extra / 2;
            width += extra;
        }
        else
        {
            var extra = width / ratio - height;
            top -= extra / 2;
            height += extra;
        }

        return (left, top, width, height);
    }

    private string MinimapViewBox()
    {
        var b = MinimapBounds();
        return $"{F(b.X)} {F(b.Y)} {F(b.W)} {F(b.H)}";
    }

    /// <summary>Bounds of the map while it is dragged: kept, so the map does not change under the pointer.</summary>
    private (double X, double Y, double W, double H)? minimapDragBounds;

    private void MinimapDown(PointerEventArgs e)
    {
        if (e.Button != 0) return;
        ctx = Ctx.None;
        minimapDragBounds = MinimapBounds();
        mode = Mode.Minimap;
        MinimapPan(e);
    }

    /// <summary>Centres the view on the world point under the pointer in the map.</summary>
    private void MinimapPan(PointerEventArgs e)
    {
        var b = minimapDragBounds ?? MinimapBounds();
        var mapLeft = rl + rw - MinimapMargin - MinimapWidth;
        var mapTop = rt + rh - MinimapMargin - MinimapHeight;
        var scale = MinimapWidth / b.W;
        var wx = b.X + (e.ClientX - mapLeft) / scale;
        var wy = b.Y + (e.ClientY - mapTop) / scale;
        panX = rw / 2 - wx * zoom;
        panY = rh / 2 - wy * zoom;
    }
}

using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

/// <summary>Visible part of the canvas: fit to window, zoom.</summary>
public partial class Home
{
    private const double MinZoom = 0.05;
    private const double MaxZoom = 4;

    /// <summary>Re-reads the on-screen size of the canvas (it changes with the window and the open panels).</summary>
    private async Task MeasureCanvasAsync()
    {
        try
        {
            var rect = await Js.InvokeAsync<double[]>("ct2.rect", "main svg");
            (rl, rt, rw, rh) = (rect[0], rect[1], rect[2], rect[3]);
        }
        catch (JSException)
        {
            /* canvas not in the DOM: keep the old size */
        }
    }

    /// <summary>Zooms and pans so that the whole workspace is visible (but not larger than 120 %).</summary>
    private async Task FitAsync()
    {
        await MeasureCanvasAsync();
        var ws = ed.Ws;
        if (ws.Plugins.Count + ws.Texts.Count == 0)
        {
            panX = panY = 40;
            zoom = 1;
            return;
        }

        var left = ws.Plugins.Select(p => p.X).Concat(ws.Texts.Select(t => t.X)).Min();
        var top = ws.Plugins.Select(p => p.Y).Concat(ws.Texts.Select(t => t.Y)).Min();
        var right = ws.Plugins.Select(p => p.X + Size(p).w).Concat(ws.Texts.Select(t => t.X + t.Width)).Max();
        var bottom = ws.Plugins.Select(p => p.Y + Size(p).h).Concat(ws.Texts.Select(t => t.Y + t.Height)).Max();
        double width = right - left, height = bottom - top;

        const double margin = 80, marginVertical = 60; // keeps the content off the edges
        zoom = Math.Clamp(Math.Min(1.2, Math.Min((rw - margin) / width, (rh - marginVertical) / height)), MinZoom, MaxZoom);
        panX = (rw - width * zoom) / 2 - left * zoom;
        panY = (rh - height * zoom) / 2 - top * zoom;
    }

    /// <summary>Multiplies the zoom by <paramref name="factor" /> while the point (px, py) (canvas pixels) stays where it is.</summary>
    private void ZoomAt(double factor, double px, double py)
    {
        var newZoom = Math.Clamp(zoom * factor, MinZoom, MaxZoom);
        panX = px - (px - panX) * newZoom / zoom;
        panY = py - (py - panY) * newZoom / zoom;
        zoom = newZoom;
    }

    private void ZoomBy(double factor)
    {
        ZoomAt(factor, rw / 2, rh / 2);
    }

    private void Zoom100()
    {
        ZoomAt(1 / zoom, rw / 2, rh / 2);
    }

    private void Wheel(WheelEventArgs e)
    {
        ZoomAt(e.DeltaY < 0 ? 1.1 : 1 / 1.1, e.ClientX - rl, e.ClientY - rt);
    }
}

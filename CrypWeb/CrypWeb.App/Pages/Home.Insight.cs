using System.Globalization;

namespace CrypWeb.App.Pages;

/// <summary>
///     Executions and time per component (View › Runtime).
/// </summary>
public partial class Home
{
    private bool showTimings
    {
        get => settings.ShowTimings;
        set { settings.ShowTimings = value; _ = settings.SaveAsync(Js); }
    }

    /// <summary>"3× · 12 ms" below a component that has executed in this run.</summary>
    private static string TimingText(NodeView view)
    {
        var total = view.TotalMs >= 10_000
            ? (view.TotalMs / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " s"
            : view.TotalMs + " ms";
        return view.Runs + "× · " + total;
    }

    /// <summary>Share of the slowest component's total time (0..1): the badge goes from green to red.</summary>
    private double TimingShare(NodeView view)
    {
        var max = engine.Nodes.Values.Max(n => n.TotalMs);
        return max <= 0 ? 0 : (double)view.TotalMs / max;
    }

    private static string TimingColor(double share)
    {
        // hue 130 (green) for the fast components, 0 (red) for the slowest one
        var hue = (int)Math.Round(130 * (1 - share));
        return $"hsl({hue},70%,42%)";
    }
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Media;

namespace System.Windows.Rendering
{
    /// <summary>Formats WPF values as CSS/SVG text.</summary>
    internal static class CssValues
    {
        public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static string Pixels(double value) => Number(value) + "px";

        public static string Escape(string text) => WebUtility.HtmlEncode(text ?? "");

        public static string Thickness(Thickness t)
        {
            return Pixels(t.Top) + " " + Pixels(t.Right) + " " + Pixels(t.Bottom) + " " + Pixels(t.Left);
        }

        public static string Color(Color color, double opacity = 1)
        {
            double alpha = color.A / 255.0 * opacity;
            return alpha >= 1
                ? "rgb(" + color.R + "," + color.G + "," + color.B + ")"
                : "rgba(" + color.R + "," + color.G + "," + color.B + "," + Number(alpha) + ")";
        }

        public static string Matrix(Matrix m)
        {
            return "matrix(" + Number(m.M11) + "," + Number(m.M12) + "," + Number(m.M21) + "," + Number(m.M22) + "," + Number(m.OffsetX) + "," + Number(m.OffsetY) + ")";
        }

        /// <summary>A brush as CSS <c>background</c> value; null when the brush draws nothing.</summary>
        public static string Background(Brush brush, Func<string, string> resolveImage)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return Color(solid.Color, solid.Opacity);

                case LinearGradientBrush linear:
                    double dx = linear.EndPoint.X - linear.StartPoint.X;
                    double dy = linear.EndPoint.Y - linear.StartPoint.Y;
                    double angle = Math.Atan2(dx, -dy) * 180 / Math.PI;
                    return "linear-gradient(" + Number(angle) + "deg," + Stops(linear) + ")";

                case RadialGradientBrush radial:
                    return "radial-gradient(ellipse " + Number(radial.RadiusX * 100) + "% " + Number(radial.RadiusY * 100) + "% at "
                        + Number(radial.GradientOrigin.X * 100) + "% " + Number(radial.GradientOrigin.Y * 100) + "%," + Stops(radial) + ")";

                case ImageBrush image when image.ImageSource?.Address != null:
                    string size = image.Stretch switch
                    {
                        Stretch.Uniform => "contain",
                        Stretch.UniformToFill => "cover",
                        Stretch.None => "auto",
                        _ => "100% 100%",
                    };
                    return "url('" + Escape(resolveImage(image.ImageSource.Address)) + "') center/" + size + " no-repeat";

                default:
                    return null;
            }
        }

        /// <summary>The single colour that stands for a brush where gradients are not possible (text, borders).</summary>
        public static string SolidColor(Brush brush)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return Color(solid.Color, solid.Opacity);
                case GradientBrush gradient when gradient.GradientStops.Count > 0:
                    return Color(gradient.GradientStops[0].Color, gradient.Opacity);
                default:
                    return null;
            }
        }

        private static string Stops(GradientBrush brush)
        {
            var stops = new StringBuilder();
            foreach (GradientStop stop in brush.GradientStops)
            {
                if (stops.Length > 0)
                {
                    stops.Append(',');
                }
                stops.Append(Color(stop.Color, brush.Opacity)).Append(' ').Append(Number(stop.Offset * 100)).Append('%');
            }
            return stops.ToString();
        }
    }

    /// <summary>Collects the CSS declarations of one element.</summary>
    internal sealed class StyleBuilder
    {
        private readonly StringBuilder _css = new StringBuilder();

        public StyleBuilder Add(string property, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _css.Append(property).Append(':').Append(value).Append(';');
            }
            return this;
        }

        public StyleBuilder AddPixels(string property, double value)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                _css.Append(property).Append(':').Append(CssValues.Pixels(value)).Append(';');
            }
            return this;
        }

        public override string ToString() => _css.ToString();
    }
}

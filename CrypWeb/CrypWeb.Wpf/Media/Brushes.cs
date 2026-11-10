using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Animation;

namespace System.Windows
{
    /// <summary>Base of shared objects (brushes, transforms, geometries). Nothing is ever frozen here.</summary>
    public abstract class Freezable : DependencyObject
    {
        public bool IsFrozen => false;

        public bool CanFreeze => true;

        public void Freeze() { }

        public Freezable Clone() => (Freezable)MemberwiseClone();

        public Freezable CloneCurrentValue() => Clone();

        public Freezable GetAsFrozen() => this;

        public event EventHandler Changed;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

namespace System.Windows.Media
{
    public enum Stretch { None, Fill, Uniform, UniformToFill }

    public enum PenLineCap { Flat, Square, Round, Triangle }

    public enum PenLineJoin { Miter, Bevel, Round }

    public enum FillRule { EvenOdd, Nonzero }

    public enum SweepDirection { Counterclockwise, Clockwise }

    public enum GradientSpreadMethod { Pad, Reflect, Repeat }

    public enum BrushMappingMode { Absolute, RelativeToBoundingBox }

    public struct Color : IEquatable<Color>
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color { A = a, R = r, G = g, B = b };

        public static Color FromRgb(byte r, byte g, byte b) => FromArgb(255, r, g, b);

        public static Color FromScRgb(float a, float r, float g, float b)
        {
            static byte Channel(float value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
            return FromArgb(Channel(a), Channel(r), Channel(g), Channel(b));
        }

        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;

        public static bool operator ==(Color a, Color b) => a.Equals(b);

        public static bool operator !=(Color a, Color b) => !a.Equals(b);

        /// <summary>"#AARRGGBB", as in WPF.</summary>
        public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>Parses "#RGB", "#ARGB", "#RRGGBB", "#AARRGGBB" or a colour name.</summary>
        public static bool TryParse(string text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            text = text.Trim();
            if (text[0] != '#')
            {
                return Colors.ByName.TryGetValue(text, out color);
            }

            string hex = text.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                var doubled = new Text.StringBuilder();
                foreach (char c in hex)
                {
                    doubled.Append(c).Append(c);
                }
                hex = doubled.ToString();
            }
            if (hex.Length == 6)
            {
                hex = "FF" + hex;
            }
            if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            {
                return false;
            }
            color = FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            return true;
        }
    }

    public class ColorConverter
    {
        public static object ConvertFromString(string text) => Color.TryParse(text, out Color color) ? color : (object)null;
    }

    public abstract class Brush : Animatable
    {
        public static readonly DependencyProperty OpacityProperty = Dp.Register<double, Brush>(nameof(Opacity), 1.0);
        public static readonly DependencyProperty TransformProperty = Dp.Register<Transform, Brush>(nameof(Transform));
        public static readonly DependencyProperty RelativeTransformProperty = Dp.Register<Transform, Brush>(nameof(RelativeTransform));

        public double Opacity { get => (double)GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
        public Transform Transform { get => (Transform)GetValue(TransformProperty); set => SetValue(TransformProperty, value); }
        public Transform RelativeTransform { get => (Transform)GetValue(RelativeTransformProperty); set => SetValue(RelativeTransformProperty, value); }

        public new Brush Clone() => (Brush)MemberwiseClone();
    }

    public sealed class SolidColorBrush : Brush
    {
        public static readonly DependencyProperty ColorProperty = Dp.Register<Color, SolidColorBrush>(nameof(Color));

        public SolidColorBrush() { }

        public SolidColorBrush(Color color) => Color = color;

        public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        public override string ToString() => Color.ToString();
    }

    public class GradientStop : Animatable
    {
        public static readonly DependencyProperty ColorProperty = Dp.Register<Color, GradientStop>(nameof(Color));
        public static readonly DependencyProperty OffsetProperty = Dp.Register<double, GradientStop>(nameof(Offset));

        public GradientStop() { }

        public GradientStop(Color color, double offset)
        {
            Color = color;
            Offset = offset;
        }

        public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
        public double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
    }

    public class GradientStopCollection : Collection<GradientStop>
    {
        protected override void InsertItem(int index, GradientStop item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    [Markup.ContentProperty(nameof(GradientStops))]
    public abstract class GradientBrush : Brush
    {
        public GradientStopCollection GradientStops { get; set; } = new GradientStopCollection();
        public GradientSpreadMethod SpreadMethod { get; set; }
        public BrushMappingMode MappingMode { get; set; } = BrushMappingMode.RelativeToBoundingBox;
    }

    public sealed class LinearGradientBrush : GradientBrush
    {
        public LinearGradientBrush() { }

        public LinearGradientBrush(Color startColor, Color endColor, double angle)
        {
            double radians = angle * Math.PI / 180;
            EndPoint = new Point(Math.Cos(radians), Math.Sin(radians));
            GradientStops.Add(new GradientStop(startColor, 0));
            GradientStops.Add(new GradientStop(endColor, 1));
        }

        public LinearGradientBrush(Color startColor, Color endColor, Point startPoint, Point endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops.Add(new GradientStop(startColor, 0));
            GradientStops.Add(new GradientStop(endColor, 1));
        }

        public LinearGradientBrush(GradientStopCollection gradientStops) => GradientStops = gradientStops;

        public LinearGradientBrush(GradientStopCollection gradientStops, double angle) : this(Colors.Black, Colors.Black, angle)
        {
            GradientStops = gradientStops;
        }

        public LinearGradientBrush(GradientStopCollection gradientStops, Point startPoint, Point endPoint)
        {
            GradientStops = gradientStops;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public Point StartPoint { get; set; } = new Point(0, 0);
        public Point EndPoint { get; set; } = new Point(1, 1);
    }

    public sealed class RadialGradientBrush : GradientBrush
    {
        public RadialGradientBrush() { }

        public RadialGradientBrush(Color startColor, Color endColor)
        {
            GradientStops.Add(new GradientStop(startColor, 0));
            GradientStops.Add(new GradientStop(endColor, 1));
        }

        public RadialGradientBrush(GradientStopCollection gradientStops) => GradientStops = gradientStops;

        public Point GradientOrigin { get; set; } = new Point(0.5, 0.5);
        public Point Center { get; set; } = new Point(0.5, 0.5);
        public double RadiusX { get; set; } = 0.5;
        public double RadiusY { get; set; } = 0.5;
    }

    public abstract class TileBrush : Brush
    {
        public Stretch Stretch { get; set; } = Stretch.Fill;
        public Rect Viewport { get; set; }
        public Rect Viewbox { get; set; }
    }

    public sealed class ImageBrush : TileBrush
    {
        public ImageBrush() { }

        public ImageBrush(ImageSource image) => ImageSource = image;

        public ImageSource ImageSource { get; set; }
    }

    /// <summary>Paints with a copy of another element. Not supported in the browser: drawn as nothing.</summary>
    public sealed class VisualBrush : TileBrush
    {
        public VisualBrush() { }

        public VisualBrush(Visual visual) => Visual = visual;

        public Visual Visual { get; set; }
    }

    public class BrushConverter
    {
        public object ConvertFromString(string text) => Color.TryParse(text, out Color color) ? new SolidColorBrush(color) : null;

        public object ConvertFrom(object value) => ConvertFromString(value?.ToString());
    }

    public sealed class Pen : Animatable
    {
        public Pen() { }

        public Pen(Brush brush, double thickness)
        {
            Brush = brush;
            Thickness = thickness;
        }

        public Brush Brush { get; set; }
        public double Thickness { get; set; } = 1;
        public DashStyle DashStyle { get; set; }
        public PenLineCap StartLineCap { get; set; }
        public PenLineCap EndLineCap { get; set; }
        public PenLineCap DashCap { get; set; }
        public PenLineJoin LineJoin { get; set; }
    }

    public sealed class DashStyle
    {
        public DashStyle() { }

        public DashStyle(Collections.Generic.IEnumerable<double> dashes, double offset)
        {
            Dashes = new DoubleCollection(dashes);
            Offset = offset;
        }

        public DoubleCollection Dashes { get; set; } = new DoubleCollection();
        public double Offset { get; set; }
    }

    public static class DashStyles
    {
        public static DashStyle Solid => new DashStyle();
        public static DashStyle Dash => new DashStyle(new double[] { 2, 2 }, 1);
        public static DashStyle Dot => new DashStyle(new double[] { 0, 2 }, 0);
        public static DashStyle DashDot => new DashStyle(new double[] { 2, 2, 0, 2 }, 1);
    }

    public class DoubleCollection : Collection<double>
    {
        public DoubleCollection() { }

        public DoubleCollection(Collections.Generic.IEnumerable<double> values)
        {
            foreach (double value in values)
            {
                Add(value);
            }
        }

        public static DoubleCollection Parse(string text)
        {
            var result = new DoubleCollection();
            foreach (string part in text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(double.Parse(part, CultureInfo.InvariantCulture));
            }
            return result;
        }
    }

    public class PointCollection : Collection<Point>
    {
        public PointCollection() { }

        public PointCollection(Collections.Generic.IEnumerable<Point> points)
        {
            foreach (Point point in points)
            {
                Add(point);
            }
        }

        protected override void InsertItem(int index, Point item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }

        public static PointCollection Parse(string text)
        {
            var result = new PointCollection();
            string[] parts = text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                result.Add(new Point(double.Parse(parts[i], CultureInfo.InvariantCulture), double.Parse(parts[i + 1], CultureInfo.InvariantCulture)));
            }
            return result;
        }
    }
}

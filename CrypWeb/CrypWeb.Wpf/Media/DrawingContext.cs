using System.Collections.Generic;
using System.Globalization;

namespace System.Windows.Media
{
    public class Typeface
    {
        public Typeface(string typefaceName) => FontFamily = new FontFamily(typefaceName);

        public Typeface(FontFamily fontFamily, FontStyle style, FontWeight weight, FontStretch stretch)
        {
            FontFamily = fontFamily;
            Style = style;
            Weight = weight;
        }

        public FontFamily FontFamily { get; }
        public FontStyle Style { get; }
        public FontWeight Weight { get; }
    }

    /// <summary>Text prepared for <see cref="DrawingContext.DrawText"/>. Sizes are estimates (the browser measures text).</summary>
    public class FormattedText
    {
        public FormattedText(string textToFormat, CultureInfo culture, FlowDirection flowDirection, Typeface typeface, double emSize, Brush foreground)
        {
            Text = textToFormat ?? "";
            Typeface = typeface;
            EmSize = emSize;
            Foreground = foreground;
        }

        public FormattedText(string textToFormat, CultureInfo culture, FlowDirection flowDirection, Typeface typeface, double emSize, Brush foreground, double pixelsPerDip)
            : this(textToFormat, culture, flowDirection, typeface, emSize, foreground) { }

        public string Text { get; }
        public Typeface Typeface { get; }
        public double EmSize { get; }
        public Brush Foreground { get; }
        public TextAlignment TextAlignment { get; set; }
        public double MaxTextWidth { get; set; }
        public double MaxTextHeight { get; set; }

        /// <summary>Estimated from the font size: about 0.55 em per character.</summary>
        public double Width => Text.Length * EmSize * 0.55;

        public double WidthIncludingTrailingWhitespace => Width;

        public double Height => EmSize * 1.33;

        public double Baseline => EmSize;

        public void SetFontWeight(FontWeight weight) { }

        public void SetFontSize(double emSize) { }
    }

    /// <summary>What kind of step a recorded drawing command is.</summary>
    internal enum DrawingCommandKind { Geometry, Text, Image, PushTransform, PushOpacity, PushClip, Pop }

    /// <summary>One recorded call on a <see cref="DrawingContext"/>.</summary>
    internal sealed class DrawingCommand
    {
        public DrawingCommandKind Kind;
        public Brush Brush;
        public Pen Pen;
        public Geometry Geometry;
        public FormattedText Text;
        public ImageSource Image;
        public Point Origin;
        public Rect Rectangle;
        public Transform Transform;
        public double Opacity = 1;
    }

    /// <summary>
    /// Target of custom drawing code (<c>OnRender</c>). The calls are recorded and later written as SVG.
    /// </summary>
    public class DrawingContext : IDisposable
    {
        internal List<DrawingCommand> Commands { get; } = new List<DrawingCommand>();

        public void DrawLine(Pen pen, Point point0, Point point1)
        {
            DrawGeometry(null, pen, new LineGeometry(point0, point1));
        }

        public void DrawRectangle(Brush brush, Pen pen, Rect rectangle)
        {
            DrawGeometry(brush, pen, new RectangleGeometry(rectangle));
        }

        public void DrawRoundedRectangle(Brush brush, Pen pen, Rect rectangle, double radiusX, double radiusY)
        {
            DrawGeometry(brush, pen, new RectangleGeometry(rectangle, radiusX, radiusY));
        }

        public void DrawEllipse(Brush brush, Pen pen, Point center, double radiusX, double radiusY)
        {
            DrawGeometry(brush, pen, new EllipseGeometry(center, radiusX, radiusY));
        }

        public void DrawGeometry(Brush brush, Pen pen, Geometry geometry)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.Geometry, Brush = brush, Pen = pen, Geometry = geometry });
        }

        public void DrawText(FormattedText formattedText, Point origin)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.Text, Text = formattedText, Origin = origin });
        }

        public void DrawImage(ImageSource imageSource, Rect rectangle)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.Image, Image = imageSource, Rectangle = rectangle });
        }

        public void PushTransform(Transform transform)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.PushTransform, Transform = transform });
        }

        public void PushOpacity(double opacity)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.PushOpacity, Opacity = opacity });
        }

        public void PushClip(Geometry clipGeometry)
        {
            Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.PushClip, Geometry = clipGeometry });
        }

        public void Pop() => Commands.Add(new DrawingCommand { Kind = DrawingCommandKind.Pop });

        public void Close() { }

        public void Dispose() { }
    }
}

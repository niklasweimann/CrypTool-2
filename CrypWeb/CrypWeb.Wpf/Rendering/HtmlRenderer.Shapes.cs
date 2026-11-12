using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace System.Windows.Rendering
{
    /// <summary>Shapes and custom drawing, written as SVG.</summary>
    internal sealed partial class HtmlRenderer
    {
        private void WriteShape(Shape shape, LayoutSlot slot)
        {
            if (shape is Rectangle || shape is Ellipse)
            {
                WriteBoxShape(shape, slot);
                return;
            }

            Geometry outline = shape.Outline;
            var defs = new StringBuilder();
            if (outline is TextGeometry textGeometry)
            {
                WriteTextShape(shape, slot, textGeometry, defs);
                return;
            }
            string data = outline?.ToSvgPathData() ?? "";
            var path = new StringBuilder("<path d=\"").Append(CssValues.Escape(data)).Append('"');
            path.Append(" fill=\"").Append(shape is Line ? "none" : SvgPaint(shape.Fill, defs)).Append('"');
            if ((outline?.EffectiveFillRule ?? FillRule.Nonzero) == FillRule.EvenOdd)
            {
                path.Append(" fill-rule=\"evenodd\"");
            }
            AppendStroke(path, shape.Stroke, shape.StrokeThickness, shape.StrokeDashArray, shape.StrokeStartLineCap, shape.StrokeLineJoin, defs);
            if (shape.Stretch != Stretch.None)
            {
                path.Append(" vector-effect=\"non-scaling-stroke\"");
            }
            if (outline?.Transform != null)
            {
                path.Append(" transform=\"").Append(CssValues.Matrix(outline.Transform.Value)).Append('"');
            }
            path.Append("/>");

            // wpf.js sizes the svg to the drawn figure (and applies Stretch) once the browser knows the outline
            string attributes = " data-shape=\"" + shape.Stretch + "\"";
            OpenTag("svg", shape, slot, "wsh", "", attributes);
            if (defs.Length > 0)
            {
                _html.Append("<defs>").Append(defs).Append("</defs>");
            }
            _html.Append(path);
            if (CssValues.Simplify(shape.Fill) is VisualBrush { Visual: UIElement visual } visualBrush && outline != null && outline.Transform == null)
            {
                // the brush's element, clipped to the outline and stretched over its bounds
                string clipId = _idPrefix + "c" + (++_gradientCount);
                _html.Append("<clipPath id=\"").Append(clipId).Append("\"><path d=\"").Append(CssValues.Escape(data)).Append("\"/></clipPath>");
                _html.Append("<g clip-path=\"url(#").Append(clipId).Append(")\">");
                AppendVisualBrush(visualBrush, visual, outline.Bounds);
                _html.Append("</g>");
            }
            _html.Append("</svg>");
        }

        /// <summary>A shape whose outline is text (FormattedText.BuildGeometry).</summary>
        private void WriteTextShape(Shape shape, LayoutSlot slot, TextGeometry geometry, StringBuilder defs)
        {
            FormattedText text = geometry.Text;
            string fill = SvgPaint(shape.Fill ?? text.Foreground, defs);
            OpenTag("svg", shape, slot, "wsh", "", " data-shape=\"None\"");
            _html.Append("<text x=\"").Append(CssValues.Number(geometry.Origin.X)).Append("\" y=\"").Append(CssValues.Number(geometry.Origin.Y + text.Baseline))
                .Append("\" font-size=\"").Append(CssValues.Number(text.EmSize)).Append("\" fill=\"").Append(fill).Append('"');
            if (text.Typeface?.FontFamily != null)
            {
                _html.Append(" font-family=\"").Append(CssValues.Escape(text.Typeface.FontFamily.CssName)).Append('"');
            }
            if (geometry.Transform != null)
            {
                _html.Append(" transform=\"").Append(CssValues.Matrix(geometry.Transform.Value)).Append('"');
            }
            _html.Append('>').Append(CssValues.Escape(text.Text)).Append("</text></svg>");
        }

        /// <summary>Rectangles and ellipses are plain boxes: they stretch like any element and can carry gradients.</summary>
        private void WriteBoxShape(Shape shape, LayoutSlot slot)
        {
            var css = new StyleBuilder();
            css.Add("background", CssValues.Background(shape.Fill, address => ResolveImage(shape, address)));
            string stroke = CssValues.SolidColor(shape.Stroke);
            if (stroke != null && shape.StrokeThickness > 0)
            {
                bool dashed = shape.StrokeDashArray != null && shape.StrokeDashArray.Count > 0;
                css.Add("border", CssValues.Pixels(shape.StrokeThickness) + (dashed ? " dashed " : " solid ") + stroke);
            }
            if (shape is Ellipse)
            {
                css.Add("border-radius", "50%");
            }
            else if (shape is Rectangle rectangle && (rectangle.RadiusX > 0 || rectangle.RadiusY > 0))
            {
                css.Add("border-radius", CssValues.Pixels(rectangle.RadiusX) + "/" + CssValues.Pixels(rectangle.RadiusY > 0 ? rectangle.RadiusY : rectangle.RadiusX));
            }
            if (CssValues.Simplify(shape.Fill) is VisualBrush { Visual: UIElement visual } visualBrush)
            {
                // the brush's element stretched over the shape (clipped to its rounded corners)
                css.Add("overflow", "hidden");
                OpenTag("div", shape, slot, "wr wvbrush", css.ToString());
                _html.Append("<svg class=\"wbrush\">");
                AppendVisualBrush(visualBrush, visual, new Rect(0, 0, 1, 1), relative: true);
                _html.Append("</svg></div>");
                return;
            }
            OpenTag("div", shape, slot, "wr", css.ToString());
            _html.Append("</div>");
        }

        /// <summary>
        /// The element of a visual brush as HTML inside SVG (foreignObject), stretched over <paramref name="area"/>
        /// as the brush's Stretch says. The element has no size of its own in a brush, so its natural size is
        /// estimated (<see cref="EstimateSize"/>). With <paramref name="relative"/>, the area is the whole SVG.
        /// </summary>
        private void AppendVisualBrush(VisualBrush brush, UIElement visual, Rect area, bool relative = false)
        {
            Size natural = EstimateSize(visual);
            string aspect = brush.Stretch switch
            {
                Stretch.Uniform => "xMidYMid meet",
                Stretch.UniformToFill => "xMidYMid slice",
                Stretch.None => "xMidYMid meet",
                _ => "none",
            };
            string size = relative
                ? " width=\"100%\" height=\"100%\""
                : " x=\"" + CssValues.Number(area.X) + "\" y=\"" + CssValues.Number(area.Y) + "\" width=\"" + CssValues.Number(area.Width)
                    + "\" height=\"" + CssValues.Number(area.Height) + "\"";
            _html.Append("<svg").Append(size).Append(" viewBox=\"0 0 ").Append(CssValues.Number(natural.Width)).Append(' ')
                .Append(CssValues.Number(natural.Height)).Append("\" preserveAspectRatio=\"").Append(aspect).Append("\">");
            _html.Append("<foreignObject width=\"").Append(CssValues.Number(natural.Width)).Append("\" height=\"")
                .Append(CssValues.Number(natural.Height)).Append("\"><div class=\"wvisual\">");
            WriteElement(visual, LayoutSlot.Flow);
            _html.Append("</div></foreignObject></svg>");
        }

        /// <summary>
        /// The size an element would take without limits, estimated from its own size, text (about 0.55 em per
        /// character, 1.33 em per line) and the panels and borders around it. Only used where the browser cannot
        /// measure first (the content of a visual brush).
        /// </summary>
        private static Size EstimateSize(UIElement element)
        {
            if (element is not FrameworkElement fe)
            {
                return new Size(100, 30);
            }
            Size content;
            switch (element)
            {
                case TextBlock text:
                    string[] lines = (text.Text ?? "").Split('\n');
                    double fontSize = text.FontSize > 0 ? text.FontSize : 12;
                    content = new Size(lines.Max(line => line.Length) * fontSize * 0.55, lines.Length * fontSize * 1.33);
                    break;
                case StackPanel stack:
                    {
                        var sizes = stack.Children.Cast<UIElement>().Select(EstimateSize).ToList();
                        content = stack.Orientation == Orientation.Vertical
                            ? new Size(sizes.Select(s => s.Width).DefaultIfEmpty().Max(), sizes.Sum(s => s.Height))
                            : new Size(sizes.Sum(s => s.Width), sizes.Select(s => s.Height).DefaultIfEmpty().Max());
                        break;
                    }
                case Panel panel:
                    {
                        var sizes = panel.Children.Cast<UIElement>().Select(EstimateSize).ToList();
                        content = new Size(sizes.Select(s => s.Width).DefaultIfEmpty().Max(), sizes.Select(s => s.Height).DefaultIfEmpty().Max());
                        break;
                    }
                case Border border when border.Child != null:
                    {
                        Size child = EstimateSize(border.Child);
                        Thickness padding = border.Padding, line = border.BorderThickness;
                        content = new Size(child.Width + padding.Left + padding.Right + line.Left + line.Right,
                            child.Height + padding.Top + padding.Bottom + line.Top + line.Bottom);
                        break;
                    }
                default:
                    content = new Size(100, 30);
                    break;
            }
            double width = double.IsNaN(fe.Width) ? content.Width : fe.Width;
            double height = double.IsNaN(fe.Height) ? content.Height : fe.Height;
            return new Size(Math.Max(1, width + fe.Margin.Left + fe.Margin.Right), Math.Max(1, height + fe.Margin.Top + fe.Margin.Bottom));
        }

        private void AppendStroke(StringBuilder svg, Brush stroke, double thickness, DoubleCollection dashes, PenLineCap cap, PenLineJoin join, StringBuilder defs)
        {
            if (stroke == null || thickness <= 0)
            {
                return;
            }
            svg.Append(" stroke=\"").Append(SvgPaint(stroke, defs)).Append("\" stroke-width=\"").Append(CssValues.Number(thickness)).Append('"');
            if (dashes != null && dashes.Count > 0)
            {
                // WPF dash lengths are multiples of the stroke thickness
                svg.Append(" stroke-dasharray=\"");
                foreach (double dash in dashes)
                {
                    svg.Append(CssValues.Number(dash * thickness)).Append(' ');
                }
                svg.Append('"');
            }
            if (cap != PenLineCap.Flat)
            {
                svg.Append(" stroke-linecap=\"").Append(cap == PenLineCap.Round ? "round" : "square").Append('"');
            }
            if (join != PenLineJoin.Miter)
            {
                svg.Append(" stroke-linejoin=\"").Append(join == PenLineJoin.Round ? "round" : "bevel").Append('"');
            }
        }

        /// <summary>A brush as SVG paint; gradients are added to <paramref name="defs"/> and referenced by id.</summary>
        private string SvgPaint(Brush brush, StringBuilder defs)
        {
            switch (CssValues.Simplify(brush))
            {
                case null:
                    return "none";
                case SolidColorBrush solid:
                    return CssValues.Color(solid.Color, solid.Opacity);
                case LinearGradientBrush linear:
                    {
                        string id = _idPrefix + "g" + (++_gradientCount);
                        defs.Append("<linearGradient id=\"").Append(id).Append("\" x1=\"").Append(CssValues.Number(linear.StartPoint.X))
                            .Append("\" y1=\"").Append(CssValues.Number(linear.StartPoint.Y)).Append("\" x2=\"").Append(CssValues.Number(linear.EndPoint.X))
                            .Append("\" y2=\"").Append(CssValues.Number(linear.EndPoint.Y)).Append("\">");
                        AppendStops(defs, linear);
                        defs.Append("</linearGradient>");
                        return "url(#" + id + ")";
                    }
                case RadialGradientBrush radial:
                    {
                        string id = _idPrefix + "g" + (++_gradientCount);
                        defs.Append("<radialGradient id=\"").Append(id).Append("\" cx=\"").Append(CssValues.Number(radial.Center.X))
                            .Append("\" cy=\"").Append(CssValues.Number(radial.Center.Y)).Append("\" r=\"").Append(CssValues.Number(radial.RadiusX))
                            .Append("\" fx=\"").Append(CssValues.Number(radial.GradientOrigin.X)).Append("\" fy=\"").Append(CssValues.Number(radial.GradientOrigin.Y)).Append("\">");
                        AppendStops(defs, radial);
                        defs.Append("</radialGradient>");
                        return "url(#" + id + ")";
                    }
                default:
                    return "none";
            }
        }

        private static void AppendStops(StringBuilder defs, GradientBrush brush)
        {
            foreach (GradientStop stop in brush.GradientStops)
            {
                defs.Append("<stop offset=\"").Append(CssValues.Number(stop.Offset)).Append("\" stop-color=\"")
                    .Append(CssValues.Color(stop.Color, brush.Opacity)).Append("\"/>");
            }
        }

        /// <summary>What the element draws itself in <c>OnRender</c>, behind its children.</summary>
        private void WriteCustomDrawing(UIElement element)
        {
            if (element is not FrameworkElement fe)
            {
                return;
            }
            var context = new DrawingContext();
            fe.InvokeOnRender(context);
            if (context.Commands.Count == 0)
            {
                return;
            }

            var defs = new StringBuilder();
            var body = new StringBuilder();
            AppendDrawing(element, context, defs, body);

            _html.Append("<svg class=\"wdraw\" width=\"1\" height=\"1\">");
            if (defs.Length > 0)
            {
                _html.Append("<defs>").Append(defs).Append("</defs>");
            }
            _html.Append(body).Append("</svg>");
        }

        /// <summary>
        /// A bitmap that was drawn into (RenderTargetBitmap, WriteableBitmap) as an SVG picture of its size, for the
        /// <c>src</c> of an image.
        /// </summary>
        private string DrawnBitmapUrl(UIElement element, DrawnBitmap bitmap)
        {
            var defs = new StringBuilder();
            var body = new StringBuilder();
            AppendDrawing(element, bitmap.Drawing, defs, body);
            var svg = new StringBuilder();
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(bitmap.DrawnWidth).Append("\" height=\"").Append(bitmap.DrawnHeight)
                .Append("\" viewBox=\"0 0 ").Append(bitmap.DrawnWidth).Append(' ').Append(bitmap.DrawnHeight).Append("\">");
            if (defs.Length > 0)
            {
                svg.Append("<defs>").Append(defs).Append("</defs>");
            }
            svg.Append(body).Append("</svg>");
            return "data:image/svg+xml;charset=utf-8," + Uri.EscapeDataString(svg.ToString());
        }

        /// <summary>Writes recorded drawing commands as SVG elements (gradients go to <paramref name="defs"/>).</summary>
        private void AppendDrawing(UIElement element, DrawingContext context, StringBuilder defs, StringBuilder body)
        {
            int open = 0;
            foreach (DrawingCommand command in context.Commands)
            {
                switch (command.Kind)
                {
                    case DrawingCommandKind.Geometry:
                        body.Append("<path d=\"").Append(CssValues.Escape(command.Geometry?.ToSvgPathData())).Append("\" fill=\"").Append(SvgPaint(command.Brush, defs)).Append('"');
                        if (command.Pen != null)
                        {
                            AppendStroke(body, command.Pen.Brush, command.Pen.Thickness, command.Pen.DashStyle?.Dashes, command.Pen.StartLineCap, command.Pen.LineJoin, defs);
                        }
                        body.Append("/>");
                        break;

                    case DrawingCommandKind.Text:
                        FormattedText text = command.Text;
                        body.Append("<text x=\"").Append(CssValues.Number(command.Origin.X)).Append("\" y=\"").Append(CssValues.Number(command.Origin.Y + text.Baseline))
                            .Append("\" font-size=\"").Append(CssValues.Number(text.EmSize)).Append("\" fill=\"").Append(SvgPaint(text.Foreground, defs)).Append('"');
                        if (text.Typeface?.FontFamily != null)
                        {
                            body.Append(" font-family=\"").Append(CssValues.Escape(text.Typeface.FontFamily.CssName)).Append('"');
                        }
                        if (text.Typeface != null && text.Typeface.Weight.ToOpenTypeWeight() >= 600)
                        {
                            body.Append(" font-weight=\"bold\"");
                        }
                        body.Append('>').Append(CssValues.Escape(text.Text)).Append("</text>");
                        break;

                    case DrawingCommandKind.Image:
                        body.Append("<image href=\"").Append(CssValues.Escape(ResolveImage(element, command.Image?.Address))).Append("\" x=\"").Append(CssValues.Number(command.Rectangle.X))
                            .Append("\" y=\"").Append(CssValues.Number(command.Rectangle.Y)).Append("\" width=\"").Append(CssValues.Number(command.Rectangle.Width))
                            .Append("\" height=\"").Append(CssValues.Number(command.Rectangle.Height)).Append("\" preserveAspectRatio=\"none\"/>");
                        break;

                    case DrawingCommandKind.PushTransform:
                        body.Append("<g transform=\"").Append(CssValues.Matrix(command.Transform?.Value ?? Matrix.Identity)).Append("\">");
                        open++;
                        break;

                    case DrawingCommandKind.PushOpacity:
                        body.Append("<g opacity=\"").Append(CssValues.Number(command.Opacity)).Append("\">");
                        open++;
                        break;

                    case DrawingCommandKind.PushClip:
                        body.Append("<g style=\"clip-path:path('").Append(CssValues.Escape(command.Geometry?.ToSvgPathData())).Append("')\">");
                        open++;
                        break;

                    case DrawingCommandKind.Pop:
                        if (open > 0)
                        {
                            body.Append("</g>");
                            open--;
                        }
                        break;
                }
            }
            for (; open > 0; open--)
            {
                body.Append("</g>");
            }
        }
    }
}

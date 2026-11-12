using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace System.Windows.Rendering
{
    /// <summary>How the parent lays out a child; decides which CSS places the child.</summary>
    internal enum LayoutSlot
    {
        /// <summary>A cell of a CSS grid (Grid, Border, content controls): alignment via justify/align-self.</summary>
        GridCell,
        StackVertical,
        StackHorizontal,
        /// <summary>Absolute coordinates (Canvas).</summary>
        Canvas,
        /// <summary>Normal flow (wrap panels, list items).</summary>
        Flow,
    }

    /// <summary>
    /// Writes an element tree as HTML and SVG. The browser does the layout: grids become CSS grids, stack panels
    /// flex boxes, canvases absolutely positioned boxes; shapes become SVG. The stylesheet for the generated
    /// class names is wpf.css in the web app.
    /// </summary>
    internal sealed partial class HtmlRenderer
    {
        private readonly StringBuilder _html = new StringBuilder();
        private readonly Func<UIElement, int> _idOf;
        private readonly string _idPrefix;
        private int _gradientCount;
        private UIElement _root;

        /// <param name="idOf">Gives an element the number under which the browser reports its events.</param>
        /// <param name="idPrefix">Makes the ids of SVG definitions unique on a page with several presentations.</param>
        public HtmlRenderer(Func<UIElement, int> idOf, string idPrefix)
        {
            _idOf = idOf;
            _idPrefix = idPrefix;
        }

        public string Render(UIElement root)
        {
            _html.Clear();
            _gradientCount = 0;
            _root = root;
            _html.Append("<div class=\"wpf\">");
            WriteElement(root, LayoutSlot.GridCell);
            _html.Append("</div>");
            return _html.ToString();
        }

        // ---- dispatch -----------------------------------------------------------------------------------------

        private void WriteElement(UIElement element, LayoutSlot slot)
        {
            if (element == null || element.Visibility == Visibility.Collapsed)
            {
                return;
            }

            switch (element)
            {
                case Shape shape: WriteShape(shape, slot); break;
                case TextBlock textBlock: WriteTextBlock(textBlock, slot); break;
                case Image image: WriteImage(image, slot); break;
                case Viewport3D viewport3D: WriteViewport3D(viewport3D, slot); break;
                case Viewbox viewbox: WriteViewbox(viewbox, slot); break;
                case Decorator decorator: WriteContainer(decorator, slot, "wb", "", LayoutSlot.GridCell); break;
                case Grid grid: WriteGrid(grid, slot); break;
                case Canvas canvas: WriteContainer(canvas, slot, "wc", "", LayoutSlot.Canvas); break;
                case StackPanel stack: WriteStack(stack, slot, stack.Orientation, wrap: false); break;
                case WrapPanel wrap: WriteStack(wrap, slot, wrap.Orientation, wrap: true); break;
                case DockPanel dock: WriteDockPanel(dock, slot); break;
                case UniformGrid uniform: WriteUniformGrid(uniform, slot); break;
                case Panel panel: WriteContainer(panel, slot, "wb", "", LayoutSlot.GridCell); break;
                case TextBox textBox: WriteTextBox(textBox, slot); break;
                case RichTextBox richText: WriteFlowDocument(richText, richText.Document, slot); break;
                case FlowDocumentScrollViewer viewer: WriteFlowDocument(viewer, viewer.Document, slot); break;
                case PasswordBox passwordBox: WritePasswordBox(passwordBox, slot); break;
                case ToggleButton toggle: WriteToggle(toggle, slot); break;
                case ButtonBase button: WriteButton(button, slot); break;
                case ProgressBar progress: WriteProgressBar(progress, slot); break;
                case RangeBase range: WriteSlider(range, slot); break;
                case ScrollViewer scroll: WriteScrollViewer(scroll, slot); break;
                case TabControl tabs: WriteTabControl(tabs, slot); break;
                case Expander expander: WriteExpander(expander, slot); break;
                case GroupBox group: WriteGroupBox(group, slot); break;
                case DataGrid dataGrid: WriteDataGrid(dataGrid, slot); break;
                case ListView listView when listView.View is GridView: WriteListView(listView, slot); break;
                case ComboBox comboBox: WriteComboBox(comboBox, slot); break;
                case ItemsControl items: WriteItemsControl(items, slot); break;
                case Separator separator: WriteSeparator(separator, slot); break;
                case GridSplitter splitter: WriteContainer(splitter, slot, "wsep", "", LayoutSlot.GridCell); break;
                case ContentControl content: WriteContentControl(content, slot); break;
                case Forms.Integration.WindowsFormsHost formsHost: WriteFormsHost(formsHost, slot); break;
                default: WriteContainer(element, slot, "wb", "", LayoutSlot.GridCell); break;
            }
        }

        /// <summary>A plain box around the children of <paramref name="element"/>.</summary>
        private void WriteContainer(UIElement element, LayoutSlot slot, string cssClass, string extraStyle, LayoutSlot childSlot)
        {
            OpenTag("div", element, slot, cssClass, extraStyle);
            WriteCustomDrawing(element);
            foreach (UIElement child in element.ChildElements)
            {
                WriteElement(child, childSlot);
            }
            _html.Append("</div>");
        }

        // ---- the opening tag: everything all elements have in common --------------------------------------------

        private void OpenTag(string tag, UIElement element, LayoutSlot slot, string cssClass, string extraStyle, string extraAttributes = "")
        {
            _html.Append('<').Append(tag).Append(" class=\"").Append(cssClass);
            if (!element.IsEnabled)
            {
                _html.Append(" off");
            }
            _html.Append('"');

            if (IsInteractive(element))
            {
                _html.Append(" data-e=\"").Append(_idOf(element)).Append('"');
                if (element.WantsDrag)
                {
                    _html.Append(" data-d=\"1\"");    // the browser lets the user drag this element (wpf.js)
                }
            }
            // sizes are only known to the browser: it reports them for the root, for canvases (code positions their
            // children by size) and wherever code listens to SizeChanged
            if (ReferenceEquals(element, _root) || element is Canvas || element is FrameworkElement { WantsSize: true })
            {
                _html.Append(" data-s=\"").Append(_idOf(element)).Append('"');
            }
            if (element is FrameworkElement { ToolTip: string toolTip })
            {
                _html.Append(" title=\"").Append(CssValues.Escape(toolTip)).Append('"');
            }
            _html.Append(extraAttributes);
            _html.Append(" style=\"").Append(CommonStyle(element, slot)).Append(extraStyle).Append("\">");
        }

        private static bool IsInteractive(UIElement element)
        {
            return element.WantsClicks || element.WantsDrag || element.AcceptsDrops
                || element is TextBoxBase || element is PasswordBox || element is RangeBase || element is Selector;
        }

        /// <summary>Size, placement in the parent, transforms, colours, border and font of an element.</summary>
        private string CommonStyle(UIElement element, LayoutSlot slot)
        {
            var css = new StyleBuilder();
            var fe = element as FrameworkElement;

            double width = fe?.Width ?? double.NaN;
            double height = fe?.Height ?? double.NaN;
            if (fe != null)
            {
                css.AddPixels("width", width).AddPixels("height", height);
                if (fe.MinWidth > 0) css.AddPixels("min-width", fe.MinWidth);
                if (fe.MinHeight > 0) css.AddPixels("min-height", fe.MinHeight);
                css.AddPixels("max-width", fe.MaxWidth).AddPixels("max-height", fe.MaxHeight);
                if (!fe.Margin.IsZero) css.Add("margin", CssValues.Thickness(fe.Margin));
                if (fe.Cursor != null) css.Add("cursor", fe.Cursor.CssName);
                else if (element.WantsClicks && element is not Control) css.Add("cursor", "pointer");
            }

            HorizontalAlignment horizontal = fe?.HorizontalAlignment ?? HorizontalAlignment.Stretch;
            VerticalAlignment vertical = fe?.VerticalAlignment ?? VerticalAlignment.Stretch;
            switch (slot)
            {
                case LayoutSlot.GridCell:
                    css.Add("grid-row", (Grid.GetRow(element) + 1) + "/span " + Math.Max(1, Grid.GetRowSpan(element)));
                    css.Add("grid-column", (Grid.GetColumn(element) + 1) + "/span " + Math.Max(1, Grid.GetColumnSpan(element)));
                    css.Add("justify-self", Align(horizontal, !double.IsNaN(width)));
                    css.Add("align-self", Align(vertical, !double.IsNaN(height)));
                    break;
                case LayoutSlot.StackVertical:
                    css.Add("flex", "none").Add("align-self", Align(horizontal, !double.IsNaN(width)));
                    break;
                case LayoutSlot.StackHorizontal:
                    css.Add("flex", "none").Add("align-self", Align(vertical, !double.IsNaN(height)));
                    break;
                case LayoutSlot.Canvas:
                    css.Add("position", "absolute");
                    double left = Canvas.GetLeft(element), top = Canvas.GetTop(element), right = Canvas.GetRight(element), bottom = Canvas.GetBottom(element);
                    if (!double.IsNaN(left)) css.AddPixels("left", left); else if (!double.IsNaN(right)) css.AddPixels("right", right); else css.Add("left", "0");
                    if (!double.IsNaN(top)) css.AddPixels("top", top); else if (!double.IsNaN(bottom)) css.AddPixels("bottom", bottom); else css.Add("top", "0");
                    break;
                case LayoutSlot.Flow:
                    css.Add("flex", "none");
                    break;
            }

            if (element.Visibility == Visibility.Hidden) css.Add("visibility", "hidden");
            if (element.Opacity < 1) css.Add("opacity", CssValues.Number(Math.Max(0, element.Opacity)));
            if (element.Effect is Media.Effects.Effect effect) css.Add("filter", effect.CssFilter);
            if (!element.IsHitTestVisible) css.Add("pointer-events", "none");
            if (element.ClipToBounds) css.Add("overflow", "hidden");
            if (element.Clip != null) css.Add("clip-path", "path('" + element.Clip.ToSvgPathData() + "')");
            int zIndex = Panel.GetZIndex(element);
            if (zIndex != 0) css.Add("z-index", zIndex.ToString());

            AddTransform(css, element, fe);
            AddBox(css, element);
            AddFont(css, element);
            return css.ToString();
        }

        /// <summary>
        /// WPF alignment as CSS self-alignment. An element with an explicit size that is asked to stretch is
        /// centred in WPF, which CSS would put at the start.
        /// </summary>
        private static string Align(HorizontalAlignment alignment, bool hasSize)
        {
            return alignment switch
            {
                HorizontalAlignment.Left => "start",
                HorizontalAlignment.Center => "center",
                HorizontalAlignment.Right => "end",
                _ => hasSize ? "center" : "stretch",
            };
        }

        private static string Align(VerticalAlignment alignment, bool hasSize)
        {
            return alignment switch
            {
                VerticalAlignment.Top => "start",
                VerticalAlignment.Center => "center",
                VerticalAlignment.Bottom => "end",
                _ => hasSize ? "center" : "stretch",
            };
        }

        private static void AddTransform(StyleBuilder css, UIElement element, FrameworkElement fe)
        {
            Matrix? matrix = null;
            if (fe?.LayoutTransform != null)
            {
                matrix = fe.LayoutTransform.Value;
            }
            if (element.RenderTransform != null)
            {
                matrix = matrix.HasValue ? Matrix.Multiply(matrix.Value, element.RenderTransform.Value) : element.RenderTransform.Value;
            }
            if (!matrix.HasValue)
            {
                return;
            }

            Point origin = element.RenderTransformOrigin;
            css.Add("transform", CssValues.Matrix(matrix.Value));
            css.Add("transform-origin", CssValues.Number(origin.X * 100) + "% " + CssValues.Number(origin.Y * 100) + "%");
        }

        /// <summary>Background, border, corner radius and padding (the same properties on panels, borders and controls).</summary>
        private void AddBox(StyleBuilder css, UIElement element)
        {
            if (element is Shape)
            {
                return;
            }

            if (element.HasValue(Panel.BackgroundProperty))
            {
                css.Add("background", CssValues.Background(element.GetValue(Panel.BackgroundProperty) as Brush, address => ResolveImage(element, address)));
            }
            if (element.HasValue(Border.BorderThicknessProperty))
            {
                var thickness = (Thickness)element.GetValue(Border.BorderThicknessProperty);
                string color = CssValues.SolidColor(element.GetValue(Border.BorderBrushProperty) as Brush) ?? "transparent";
                css.Add("border-style", "solid").Add("border-width", CssValues.Thickness(thickness)).Add("border-color", color);
            }
            if (element is Border border)
            {
                CornerRadius radius = border.CornerRadius;
                if (radius.TopLeft != 0 || radius.TopRight != 0 || radius.BottomRight != 0 || radius.BottomLeft != 0)
                {
                    css.Add("border-radius", CssValues.Pixels(radius.TopLeft) + " " + CssValues.Pixels(radius.TopRight) + " " + CssValues.Pixels(radius.BottomRight) + " " + CssValues.Pixels(radius.BottomLeft));
                }
            }
            if (element.HasValue(Border.PaddingProperty))
            {
                css.Add("padding", CssValues.Thickness((Thickness)element.GetValue(Border.PaddingProperty)));
            }
        }

        /// <summary>Font properties that were set on this object; unset ones are inherited through CSS.</summary>
        private static void AddFont(StyleBuilder css, DependencyObject target)
        {
            if (target.HasValue(TextElement.FontSizeProperty))
            {
                css.AddPixels("font-size", (double)target.GetValue(TextElement.FontSizeProperty));
            }
            if (target.HasValue(TextElement.FontFamilyProperty) && target.GetValue(TextElement.FontFamilyProperty) is FontFamily family)
            {
                css.Add("font-family", "'" + CssValues.Escape(family.CssName) + "',sans-serif");
            }
            if (target.HasValue(TextElement.FontWeightProperty))
            {
                css.Add("font-weight", ((FontWeight)target.GetValue(TextElement.FontWeightProperty)).ToOpenTypeWeight().ToString());
            }
            if (target.HasValue(TextElement.FontStyleProperty))
            {
                css.Add("font-style", ((FontStyle)target.GetValue(TextElement.FontStyleProperty)).IsItalic ? "italic" : "normal");
            }
            if (target.HasValue(TextElement.ForegroundProperty))
            {
                css.Add("color", CssValues.SolidColor(target.GetValue(TextElement.ForegroundProperty) as Brush));
            }
        }

        // ---- pictures -----------------------------------------------------------------------------------------

        /// <summary>
        /// Web address of a picture: resource paths of a plugin ("images/a.png", "/Asm;component/Images/a.png", pack
        /// URIs) are served lower-cased below <c>wpf-res/{plugin folder}/</c>.
        /// </summary>
        internal static string ResolveImage(UIElement element, string address)
        {
            if (string.IsNullOrEmpty(address) || address.StartsWith("data:", StringComparison.Ordinal) || address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return address;
            }

            FrameworkElement root = null;
            for (UIElement e = element; e != null && root == null; e = e.ParentElement)
            {
                if (e is FrameworkElement candidate && candidate.ResourceRoot != null)
                {
                    root = candidate;
                }
            }
            if (root == null)
            {
                return address;
            }

            string path = address.Replace('\\', '/');
            const string packPrefix = "pack://application:,,,";
            if (path.StartsWith(packPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(packPrefix.Length);
            }

            int component = path.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
            if (component >= 0)
            {
                path = path.Substring(component + ";component/".Length);
            }
            else if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path.TrimStart('/');
            }
            else
            {
                path = root.ResourceDirectory + path;       // relative to the XAML file
            }

            var segments = new List<string>();
            foreach (string segment in path.Split('/'))
            {
                if (segment == ".." && segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
                else if (segment.Length > 0 && segment != "." && segment != "..")
                {
                    segments.Add(segment);
                }
            }
            return ("wpf-res/" + root.ResourceRoot + "/" + string.Join("/", segments)).ToLowerInvariant();
        }
    }
}

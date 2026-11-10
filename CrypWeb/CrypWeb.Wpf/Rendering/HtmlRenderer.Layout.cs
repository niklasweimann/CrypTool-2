using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace System.Windows.Rendering
{
    /// <summary>Panels, text, pictures and view boxes.</summary>
    internal sealed partial class HtmlRenderer
    {
        private void WriteGrid(Grid grid, LayoutSlot slot)
        {
            string rows = grid.RowDefinitions.Count == 0 ? "minmax(0,1fr)" : string.Join(" ", grid.RowDefinitions.Select(r => Track(r.Height, r.MinHeight)));
            string columns = grid.ColumnDefinitions.Count == 0 ? "minmax(0,1fr)" : string.Join(" ", grid.ColumnDefinitions.Select(c => Track(c.Width, c.MinWidth)));
            WriteContainer(grid, slot, "wg", "grid-template-rows:" + rows + ";grid-template-columns:" + columns + ";", LayoutSlot.GridCell);
        }

        private static string Track(GridLength length, double minimum)
        {
            if (length.IsAuto)
            {
                return minimum > 0 ? "minmax(" + CssValues.Pixels(minimum) + ",auto)" : "auto";
            }
            if (length.IsStar)
            {
                return "minmax(" + CssValues.Pixels(minimum) + "," + CssValues.Number(length.Value) + "fr)";
            }
            return CssValues.Pixels(length.Value);
        }

        private void WriteStack(Panel panel, LayoutSlot slot, Orientation orientation, bool wrap)
        {
            bool horizontal = orientation == Orientation.Horizontal;
            string style = "flex-direction:" + (horizontal ? "row" : "column") + ";" + (wrap ? "flex-wrap:wrap;align-content:flex-start;" : "");
            LayoutSlot childSlot = wrap ? LayoutSlot.Flow : horizontal ? LayoutSlot.StackHorizontal : LayoutSlot.StackVertical;
            WriteContainer(panel, slot, "ws", style, childSlot);
        }

        private void WriteUniformGrid(UniformGrid grid, LayoutSlot slot)
        {
            int count = grid.Children.Count(c => c.Visibility != Visibility.Collapsed);
            int columns = grid.Columns > 0 ? grid.Columns : grid.Rows > 0 ? (int)Math.Ceiling(count / (double)grid.Rows) : (int)Math.Ceiling(Math.Sqrt(count));
            columns = Math.Max(1, columns);

            OpenTag("div", grid, slot, "wg", "grid-template-columns:repeat(" + columns + ",minmax(0,1fr));grid-auto-rows:minmax(0,1fr);");
            foreach (UIElement child in grid.Children)
            {
                // uniform grids place their children one after the other; a wrapper keeps the cell alignment rules
                _html.Append("<div class=\"wb\">");
                WriteElement(child, LayoutSlot.GridCell);
                _html.Append("</div>");
            }
            _html.Append("</div>");
        }

        /// <summary>
        /// A dock panel as nested flex boxes: each docked child takes one edge of the space that is left, the rest
        /// goes to the following children.
        /// </summary>
        private void WriteDockPanel(DockPanel dock, LayoutSlot slot)
        {
            List<UIElement> children = dock.Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            OpenTag("div", dock, slot, "ws", "");
            int open = 0;
            for (int i = 0; i < children.Count; i++)
            {
                bool fills = i == children.Count - 1 && dock.LastChildFill;
                if (fills)
                {
                    _html.Append("<div class=\"wb\" style=\"flex:1;\">");
                    WriteElement(children[i], LayoutSlot.GridCell);
                    _html.Append("</div>");
                    break;
                }

                Dock side = DockPanel.GetDock(children[i]);
                bool vertical = side == Dock.Top || side == Dock.Bottom;
                string direction = (vertical ? "column" : "row") + (side == Dock.Bottom || side == Dock.Right ? "-reverse" : "");
                _html.Append("<div class=\"ws\" style=\"flex:1;min-width:0;min-height:0;flex-direction:").Append(direction).Append(";\">");
                open++;
                WriteElement(children[i], vertical ? LayoutSlot.StackVertical : LayoutSlot.StackHorizontal);
            }
            for (int i = 0; i < open; i++)
            {
                _html.Append("</div>");
            }
            _html.Append("</div>");
        }

        /// <summary>The view box is scaled in the browser (wpf.js), which knows the available space.</summary>
        private void WriteViewbox(Viewbox viewbox, LayoutSlot slot)
        {
            double width = (viewbox.Child as FrameworkElement)?.Width ?? double.NaN;
            double height = (viewbox.Child as FrameworkElement)?.Height ?? double.NaN;
            string attributes = " data-stretch=\"" + viewbox.Stretch + "\""
                + (double.IsNaN(width) ? "" : " data-w=\"" + CssValues.Number(width) + "\"")
                + (double.IsNaN(height) ? "" : " data-h=\"" + CssValues.Number(height) + "\"");
            OpenTag("div", viewbox, slot, "wvb", "", attributes);
            _html.Append("<div class=\"wvbi\">");
            WriteElement(viewbox.Child, LayoutSlot.Flow);
            _html.Append("</div></div>");
        }

        private void WriteImage(Image image, LayoutSlot slot)
        {
            string fit = image.Stretch switch
            {
                Stretch.Fill => "fill",
                Stretch.UniformToFill => "cover",
                Stretch.None => "none",
                _ => "contain",
            };
            string style = "object-fit:" + fit + ";";
            bool sized = !double.IsNaN(image.Width) || !double.IsNaN(image.Height);
            if (!sized && slot == LayoutSlot.GridCell && image.Stretch != Stretch.None)
            {
                style += "width:100%;height:100%;";      // an unsized picture fills its cell, as in WPF
            }

            string address = image.Source?.Address;
            string source = string.IsNullOrEmpty(address) ? "" : " src=\"" + CssValues.Escape(ResolveImage(image, address)) + "\"";
            OpenTag("img", image, slot, "wi", style, source + " alt=\"\"");
        }

        // ---- text ---------------------------------------------------------------------------------------------

        private void WriteTextBlock(TextBlock block, LayoutSlot slot)
        {
            var css = new StyleBuilder();
            bool wraps = block.TextWrapping != TextWrapping.NoWrap;
            css.Add("white-space", wraps ? "pre-wrap" : "pre");
            if (block.HasLocalValue(TextBlock.TextAlignmentProperty))
            {
                css.Add("text-align", block.TextAlignment.ToString().ToLowerInvariant());
            }
            if (block.TextTrimming != TextTrimming.None)
            {
                css.Add("overflow", "hidden").Add("text-overflow", "ellipsis");
            }
            if (!string.IsNullOrEmpty(block.TextDecorations?.Css))
            {
                css.Add("text-decoration", block.TextDecorations.Css);
            }
            css.AddPixels("line-height", block.LineHeight);

            OpenTag("div", block, slot, "wt", css.ToString());
            if (block.HasBoundText)
            {
                _html.Append(CssValues.Escape(block.Text));
            }
            else
            {
                WriteInlines(block.Inlines);
            }
            _html.Append("</div>");
        }

        private void WriteInlines(InlineCollection inlines)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case LineBreak:
                        _html.Append("<br/>");
                        break;
                    case Run run:
                        WriteSpan(run, () => _html.Append(CssValues.Escape(run.Text)));
                        break;
                    case Span span:
                        WriteSpan(span, () => WriteInlines(span.Inlines));
                        break;
                    case InlineUIContainer container:
                        _html.Append("<span class=\"wb\" style=\"display:inline-grid;vertical-align:baseline;\">");
                        WriteElement(container.Child, LayoutSlot.GridCell);
                        _html.Append("</span>");
                        break;
                }
            }
        }

        private void WriteSpan(Inline inline, Action writeContent)
        {
            var css = new StyleBuilder();
            AddFont(css, inline);
            if (inline.HasLocalValue(TextElement.BackgroundProperty))
            {
                css.Add("background", CssValues.SolidColor(inline.Background));
            }
            if (!string.IsNullOrEmpty(inline.TextDecorations?.Css))
            {
                css.Add("text-decoration", inline.TextDecorations.Css);
            }

            string style = css.ToString();
            if (style.Length == 0)
            {
                writeContent();
                return;
            }
            _html.Append("<span style=\"").Append(style).Append("\">");
            writeContent();
            _html.Append("</span>");
        }

        /// <summary>Content of a content control: an element, or text for everything else.</summary>
        private void WriteContent(object content, string format = null)
        {
            if (content is UIElement element)
            {
                WriteElement(element, LayoutSlot.GridCell);
            }
            else if (content != null)
            {
                object shown = string.IsNullOrEmpty(format) ? content : Data.BindingFormat.Apply(format, content);
                _html.Append("<span class=\"wtx\">").Append(CssValues.Escape(Convert.ToString(shown, Globalization.CultureInfo.CurrentCulture))).Append("</span>");
            }
        }
    }
}

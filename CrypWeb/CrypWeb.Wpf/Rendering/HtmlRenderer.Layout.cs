using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

            // a bitmap drawn by code shows its drawing as SVG; other pictures are loaded from their address
            string address = image.Source is DrawnBitmap { Drawing: not null } drawn ? DrawnBitmapUrl(image, drawn) : image.Source?.Address;
            string source = string.IsNullOrEmpty(address) ? "" : " src=\"" + CssValues.Escape(ResolveImage(image, address)) + "\"";
            OpenTag("img", image, slot, "wi", style, source + " alt=\"\"");
        }

        // ---- text ---------------------------------------------------------------------------------------------

        private void WriteTextBlock(TextBlock block, LayoutSlot slot)
        {
            var css = new StyleBuilder();
            bool wraps = block.TextWrapping != TextWrapping.NoWrap;
            css.Add("white-space", wraps ? "pre-wrap" : "pre");
            if (block.HasValue(TextBlock.TextAlignmentProperty))
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
            if (block.TextEffects != null && block.TextEffects.Count > 0)
            {
                WriteTextWithEffects(block.Text, block.TextEffects);
            }
            else if (block.HasBoundText)
            {
                _html.Append(CssValues.Escape(block.Text));
            }
            else
            {
                WriteInlines(block.Inlines);
            }
            _html.Append("</div>");
        }

        // the element showing the flow document being written (RichTextBox, viewer)
        private UIElement _documentOwner;

        /// <summary>
        /// Elements embedded in a flow document belong to the element that shows the document: as in WPF they get
        /// its data context and resources from there.
        /// </summary>
        private void AdoptIntoDocumentOwner(UIElement child)
        {
            if (child != null && child.ParentElement == null && _documentOwner != null)
            {
                UIElement.SetParent(child, _documentOwner);
            }
        }

        private void WriteFlowDocument(UIElement owner, FlowDocument document, LayoutSlot slot)
        {
            UIElement previousOwner = _documentOwner;
            _documentOwner = owner;
            try
            {
                WriteFlowDocumentContent(owner, document, slot);
            }
            finally
            {
                _documentOwner = previousOwner;
            }
        }

        private void WriteFlowDocumentContent(UIElement owner, FlowDocument document, LayoutSlot slot)
        {
            OpenTag("div", owner, slot, "wdoc", "");
            if (document != null)
            {
                var documentCss = new StyleBuilder();
                AddFont(documentCss, document);
                _html.Append("<div style=\"").Append(documentCss).Append("\">");
                WriteBlocks(document.Blocks);
                _html.Append("</div>");
            }
            _html.Append("</div>");
        }

        /// <summary>The blocks of a flow document: paragraphs, sections, lists and tables as HTML.</summary>
        private void WriteBlocks(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                var css = new StyleBuilder();
                AddFont(css, block);
                if (block.TextAlignment != TextAlignment.Left)
                {
                    css.Add("text-align", block.TextAlignment.ToString().ToLowerInvariant());
                }
                if (block.GetValue(TextElement.BackgroundProperty) is Brush background)
                {
                    css.Add("background", CssValues.Background(background, address => address));
                }
                switch (block)
                {
                    case Paragraph paragraph:
                        _html.Append("<p style=\"").Append(css).Append("\">");
                        WriteInlines(paragraph.Inlines);
                        _html.Append("</p>");
                        break;
                    case Section section:
                        _html.Append("<div style=\"").Append(css).Append("\">");
                        WriteBlocks(section.Blocks);
                        _html.Append("</div>");
                        break;
                    case List list:
                        WriteList(list, css);
                        break;
                    case Table table:
                        WriteTable(table, css);
                        break;
                    case BlockUIContainer container when container.Child != null:
                        AdoptIntoDocumentOwner(container.Child);
                        _html.Append("<div style=\"").Append(css).Append("\">");
                        WriteElement(container.Child, LayoutSlot.Flow);
                        _html.Append("</div>");
                        break;
                }
            }
        }

        private void WriteList(List list, StyleBuilder css)
        {
            string marker = list.MarkerStyle switch
            {
                TextMarkerStyle.None => "none",
                TextMarkerStyle.Circle => "circle",
                TextMarkerStyle.Square or TextMarkerStyle.Box => "square",
                TextMarkerStyle.LowerRoman => "lower-roman",
                TextMarkerStyle.UpperRoman => "upper-roman",
                TextMarkerStyle.LowerLatin => "lower-latin",
                TextMarkerStyle.UpperLatin => "upper-latin",
                TextMarkerStyle.Decimal => "decimal",
                _ => "disc",
            };
            css.Add("list-style-type", marker);
            bool ordered = marker != "disc" && marker != "circle" && marker != "square" && marker != "none";
            string tag = ordered ? "ol" : "ul";
            _html.Append('<').Append(tag).Append(ordered && list.StartIndex != 1 ? " start=\"" + list.StartIndex + "\"" : "")
                .Append(" style=\"").Append(css).Append("\">");
            foreach (ListItem item in list.ListItems)
            {
                var itemCss = new StyleBuilder();
                AddFont(itemCss, item);
                if (item.GetValue(TextElement.BackgroundProperty) is Brush background)
                {
                    itemCss.Add("background", CssValues.Background(background, address => address));
                }
                _html.Append("<li style=\"").Append(itemCss).Append("\">");
                WriteBlocks(item.Blocks);
                _html.Append("</li>");
            }
            _html.Append("</").Append(tag).Append('>');
        }

        private void WriteTable(Table table, StyleBuilder css)
        {
            css.Add("border-spacing", CssValues.Pixels(table.CellSpacing)).Add("border-collapse", "separate");
            _html.Append("<table style=\"").Append(css).Append("\">");
            if (table.Columns.Count > 0)
            {
                _html.Append("<colgroup>");
                foreach (TableColumn column in table.Columns)
                {
                    string width = column.Width.IsAbsolute ? CssValues.Pixels(column.Width.Value)
                        : column.Width.IsStar ? CssValues.Number(column.Width.Value * 100) + "*" : "";
                    _html.Append(column.Width.IsAbsolute ? "<col style=\"width:" + width + "\"/>" : "<col/>");
                }
                _html.Append("</colgroup>");
            }
            foreach (TableRowGroup group in table.RowGroups)
            {
                _html.Append("<tbody>");
                foreach (TableRow row in group.Rows)
                {
                    _html.Append("<tr>");
                    foreach (TableCell cell in row.Cells)
                    {
                        var cellCss = new StyleBuilder();
                        AddFont(cellCss, cell);
                        cellCss.Add("vertical-align", "top");
                        if (cell.GetValue(TextElement.BackgroundProperty) is Brush background)
                        {
                            cellCss.Add("background", CssValues.Background(background, address => address));
                        }
                        if (cell.TextAlignment != TextAlignment.Left)
                        {
                            cellCss.Add("text-align", cell.TextAlignment.ToString().ToLowerInvariant());
                        }
                        _html.Append("<td").Append(cell.ColumnSpan > 1 ? " colspan=\"" + cell.ColumnSpan + "\"" : "")
                            .Append(cell.RowSpan > 1 ? " rowspan=\"" + cell.RowSpan + "\"" : "")
                            .Append(" style=\"").Append(cellCss).Append("\">");
                        WriteBlocks(cell.Blocks);
                        _html.Append("</td>");
                    }
                    _html.Append("</tr>");
                }
                _html.Append("</tbody>");
            }
            _html.Append("</table>");
        }

        /// <summary>Plain text in which character ranges have their own colour.</summary>
        private void WriteTextWithEffects(string text, TextEffectCollection effects)
        {
            var colors = new string[text.Length];
            foreach (TextEffect effect in effects)
            {
                string color = CssValues.SolidColor(effect.Foreground);
                for (int i = Math.Max(0, effect.PositionStart); i < Math.Min(text.Length, effect.PositionStart + effect.PositionCount); i++)
                {
                    colors[i] = color;
                }
            }

            int start = 0;
            while (start < text.Length)
            {
                int end = start;
                while (end < text.Length && colors[end] == colors[start])
                {
                    end++;
                }
                string part = CssValues.Escape(text.Substring(start, end - start));
                if (colors[start] == null)
                {
                    _html.Append(part);
                }
                else
                {
                    _html.Append("<span style=\"color:").Append(colors[start]).Append("\">").Append(part).Append("</span>");
                }
                start = end;
            }
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
                        WriteSpan(run, () => WriteRunText(run));
                        break;
                    case Span span:
                        WriteSpan(span, () => WriteInlines(span.Inlines));
                        break;
                    case InlineUIContainer container:
                        AdoptIntoDocumentOwner(container.Child);
                        _html.Append("<span class=\"wb\" style=\"display:inline-grid;vertical-align:baseline;\">");
                        WriteElement(container.Child, LayoutSlot.GridCell);
                        _html.Append("</span>");
                        break;
                }
            }
        }

        /// <summary>The text of a run; character ranges formatted through a TextRange get their own spans.</summary>
        private void WriteRunText(Run run)
        {
            string text = run.Text ?? "";
            if (run.Formats.Count == 0)
            {
                _html.Append(CssValues.Escape(text));
                return;
            }

            // cut the text at every range boundary; each piece gets the formats that cover it (later ones win)
            var cuts = new SortedSet<int> { 0, text.Length };
            foreach (var format in run.Formats)
            {
                cuts.Add(Math.Clamp(format.From, 0, text.Length));
                cuts.Add(Math.Clamp(format.To, 0, text.Length));
            }
            int start = 0;
            foreach (int end in cuts)
            {
                if (end <= start)
                {
                    continue;
                }
                var css = new StyleBuilder();
                foreach (var format in run.Formats)
                {
                    if (format.From <= start && format.To >= end)
                    {
                        AddRangeFormat(css, format.Property, format.Value);
                    }
                }
                string piece = CssValues.Escape(text.Substring(start, end - start));
                string style = css.ToString();
                if (style.Length == 0)
                {
                    _html.Append(piece);
                }
                else
                {
                    _html.Append("<span style=\"").Append(style).Append("\">").Append(piece).Append("</span>");
                }
                start = end;
            }
        }

        /// <summary>CSS for one formatting property applied to a character range.</summary>
        private static void AddRangeFormat(StyleBuilder css, DependencyProperty property, object value)
        {
            if (property == TextElement.ForegroundProperty && value is Brush foreground)
            {
                css.Add("color", CssValues.SolidColor(foreground));
            }
            else if (property == TextElement.BackgroundProperty && value is Brush background)
            {
                css.Add("background", CssValues.SolidColor(background));
            }
            else if (property == TextElement.FontWeightProperty && value is FontWeight weight)
            {
                css.Add("font-weight", weight.ToOpenTypeWeight().ToString(CultureInfo.InvariantCulture));
            }
            else if (property == TextElement.FontStyleProperty && value is FontStyle style)
            {
                css.Add("font-style", style.IsItalic ? "italic" : "normal");
            }
        }

        private void WriteSpan(Inline inline, Action writeContent)
        {
            var css = new StyleBuilder();
            AddFont(css, inline);
            if (inline.HasValue(TextElement.BackgroundProperty))
            {
                css.Add("background", CssValues.SolidColor(inline.Background));
            }
            if (!string.IsNullOrEmpty(inline.TextDecorations?.Css))
            {
                css.Add("text-decoration", inline.TextDecorations.Css);
            }
            string verticalAlign = inline.BaselineAlignment switch
            {
                BaselineAlignment.Superscript => "super",
                BaselineAlignment.Subscript => "sub",
                BaselineAlignment.Top or BaselineAlignment.TextTop => "text-top",
                BaselineAlignment.Bottom or BaselineAlignment.TextBottom => "text-bottom",
                BaselineAlignment.Center => "middle",
                _ => null,
            };
            if (verticalAlign != null)
            {
                css.Add("vertical-align", verticalAlign);
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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace System.Windows.Rendering
{
    /// <summary>Controls: content controls, buttons, inputs, lists and tables.</summary>
    internal sealed partial class HtmlRenderer
    {
        private void WriteContentControl(ContentControl control, LayoutSlot slot)
        {
            var css = new StyleBuilder();
            if (control.Content is not UIElement)
            {
                // text content follows the content alignment of the control
                css.Add("justify-items", Align(control.HorizontalContentAlignment, false));
                css.Add("align-items", Align(control.VerticalContentAlignment, false));
            }
            OpenTag("div", control, slot, control is Label ? "wb wlabel" : "wb", css.ToString());
            WriteCustomDrawing(control);
            if (control.TemplatedContent is { } templated)
            {
                WriteElement(templated, LayoutSlot.GridCell);     // content shown through a data template
            }
            else
            {
                WriteContent(control.Content, control.ContentStringFormat);
            }
            _html.Append("</div>");
        }

        /// <summary>
        /// A hosted Windows Forms web browser: its HTML text is shown in a sandboxed iframe (no scripts, no access to
        /// the app), an address is loaded as the iframe's source.
        /// </summary>
        private void WriteFormsHost(System.Windows.Forms.Integration.WindowsFormsHost host, LayoutSlot slot)
        {
            if (host.Child is not System.Windows.Forms.WebBrowser browser)
            {
                WriteContainer(host, slot, "wb", "", LayoutSlot.GridCell);
                return;
            }
            string source = browser.DocumentText != null
                ? " srcdoc=\"" + CssValues.Escape(browser.DocumentText) + "\""
                : browser.Url != null ? " src=\"" + CssValues.Escape(browser.Url.ToString()) + "\"" : "";
            OpenTag("iframe", host, slot, "wweb", "", " sandbox=\"allow-popups\"" + source);
            _html.Append("</iframe>");
        }

        private void WriteButton(ButtonBase button, LayoutSlot slot)
        {
            OpenTag("button", button, slot, "wbtn", "", button.IsEnabled ? " type=\"button\"" : " type=\"button\" disabled");
            WriteContent(button.Content);
            _html.Append("</button>");
        }

        private void WriteToggle(ToggleButton toggle, LayoutSlot slot)
        {
            if (toggle is not CheckBox && toggle is not RadioButton)
            {
                OpenTag("button", toggle, slot, toggle.IsChecked == true ? "wbtn on" : "wbtn", "", " type=\"button\"");
                WriteContent(toggle.Content);
                _html.Append("</button>");
                return;
            }

            OpenTag("label", toggle, slot, "wchk", "");
            _html.Append("<input type=\"").Append(toggle is RadioButton ? "radio" : "checkbox").Append('"')
                .Append(toggle.IsChecked == true ? " checked" : "").Append(toggle.IsEnabled ? "" : " disabled").Append("/>");
            WriteContent(toggle.Content);
            _html.Append("</label>");
        }

        private void WriteTextBox(TextBox box, LayoutSlot slot)
        {
            // a box that can scroll or holds several lines shows its text from the top left (a browser input
            // centres one line vertically); without wrapping, long lines scroll sideways as in WPF
            var verticalBar = (ScrollBarVisibility)box.GetValue(ScrollViewer.VerticalScrollBarVisibilityProperty);
            bool scrolls = box.HasValue(ScrollViewer.VerticalScrollBarVisibilityProperty)
                && (verticalBar == ScrollBarVisibility.Visible || verticalBar == ScrollBarVisibility.Auto);
            bool multiline = box.AcceptsReturn || box.TextWrapping != TextWrapping.NoWrap || scrolls || (box.Text ?? "").Contains('\n');
            string attributes = " data-k=\"text\"" + (box.IsReadOnly ? " readonly" : "") + (box.IsEnabled ? "" : " disabled")
                + (box.MaxLength > 0 ? " maxlength=\"" + box.MaxLength + "\"" : "");
            string style = box.HasValue(TextBox.TextAlignmentProperty) ? "text-align:" + box.TextAlignment.ToString().ToLowerInvariant() + ";" : "";
            if (multiline)
            {
                if (box.TextWrapping == TextWrapping.NoWrap)
                {
                    attributes += " wrap=\"off\"";
                }
                if (scrolls && verticalBar == ScrollBarVisibility.Visible)
                {
                    style += "overflow-y:scroll;";
                }
                OpenTag("textarea", box, slot, "wtb", style, attributes);
                _html.Append(CssValues.Escape(box.Text)).Append("</textarea>");
            }
            else
            {
                // without a width, a WPF text box is as wide as its text (size= for browsers without field-sizing)
                string size = " size=\"" + Math.Max(1, (box.Text ?? "").Length) + "\"";
                OpenTag("input", box, slot, "wtb", style, attributes + size + " type=\"text\" value=\"" + CssValues.Escape(box.Text) + "\"");
            }
        }

        private void WritePasswordBox(PasswordBox box, LayoutSlot slot)
        {
            OpenTag("input", box, slot, "wtb", "", " data-k=\"text\" type=\"password\" value=\"" + CssValues.Escape(box.Password) + "\"");
        }

        private void WriteProgressBar(ProgressBar bar, LayoutSlot slot)
        {
            double range = bar.Maximum - bar.Minimum;
            double percent = range <= 0 ? 0 : Math.Clamp((bar.Value - bar.Minimum) / range * 100, 0, 100);
            string color = bar.HasValue(Control.ForegroundProperty) ? CssValues.SolidColor(bar.Foreground) : null;
            OpenTag("div", bar, slot, bar.IsIndeterminate ? "wpb busy" : "wpb", "");
            _html.Append("<div style=\"width:").Append(CssValues.Number(percent)).Append("%;").Append(color == null ? "" : "background:" + color + ";").Append("\"></div></div>");
        }

        private void WriteSlider(RangeBase range, LayoutSlot slot)
        {
            double step = range is Slider { IsSnapToTickEnabled: true } slider ? slider.TickFrequency : 0;
            string attributes = " data-k=\"range\" type=\"range\" min=\"" + CssValues.Number(range.Minimum) + "\" max=\"" + CssValues.Number(range.Maximum)
                + "\" step=\"" + (step > 0 ? CssValues.Number(step) : "any") + "\" value=\"" + range.Value.ToString("R", CultureInfo.InvariantCulture) + "\""
                + (range.IsEnabled ? "" : " disabled");
            OpenTag("input", range, slot, "wsl", "", attributes);
        }

        private void WriteScrollViewer(ScrollViewer viewer, LayoutSlot slot)
        {
            static string Overflow(ScrollBarVisibility visibility) => visibility switch
            {
                ScrollBarVisibility.Disabled => "hidden",
                ScrollBarVisibility.Hidden => "hidden",
                ScrollBarVisibility.Visible => "scroll",
                _ => "auto",
            };
            string style = "overflow-x:" + Overflow(viewer.HorizontalScrollBarVisibility) + ";overflow-y:" + Overflow(viewer.VerticalScrollBarVisibility) + ";";
            OpenTag("div", viewer, slot, "wb wscroll", style);
            WriteContent(viewer.Content);
            _html.Append("</div>");
        }

        private void WriteGroupBox(GroupBox box, LayoutSlot slot)
        {
            OpenTag("fieldset", box, slot, "wgb", "");
            _html.Append("<legend>");
            WriteContent(box.Header);
            _html.Append("</legend><div class=\"wb\">");
            WriteContent(box.Content);
            _html.Append("</div></fieldset>");
        }

        private void WriteExpander(Expander expander, LayoutSlot slot)
        {
            OpenTag("div", expander, slot, "wexp", "");
            // only the header toggles; clicks inside the content belong to the content
            _html.Append("<div class=\"wexph\" data-e=\"").Append(_idOf(expander)).Append("\" data-k=\"toggle\">").Append(expander.IsExpanded ? "▾ " : "▸ ");
            WriteContent(expander.Header);
            _html.Append("</div>");
            if (expander.IsExpanded)
            {
                _html.Append("<div class=\"wb\">");
                WriteContent(expander.Content);
                _html.Append("</div>");
            }
            _html.Append("</div>");
        }

        private void WriteSeparator(Separator separator, LayoutSlot slot)
        {
            OpenTag("div", separator, slot, "wsep", "");
            _html.Append("</div>");
        }

        private void WriteTabControl(TabControl tabs, LayoutSlot slot)
        {
            TabItem current = tabs.CurrentTab;
            OpenTag("div", tabs, slot, "wtab", "");
            _html.Append("<div class=\"wtabh\">");
            foreach (TabItem tab in tabs.EffectiveItems.OfType<TabItem>())
            {
                if (tab.Visibility == Visibility.Collapsed)
                {
                    continue;
                }
                _html.Append("<div class=\"").Append(ReferenceEquals(tab, current) ? "on" : "").Append("\" data-e=\"").Append(_idOf(tab)).Append("\">");
                if (tab.TemplatedHeader is { } header)
                {
                    WriteElement(header, LayoutSlot.GridCell);
                }
                else
                {
                    WriteContent(tab.Header);
                }
                _html.Append("</div>");
            }
            _html.Append("</div><div class=\"wb wtabc\">");
            if (current != null)
            {
                WriteContent(current.Content);
            }
            _html.Append("</div></div>");
        }

        // ---- lists --------------------------------------------------------------------------------------------

        private void WriteItemsControl(ItemsControl items, LayoutSlot slot)
        {
            bool selectable = items is Selector;
            int selected = (items as Selector)?.SelectedIndex ?? -1;
            OpenTag("div", items, slot, selectable ? "wlist sel" : "wlist", ItemsPanelStyle(items));
            int index = 0;
            foreach (object item in items.EffectiveItems)
            {
                _html.Append("<div class=\"wli").Append(index == selected ? " on" : "").Append("\" data-i=\"").Append(index).Append("\">");
                UIElement element = items.ElementFor(item);
                if (element != null)
                {
                    WriteElement(element, LayoutSlot.GridCell);
                }
                else
                {
                    _html.Append(CssValues.Escape(items.TextFor(item)));
                }
                _html.Append("</div>");
                index++;
            }
            _html.Append("</div>");
        }

        /// <summary>
        /// The arrangement of the items given by <see cref="ItemsControl.ItemsPanel"/>: a horizontal StackPanel becomes a
        /// row, a WrapPanel a wrapping row (or column), a UniformGrid a grid of equal cells. Without a panel the items
        /// stack vertically (the default VirtualizingStackPanel).
        /// </summary>
        private static string ItemsPanelStyle(ItemsControl items)
        {
            Panel panel = items.ItemsPanelPrototype;
            var css = new StyleBuilder();
            switch (panel)
            {
                case StackPanel { Orientation: Orientation.Horizontal }:
                    css.Add("display", "flex").Add("flex-direction", "row").Add("align-items", "stretch");
                    break;
                case WrapPanel wrap:
                    css.Add("display", "flex").Add("flex-wrap", "wrap")
                        .Add("flex-direction", wrap.Orientation == Orientation.Horizontal ? "row" : "column");
                    break;
                case UniformGrid grid:
                    if (grid.Columns > 0) css.Add("display", "grid").Add("grid-template-columns", "repeat(" + grid.Columns + ",minmax(0,1fr))");
                    else if (grid.Rows > 0) css.Add("display", "grid").Add("grid-auto-flow", "column").Add("grid-template-rows", "repeat(" + grid.Rows + ",minmax(0,1fr))");
                    break;
            }
            if (panel?.HasValue(Panel.BackgroundProperty) == true)
            {
                css.Add("background", CssValues.SolidColor(panel.Background));
            }
            return css.ToString();
        }

        private void WriteComboBox(ComboBox combo, LayoutSlot slot)
        {
            OpenTag("select", combo, slot, "wcb", "", " data-k=\"select\"" + (combo.IsEnabled ? "" : " disabled"));
            int index = 0;
            foreach (object item in combo.EffectiveItems)
            {
                string text = item is ContentControl { Content: not UIElement } container ? Convert.ToString(container.Content, CultureInfo.CurrentCulture) : combo.TextFor(item);
                _html.Append("<option value=\"").Append(index).Append('"').Append(index == combo.SelectedIndex ? " selected" : "").Append('>')
                    .Append(CssValues.Escape(text)).Append("</option>");
                index++;
            }
            _html.Append("</select>");
        }

        private void WriteListView(ListView list, LayoutSlot slot)
        {
            var view = (GridView)list.View;
            OpenTag("div", list, slot, "wtable sel", "");
            _html.Append("<table><thead><tr>");
            foreach (GridViewColumn column in view.Columns)
            {
                _html.Append("<th").Append(double.IsNaN(column.Width) ? "" : " style=\"width:" + CssValues.Pixels(column.Width) + "\"").Append('>');
                WriteContent(column.Header);
                _html.Append("</th>");
            }
            _html.Append("</tr></thead><tbody>");

            int index = 0;
            foreach (object item in list.EffectiveItems)
            {
                _html.Append("<tr class=\"").Append(index == list.SelectedIndex ? "on" : "").Append("\" data-i=\"").Append(index).Append("\">");
                foreach (GridViewColumn column in view.Columns)
                {
                    _html.Append("<td>");
                    WriteCell(list, item, column, column.DisplayMemberBinding, column.CellTemplate);
                    _html.Append("</td>");
                }
                _html.Append("</tr>");
                index++;
            }
            _html.Append("</tbody></table></div>");
        }

        /// <summary>
        /// A data grid as an HTML table, with the look WPF gives it: black grid lines (in the directions of
        /// GridLinesVisibility, coloured by the grid-line brushes), row and alternating row backgrounds, row and header
        /// heights, column widths (pixels, shares of the free width "2*", minimum widths) and the alignment, colour
        /// and weight that the columns' ElementStyle and HeaderStyle set.
        /// </summary>
        private void WriteDataGrid(DataGrid grid, LayoutSlot slot)
        {
            IList<object> items = grid.EffectiveItems;
            List<DataGridColumn> columns = grid.Columns.Count > 0 || !grid.AutoGenerateColumns ? grid.Columns.ToList() : GenerateColumns(grid, items);
            columns = columns.Where(column => column.Visibility == Visibility.Visible).ToList();
            bool headers = grid.HeadersVisibility == DataGridHeadersVisibility.All || grid.HeadersVisibility == DataGridHeadersVisibility.Column;
            bool anyStar = columns.Any(column => column.Width.IsStar);

            OpenTag("div", grid, slot, "wtable sel", "");
            _html.Append(anyStar ? "<table style=\"width:100%\">" : "<table>");
            WriteColumnWidths(columns);

            string cellLines = GridLineCss(grid);
            if (headers)
            {
                _html.Append("<thead><tr>");
                foreach (DataGridColumn column in columns)
                {
                    var css = new StyleBuilder();
                    if (!double.IsNaN(grid.ColumnHeaderHeight)) css.AddPixels("height", grid.ColumnHeaderHeight);
                    AddTextStyle(css, column.HeaderStyle ?? grid.ColumnHeaderStyle, nameof(Control.HorizontalContentAlignment));
                    _html.Append("<th style=\"").Append(css).Append(cellLines).Append("\">");
                    WriteContent(column.Header);
                    _html.Append("</th>");
                }
                _html.Append("</tr></thead>");
            }

            _html.Append("<tbody>");
            int index = 0;
            foreach (object item in items)
            {
                Brush rowBrush = index % 2 == 1 && grid.AlternatingRowBackground != null ? grid.AlternatingRowBackground : grid.RowBackground;
                string rowBackground = rowBrush == null ? null : CssValues.Background(rowBrush, address => address);
                _html.Append("<tr class=\"").Append(index == grid.SelectedIndex ? "on" : "").Append("\" data-i=\"").Append(index).Append('"');
                if (rowBackground != null)
                {
                    _html.Append(" style=\"background:").Append(rowBackground).Append('"');
                }
                _html.Append('>');
                foreach (DataGridColumn column in columns)
                {
                    var css = new StyleBuilder();
                    if (!double.IsNaN(grid.RowHeight)) css.AddPixels("height", grid.RowHeight);
                    if (column is DataGridBoundColumn bound) AddTextStyle(css, bound.ElementStyle, nameof(HorizontalAlignment));
                    if (column is DataGridTextColumn text)
                    {
                        if (text.FontSize > 0) css.AddPixels("font-size", text.FontSize);
                        if (text.Foreground != null) css.Add("color", CssValues.SolidColor(text.Foreground));
                    }
                    string cellCss = css.ToString() + cellLines;

                    Style cellStyle = column.CellStyle ?? grid.CellStyle;
                    if (cellStyle != null && column.CellContentTemplate == null && column.CellBinding != null)
                    {
                        // a styled cell is a real DataGridCell, so the style's setters and triggers apply
                        string value = Convert.ToString(BindingEvaluator.Evaluate(column.CellBinding, item), CultureInfo.CurrentCulture);
                        _html.Append("<td class=\"wcell\" style=\"").Append(cellCss).Append("\">");
                        WriteElement(grid.StyledCell(item, column, cellStyle, value), LayoutSlot.GridCell);
                        _html.Append("</td>");
                        continue;
                    }
                    _html.Append("<td style=\"").Append(cellCss).Append("\">");
                    WriteCell(grid, item, column, column.CellBinding, column.CellContentTemplate);
                    _html.Append("</td>");
                }
                _html.Append("</tr>");
                index++;
            }
            _html.Append("</tbody></table></div>");
        }

        /// <summary>
        /// The columns' widths as a colgroup: pixel widths as they are, star widths as their share of the table
        /// (the table then fills the grid), minimum widths as min-width.
        /// </summary>
        private void WriteColumnWidths(List<DataGridColumn> columns)
        {
            double stars = columns.Where(column => column.Width.IsStar).Sum(column => column.Width.Value);
            _html.Append("<colgroup>");
            foreach (DataGridColumn column in columns)
            {
                var css = new StyleBuilder();
                if (column.Width.IsAbsolute)
                {
                    css.AddPixels("width", column.Width.Value);
                }
                else if (column.Width.IsStar && stars > 0)
                {
                    css.Add("width", CssValues.Number(100 * column.Width.Value / stars) + "%");
                }
                if (column.MinWidth > 0)
                {
                    css.AddPixels("min-width", column.MinWidth);
                }
                _html.Append("<col style=\"").Append(css).Append("\">");
            }
            _html.Append("</colgroup>");
        }

        /// <summary>Borders of a cell for the grid lines WPF draws (black by default).</summary>
        private static string GridLineCss(DataGrid grid)
        {
            DataGridGridLinesVisibility lines = grid.GridLinesVisibility;
            bool horizontal = lines == DataGridGridLinesVisibility.All || lines == DataGridGridLinesVisibility.Horizontal;
            bool vertical = lines == DataGridGridLinesVisibility.All || lines == DataGridGridLinesVisibility.Vertical;
            var css = new StringBuilder();
            if (horizontal)
            {
                css.Append("border-bottom:1px solid ").Append(CssValues.SolidColor(grid.HorizontalGridLinesBrush) ?? "#000").Append(';');
            }
            if (vertical)
            {
                css.Append("border-right:1px solid ").Append(CssValues.SolidColor(grid.VerticalGridLinesBrush) ?? "#000").Append(';');
            }
            return css.ToString();
        }

        /// <summary>
        /// Text alignment, colour, background and weight from the setters of a cell's element style or a column's
        /// header style; <paramref name="alignmentProperty"/> names the setter that aligns the content.
        /// </summary>
        private static void AddTextStyle(StyleBuilder css, Style style, string alignmentProperty)
        {
            if (style == null)
            {
                return;
            }
            if (StyleSetterValue(style, alignmentProperty) is HorizontalAlignment alignment)
            {
                css.Add("text-align", alignment switch
                {
                    HorizontalAlignment.Center => "center",
                    HorizontalAlignment.Right => "right",
                    _ => "left",
                });
            }
            if (StyleSetterValue(style, nameof(TextBlock.TextAlignment)) is TextAlignment textAlignment)
            {
                css.Add("text-align", textAlignment.ToString().ToLowerInvariant());
            }
            if (StyleSetterValue(style, nameof(Control.Foreground)) is Brush foreground)
            {
                css.Add("color", CssValues.SolidColor(foreground));
            }
            if (StyleSetterValue(style, nameof(Control.Background)) is Brush background)
            {
                css.Add("background", CssValues.Background(background, address => address));
            }
            if (StyleSetterValue(style, nameof(Control.FontWeight)) is FontWeight weight)
            {
                css.Add("font-weight", weight.ToOpenTypeWeight().ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The value a style (or a style it is based on) sets for the named property, converted to the property's type.</summary>
        private static object StyleSetterValue(Style style, string propertyName)
        {
            for (Style current = style; current != null; current = current.BasedOn)
            {
                foreach (SetterBase setterBase in current.Setters)
                {
                    if (setterBase is not Setter setter || !string.IsNullOrEmpty(setter.TargetName))
                    {
                        continue;
                    }
                    string name = setter.Property?.Name ?? setter.PropertyName;
                    if (name == null || name.Substring(name.LastIndexOf('.') + 1) != propertyName)
                    {
                        continue;
                    }
                    Type type = setter.Property?.PropertyType;
                    object value = setter.Value;
                    if (value is string text && type != null && type != typeof(string) && type != typeof(object))
                    {
                        return Markup.XamlValueConverter.TryConvert(text, type, out object converted) ? converted : null;
                    }
                    return value;
                }
            }
            return null;
        }

        /// <summary>A table cell: the bound value as text, else the cell template (in WPF a GridViewColumn's
        /// DisplayMemberBinding wins over its CellTemplate when both are set).</summary>
        private void WriteCell(ItemsControl owner, object item, object column, BindingBase binding, DataTemplate template)
        {
            if (binding != null)
            {
                _html.Append(CssValues.Escape(Convert.ToString(BindingEvaluator.Evaluate(binding, item), CultureInfo.CurrentCulture)));
            }
            else if (template != null)
            {
                _html.Append("<div class=\"wb\">");
                WriteElement(owner.CellElement(item, column, template), LayoutSlot.GridCell);
                _html.Append("</div>");
            }
        }

        /// <summary>Columns of a grid without declared columns: the columns of the data table or the item's properties.</summary>
        private static List<DataGridColumn> GenerateColumns(DataGrid grid, IList<object> items)
        {
            var columns = new List<DataGridColumn>();
            object first = items.FirstOrDefault();
            IEnumerable<(string Name, Type Type)> members;
            if (first is System.Data.DataRowView row)
            {
                members = row.Row.Table.Columns.Cast<System.Data.DataColumn>().Select(c => (c.ColumnName, c.DataType));
            }
            else if (first != null)
            {
                members = first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0).Select(p => (p.Name, p.PropertyType));
            }
            else
            {
                return columns;
            }

            foreach ((string name, Type type) in members)
            {
                var column = new DataGridTextColumn { Header = name, Binding = new Binding(first is System.Data.DataRowView ? "[" + name + "]" : name) };
                var args = new DataGridAutoGeneratingColumnEventArgs { Column = column, PropertyName = name, PropertyType = type };
                if (grid.RaiseAutoGeneratingColumn(args) && args.Column != null)
                {
                    columns.Add(args.Column);
                }
            }
            return columns;
        }
    }
}

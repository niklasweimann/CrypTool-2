using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            bool multiline = box.AcceptsReturn || box.TextWrapping != TextWrapping.NoWrap;
            string attributes = " data-k=\"text\"" + (box.IsReadOnly ? " readonly" : "") + (box.IsEnabled ? "" : " disabled")
                + (box.MaxLength > 0 ? " maxlength=\"" + box.MaxLength + "\"" : "");
            string style = box.HasValue(TextBox.TextAlignmentProperty) ? "text-align:" + box.TextAlignment.ToString().ToLowerInvariant() + ";" : "";
            if (multiline)
            {
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

        private void WriteDataGrid(DataGrid grid, LayoutSlot slot)
        {
            IList<object> items = grid.EffectiveItems;
            List<DataGridColumn> columns = grid.Columns.Count > 0 || !grid.AutoGenerateColumns ? grid.Columns.ToList() : GenerateColumns(grid, items);
            bool headers = grid.HeadersVisibility == DataGridHeadersVisibility.All || grid.HeadersVisibility == DataGridHeadersVisibility.Column;
            bool lines = grid.GridLinesVisibility != DataGridGridLinesVisibility.None;

            OpenTag("div", grid, slot, lines ? "wtable sel lines" : "wtable sel", "");
            _html.Append("<table>");
            if (headers)
            {
                _html.Append("<thead><tr>");
                foreach (DataGridColumn column in columns)
                {
                    _html.Append("<th>");
                    WriteContent(column.Header);
                    _html.Append("</th>");
                }
                _html.Append("</tr></thead>");
            }

            _html.Append("<tbody>");
            int index = 0;
            foreach (object item in items)
            {
                _html.Append("<tr class=\"").Append(index == grid.SelectedIndex ? "on" : "").Append("\" data-i=\"").Append(index).Append("\">");
                foreach (DataGridColumn column in columns)
                {
                    Style cellStyle = column.CellStyle ?? grid.CellStyle;
                    if (cellStyle != null && column.CellContentTemplate == null && column.CellBinding != null)
                    {
                        // a styled cell is a real DataGridCell, so the style's setters and triggers apply
                        string text = Convert.ToString(BindingEvaluator.Evaluate(column.CellBinding, item), CultureInfo.CurrentCulture);
                        _html.Append("<td class=\"wcell\">");
                        WriteElement(grid.StyledCell(item, column, cellStyle, text), LayoutSlot.GridCell);
                        _html.Append("</td>");
                        continue;
                    }
                    _html.Append("<td>");
                    WriteCell(grid, item, column, column.CellBinding, column.CellContentTemplate);
                    _html.Append("</td>");
                }
                _html.Append("</tr>");
                index++;
            }
            _html.Append("</tbody></table></div>");
        }

        private void WriteCell(ItemsControl owner, object item, object column, BindingBase binding, DataTemplate template)
        {
            if (template != null)
            {
                _html.Append("<div class=\"wb\">");
                WriteElement(owner.CellElement(item, column, template), LayoutSlot.GridCell);
                _html.Append("</div>");
            }
            else if (binding != null)
            {
                _html.Append(CssValues.Escape(Convert.ToString(BindingEvaluator.Evaluate(binding, item), CultureInfo.CurrentCulture)));
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

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClassicCiphers
{
    /// <summary>
    /// The presentation of the classic cipher components: shows a <see cref="CipherTrace"/> as a title, an explanation and tables (key squares, alphabets,
    /// the steps letter by letter). Built from plain WPF elements, so it works in CrypTool 2 and in the browser edition.
    /// </summary>
    public class ClassicCipherView : UserControl
    {
        private static readonly Brush Used = new SolidColorBrush(Color.FromRgb(255, 235, 130));
        private static readonly Brush Result = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush Header = new SolidColorBrush(Color.FromRgb(225, 232, 245));
        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        public ClassicCipherView()
        {
            Content = _scroll;
            Background = Brushes.White;
        }

        private static TextBlock Text(string text, double size, FontWeight weight)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        }

        /// <summary>Shows why nothing can be shown (for example a wrong key).</summary>
        public void ShowMessage(string title, string message)
        {
            Run(() => { var trace = new CipherTrace { Title = title, Summary = message }; return trace; });
        }

        /// <summary>Shows the trace.</summary>
        public void Show(CipherTrace trace)
        {
            Run(() => trace);
        }

        private void Run(Func<CipherTrace> make)
        {
            var trace = make();
            Action show = () => _scroll.Content = Build(trace);
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
        }

        private static UIElement Build(CipherTrace trace)
        {
            var panel = new StackPanel { Margin = new Thickness(10) };
            if (!string.IsNullOrEmpty(trace.Title)) { panel.Children.Add(Text(trace.Title, 16, FontWeights.Bold)); }
            if (!string.IsNullOrEmpty(trace.Summary)) { panel.Children.Add(Text(trace.Summary, 12, FontWeights.Normal)); }
            foreach (var section in trace.Sections)
            {
                var block = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                if (!string.IsNullOrEmpty(section.Title)) { block.Children.Add(Text(section.Title, 13, FontWeights.Bold)); }
                if (!string.IsNullOrEmpty(section.Note)) { block.Children.Add(Text(section.Note, 12, FontWeights.Normal)); }
                block.Children.Add(Table(section));
                panel.Children.Add(block);
            }
            return panel;
        }

        private static UIElement Table(TraceSection section)
        {
            int rows = Math.Min(section.Cells.Length, 80);
            int columns = section.Cells.Length == 0 ? 0 : section.Cells.Max(r => r.Length);
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
            var widths = new double[columns];
            for (int c = 0; c < columns; c++)
            {
                int longest = 1;
                for (int r = 0; r < rows; r++) { if (c < section.Cells[r].Length) { longest = Math.Max(longest, (section.Cells[r][c] ?? "").Length); } }
                widths[c] = Math.Min(260, Math.Max(24, 9 * longest + 10));
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[c]) });
            }
            for (int r = 0; r < rows; r++) { grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) }); }
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < section.Cells[r].Length; c++)
                {
                    string key = TraceSection.Key(r, c);
                    bool header = r < section.HeaderRows || c < section.HeaderColumns;
                    Brush background = section.Result.Contains(key) ? Result : section.Marked.Contains(key) ? Used : header ? Header : Brushes.White;
                    var cell = new Border
                    {
                        Background = background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5),
                        Child = new TextBlock
                        {
                            Text = section.Cells[r][c] ?? "", FontSize = 12, FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
            }
            return grid;
        }
    }
}

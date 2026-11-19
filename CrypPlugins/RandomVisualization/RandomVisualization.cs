using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RandomVisualization
{
    /// <summary>
    /// Shows three pictures of a sequence of random bytes: the bits as black and white squares, the histogram of the byte values and the plot of
    /// every byte against the next one. Patterns in these pictures reveal a weak generator.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("RandomVisualization.Properties.Resources", "PluginCaption", "PluginTooltip", "RandomVisualization/DetailedDescription/doc.xml", "RandomVisualization/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsRandomNumbers)]
    public class RandomVisualization : ICrypComponent
    {
        private RandomVisualizationSettings _settings = new RandomVisualizationSettings();
        private RandomVisualizationPresentation _presentation;

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public byte[] InputData { get; set; }

        [PropertyInfo(Direction.OutputData, "SummaryCaption", "SummaryTooltip", false)]
        public string Summary { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (RandomVisualizationSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new RandomVisualizationPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            var view = (RandomVisualizationPresentation)Presentation;
            if (InputData == null || InputData.Length < 2)
            {
                GuiLogMessage("At least two bytes are needed.", NotificationLevel.Error);
                view.ShowMessage(Properties.Resources.TooShort);
                return;
            }

            Statistics statistics = Statistics.Of(InputData);
            Summary = string.Format(System.Globalization.CultureInfo.InvariantCulture, "bytes={0} mean={1:F3} chi2={2:F2} correlation={3:F4}", statistics.Count, statistics.Mean, statistics.ChiSquare, statistics.Correlation);
            view.Show(InputData, statistics);
            OnPropertyChanged("Summary");
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            InputData = null;
            Summary = null;
        }

        public void PostExecution()
        {
            InputData = null;
            Summary = null;
        }

        public void Dispose()
        {
            InputData = null;
            Summary = null;
        }

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string message, NotificationLevel level)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, message, level);
        }
    }

    /// <summary>The numbers behind the pictures, free of plugin infrastructure.</summary>
    public sealed class Statistics
    {
        public int Count;
        public double Mean;
        /// <summary>Pearson's chi-square statistic of the byte values against the uniform distribution (255 degrees of freedom, expected about 255).</summary>
        public double ChiSquare;
        /// <summary>The serial correlation coefficient of each byte with the next one: near 0 for random bytes.</summary>
        public double Correlation;
        public int[] Counts = new int[256];

        public static Statistics Of(byte[] data)
        {
            var s = new Statistics { Count = data.Length };
            foreach (byte b in data) { s.Counts[b]++; }
            s.Mean = data.Select(b => (double)b).Average();
            double expected = (double)data.Length / 256;
            s.ChiSquare = s.Counts.Sum(c => (c - expected) * (c - expected) / expected);
            double numerator = 0, denominator = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double x = data[i] - s.Mean;
                denominator += x * x;
                if (i + 1 < data.Length) { numerator += x * (data[i + 1] - s.Mean); }
            }
            s.Correlation = denominator == 0 ? 0 : numerator / denominator;
            return s;
        }
    }

    /// <summary>The pictures, built from plain WPF elements in code.</summary>
    public class RandomVisualizationPresentation : UserControl
    {
        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(30, 30, 60));
        private static readonly Brush Bar = new SolidColorBrush(Color.FromRgb(70, 120, 200));
        private static readonly Brush Dotted = new SolidColorBrush(Color.FromRgb(200, 60, 40));

        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        public RandomVisualizationPresentation()
        {
            Content = _scroll;
            Background = Brushes.White;
            _scroll.Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) };
        }

        private static TextBlock Text(string text, double size = 12, bool bold = false)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2) };
        }

        private void Run(Action<StackPanel> build)
        {
            Action show = () =>
            {
                var panel = new StackPanel { Margin = new Thickness(10) };
                build(panel);
                _scroll.Content = panel;
            };
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
        }

        public void ShowMessage(string message)
        {
            Run(panel =>
            {
                panel.Children.Add(Text(Properties.Resources.ErrorTitle, 16, true));
                panel.Children.Add(Text(message, 13));
            });
        }

        private static void Square(Canvas canvas, double x, double y, double width, double height, Brush fill)
        {
            var rectangle = new Rectangle { Width = width, Height = height, Fill = fill };
            Canvas.SetLeft(rectangle, x);
            Canvas.SetTop(rectangle, y);
            canvas.Children.Add(rectangle);
        }

        public void Show(byte[] data, Statistics statistics)
        {
            Run(panel =>
            {
                panel.Children.Add(Text(Properties.Resources.Title, 16, true));
                panel.Children.Add(Text(string.Format(System.Globalization.CultureInfo.InvariantCulture, Properties.Resources.SummaryLine, statistics.Count, statistics.Mean, statistics.ChiSquare, statistics.Correlation), 12));

                // 1. the bits: 64 per row, as many rows as the data has (at most 64): a black square is a 1
                panel.Children.Add(Text(Properties.Resources.BitsTitle, 13, true));
                int bits = Math.Min(data.Length * 8, 64 * 64), columns = 64;
                var bitCanvas = new Canvas { Width = columns * 5 + 2, Height = ((bits + columns - 1) / columns) * 5 + 2, Background = Brushes.White };
                for (int i = 0; i < bits; i++)
                {
                    if ((data[i / 8] >> (7 - i % 8) & 1) == 1) { Square(bitCanvas, 1 + (i % columns) * 5, 1 + (i / columns) * 5, 4, 4, Ink); }
                }
                panel.Children.Add(bitCanvas);
                panel.Children.Add(Text(Properties.Resources.BitsExplanation, 11));

                // 2. the histogram: 64 columns of four byte values each
                panel.Children.Add(Text(Properties.Resources.HistogramTitle, 13, true));
                var bins = Enumerable.Range(0, 64).Select(b => statistics.Counts.Skip(b * 4).Take(4).Sum()).ToArray();
                int maximum = Math.Max(1, bins.Max());
                double expectedBin = (double)data.Length / 64;
                var histogram = new Canvas { Width = 64 * 7 + 4, Height = 124, Background = Brushes.White };
                for (int b = 0; b < 64; b++)
                {
                    double height = 120.0 * bins[b] / Math.Max(maximum, expectedBin * 1.0);
                    Square(histogram, 2 + b * 7, 122 - height, 6, Math.Max(0.5, height), Bar);
                }
                double expectedHeight = 120.0 * expectedBin / Math.Max(maximum, expectedBin);
                histogram.Children.Add(new Line { X1 = 0, X2 = 64 * 7 + 4, Y1 = 122 - expectedHeight, Y2 = 122 - expectedHeight, Stroke = Dotted, StrokeThickness = 1 });
                panel.Children.Add(histogram);
                panel.Children.Add(Text(Properties.Resources.HistogramExplanation, 11));

                // 3. every byte against the next one
                panel.Children.Add(Text(Properties.Resources.PairsTitle, 13, true));
                var pairs = new Canvas { Width = 262, Height = 262, Background = Brushes.White };
                pairs.Children.Add(new Rectangle { Width = 258, Height = 258, Stroke = Brushes.Gray, StrokeThickness = 1 });
                for (int i = 0; i + 1 < Math.Min(data.Length, 4000); i++) { Square(pairs, 2 + data[i], 257 - data[i + 1], 2, 2, Ink); }
                panel.Children.Add(pairs);
                panel.Children.Add(Text(Properties.Resources.PairsExplanation, 11));
            });
        }
    }
}

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text;

namespace SideChannel
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("SideChannel.Properties.Resources", "PluginCaption", "PluginTooltip", "SideChannel/DetailedDescription/doc.xml", "SideChannel/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class SideChannel : ICrypComponent
    {
        private SideChannelSettings _settings = new SideChannelSettings();
        private SideChannelPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (SideChannelSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new SideChannelPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>Starts a new game when the workspace runs; the game then goes on in the presentation.</summary>
        public void Execute()
        {
            ((SideChannelPresentation)Presentation).Start(_settings.Mode, _settings.Seed, _settings.Traces, _settings.Noise);
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Two side-channel attacks on simulated power measurements. SPA (simple power analysis): in square-and-multiply a multiplication is done only for a 1 bit
    /// of the exponent, so the sequence of squarings and multiplications in one trace shows the secret exponent. DPA (differential power analysis, Kocher et al.):
    /// the power depends on the Hamming weight of what the device computes, here the S-box output of the first AES round; for every guess of the key byte the
    /// predicted weights are correlated with the measured power of many traces, and the right guess gives the highest correlation.
    /// </summary>
    public sealed class SideChannelModel
    {
        public const int ExponentBits = 16;
        public const int SamplesPerOperation = 5;
        public const int Samples = 10;
        public const int LeakSample = 4;

        public static readonly byte[] SBox = MakeSBox();

        private static byte Multiply(byte a, byte b)
        {
            int result = 0, x = a;
            for (int i = 0; i < 8; i++)
            {
                if ((b >> i & 1) == 1) { result ^= x; }
                x <<= 1;
                if ((x & 0x100) != 0) { x ^= 0x11b; }
            }
            return (byte)result;
        }

        /// <summary>The AES S-box: the inverse in GF(2^8) followed by the affine transformation.</summary>
        private static byte[] MakeSBox()
        {
            var box = new byte[256];
            for (int x = 0; x < 256; x++)
            {
                byte inverse = 0;
                for (int y = 1; y < 256 && x != 0; y++) { if (Multiply((byte)x, (byte)y) == 1) { inverse = (byte)y; break; } }
                int b = inverse, result = b;
                for (int shift = 1; shift <= 4; shift++) { result ^= ((b << shift) | (b >> (8 - shift))) & 0xff; }
                box[x] = (byte)(result ^ 0x63);
            }
            return box;
        }

        public static int Weight(int value)
        {
            int weight = 0;
            for (; value != 0; value >>= 1) { weight += value & 1; }
            return weight;
        }

        private static double Gauss(Random random)
        {
            return Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        }

        // ---- SPA

        public sealed class SpaResult
        {
            public int Exponent, Recovered;
            public string Operations;   // S and M, as the device executes them
            public double[] Trace;
            public string Reading;      // what the attacker reads from the trace
        }

        public static SpaResult Spa(int seed, double noise)
        {
            var random = new Random(seed);
            var result = new SpaResult { Exponent = (int)(random.Next(1 << (ExponentBits - 1), 1 << ExponentBits)) };
            var operations = new StringBuilder();
            for (int bit = ExponentBits - 2; bit >= 0; bit--)
            {
                operations.Append('S');
                if ((result.Exponent >> bit & 1) == 1) { operations.Append('M'); }
            }
            result.Operations = operations.ToString();
            var trace = new List<double>();
            foreach (char op in result.Operations)
            {
                for (int i = 0; i < SamplesPerOperation; i++) { trace.Add((op == 'S' ? 2.0 : 3.0) + noise * Gauss(random)); }
            }
            result.Trace = trace.ToArray();
            // the attacker: the mean of every operation decides between square and multiply
            var reading = new StringBuilder();
            for (int i = 0; i < result.Trace.Length; i += SamplesPerOperation)
            {
                reading.Append(result.Trace.Skip(i).Take(SamplesPerOperation).Average() > 2.5 ? 'M' : 'S');
            }
            result.Reading = reading.ToString();
            int value = 1;
            for (int i = 0; i < result.Reading.Length; i++)
            {
                if (result.Reading[i] == 'S') { value <<= 1; }
                else { value |= 1; }
            }
            result.Recovered = value;
            return result;
        }

        // ---- DPA

        public sealed class DpaResult
        {
            public int Key, Recovered, Traces;
            public double[] Correlation = new double[256];
            public double[] BestPerSample = new double[Samples];
            public double[] FirstTrace;
            public int Plaintext0;
        }

        public static double Pearson(double[] x, double[] y)
        {
            double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < x.Length; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
            return sxx == 0 || syy == 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
        }

        public static DpaResult Dpa(int seed, int traces, double noise)
        {
            var random = new Random(seed);
            var result = new DpaResult { Key = random.Next(256), Traces = traces };
            var plaintext = new int[traces];
            var measured = new double[traces][];
            for (int t = 0; t < traces; t++)
            {
                plaintext[t] = random.Next(256);
                measured[t] = new double[Samples];
                for (int s = 0; s < Samples; s++) { measured[t][s] = 4 + noise * Gauss(random); }
                measured[t][1] += Weight(plaintext[t]);                                   // loading of the plaintext
                measured[t][LeakSample] += Weight(SBox[plaintext[t] ^ result.Key]);      // S-box output of the first round
            }
            result.FirstTrace = measured[0];
            result.Plaintext0 = plaintext[0];
            var column = Enumerable.Range(0, traces).Select(t => measured[t][LeakSample]).ToArray();
            for (int guess = 0; guess < 256; guess++)
            {
                var predicted = Enumerable.Range(0, traces).Select(t => (double)Weight(SBox[plaintext[t] ^ guess])).ToArray();
                result.Correlation[guess] = Pearson(predicted, column);
            }
            result.Recovered = Enumerable.Range(0, 256).OrderByDescending(g => Math.Abs(result.Correlation[g])).First();
            for (int s = 0; s < Samples; s++)
            {
                var predicted = Enumerable.Range(0, traces).Select(t => (double)Weight(SBox[plaintext[t] ^ result.Recovered])).ToArray();
                result.BestPerSample[s] = Pearson(predicted, Enumerable.Range(0, traces).Select(t => measured[t][s]).ToArray());
            }
            return result;
        }
    }

    public class SideChannelPresentation : UserControl
    {
        public SideChannelPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public SideChannelModel.SpaResult Spa { get; private set; }

        public SideChannelModel.DpaResult Dpa { get; private set; }

        public void Start(int mode, int seed, int traces, int noise)
        {
            Action start = () =>
            {
                if (mode == 0) { Spa = SideChannelModel.Spa(seed, noise / 100.0 * 0.3); Dpa = null; ShowSpa(); }
                else { Dpa = SideChannelModel.Dpa(seed, traces, noise / 100.0); Spa = null; ShowDpa(); }
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private static StackPanel NewPanel(string title, string text)
        {
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            return panel;
        }

        private static void Line(StackPanel panel, string text, bool bold = false)
        {
            panel.Children.Add(new TextBlock { Text = text, FontSize = bold ? 14 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
        }

        private void ShowSpa()
        {
            var panel = NewPanel(Properties.Resources.SpaTitle, Properties.Resources.SpaText);
            Line(panel, Properties.Resources.TraceTitle, true);
            var canvas = new Canvas { Width = 820, Height = 130, Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), HorizontalAlignment = HorizontalAlignment.Left };
            double[] trace = Spa.Trace;
            double step = 800.0 / trace.Length;
            for (int i = 1; i < trace.Length; i++)
            {
                canvas.Children.Add(new Line { X1 = 10 + (i - 1) * step, Y1 = 120 - trace[i - 1] * 28, X2 = 10 + i * step, Y2 = 120 - trace[i] * 28, Stroke = Brushes.Blue, StrokeThickness = 1 });
            }
            panel.Children.Add(canvas);
            Line(panel, string.Format(Properties.Resources.SpaReading, Spa.Reading));
            Line(panel, string.Format(Properties.Resources.SpaTruth, Spa.Operations));
            Line(panel, string.Format(Properties.Resources.SpaExponent, Convert.ToString(Spa.Exponent, 2), Convert.ToString(Spa.Recovered, 2), Spa.Exponent), true);
            Line(panel, Spa.Exponent == Spa.Recovered ? Properties.Resources.Success : Properties.Resources.Failure, true);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void ShowDpa()
        {
            var panel = NewPanel(Properties.Resources.DpaTitle, Properties.Resources.DpaText);
            Line(panel, string.Format(Properties.Resources.DpaSetup, Dpa.Traces), false);
            Line(panel, Properties.Resources.CorrelationTitle, true);
            var canvas = new Canvas { Width = 20 + 256 * 3, Height = 160, Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), HorizontalAlignment = HorizontalAlignment.Left };
            double maximum = Dpa.Correlation.Max(c => Math.Abs(c));
            for (int guess = 0; guess < 256; guess++)
            {
                double height = 140 * Math.Abs(Dpa.Correlation[guess]) / Math.Max(1e-9, maximum);
                var bar = new Rectangle { Width = 2, Height = Math.Max(1, height), Fill = guess == Dpa.Recovered ? Brushes.Red : Brushes.SteelBlue };
                Canvas.SetLeft(bar, 10 + guess * 3);
                Canvas.SetTop(bar, 150 - Math.Max(1, height));
                canvas.Children.Add(bar);
            }
            panel.Children.Add(canvas);
            var sorted = Enumerable.Range(0, 256).OrderByDescending(g => Math.Abs(Dpa.Correlation[g])).Take(3).ToArray();
            Line(panel, string.Format(Properties.Resources.DpaBest, Dpa.Recovered.ToString("X2"), Math.Abs(Dpa.Correlation[Dpa.Recovered]).ToString("F3"), sorted[1].ToString("X2"), Math.Abs(Dpa.Correlation[sorted[1]]).ToString("F3")), true);
            Line(panel, string.Format(Properties.Resources.DpaKey, Dpa.Key.ToString("X2")), true);
            Line(panel, Dpa.Key == Dpa.Recovered ? Properties.Resources.Success : Properties.Resources.Failure, true);
            Line(panel, Properties.Resources.SampleTitle + " " + string.Join("  ", Dpa.BestPerSample.Select((c, i) => i + ": " + c.ToString("F2"))), false);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MathVisualization
{
    /// <summary>
    /// Shows the calculations of three public-key topics with small numbers: the numbers of an RSA example, a polynomial of Shamir's
    /// secret sharing over a small prime field, and the points of an elliptic curve over a small prime field with the addition of two points.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("MathVisualization.Properties.Resources", "PluginCaption", "PluginTooltip", "MathVisualization/DetailedDescription/doc.xml", "MathVisualization/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class MathVisualization : ICrypComponent
    {
        private MathVisualizationSettings _settings = new MathVisualizationSettings();
        private MathVisualizationPresentation _presentation;

        [PropertyInfo(Direction.InputData, "ParametersCaption", "ParametersTooltip", true)]
        public string Parameters { get; set; }

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (MathVisualizationSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new MathVisualizationPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (Parameters == null)
            {
                GuiLogMessage("The parameters are needed.", NotificationLevel.Error);
                return;
            }

            var view = (MathVisualizationPresentation)Presentation;
            try
            {
                switch (_settings.Visualization)
                {
                    case 1:
                        Calculations.ShamirResult shamir = Calculations.Shamir(Parameters);
                        Result = shamir.Secret.ToString();
                        view.ShowShamir(shamir);
                        break;
                    case 2:
                        Calculations.CurveResult curve = Calculations.Curve(Parameters);
                        Result = curve.Sum.HasValue ? curve.Sum.Value.X + "," + curve.Sum.Value.Y : (curve.First.HasValue ? "O" : "");
                        view.ShowCurve(curve);
                        break;
                    default:
                        Calculations.RsaResult rsa = Calculations.Rsa(Parameters);
                        Result = rsa.Ciphertext.ToString();
                        view.ShowRsa(rsa);
                        break;
                }
                OnPropertyChanged("Result");
            }
            catch (ArgumentException ex)
            {
                GuiLogMessage(ex.Message, NotificationLevel.Error);
            }
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            Parameters = Result = null;
        }

        public void PostExecution()
        {
            Parameters = Result = null;
        }

        public void Dispose()
        {
            Parameters = Result = null;
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

    /// <summary>The calculations, free of plugin infrastructure; invalid parameters throw ArgumentException.</summary>
    public static class Calculations
    {
        private static long Mod(long a, long n) => ((a % n) + n) % n;

        private static string[] Tokens(string parameters)
        {
            return parameters.Split(new[] { ' ', ',', ';', '\n', '\r', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 0 && (char.IsDigit(t[0]) || t[0] == '-')).ToArray();
        }

        private static BigInteger[] Numbers(string parameters, int minimum, string usage)
        {
            string[] tokens = Tokens(parameters);
            var numbers = new List<BigInteger>();
            foreach (string token in tokens)
            {
                if (!BigInteger.TryParse(token, out BigInteger number)) { throw new ArgumentException("Not a number: " + token); }
                numbers.Add(number);
            }
            if (numbers.Count < minimum) { throw new ArgumentException("The parameters are " + usage + "."); }
            return numbers.ToArray();
        }

        private static bool IsPrime(long n)
        {
            if (n < 2) { return false; }
            for (long d = 2; d * d <= n; d++) { if (n % d == 0) { return false; } }
            return true;
        }

        private static BigInteger Inverse(BigInteger a, BigInteger n)
        {
            BigInteger t = 0, newT = 1, r = n, newR = ((a % n) + n) % n;
            while (!newR.IsZero)
            {
                BigInteger q = r / newR;
                BigInteger tmp = t - q * newT; t = newT; newT = tmp;
                tmp = r - q * newR; r = newR; newR = tmp;
            }
            if (r != 1) { throw new ArgumentException(a + " has no inverse modulo " + n + "."); }
            return ((t % n) + n) % n;
        }

        // ---------------------------------------------------------------- RSA

        public sealed class RsaResult
        {
            public BigInteger P, Q, N, Phi, E, D, Message, Ciphertext, Decrypted;
        }

        /// <summary>Parameters: p q e m (two primes, the public exponent, the message as a number smaller than n).</summary>
        public static RsaResult Rsa(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 4, "p, q, e and m: two prime numbers, the public exponent and the message (a number smaller than p*q)");
            var r = new RsaResult { P = v[0], Q = v[1], E = v[2], Message = v[3] };
            if (r.P < 2 || r.Q < 2 || r.P == r.Q) { throw new ArgumentException("p and q must be two different primes."); }
            r.N = r.P * r.Q;
            r.Phi = (r.P - 1) * (r.Q - 1);
            if (r.E < 2 || r.E >= r.Phi || BigInteger.GreatestCommonDivisor(r.E, r.Phi) != 1) { throw new ArgumentException("e must be coprime to phi(n) = " + r.Phi + " and smaller than it."); }
            if (r.Message < 0 || r.Message >= r.N) { throw new ArgumentException("The message must be a number from 0 to n-1 = " + (r.N - 1) + "."); }
            r.D = Inverse(r.E, r.Phi);
            r.Ciphertext = BigInteger.ModPow(r.Message, r.E, r.N);
            r.Decrypted = BigInteger.ModPow(r.Ciphertext, r.D, r.N);
            return r;
        }

        // ---------------------------------------------------------------- Shamir

        public sealed class ShamirResult
        {
            public long P;
            public long[] Coefficients;
            public int Shares;
            public List<KeyValuePair<long, long>> Points = new List<KeyValuePair<long, long>>();   // (x, f(x) mod p) for x = 1..shares
            public long[] Curve;                                                                    // f(x) mod p for x = 0..p-1
            public long Secret;                                                                     // reconstructed from the first k shares
        }

        public static long Evaluate(long[] coefficients, long x, long p)
        {
            long result = 0;
            for (int j = coefficients.Length - 1; j >= 0; j--) { result = (result * x + coefficients[j]) % p; }
            return result;
        }

        /// <summary>Parameters: p n a0 a1 ... (a prime up to 211, the number of shares, the coefficients of the polynomial; a0 is the secret).</summary>
        public static ShamirResult Shamir(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 4, "p, n, a0, a1, ...: a prime number up to 211, the number of shares and the coefficients of the polynomial (a0 is the secret)");
            long p = (long)v[0];
            if (!IsPrime(p) || p > 211) { throw new ArgumentException("p must be a prime number up to 211."); }
            var r = new ShamirResult { P = p, Shares = (int)v[1], Coefficients = v.Skip(2).Select(c => Mod((long)c, p)).ToArray() };
            int k = r.Coefficients.Length;
            if (r.Shares < k || r.Shares >= p) { throw new ArgumentException("The number of shares must be at least the threshold (" + k + ") and smaller than p."); }
            r.Curve = Enumerable.Range(0, (int)p).Select(x => Evaluate(r.Coefficients, x, p)).ToArray();
            for (int x = 1; x <= r.Shares; x++) { r.Points.Add(new KeyValuePair<long, long>(x, r.Curve[x])); }

            // Lagrange interpolation at 0 from the first k shares
            long secret = 0;
            for (int i = 0; i < k; i++)
            {
                BigInteger numerator = 1, denominator = 1;
                for (int j = 0; j < k; j++)
                {
                    if (i == j) { continue; }
                    numerator = numerator * r.Points[j].Key % p;
                    denominator = denominator * Mod(r.Points[j].Key - r.Points[i].Key, p) % p;
                }
                secret = (long)((secret + r.Points[i].Value * numerator % p * Inverse(denominator, p)) % p);
            }
            r.Secret = secret;
            return r;
        }

        // ---------------------------------------------------------------- elliptic curve

        public struct Point
        {
            public long X, Y;
            public Point(long x, long y) { X = x; Y = y; }
        }

        public sealed class CurveResult
        {
            public long P, A, B;
            public List<Point> Points = new List<Point>();   // without the point at infinity
            public Point? First, Second, Sum;                  // Sum is null for the point at infinity
            public bool SumIsInfinity;
            public string Formula;
        }

        /// <summary>Parameters: p a b [x1 y1 x2 y2]: the curve y^2 = x^3 + a*x + b over F_p (p a prime up to 211) and optionally two points to add.</summary>
        public static CurveResult Curve(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 3, "p, a, b and optionally x1, y1, x2, y2: the curve y^2 = x^3 + a*x + b modulo the prime p, and two points on it");
            long p = (long)v[0];
            if (!IsPrime(p) || p < 3 || p > 211) { throw new ArgumentException("p must be a prime number from 3 to 211."); }
            var r = new CurveResult { P = p, A = Mod((long)v[1], p), B = Mod((long)v[2], p) };
            if (Mod(4 * r.A * r.A % p * r.A + 27 * r.B % p * r.B, p) == 0) { throw new ArgumentException("The curve is singular (4a^3 + 27b^2 = 0 modulo p)."); }
            for (long x = 0; x < p; x++)
            {
                long right = Mod(x * x % p * x + r.A * x + r.B, p);
                for (long y = 0; y < p; y++) { if (y * y % p == right) { r.Points.Add(new Point(x, y)); } }
            }

            if (v.Length >= 7)
            {
                var first = new Point(Mod((long)v[3], p), Mod((long)v[4], p));
                var second = new Point(Mod((long)v[5], p), Mod((long)v[6], p));
                if (!r.Points.Contains(first)) { throw new ArgumentException("(" + first.X + ", " + first.Y + ") is not on the curve."); }
                if (!r.Points.Contains(second)) { throw new ArgumentException("(" + second.X + ", " + second.Y + ") is not on the curve."); }
                r.First = first;
                r.Second = second;
                if (first.X == second.X && Mod(first.Y + second.Y, p) == 0)
                {
                    r.SumIsInfinity = true;
                    r.Formula = "P + Q = O (the points are opposite: same x, y1 + y2 = 0)";
                }
                else
                {
                    long lambda;
                    if (first.X == second.X)
                    {
                        lambda = (long)(Mod(3 * first.X * first.X + r.A, p) * Inverse(2 * first.Y, p) % p);
                        r.Formula = "lambda = (3*x1^2 + a) / (2*y1) = " + lambda + " (doubling)";
                    }
                    else
                    {
                        lambda = (long)(Mod(second.Y - first.Y, p) * Inverse(second.X - first.X, p) % p);
                        r.Formula = "lambda = (y2 - y1) / (x2 - x1) = " + lambda;
                    }
                    long x3 = Mod(lambda * lambda - first.X - second.X, p);
                    long y3 = Mod(lambda * (first.X - x3) - first.Y, p);
                    r.Sum = new Point(x3, y3);
                    r.Formula += "; x3 = lambda^2 - x1 - x2 = " + x3 + "; y3 = lambda*(x1 - x3) - y1 = " + y3;
                }
            }
            return r;
        }
    }

    /// <summary>The pictures, built from plain WPF elements in code.</summary>
    public class MathVisualizationPresentation : UserControl
    {
        private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(225, 232, 245));
        private static readonly Brush ResultBrush = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(240, 140, 30));
        private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(40, 90, 200));
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(200, 40, 40));
        private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(30, 150, 60));
        private static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(120, 120, 120));

        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private StackPanel _panel;

        public MathVisualizationPresentation()
        {
            Content = _scroll;
            Background = Brushes.White;
        }

        private static TextBlock Text(string text, double size = 12, bool bold = false, bool mono = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return block;
        }

        private static Border Cell(string text, Brush background, double width, double height = 24, bool bold = false)
        {
            return new Border
            {
                Width = width, Height = height, Background = background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5),
                Child = new TextBlock { Text = text, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) }
            };
        }

        private void Show(Action build)
        {
            Action show = () =>
            {
                _panel = new StackPanel { Margin = new Thickness(10) };
                build();
                _scroll.Content = _panel;
            };
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
        }

        private void Add(UIElement element) => _panel.Children.Add(element);

        private void AddRows(IEnumerable<KeyValuePair<string, string>> rows, string highlightFrom = null)
        {
            bool highlight = false;
            foreach (var row in rows)
            {
                if (row.Key == highlightFrom) { highlight = true; }
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                line.Children.Add(Cell(row.Key, HeaderBrush, 190, 24, true));
                line.Children.Add(Cell(row.Value, highlight ? ResultBrush : Brushes.White, 360));
                Add(line);
            }
        }

        private static Canvas Plot(double width, double height)
        {
            return new Canvas { Width = width, Height = height, Background = Brushes.White, Margin = new Thickness(0, 10, 0, 0) };
        }

        private static void Dot(Canvas canvas, double x, double y, double size, Brush fill, Brush stroke = null)
        {
            var dot = new Ellipse { Width = size, Height = size, Fill = fill, Stroke = stroke, StrokeThickness = stroke == null ? 0 : 1 };
            Canvas.SetLeft(dot, x - size / 2);
            Canvas.SetTop(dot, y - size / 2);
            canvas.Children.Add(dot);
        }

        private static void Segment(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double thickness = 1)
        {
            canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness });
        }

        private static void Label(Canvas canvas, string text, double x, double y, Brush color = null)
        {
            var block = new TextBlock { Text = text, FontSize = 11, Foreground = color ?? Brushes.Black };
            Canvas.SetLeft(block, x);
            Canvas.SetTop(block, y);
            canvas.Children.Add(block);
        }

        // ------------------------------------------------------------------ RSA

        public void ShowRsa(Calculations.RsaResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.RsaTitle, 16, true));
                Add(Text(Properties.Resources.RsaExplanation, 12));
                Add(new Border { Height = 8 });
                AddRows(new[]
                {
                    new KeyValuePair<string, string>("p, q", r.P + ",  " + r.Q),
                    new KeyValuePair<string, string>("n = p * q", r.P + " * " + r.Q + " = " + r.N),
                    new KeyValuePair<string, string>("phi(n) = (p - 1) * (q - 1)", (r.P - 1) + " * " + (r.Q - 1) + " = " + r.Phi),
                    new KeyValuePair<string, string>("e (" + Properties.Resources.RsaPublic + ")", r.E.ToString()),
                    new KeyValuePair<string, string>("d = e^-1 mod phi(n)", r.E + " * " + r.D + " = " + (r.E * r.D) + " = 1 mod " + r.Phi),
                    new KeyValuePair<string, string>(Properties.Resources.RsaPublicKey, "(n, e) = (" + r.N + ", " + r.E + ")"),
                    new KeyValuePair<string, string>(Properties.Resources.RsaPrivateKey, "(n, d) = (" + r.N + ", " + r.D + ")"),
                    new KeyValuePair<string, string>("m", r.Message.ToString()),
                    new KeyValuePair<string, string>("c = m^e mod n", r.Message + "^" + r.E + " mod " + r.N + " = " + r.Ciphertext),
                    new KeyValuePair<string, string>("m' = c^d mod n", r.Ciphertext + "^" + r.D + " mod " + r.N + " = " + r.Decrypted + (r.Decrypted == r.Message ? "  = m" : "")),
                }, "c = m^e mod n");
            });
        }

        // ------------------------------------------------------------------ Shamir

        public void ShowShamir(Calculations.ShamirResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ShamirTitle, 16, true));
                int k = r.Coefficients.Length;
                string polynomial = string.Join(" + ", r.Coefficients.Select((c, i) => i == 0 ? c.ToString() : c + "x" + (i > 1 ? "^" + i : "")));
                Add(Text(string.Format(Properties.Resources.ShamirExplanation, polynomial, r.P, k, r.Shares), 12));

                double cell = Math.Max(2, Math.Floor(420.0 / r.P));
                double size = cell * r.P, margin = 30;
                var canvas = Plot(size + margin + 20, size + margin + 20);
                double ScreenX(long x) => margin + x * cell + cell / 2;
                double ScreenY(long y) => margin + size - y * cell - cell / 2;
                Segment(canvas, margin, margin + size, margin + size, margin + size, Gray);
                Segment(canvas, margin, margin, margin, margin + size, Gray);
                Label(canvas, "x", margin + size + 4, margin + size - 8, Gray);
                Label(canvas, "y", margin - 14, margin - 18, Gray);
                Label(canvas, "0", margin - 12, margin + size + 2, Gray);
                Label(canvas, (r.P - 1).ToString(), margin + size - 14, margin + size + 2, Gray);
                for (long x = 0; x < r.P; x++) { Dot(canvas, ScreenX(x), ScreenY(r.Curve[x]), Math.Max(2, cell * 0.6), Gray); }
                foreach (var point in r.Points) { Dot(canvas, ScreenX(point.Key), ScreenY(point.Value), Math.Max(7, cell + 3), Orange, Brushes.Black); }
                Dot(canvas, ScreenX(0), ScreenY(r.Curve[0]), Math.Max(9, cell + 5), Green, Brushes.Black);
                Add(canvas);

                Add(Text(Properties.Resources.ShamirLegend, 12));
                var rows = new List<KeyValuePair<string, string>>();
                foreach (var point in r.Points) { rows.Add(new KeyValuePair<string, string>(Properties.Resources.ShamirShare + " " + point.Key, "(" + point.Key + ", " + point.Value + ")")); }
                rows.Add(new KeyValuePair<string, string>(string.Format(Properties.Resources.ShamirReconstructed, k), r.Secret + (r.Secret == r.Coefficients[0] ? "  = a0" : "")));
                var holder = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                _panel.Children.Add(holder);
                var previous = _panel;
                _panel = holder;
                AddRows(rows, rows[rows.Count - 1].Key);
                _panel = previous;
            });
        }

        // ------------------------------------------------------------------ elliptic curve

        public void ShowCurve(Calculations.CurveResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.CurveTitle, 16, true));
                Add(Text(string.Format(Properties.Resources.CurveExplanation, r.A, r.B, r.P, r.Points.Count + 1), 12));

                double cell = Math.Max(2, Math.Floor(420.0 / r.P));
                double size = cell * r.P, margin = 30;
                var canvas = Plot(size + margin + 20, size + margin + 20);
                double ScreenX(long x) => margin + x * cell + cell / 2;
                double ScreenY(long y) => margin + size - y * cell - cell / 2;
                Segment(canvas, margin, margin + size, margin + size, margin + size, Gray);
                Segment(canvas, margin, margin, margin, margin + size, Gray);
                Label(canvas, "x", margin + size + 4, margin + size - 8, Gray);
                Label(canvas, "y", margin - 14, margin - 18, Gray);
                foreach (var point in r.Points) { Dot(canvas, ScreenX(point.X), ScreenY(point.Y), Math.Max(3, cell * 0.7), Gray); }
                if (r.First.HasValue)
                {
                    Calculations.Point a = r.First.Value, b = r.Second.Value;
                    if (r.Sum.HasValue)
                    {
                        // the line through P and Q, the third intersection point -R and its reflection R = P + Q
                        Calculations.Point s = r.Sum.Value;
                        Segment(canvas, ScreenX(a.X), ScreenY(a.Y), ScreenX(b.X), ScreenY(b.Y), Blue, 1);
                        Segment(canvas, ScreenX(s.X), ScreenY(s.Y), ScreenX(s.X), ScreenY(r.P - s.Y), Green, 1);
                        Dot(canvas, ScreenX(s.X), ScreenY(r.P - s.Y), Math.Max(6, cell + 2), Brushes.White, Green);
                        Dot(canvas, ScreenX(s.X), ScreenY(s.Y), Math.Max(9, cell + 5), Green, Brushes.Black);
                        Label(canvas, "P+Q", ScreenX(s.X) + 6, ScreenY(s.Y) - 14, Green);
                    }
                    Dot(canvas, ScreenX(a.X), ScreenY(a.Y), Math.Max(9, cell + 5), Red, Brushes.Black);
                    Label(canvas, "P", ScreenX(a.X) + 6, ScreenY(a.Y) - 14, Red);
                    if (!(a.X == b.X && a.Y == b.Y))
                    {
                        Dot(canvas, ScreenX(b.X), ScreenY(b.Y), Math.Max(9, cell + 5), Blue, Brushes.Black);
                        Label(canvas, "Q", ScreenX(b.X) + 6, ScreenY(b.Y) - 14, Blue);
                    }
                }
                Add(canvas);
                if (r.First.HasValue)
                {
                    Add(Text(string.Format(Properties.Resources.CurveSum, r.First.Value.X, r.First.Value.Y, r.Second.Value.X, r.Second.Value.Y,
                        r.Sum.HasValue ? "(" + r.Sum.Value.X + ", " + r.Sum.Value.Y + ")" : "O"), 13, true));
                    Add(Text(r.Formula, 12, false, true));
                    Add(Text(Properties.Resources.CurveLegend, 12));
                }
                else
                {
                    Add(Text(Properties.Resources.CurveNoPoints, 12));
                }
            });
        }
    }
}

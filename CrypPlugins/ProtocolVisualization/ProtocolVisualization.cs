using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProtocolVisualization
{
    /// <summary>
    /// Shows two protocols as a message sequence between Alice and Bob, with every number calculated: the Diffie-Hellman key exchange with
    /// fresh exponents for every session (perfect forward secrecy), and a digital signature (hash, RSA signature, verification).
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ProtocolVisualization.Properties.Resources", "PluginCaption", "PluginTooltip", "ProtocolVisualization/DetailedDescription/doc.xml", "ProtocolVisualization/icon.png")]
    [ComponentCategory(ComponentCategory.Protocols)]
    public class ProtocolVisualization : ICrypComponent
    {
        private ProtocolVisualizationSettings _settings = new ProtocolVisualizationSettings();
        private ProtocolVisualizationPresentation _presentation;

        [PropertyInfo(Direction.InputData, "ParametersCaption", "ParametersTooltip", true)]
        public string Parameters { get; set; }

        [PropertyInfo(Direction.InputData, "MessageCaption", "MessageTooltip", false)]
        public string Message { get; set; }

        [PropertyInfo(Direction.InputData, "ReceivedCaption", "ReceivedTooltip", false)]
        public string Received { get; set; }

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (ProtocolVisualizationSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new ProtocolVisualizationPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            var view = (ProtocolVisualizationPresentation)Presentation;
            if (Parameters == null)
            {
                GuiLogMessage("The parameters are needed.", NotificationLevel.Error);
                view.ShowMessage("The parameters are needed.");
                return;
            }

            try
            {
                if (_settings.Protocol == 1)
                {
                    Calculations.SignatureResult signature = Calculations.Signature(Parameters, Message ?? "", Received ?? Message ?? "");
                    Result = signature.Valid ? "valid" : "invalid";
                    view.ShowSignature(signature);
                }
                else
                {
                    Calculations.ExchangeResult exchange = Calculations.Exchange(Parameters);
                    Result = string.Join(",", exchange.Sessions.Select(s => s.Key.ToString()));
                    view.ShowExchange(exchange);
                }
                OnPropertyChanged("Result");
            }
            catch (ArgumentException ex)
            {
                GuiLogMessage(ex.Message, NotificationLevel.Error);
                view.ShowMessage(ex.Message);
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
            Parameters = Message = Received = Result = null;
        }

        public void PostExecution()
        {
            Parameters = Message = Received = Result = null;
        }

        public void Dispose()
        {
            Parameters = Message = Received = Result = null;
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

    /// <summary>The calculations of the protocols, free of plugin infrastructure; invalid parameters throw ArgumentException.</summary>
    public static class Calculations
    {
        private static BigInteger[] Numbers(string parameters, int minimum, string usage)
        {
            var numbers = new List<BigInteger>();
            foreach (string token in parameters.Split(new[] { ' ', ',', ';', '\n', '\r', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length == 0 || !(char.IsDigit(token[0]) || token[0] == '-')) { continue; }
                if (!BigInteger.TryParse(token, out BigInteger number)) { throw new ArgumentException("Not a number: " + token); }
                numbers.Add(number);
            }
            if (numbers.Count < minimum) { throw new ArgumentException("The parameters are " + usage + "."); }
            return numbers.ToArray();
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

        /// <summary>Miller-Rabin test with the first twelve primes as bases; exact for numbers below 3.3 * 10^24.</summary>
        private static bool IsPrime(BigInteger n)
        {
            if (n < 2) { return false; }
            foreach (int p in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                if (n == p) { return true; }
                if (n % p == 0) { return false; }
            }
            BigInteger d = n - 1;
            int s = 0;
            while (d.IsEven) { d >>= 1; s++; }
            foreach (int a in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) { continue; }
                bool composite = true;
                for (int r = 1; r < s && composite; r++)
                {
                    x = x * x % n;
                    if (x == n - 1) { composite = false; }
                }
                if (composite) { return false; }
            }
            return true;
        }

        // ---------------------------------------------------------------- Diffie-Hellman

        public sealed class Session
        {
            public BigInteger A, B;          // the secret exponents of Alice and Bob
            public BigInteger PublicA, PublicB;
            public BigInteger Key;           // g^(ab) mod p, as computed by Alice and by Bob
            public BigInteger KeyAlice, KeyBob;
            public BigInteger? Found;        // the exponent that an eavesdropper finds by trying (small p only)
        }

        public sealed class ExchangeResult
        {
            public BigInteger P, G;
            public List<Session> Sessions = new List<Session>();
        }

        /// <summary>Parameters: p g a1 b1 [a2 b2 ...]: the prime, the generator, and the secret exponents of Alice and Bob for each session.</summary>
        public static ExchangeResult Exchange(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 4, "p, g, and then the secret exponents of Alice and Bob for each session: p g a1 b1 [a2 b2 ...]");
            var result = new ExchangeResult { P = v[0], G = v[1] };
            if (result.P < 5 || !IsPrime(result.P) || result.G < 2 || result.G >= result.P - 1) { throw new ArgumentException("p must be a prime number of at least 5 and g a number from 2 to p-2."); }
            for (int i = 2; i + 1 < v.Length && result.Sessions.Count < 3; i += 2)
            {
                var s = new Session { A = v[i], B = v[i + 1] };
                if (s.A < 1 || s.B < 1) { throw new ArgumentException("The exponents must be positive."); }
                s.PublicA = BigInteger.ModPow(result.G, s.A, result.P);
                s.PublicB = BigInteger.ModPow(result.G, s.B, result.P);
                s.KeyAlice = BigInteger.ModPow(s.PublicB, s.A, result.P);
                s.KeyBob = BigInteger.ModPow(s.PublicA, s.B, result.P);
                s.Key = s.KeyAlice;
                if (result.P < 2000000)
                {
                    // the eavesdropper knows p, g and A: tries the exponents one after the other
                    BigInteger x = 1;
                    for (long e = 1; e < (long)result.P; e++)
                    {
                        x = x * result.G % result.P;
                        if (x == s.PublicA) { s.Found = e; break; }
                    }
                }
                result.Sessions.Add(s);
            }
            return result;
        }

        // ---------------------------------------------------------------- signature

        public sealed class SignatureResult
        {
            public BigInteger P, Q, N, Phi, E, D;
            public string Message, Received;
            public string Hash, ReceivedHash;                 // SHA-256 as hexadecimal text
            public BigInteger Reduced, Signature, Recovered, ReceivedReduced;
            public bool Valid;
            public bool Tampered;
        }

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
            }
        }

        private static BigInteger Reduce(string hash, BigInteger n)
        {
            return BigInteger.Parse("0" + hash, System.Globalization.NumberStyles.HexNumber) % n;
        }

        /// <summary>
        /// Parameters: p q e (two primes and the public exponent of an RSA key). The message is hashed with SHA-256, the hash value (reduced modulo n,
        /// which is only needed because the keys of the example are small) is signed with the private key; the receiver checks the received message.
        /// </summary>
        public static SignatureResult Signature(string parameters, string message, string received)
        {
            BigInteger[] v = Numbers(parameters, 3, "p, q and e: two prime numbers and the public exponent of an RSA key");
            var r = new SignatureResult { P = v[0], Q = v[1], E = v[2], Message = message, Received = received };
            if (!IsPrime(r.P) || !IsPrime(r.Q) || r.P == r.Q) { throw new ArgumentException("p and q must be two different primes."); }
            r.N = r.P * r.Q;
            r.Phi = (r.P - 1) * (r.Q - 1);
            if (r.E < 2 || r.E >= r.Phi || BigInteger.GreatestCommonDivisor(r.E, r.Phi) != 1) { throw new ArgumentException("e must be coprime to phi(n) = " + r.Phi + " and smaller than it."); }
            r.D = Inverse(r.E, r.Phi);
            r.Hash = Sha256(message);
            r.Reduced = Reduce(r.Hash, r.N);
            r.Signature = BigInteger.ModPow(r.Reduced, r.D, r.N);
            r.ReceivedHash = Sha256(received);
            r.ReceivedReduced = Reduce(r.ReceivedHash, r.N);
            r.Recovered = BigInteger.ModPow(r.Signature, r.E, r.N);
            r.Valid = r.Recovered == r.ReceivedReduced;
            r.Tampered = received != message;
            return r;
        }
    }

    /// <summary>The pictures, built from plain WPF elements in code.</summary>
    public class ProtocolVisualizationPresentation : UserControl
    {
        private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(225, 232, 245));
        private static readonly Brush AliceBrush = new SolidColorBrush(Color.FromRgb(255, 240, 215));
        private static readonly Brush BobBrush = new SolidColorBrush(Color.FromRgb(215, 235, 255));
        private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush BadBrush = new SolidColorBrush(Color.FromRgb(240, 160, 160));
        private static readonly Brush EveBrush = new SolidColorBrush(Color.FromRgb(240, 215, 240));

        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private StackPanel _panel;

        public ProtocolVisualizationPresentation()
        {
            Content = _scroll;
            Background = Brushes.White;
            _scroll.Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) };
        }

        private static TextBlock Text(string text, double size = 12, bool bold = false, bool mono = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return block;
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

        public void ShowMessage(string message)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ErrorTitle, 16, true));
                Add(Text(message, 13));
                Add(Text(Environment.NewLine + Properties.Resources.ParameterExamples, 12, false, true));
            });
        }

        private static Border Box(string text, Brush background, double width, bool bold = false, bool mono = true)
        {
            var block = new TextBlock { Text = text, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 3, 6, 3) };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return new Border { Width = width, Background = background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5), Child = block };
        }

        /// <summary>One line of the sequence: what Alice calculates, the message between them (an arrow), what Bob calculates.</summary>
        private void Row(string alice, string arrow, string bob, bool toBob = true, Brush arrowBrush = null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(Box(alice, alice.Length > 0 ? AliceBrush : Brushes.White, 270));
            row.Children.Add(Box(arrow.Length > 0 ? (toBob ? arrow + "  \u2192" : "\u2190  " + arrow) : "", arrowBrush ?? (arrow.Length > 0 ? EveBrush : Brushes.White), 250, true));
            row.Children.Add(Box(bob, bob.Length > 0 ? BobBrush : Brushes.White, 270));
            Add(row);
        }

        private void Heading(string left, string middle, string right)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
            row.Children.Add(Box(left, HeaderBrush, 270, true, false));
            row.Children.Add(Box(middle, HeaderBrush, 250, true, false));
            row.Children.Add(Box(right, HeaderBrush, 270, true, false));
            Add(row);
        }

        // ------------------------------------------------------------------ Diffie-Hellman

        public void ShowExchange(Calculations.ExchangeResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ExchangeTitle, 16, true));
                Add(Text(string.Format(Properties.Resources.ExchangeExplanation, r.P, r.G), 12));
                int number = 0;
                foreach (var s in r.Sessions)
                {
                    number++;
                    Add(Text(string.Format(Properties.Resources.SessionTitle, number), 14, true));
                    Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                    Row(string.Format(Properties.Resources.ChooseSecret, "a", s.A), "", string.Format(Properties.Resources.ChooseSecret, "b", s.B));
                    Row("A = g^a mod p = " + r.G + "^" + s.A + " mod " + r.P + " = " + s.PublicA, "A = " + s.PublicA, "");
                    Row("", "B = " + s.PublicB, "B = g^b mod p = " + r.G + "^" + s.B + " mod " + r.P + " = " + s.PublicB, false);
                    Row("K = B^a mod p = " + s.PublicB + "^" + s.A + " mod " + r.P + " = " + s.KeyAlice, "", "K = A^b mod p = " + s.PublicA + "^" + s.B + " mod " + r.P + " = " + s.KeyBob);
                    var key = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                    key.Children.Add(Box(string.Format(Properties.Resources.SameKey, s.Key), GoodBrush, 790, true, false));
                    Add(key);
                    var eve = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                    eve.Children.Add(Box(s.Found.HasValue
                        ? string.Format(Properties.Resources.EveFound, r.P, r.G, s.PublicA, s.Found.Value, BigInteger.ModPow(s.PublicB, s.Found.Value, r.P))
                        : Properties.Resources.EveFails, EveBrush, 790, false, false));
                    Add(eve);
                }
                if (r.Sessions.Count > 1)
                {
                    bool different = r.Sessions.Select(s => s.Key).Distinct().Count() == r.Sessions.Count;
                    Add(Text(Environment.NewLine + (different ? Properties.Resources.ForwardSecrecyDifferent : Properties.Resources.ForwardSecrecySame), 13, true));
                }
                Add(Text(Properties.Resources.ForwardSecrecyExplanation, 12));
            });
        }

        // ------------------------------------------------------------------ signature

        public void ShowSignature(Calculations.SignatureResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.SignatureTitle, 16, true));
                Add(Text(string.Format(Properties.Resources.SignatureExplanation, r.N, r.E, r.D), 12));
                Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                Row(Properties.Resources.Message + ": \"" + r.Message + "\"", "", "");
                Row("H = SHA-256(" + Properties.Resources.Message + ")\n" + r.Hash, "", "");
                Row("h = H mod n = " + r.Reduced, "", "");
                Row("s = h^d mod n = " + r.Reduced + "^" + r.D + " mod " + r.N + " = " + r.Signature, "", "");
                Row("", Properties.Resources.Message + (r.Tampered ? " (" + Properties.Resources.Changed + ")" : "") + ", s = " + r.Signature, "", true, r.Tampered ? BadBrush : null);
                Row("", "", Properties.Resources.Received + ": \"" + r.Received + "\"");
                Row("", "", "H' = SHA-256(" + Properties.Resources.Received + ")\n" + r.ReceivedHash);
                Row("", "", "h' = H' mod n = " + r.ReceivedReduced);
                Row("", "", "s^e mod n = " + r.Signature + "^" + r.E + " mod " + r.N + " = " + r.Recovered);
                var verdict = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                verdict.Children.Add(Box(r.Valid
                    ? string.Format(Properties.Resources.SignatureValid, r.Recovered)
                    : string.Format(Properties.Resources.SignatureInvalid, r.Recovered, r.ReceivedReduced), r.Valid ? GoodBrush : BadBrush, 790, true, false));
                Add(verdict);
                if (r.N < BigInteger.Pow(2, 64)) { Add(Text(Environment.NewLine + Properties.Resources.SmallKeyNote, 12)); }
            });
        }
    }
}

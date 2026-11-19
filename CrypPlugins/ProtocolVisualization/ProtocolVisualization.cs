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
                if (_settings.Protocol == 2)
                {
                    Calculations.AuthenticationResult authentication = Calculations.Authentication(Parameters);
                    Result = authentication.Accepted ? "ok" : "failed";
                    view.ShowAuthentication(authentication);
                }
                else if (_settings.Protocol == 3)
                {
                    Calculations.SmimeResult smime = Calculations.Smime(Parameters, Message ?? "");
                    Result = smime.Valid ? "valid" : "invalid";
                    view.ShowSmime(smime);
                }
                else if (_settings.Protocol == 1)
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


        // ---------------------------------------------------------------- authentication in a network

        public sealed class AuthenticationResult
        {
            public string Password, Nonce1, Nonce2;
            public string PasswordHash;                 // SHA-256 of the password
            public string Response1, Response2;         // HMAC-SHA-256 of the nonces with the password as key
            public bool Accepted;                       // the genuine response to the first challenge is accepted
            public bool ReplayAccepted;                 // the response to the first challenge, replayed against the second one
        }

        public static string Hmac(string key, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "");
            }
        }

        /// <summary>Parameters: password nonce1 nonce2 (three words): the shared password and the two challenges that the server sends in two logins.</summary>
        public static AuthenticationResult Authentication(string parameters)
        {
            string[] words = parameters.Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3) { throw new ArgumentException("The parameters are password, nonce1 and nonce2: the password and the two challenges of the server, as three words."); }
            var r = new AuthenticationResult { Password = words[0], Nonce1 = words[1], Nonce2 = words[2] };
            r.PasswordHash = Sha256(r.Password);
            r.Response1 = Hmac(r.Password, r.Nonce1);
            r.Response2 = Hmac(r.Password, r.Nonce2);
            r.Accepted = r.Response1 == Hmac(r.Password, r.Nonce1);
            r.ReplayAccepted = r.Response1 == r.Response2;
            return r;
        }

        // ---------------------------------------------------------------- S/MIME

        public sealed class SmimeResult
        {
            public BigInteger NAlice, EAlice, DAlice, NBob, EBob, DBob;
            public string Message, Hash, SessionKey, Cipher;
            public BigInteger Reduced, Signature, Recovered;
            public List<BigInteger> WrappedKey = new List<BigInteger>();
            public string Decrypted;
            public bool Valid;
        }

        /// <summary>
        /// Parameters: pA qA eA pB qB eB: the RSA keys of Alice (signing) and Bob (encryption) as two primes and a public exponent each; n of Bob must exceed 255.
        /// Alice signs the hash of the message, encrypts message and signature with a session key (AES-128-CBC) and encrypts the session key for Bob with his RSA key
        /// (byte by byte only because the example key is so small); Bob reverses the steps and verifies the signature.
        /// </summary>
        public static SmimeResult Smime(string parameters, string message)
        {
            BigInteger[] v = Numbers(parameters, 6, "pA qA eA pB qB eB: two primes and the public exponent for Alice's signature key, and the same for Bob's encryption key");
            var r = new SmimeResult { Message = message };
            Func<BigInteger, BigInteger, BigInteger, BigInteger[]> key = (p, q, e) =>
            {
                if (!IsPrime(p) || !IsPrime(q) || p == q) { throw new ArgumentException("p and q must be two different primes."); }
                BigInteger phi = (p - 1) * (q - 1);
                if (e < 2 || e >= phi || BigInteger.GreatestCommonDivisor(e, phi) != 1) { throw new ArgumentException("e must be coprime to phi(n) = " + phi + " and smaller than it."); }
                return new[] { p * q, e, Inverse(e, phi) };
            };
            BigInteger[] alice = key(v[0], v[1], v[2]), bob = key(v[3], v[4], v[5]);
            r.NAlice = alice[0]; r.EAlice = alice[1]; r.DAlice = alice[2];
            r.NBob = bob[0]; r.EBob = bob[1]; r.DBob = bob[2];
            if (r.NBob < 256) { throw new ArgumentException("The modulus of Bob's key must be at least 256, because the session key is encrypted byte by byte."); }

            // Alice: sign, then encrypt message and signature with the session key, then wrap the session key for Bob
            r.Hash = Sha256(message);
            r.Reduced = Reduce(r.Hash, r.NAlice);
            r.Signature = BigInteger.ModPow(r.Reduced, r.DAlice, r.NAlice);
            byte[] sessionKey;
            using (var sha = SHA256.Create()) { sessionKey = sha.ComputeHash(Encoding.UTF8.GetBytes("session key for " + message)).Take(16).ToArray(); }
            r.SessionKey = BitConverter.ToString(sessionKey).Replace("-", "");
            byte[] cipher = AesCbc(sessionKey, Encoding.UTF8.GetBytes(message + "|" + r.Signature), true);
            r.Cipher = BitConverter.ToString(cipher).Replace("-", "");
            foreach (byte b in sessionKey) { r.WrappedKey.Add(BigInteger.ModPow(b, r.EBob, r.NBob)); }

            // Bob: unwrap the session key, decrypt, verify the signature
            byte[] unwrapped = r.WrappedKey.Select(c => (byte)BigInteger.ModPow(c, r.DBob, r.NBob)).ToArray();
            string plain = Encoding.UTF8.GetString(AesCbc(unwrapped, cipher, false));
            int bar = plain.LastIndexOf('|');
            r.Decrypted = bar < 0 ? plain : plain.Substring(0, bar);
            BigInteger.TryParse(bar < 0 ? "0" : plain.Substring(bar + 1), out BigInteger signature);
            r.Recovered = BigInteger.ModPow(signature, r.EAlice, r.NAlice);
            r.Valid = r.Recovered == Reduce(Sha256(r.Decrypted), r.NAlice);
            return r;
        }

        private static byte[] AesCbc(byte[] key, byte[] data, bool encrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = new byte[16];   // a fixed IV is only acceptable because every message has its own session key
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                {
                    return transform.TransformFinalBlock(data, 0, data.Length);
                }
            }
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

        public static BigInteger Sha256Reduced(string text, BigInteger n) => Reduce(Sha256(text), n);

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


        // ------------------------------------------------------------------ authentication

        private static string Short(string hex) => hex.Length > 32 ? hex.Substring(0, 32) + "..." : hex;

        public void ShowAuthentication(Calculations.AuthenticationResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.AuthenticationTitle, 16, true));
                Add(Text(Properties.Resources.AuthenticationExplanation, 12));

                Add(Text(Properties.Resources.AuthPlainTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("", "login: alice, password: " + r.Password, "password is " + r.Password + ": accepted");
                Add(EveLine(string.Format(Properties.Resources.AuthPlainEve, r.Password), BadBrush));

                Add(Text(Properties.Resources.AuthHashTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("H = SHA-256(password)\n" + Short(r.PasswordHash), "login: alice, H = " + Short(r.PasswordHash), "compares H with the stored value: accepted");
                Add(EveLine(Properties.Resources.AuthHashEve, BadBrush));

                Add(Text(Properties.Resources.AuthChallengeTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("", string.Format(Properties.Resources.AuthChallenge, r.Nonce1), "chooses the challenge " + r.Nonce1, false);
                Row("R = HMAC(password, " + r.Nonce1 + ")\n" + Short(r.Response1), "R = " + Short(r.Response1), "computes the same value from the stored password: " + (r.Accepted ? "accepted" : "rejected"));
                Row("", string.Format(Properties.Resources.AuthChallenge, r.Nonce2), "next login, new challenge " + r.Nonce2, false);
                Row("Eve sends the old response\n" + Short(r.Response1), "R = " + Short(r.Response1), "expects HMAC(password, " + r.Nonce2 + ")\n" + Short(r.Response2) + "\n" + (r.ReplayAccepted ? "the same: replay accepted" : "different: rejected"), true, r.ReplayAccepted ? BadBrush : GoodBrush);
                Add(EveLine(r.ReplayAccepted ? Properties.Resources.AuthSameNonce : Properties.Resources.AuthChallengeEve, r.ReplayAccepted ? BadBrush : GoodBrush));
            });
        }

        private static StackPanel EveLine(string text, Brush brush)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            line.Children.Add(Box(text, brush, 790, false, false));
            return line;
        }

        // ------------------------------------------------------------------ S/MIME

        public void ShowSmime(Calculations.SmimeResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.SmimeTitle, 16, true));
                Add(Text(Properties.Resources.SmimeExplanation, 12));
                Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                Row(Properties.Resources.Message + ": \"" + r.Message + "\"", "", "");
                Row("1. h = SHA-256(" + Properties.Resources.Message + ") mod nA = " + r.Reduced + "\ns = h^dA mod nA = " + r.Signature, "", "");
                Row("2. K = " + r.SessionKey + "\n(" + Properties.Resources.SessionKeyNote + ")", "", "");
                Row("3. AES-128-CBC(K, message + signature)\n" + Short(r.Cipher), "", "");
                Row("4. K^eB mod nB, byte by byte\n" + string.Join(" ", r.WrappedKey.Take(6)) + " ...", "", "");
                Row("", string.Join(" ", r.WrappedKey.Take(3)) + " ...\n" + Short(r.Cipher), "", true);
                Row("", "", "5. K^dB mod nB: " + r.SessionKey);
                Row("", "", "6. AES decryption: \"" + r.Decrypted + "\" and s = " + r.Signature);
                Row("", "", "7. s^eA mod nA = " + r.Recovered + "\nSHA-256 of the message mod nA = " + Reduce256(r));
                var verdict = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                verdict.Children.Add(Box(r.Valid ? Properties.Resources.SmimeValid : Properties.Resources.SmimeInvalid, r.Valid ? GoodBrush : BadBrush, 790, true, false));
                Add(verdict);
                Add(Text(Environment.NewLine + Properties.Resources.SmimeNote, 12));
            });
        }

        private static string Reduce256(Calculations.SmimeResult r) => Calculations.Sha256Reduced(r.Decrypted, r.NAlice).ToString();

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

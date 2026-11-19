using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Numerics;

namespace HomomorphicEncryption
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("HomomorphicEncryption.Properties.Resources", "PluginCaption", "PluginTooltip", "HomomorphicEncryption/DetailedDescription/doc.xml", "HomomorphicEncryption/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class HomomorphicEncryption : ICrypComponent
    {
        private HomomorphicEncryptionSettings _settings = new HomomorphicEncryptionSettings();

        [PropertyInfo(Direction.InputData, "InputACaption", "InputATooltip", true)]
        public string InputA { get; set; }

        [PropertyInfo(Direction.InputData, "InputBCaption", "InputBTooltip", true)]
        public string InputB { get; set; }

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged("Result"); }
        }
        private string _result;

        [PropertyInfo(Direction.OutputData, "ReportCaption", "ReportTooltip", false)]
        public string Report
        {
            get => _report;
            set { _report = value; OnPropertyChanged("Report"); }
        }
        private string _report;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (HomomorphicEncryptionSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            try
            {
                BigInteger a = BigInteger.Parse(InputA.Trim()), b = BigInteger.Parse(InputB.Trim());
                var report = new StringBuilder();
                if (_settings.Scheme == 0)
                {
                    var rsa = new Homomorphic.RsaScheme(_settings.Seed);
                    BigInteger ca = rsa.Encrypt(a), cb = rsa.Encrypt(b), product = rsa.Multiply(ca, cb);
                    report.AppendLine("RSA: n = " + rsa.N + ", e = " + rsa.E);
                    report.AppendLine("E(" + a + ") = " + ca); report.AppendLine("E(" + b + ") = " + cb);
                    report.AppendLine("E(a) * E(b) mod n = " + product);
                    Result = rsa.Decrypt(product).ToString();
                    report.AppendLine("Decryption: " + Result + " = " + a + " * " + b + " mod n: " + (Result == (a * b % rsa.N).ToString() ? "equal" : "different"));
                }
                else if (_settings.Scheme == 1)
                {
                    var paillier = new Homomorphic.PaillierScheme(_settings.Seed);
                    BigInteger ca = paillier.Encrypt(a), cb = paillier.Encrypt(b), sum = paillier.Add(ca, cb);
                    report.AppendLine("Paillier: n = " + paillier.N);
                    report.AppendLine("E(" + a + ") = " + ca); report.AppendLine("E(" + b + ") = " + cb);
                    report.AppendLine("E(a) * E(b) mod n^2 = " + sum);
                    Result = paillier.Decrypt(sum).ToString();
                    report.AppendLine("Decryption: " + Result + " = " + a + " + " + b + " mod n: " + (Result == ((a + b) % paillier.N).ToString() ? "equal" : "different"));
                    report.AppendLine("E(a)^" + b + " mod n^2 decrypts to " + paillier.Decrypt(paillier.Scale(ca, b)) + " = " + a + " * " + b + " mod n");
                }
                else
                {
                    if (a > 1 || b > 1 || a < 0 || b < 0) { throw new ArgumentException("DGHV encrypts bits: the numbers must be 0 or 1."); }
                    var dghv = new Homomorphic.DghvScheme(_settings.Seed);
                    BigInteger ca = dghv.Encrypt((int)a), cb = dghv.Encrypt((int)b);
                    report.AppendLine("Secret p: " + dghv.P.GetBitLength() + " bits; fresh ciphertexts have " + dghv.Noise(ca) + " and " + dghv.Noise(cb) + " noise bits");
                    BigInteger x = dghv.Xor(ca, cb), y = dghv.And(ca, cb);
                    report.AppendLine("E(a) + E(b) decrypts to " + dghv.Decrypt(x) + " = a XOR b = " + (a ^ b) + " (noise " + dghv.Noise(x) + " bits)");
                    report.AppendLine("E(a) * E(b) decrypts to " + dghv.Decrypt(y) + " = a AND b = " + (a & b) + " (noise " + dghv.Noise(y) + " bits)");
                    BigInteger deep = ca;
                    for (int level = 1; level <= 12; level++)
                    {
                        deep = dghv.And(deep, dghv.Encrypt((int)a));
                        int noise = dghv.Noise(deep);
                        report.AppendLine("After " + level + " more multiplications: noise " + noise + " bits of " + (dghv.P.GetBitLength() - 1) + ", decryption " + (dghv.Decrypt(deep) == (int)a ? "correct" : "WRONG"));
                        if (dghv.Decrypt(deep) != (int)a) { break; }
                    }
                    Result = dghv.Decrypt(y).ToString();
                }
                Report = report.ToString().TrimEnd();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is InvalidOperationException || ex is OverflowException)
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
            InputA = InputB = Result = Report = null;
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
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


    public static class Homomorphic
    {
        public static BigInteger NextPrime(Random random, int bits)
        {
            while (true)
            {
                var bytes = new byte[(bits + 7) / 8 + 1];
                random.NextBytes(bytes);
                bytes[bytes.Length - 1] = 0;
                BigInteger candidate = (new BigInteger(bytes) & ((BigInteger.One << bits) - 1)) | (BigInteger.One << (bits - 1)) | 1;
                if (IsPrime(candidate)) { return candidate; }
            }
        }

        public static bool IsPrime(BigInteger n)
        {
            if (n < 2) { return false; }
            foreach (int p in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }) { if (n == p) { return true; } if (n % p == 0) { return false; } }
            BigInteger d = n - 1; int s = 0;
            while (d.IsEven) { d >>= 1; s++; }
            foreach (int a in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) { continue; }
                bool composite = true;
                for (int r = 1; r < s && composite; r++) { x = x * x % n; if (x == n - 1) { composite = false; } }
                if (composite) { return false; }
            }
            return true;
        }

        public static BigInteger Inverse(BigInteger a, BigInteger m)
        {
            BigInteger g = m, x = 0, y = 1, r = a % m;
            while (r != 0) { BigInteger q = g / r; BigInteger t = g - q * r; g = r; r = t; t = x - q * y; x = y; y = t; }
            return (x % m + m) % m;
        }

        public static BigInteger RandomBelow(Random random, BigInteger limit)
        {
            var bytes = new byte[limit.ToByteArray().Length + 1];
            random.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            return new BigInteger(bytes) % (limit - 1) + 1;
        }

        // ---- RSA: multiplicative

        public sealed class RsaScheme
        {
            public BigInteger N, E = 65537, D;

            public RsaScheme(int seed)
            {
                var random = new Random(seed);
                BigInteger p = NextPrime(random, 64), q;
                do { q = NextPrime(random, 64); } while (q == p);
                N = p * q;
                D = Inverse(E, (p - 1) * (q - 1));
            }

            public BigInteger Encrypt(BigInteger m) => BigInteger.ModPow(m, E, N);

            public BigInteger Decrypt(BigInteger c) => BigInteger.ModPow(c, D, N);

            /// <summary>The product of two ciphertexts is the ciphertext of the product.</summary>
            public BigInteger Multiply(BigInteger c1, BigInteger c2) => c1 * c2 % N;
        }

        // ---- Paillier: additive

        public sealed class PaillierScheme
        {
            public BigInteger N, N2, G, Lambda, Mu;
            private readonly Random _random;

            public PaillierScheme(int seed)
            {
                _random = new Random(seed);
                BigInteger p = NextPrime(_random, 64), q;
                do { q = NextPrime(_random, 64); } while (q == p);
                N = p * q; N2 = N * N; G = N + 1;
                Lambda = (p - 1) * (q - 1) / BigInteger.GreatestCommonDivisor(p - 1, q - 1);
                Mu = Inverse(L(BigInteger.ModPow(G, Lambda, N2)), N);
            }

            private BigInteger L(BigInteger x) => (x - 1) / N;

            public BigInteger Encrypt(BigInteger m)
            {
                BigInteger r;
                do { r = RandomBelow(_random, N); } while (BigInteger.GreatestCommonDivisor(r, N) != 1);
                return BigInteger.ModPow(G, m, N2) * BigInteger.ModPow(r, N, N2) % N2;
            }

            public BigInteger Decrypt(BigInteger c) => L(BigInteger.ModPow(c, Lambda, N2)) * Mu % N;

            /// <summary>The product of two ciphertexts is the ciphertext of the sum of the plaintexts.</summary>
            public BigInteger Add(BigInteger c1, BigInteger c2) => c1 * c2 % N2;

            /// <summary>The power of a ciphertext is the ciphertext of the multiple of the plaintext.</summary>
            public BigInteger Scale(BigInteger c, BigInteger k) => BigInteger.ModPow(c, k, N2);
        }

        // ---- DGHV: the fully homomorphic scheme over the integers of van Dijk, Gentry, Halevi and Vaikuntanathan (symmetric version)

        public sealed class DghvScheme
        {
            public const int NoiseBits = 12;
            public BigInteger P;
            private readonly Random _random;

            public DghvScheme(int seed)
            {
                _random = new Random(seed);
                P = NextPrime(_random, 128);
            }

            /// <summary>A bit m is encrypted as m + 2 r + p q with small noise r and a large random q.</summary>
            public BigInteger Encrypt(int bit)
            {
                BigInteger q = RandomBelow(_random, BigInteger.One << 512), r = RandomBelow(_random, BigInteger.One << NoiseBits);
                return bit + 2 * r + P * q;
            }

            private BigInteger Centered(BigInteger c)
            {
                BigInteger m = ((c % P) + P) % P;
                return m > P / 2 ? m - P : m;
            }

            public int Decrypt(BigInteger c) => (int)(((Centered(c) % 2) + 2) % 2);

            /// <summary>The size of the noise (bits) in the ciphertext; decryption works while it stays below p / 2.</summary>
            public int Noise(BigInteger c) => (int)BigInteger.Abs(Centered(c)).GetBitLength();

            public BigInteger Xor(BigInteger a, BigInteger b) => a + b;

            public BigInteger And(BigInteger a, BigInteger b) => a * b;
        }
    }

}

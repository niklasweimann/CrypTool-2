using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace MlKem
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("MlKem.Properties.Resources", "PluginCaption", "PluginTooltip", "MlKem/DetailedDescription/doc.xml", "MlKem/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class MlKem : ICrypComponent
    {
        private MlKemSettings _settings = new MlKemSettings();

        [PropertyInfo(Direction.InputData, "EncapsulationKeyCaption", "EncapsulationKeyTooltip", false)]
        public string EncapsulationKey { get; set; }

        [PropertyInfo(Direction.InputData, "DecapsulationKeyCaption", "DecapsulationKeyTooltip", false)]
        public string DecapsulationKey { get; set; }

        [PropertyInfo(Direction.InputData, "CiphertextCaption", "CiphertextTooltip", false)]
        public string Ciphertext { get; set; }

        [PropertyInfo(Direction.OutputData, "NewEncapsulationKeyCaption", "NewEncapsulationKeyTooltip", false)]
        public string NewEncapsulationKey
        {
            get => _newEncapsulationKey;
            set { _newEncapsulationKey = value; OnPropertyChanged("NewEncapsulationKey"); }
        }
        private string _newEncapsulationKey;

        [PropertyInfo(Direction.OutputData, "NewDecapsulationKeyCaption", "NewDecapsulationKeyTooltip", false)]
        public string NewDecapsulationKey
        {
            get => _newDecapsulationKey;
            set { _newDecapsulationKey = value; OnPropertyChanged("NewDecapsulationKey"); }
        }
        private string _newDecapsulationKey;

        [PropertyInfo(Direction.OutputData, "NewCiphertextCaption", "NewCiphertextTooltip", false)]
        public string NewCiphertext
        {
            get => _newCiphertext;
            set { _newCiphertext = value; OnPropertyChanged("NewCiphertext"); }
        }
        private string _newCiphertext;

        [PropertyInfo(Direction.OutputData, "SharedSecretCaption", "SharedSecretTooltip", false)]
        public string SharedSecret
        {
            get => _sharedSecret;
            set { _sharedSecret = value; OnPropertyChanged("SharedSecret"); }
        }
        private string _sharedSecret;

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
            set => _settings = (MlKemSettings)value;
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
                var scheme = new MlKemScheme(_settings.ParameterSet);
                var random = new Org.BouncyCastle.Security.SecureRandom();
                if (_settings.Mode == 0)
                {
                    byte[] ek, dk;
                    scheme.KeyGen(random.GenerateSeed(32), random.GenerateSeed(32), out ek, out dk);
                    NewEncapsulationKey = MlKemScheme.Hex(ek);
                    NewDecapsulationKey = MlKemScheme.Hex(dk);
                    Report = string.Format(Properties.Resources.KeyReport, scheme.Name, ek.Length, dk.Length);
                }
                else if (_settings.Mode == 1)
                {
                    byte[] secret, ciphertext;
                    scheme.Encapsulate(MlKemScheme.FromHex(EncapsulationKey), random.GenerateSeed(32), out secret, out ciphertext);
                    NewCiphertext = MlKemScheme.Hex(ciphertext);
                    SharedSecret = MlKemScheme.Hex(secret);
                    Report = string.Format(Properties.Resources.EncapsulateReport, scheme.Name, ciphertext.Length);
                }
                else
                {
                    SharedSecret = MlKemScheme.Hex(scheme.Decapsulate(MlKemScheme.FromHex(DecapsulationKey), MlKemScheme.FromHex(Ciphertext)));
                    Report = string.Format(Properties.Resources.DecapsulateReport, scheme.Name);
                }
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
            EncapsulationKey = DecapsulationKey = Ciphertext = NewEncapsulationKey = NewDecapsulationKey = NewCiphertext = SharedSecret = Report = null;
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


    /// <summary>
    /// ML-KEM (Kyber), the key encapsulation mechanism standardized in FIPS 203 (2024), written from the standard: polynomials over Z_3329[x]/(x^256 + 1) with the number theoretic transform,
    /// sampling with SHAKE, compression of the coefficients, the public-key encryption K-PKE and the Fujisaki-Okamoto transform with implicit rejection. The parameter sets are ML-KEM-512, -768 and -1024.
    /// </summary>
    public sealed class MlKemScheme
    {
        private const int N = 256, Q = 3329;
        public int K { get; }
        public int Eta1 { get; }
        public int Eta2 { get; }
        public int Du { get; }
        public int Dv { get; }
        public string Name => "ML-KEM-" + (K * 256);
        public int EncapsulationKeySize => 384 * K + 32;
        public int DecapsulationKeySize => 768 * K + 96;
        public int CiphertextSize => 32 * (Du * K + Dv);

        public MlKemScheme(int parameterSet)
        {
            switch (parameterSet)
            {
                case 0: K = 2; Eta1 = 3; Eta2 = 2; Du = 10; Dv = 4; break;
                case 1: K = 3; Eta1 = 2; Eta2 = 2; Du = 10; Dv = 4; break;
                default: K = 4; Eta1 = 2; Eta2 = 2; Du = 11; Dv = 5; break;
            }
        }

        // ---- hash functions

        private static byte[] Sha3(int bits, params byte[][] parts)
        {
            var digest = new Org.BouncyCastle.Crypto.Digests.Sha3Digest(bits);
            foreach (var part in parts) { digest.BlockUpdate(part, 0, part.Length); }
            var result = new byte[bits / 8];
            digest.DoFinal(result, 0);
            return result;
        }

        private static byte[] Shake256(int length, params byte[][] parts)
        {
            var digest = new Org.BouncyCastle.Crypto.Digests.ShakeDigest(256);
            foreach (var part in parts) { digest.BlockUpdate(part, 0, part.Length); }
            var result = new byte[length];
            digest.OutputFinal(result, 0, length);
            return result;
        }

        private static byte[] Slice(byte[] data, int start, int length) => data.Skip(start).Take(length).ToArray();

        // ---- arithmetic in Z_q and the number theoretic transform

        private static int Power(int a, int e) { long result = 1, b = a; for (; e > 0; e >>= 1) { if ((e & 1) == 1) { result = result * b % Q; } b = b * b % Q; } return (int)result; }

        private static int BitReverse7(int x) { int r = 0; for (int i = 0; i < 7; i++) { r |= (x >> i & 1) << (6 - i); } return r; }

        private static readonly int[] Zetas = Enumerable.Range(0, 128).Select(i => Power(17, BitReverse7(i))).ToArray();
        private static readonly int[] Gammas = Enumerable.Range(0, 128).Select(i => Power(17, 2 * BitReverse7(i) + 1)).ToArray();

        public static int[] Ntt(int[] polynomial)
        {
            var f = (int[])polynomial.Clone();
            int i = 1;
            for (int length = 128; length >= 2; length /= 2)
            {
                for (int start = 0; start < N; start += 2 * length)
                {
                    int zeta = Zetas[i++];
                    for (int j = start; j < start + length; j++)
                    {
                        int t = (int)((long)zeta * f[j + length] % Q);
                        f[j + length] = (f[j] - t + Q) % Q;
                        f[j] = (f[j] + t) % Q;
                    }
                }
            }
            return f;
        }

        public static int[] InverseNtt(int[] transformed)
        {
            var f = (int[])transformed.Clone();
            int i = 127;
            for (int length = 2; length <= 128; length *= 2)
            {
                for (int start = 0; start < N; start += 2 * length)
                {
                    int zeta = Zetas[i--];
                    for (int j = start; j < start + length; j++)
                    {
                        int t = f[j];
                        f[j] = (t + f[j + length]) % Q;
                        f[j + length] = (int)((long)zeta * ((f[j + length] - t + Q) % Q) % Q);
                    }
                }
            }
            for (int j = 0; j < N; j++) { f[j] = (int)((long)f[j] * 3303 % Q); }   // 3303 = 128^-1 mod q
            return f;
        }

        /// <summary>The product of two polynomials in the transformed domain: 128 products of linear polynomials modulo x^2 - gamma.</summary>
        public static int[] MultiplyNtt(int[] f, int[] g)
        {
            var h = new int[N];
            for (int i = 0; i < 128; i++)
            {
                long a0 = f[2 * i], a1 = f[2 * i + 1], b0 = g[2 * i], b1 = g[2 * i + 1];
                h[2 * i] = (int)((a0 * b0 + a1 * b1 % Q * Gammas[i]) % Q);
                h[2 * i + 1] = (int)((a0 * b1 + a1 * b0) % Q);
            }
            return h;
        }

        private static int[] Add(int[] a, int[] b) => a.Select((x, i) => (x + b[i]) % Q).ToArray();

        private static int[] Subtract(int[] a, int[] b) => a.Select((x, i) => (x - b[i] + Q) % Q).ToArray();

        // ---- sampling

        /// <summary>A polynomial in the transformed domain from the uniform output of SHAKE128 (rejection sampling of 12-bit values below q).</summary>
        private static int[] SampleNtt(byte[] rho, int j, int i)
        {
            var shake = new Org.BouncyCastle.Crypto.Digests.ShakeDigest(128);
            var seed = rho.Concat(new[] { (byte)j, (byte)i }).ToArray();
            shake.BlockUpdate(seed, 0, seed.Length);
            var a = new int[N];
            int count = 0;
            var block = new byte[168];
            while (count < N)
            {
                shake.Output(block, 0, block.Length);
                for (int p = 0; p + 2 < block.Length && count < N; p += 3)
                {
                    int d1 = block[p] + 256 * (block[p + 1] & 15), d2 = (block[p + 1] >> 4) + 16 * block[p + 2];
                    if (d1 < Q) { a[count++] = d1; }
                    if (d2 < Q && count < N) { a[count++] = d2; }
                }
            }
            return a;
        }

        /// <summary>A polynomial with small coefficients from 64 eta random bytes (centered binomial distribution).</summary>
        private static int[] SampleCbd(byte[] bytes, int eta)
        {
            var f = new int[N];
            Func<int, int> bit = k => bytes[k / 8] >> (k % 8) & 1;
            for (int i = 0; i < N; i++)
            {
                int x = 0, y = 0;
                for (int j = 0; j < eta; j++) { x += bit(2 * i * eta + j); y += bit(2 * i * eta + eta + j); }
                f[i] = (x - y + Q) % Q;
            }
            return f;
        }

        private static byte[] Prf(int eta, byte[] seed, int counter) => Shake256(64 * eta, seed, new[] { (byte)counter });

        // ---- encoding and compression

        public static byte[] ByteEncode(int[] f, int d)
        {
            var bytes = new byte[32 * d];
            for (int i = 0; i < N; i++) { for (int j = 0; j < d; j++) { if ((f[i] >> j & 1) == 1) { int position = i * d + j; bytes[position / 8] |= (byte)(1 << (position % 8)); } } }
            return bytes;
        }

        public static int[] ByteDecode(byte[] bytes, int d)
        {
            var f = new int[N];
            for (int i = 0; i < N; i++)
            {
                int value = 0;
                for (int j = 0; j < d; j++) { int position = i * d + j; value |= (bytes[position / 8] >> (position % 8) & 1) << j; }
                f[i] = d == 12 ? value % Q : value;
            }
            return f;
        }

        public static int Compress(int x, int d) => (int)(((((long)x << d) + Q / 2) / Q) & ((1 << d) - 1));

        public static int Decompress(int y, int d) => (int)((((long)y * Q) + (1 << (d - 1))) >> d);

        // ---- K-PKE

        private int[][][] Matrix(byte[] rho)
        {
            var a = new int[K][][];
            for (int i = 0; i < K; i++) { a[i] = new int[K][]; for (int j = 0; j < K; j++) { a[i][j] = SampleNtt(rho, j, i); } }
            return a;
        }

        private void KeyGenPke(byte[] d, out byte[] ek, out byte[] dk)
        {
            byte[] g = Sha3(512, d, new[] { (byte)K });
            byte[] rho = Slice(g, 0, 32), sigma = Slice(g, 32, 32);
            var a = Matrix(rho);
            int counter = 0;
            var s = new int[K][]; var e = new int[K][];
            for (int i = 0; i < K; i++) { s[i] = Ntt(SampleCbd(Prf(Eta1, sigma, counter++), Eta1)); }
            for (int i = 0; i < K; i++) { e[i] = Ntt(SampleCbd(Prf(Eta1, sigma, counter++), Eta1)); }
            var t = new int[K][];
            for (int i = 0; i < K; i++)
            {
                t[i] = e[i];
                for (int j = 0; j < K; j++) { t[i] = Add(t[i], MultiplyNtt(a[i][j], s[j])); }
            }
            ek = t.SelectMany(p => ByteEncode(p, 12)).Concat(rho).ToArray();
            dk = s.SelectMany(p => ByteEncode(p, 12)).ToArray();
        }

        private byte[] EncryptPke(byte[] ek, byte[] message, byte[] randomness)
        {
            var t = Enumerable.Range(0, K).Select(i => ByteDecode(Slice(ek, 384 * i, 384), 12)).ToArray();
            byte[] rho = Slice(ek, 384 * K, 32);
            var a = Matrix(rho);
            int counter = 0;
            var y = new int[K][]; var e1 = new int[K][];
            for (int i = 0; i < K; i++) { y[i] = Ntt(SampleCbd(Prf(Eta1, randomness, counter++), Eta1)); }
            for (int i = 0; i < K; i++) { e1[i] = SampleCbd(Prf(Eta2, randomness, counter++), Eta2); }
            int[] e2 = SampleCbd(Prf(Eta2, randomness, counter++), Eta2);
            var u = new int[K][];
            for (int i = 0; i < K; i++)
            {
                var sum = new int[N];
                for (int j = 0; j < K; j++) { sum = Add(sum, MultiplyNtt(a[j][i], y[j])); }
                u[i] = Add(InverseNtt(sum), e1[i]);
            }
            int[] mu = ByteDecode(message, 1).Select(b => Decompress(b, 1)).ToArray();
            var product = new int[N];
            for (int j = 0; j < K; j++) { product = Add(product, MultiplyNtt(t[j], y[j])); }
            int[] v = Add(Add(InverseNtt(product), e2), mu);
            var c1 = u.SelectMany(p => ByteEncode(p.Select(x => Compress(x, Du)).ToArray(), Du));
            var c2 = ByteEncode(v.Select(x => Compress(x, Dv)).ToArray(), Dv);
            return c1.Concat(c2).ToArray();
        }

        private byte[] DecryptPke(byte[] dk, byte[] ciphertext)
        {
            var u = Enumerable.Range(0, K).Select(i => ByteDecode(Slice(ciphertext, 32 * Du * i, 32 * Du), Du).Select(x => Decompress(x, Du)).ToArray()).ToArray();
            int[] v = ByteDecode(Slice(ciphertext, 32 * Du * K, 32 * Dv), Dv).Select(x => Decompress(x, Dv)).ToArray();
            var s = Enumerable.Range(0, K).Select(i => ByteDecode(Slice(dk, 384 * i, 384), 12)).ToArray();
            var product = new int[N];
            for (int j = 0; j < K; j++) { product = Add(product, MultiplyNtt(s[j], Ntt(u[j]))); }
            int[] w = Subtract(v, InverseNtt(product));
            return ByteEncode(w.Select(x => Compress(x, 1)).ToArray(), 1);
        }

        // ---- ML-KEM

        /// <summary>The key pair from two 32-byte random values d and z: ek = K-PKE key, dk = (K-PKE secret, ek, H(ek), z).</summary>
        public void KeyGen(byte[] d, byte[] z, out byte[] encapsulationKey, out byte[] decapsulationKey)
        {
            byte[] ekPke, dkPke;
            KeyGenPke(d, out ekPke, out dkPke);
            encapsulationKey = ekPke;
            decapsulationKey = dkPke.Concat(ekPke).Concat(Sha3(256, ekPke)).Concat(z).ToArray();
        }

        /// <summary>The check of FIPS 203 for an encapsulation key: its length and that every 12-bit value is below q.</summary>
        public bool IsValidEncapsulationKey(byte[] ek)
        {
            if (ek.Length != EncapsulationKeySize) { return false; }
            for (int i = 0; i < K; i++) { byte[] part = Slice(ek, 384 * i, 384); if (!ByteEncode(ByteDecode(part, 12), 12).SequenceEqual(part)) { return false; } }
            return true;
        }

        /// <summary>From a random message m: the shared secret and the ciphertext.</summary>
        public void Encapsulate(byte[] ek, byte[] m, out byte[] sharedSecret, out byte[] ciphertext)
        {
            if (!IsValidEncapsulationKey(ek)) { throw new ArgumentException("This is not a valid " + Name + " encapsulation key (" + EncapsulationKeySize + " bytes)."); }
            byte[] g = Sha3(512, m, Sha3(256, ek));
            sharedSecret = Slice(g, 0, 32);
            ciphertext = EncryptPke(ek, m, Slice(g, 32, 32));
        }

        /// <summary>The shared secret; a changed ciphertext gives a pseudo-random secret instead of an error (implicit rejection).</summary>
        public byte[] Decapsulate(byte[] dk, byte[] ciphertext)
        {
            if (dk.Length != DecapsulationKeySize) { throw new ArgumentException("A " + Name + " decapsulation key has " + DecapsulationKeySize + " bytes."); }
            if (ciphertext.Length != CiphertextSize) { throw new ArgumentException("A " + Name + " ciphertext has " + CiphertextSize + " bytes."); }
            byte[] dkPke = Slice(dk, 0, 384 * K), ekPke = Slice(dk, 384 * K, 384 * K + 32), h = Slice(dk, 768 * K + 32, 32), z = Slice(dk, 768 * K + 64, 32);
            byte[] m = DecryptPke(dkPke, ciphertext);
            byte[] g = Sha3(512, m, h);
            byte[] secret = Slice(g, 0, 32), rejection = Shake256(32, z, ciphertext);
            return EncryptPke(ekPke, m, Slice(g, 32, 32)).SequenceEqual(ciphertext) ? secret : rejection;
        }

        public static string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        public static byte[] FromHex(string text)
        {
            text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (text.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
        }
    }

}

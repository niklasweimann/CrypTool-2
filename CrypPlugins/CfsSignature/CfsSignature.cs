using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace CfsSignature
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("CfsSignature.Properties.Resources", "PluginCaption", "PluginTooltip", "CfsSignature/DetailedDescription/doc.xml", "CfsSignature/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class CfsSignature : ICrypComponent
    {
        private CfsSignatureSettings _settings = new CfsSignatureSettings();

        [PropertyInfo(Direction.InputData, "MessageCaption", "MessageTooltip", true)]
        public string Message { get; set; }

        [PropertyInfo(Direction.InputData, "ReceivedCaption", "ReceivedTooltip", false)]
        public string Received { get; set; }

        [PropertyInfo(Direction.OutputData, "SignatureCaption", "SignatureTooltip", false)]
        public string Signature
        {
            get => _signature;
            set { _signature = value; OnPropertyChanged("Signature"); }
        }
        private string _signature;

        [PropertyInfo(Direction.OutputData, "ValidCaption", "ValidTooltip", false)]
        public string Valid
        {
            get => _valid;
            set { _valid = value; OnPropertyChanged("Valid"); }
        }
        private string _valid;

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
            set => _settings = (CfsSignatureSettings)value;
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
                var cfs = new CfsScheme(_settings.Seed);
                int counter;
                int[] positions = cfs.Sign(Message, out counter);
                Signature = string.Join(" ", positions) + " / " + counter;
                string received = string.IsNullOrEmpty(Received) ? Message : Received;
                bool valid = CfsScheme.Verify(received, positions, counter, cfs.PublicColumns);
                Valid = valid.ToString();
                Report = string.Format(Properties.Resources.CfsReport, CfsScheme.M, CfsScheme.T, CfsScheme.Length, CfsScheme.SyndromeBits, counter + 1, positions.Length, string.Join(" ", positions), valid ? Properties.Resources.ValidText : Properties.Resources.InvalidText);
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
            Message = Received = Signature = Valid = Report = null;
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


    /// <summary>The field GF(2^8) with the polynomial x^8 + x^4 + x^3 + x^2 + 1 and polynomials over it (coefficient i belongs to x^i).</summary>
    public static class Gf
    {
        private static readonly byte[] Exp = new byte[512];
        private static readonly byte[] Log = new byte[256];

        static Gf()
        {
            int x = 1;
            for (int i = 0; i < 255; i++) { Exp[i] = (byte)x; Log[x] = (byte)i; x <<= 1; if ((x & 0x100) != 0) { x ^= 0x11d; } }
            for (int i = 255; i < 512; i++) { Exp[i] = Exp[i - 255]; }
        }

        public static byte Mul(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : Exp[Log[a] + Log[b]];

        public static byte Inv(byte a) => Exp[255 - Log[a]];

        public static int Degree(byte[] p) { for (int i = p.Length - 1; i >= 0; i--) { if (p[i] != 0) { return i; } } return -1; }

        public static byte[] Trim(byte[] p) { int d = Degree(p); return p.Take(Math.Max(d + 1, 0)).ToArray(); }

        public static byte[] Add(byte[] a, byte[] b)
        {
            var r = new byte[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < r.Length; i++) { r[i] = (byte)((i < a.Length ? a[i] : 0) ^ (i < b.Length ? b[i] : 0)); }
            return Trim(r);
        }

        public static byte[] Multiply(byte[] a, byte[] b)
        {
            if (Degree(a) < 0 || Degree(b) < 0) { return new byte[0]; }
            var r = new byte[a.Length + b.Length - 1];
            for (int i = 0; i < a.Length; i++) { for (int j = 0; j < b.Length; j++) { r[i + j] ^= Mul(a[i], b[j]); } }
            return Trim(r);
        }

        public static void DivMod(byte[] a, byte[] b, out byte[] quotient, out byte[] remainder)
        {
            b = Trim(b);
            var r = (byte[])Trim(a).Clone();
            var q = new byte[Math.Max(r.Length, 1)];
            byte leadInverse = Inv(b[b.Length - 1]);
            while (Degree(r) >= b.Length - 1)
            {
                int shift = Degree(r) - (b.Length - 1);
                byte factor = Mul(r[Degree(r)], leadInverse);
                q[shift] = factor;
                for (int i = 0; i < b.Length; i++) { r[i + shift] ^= Mul(factor, b[i]); }
            }
            quotient = Trim(q); remainder = Trim(r);
        }

        public static byte[] Mod(byte[] a, byte[] m) { DivMod(a, m, out _, out var r); return r; }

        public static byte[] MultiplyMod(byte[] a, byte[] b, byte[] m) => Mod(Multiply(a, b), m);

        public static byte[] Gcd(byte[] a, byte[] b)
        {
            a = Trim(a); b = Trim(b);
            while (Degree(b) >= 0) { var r = Mod(a, b); a = b; b = r; }
            return a;
        }

        /// <summary>The inverse of a modulo g (extended Euclidean algorithm); g must be irreducible, a not zero.</summary>
        public static byte[] InverseMod(byte[] a, byte[] g)
        {
            byte[] r0 = Trim(g), r1 = Mod(a, g), t0 = new byte[0], t1 = new byte[] { 1 };
            while (Degree(r1) > 0)
            {
                DivMod(r0, r1, out var q, out var r2);
                var t2 = Add(t0, Multiply(q, t1));
                r0 = r1; r1 = r2; t0 = t1; t1 = t2;
            }
            byte inverse = Inv(r1[0]);
            return Mod(Multiply(t1, new[] { inverse }), g);
        }

        /// <summary>The square of a polynomial (no reduction).</summary>
        public static byte[] Square(byte[] a)
        {
            var r = new byte[Math.Max(2 * a.Length - 1, 0)];
            for (int i = 0; i < a.Length; i++) { r[2 * i] = Mul(a[i], a[i]); }
            return Trim(r);
        }

        /// <summary>The square of a polynomial modulo g (in characteristic 2 the coefficients are squared and the powers doubled).</summary>
        public static byte[] SquareMod(byte[] a, byte[] g)
        {
            var r = new byte[Math.Max(2 * a.Length - 1, 0)];
            for (int i = 0; i < a.Length; i++) { r[2 * i] = Mul(a[i], a[i]); }
            return Mod(r, g);
        }

        public static byte Evaluate(byte[] p, byte x)
        {
            byte result = 0;
            for (int i = p.Length - 1; i >= 0; i--) { result = (byte)(Mul(result, x) ^ p[i]); }
            return result;
        }
    }

    /// <summary>
    /// The signature scheme of Courtois, Finiasz and Sendrier (CFS, 2001) based on the McEliece / Niederreiter cryptosystem: the public key is a parity-check matrix of a binary Goppa code
    /// (made unrecognisable by a permutation and a change of basis), a signature is a vector of small weight whose syndrome is the hash of the message. A hash value is a syndrome that can be decoded
    /// with probability about 1/t!, so the signer tries counters until one hash value can be decoded with the secret Goppa code (Patterson's algorithm). The parameters here are small (m = 8, t = 4).
    /// </summary>
    public sealed class CfsScheme
    {
        public const int M = 8, T = 4, Length = 256, SyndromeBits = M * T;
        public byte[] Goppa { get; private set; }
        private readonly uint[] _columns = new uint[Length];     // columns of the parity-check matrix of the Goppa code (syndromes of single errors)
        private readonly int[] _permutation = new int[Length];    // position j of the public matrix is position _permutation[j] of the Goppa code
        private readonly int[] _inverse = new int[Length];
        public uint[] PublicColumns { get; } = new uint[Length];
        private uint[] _basisColumns;                              // the first 32 columns of the permuted matrix (the inverse of the change of basis)

        public CfsScheme(int seed)
        {
            var random = new Random(seed);
            do { Goppa = new byte[T + 1]; for (int i = 0; i < T; i++) { Goppa[i] = (byte)random.Next(256); } Goppa[T] = 1; } while (!IsIrreducible(Goppa));
            for (int i = 0; i < Length; i++)
            {
                var c = Gf.InverseMod(new byte[] { (byte)i, 1 }, Goppa);   // 1 / (x - alpha_i) mod g
                for (int k = 0; k < T; k++) { _columns[i] |= (uint)(k < c.Length ? c[k] : 0) << (8 * k); }
            }
            while (true)
            {
                var order = Enumerable.Range(0, Length).OrderBy(x => random.Next()).ToArray();
                Array.Copy(order, _permutation, Length);
                _basisColumns = Enumerable.Range(0, SyndromeBits).Select(j => _columns[_permutation[j]]).ToArray();
                uint[] inverse = Invert(_basisColumns);
                if (inverse == null) { continue; }
                for (int j = 0; j < Length; j++) { PublicColumns[j] = Apply(inverse, _columns[_permutation[j]]); _inverse[_permutation[j]] = j; }
                break;
            }
        }

        private static uint Apply(uint[] columns, uint x) { uint r = 0; for (int b = 0; b < columns.Length; b++) { if ((x >> b & 1) == 1) { r ^= columns[b]; } } return r; }

        /// <summary>The inverse of a 32 x 32 binary matrix given by its columns (Gauss-Jordan elimination), or null if it is singular.</summary>
        public static uint[] Invert(uint[] columns)
        {
            int n = columns.Length;
            var rows = new uint[n]; var inverse = new uint[n];
            for (int r = 0; r < n; r++) { for (int c = 0; c < n; c++) { if ((columns[c] >> r & 1) == 1) { rows[r] |= 1u << c; } } inverse[r] = 1u << r; }
            for (int c = 0; c < n; c++)
            {
                int pivot = -1;
                for (int r = c; r < n; r++) { if ((rows[r] >> c & 1) == 1) { pivot = r; break; } }
                if (pivot < 0) { return null; }
                (rows[c], rows[pivot]) = (rows[pivot], rows[c]); (inverse[c], inverse[pivot]) = (inverse[pivot], inverse[c]);
                for (int r = 0; r < n; r++) { if (r != c && (rows[r] >> c & 1) == 1) { rows[r] ^= rows[c]; inverse[r] ^= inverse[c]; } }
            }
            var result = new uint[n];
            for (int r = 0; r < n; r++) { for (int c = 0; c < n; c++) { if ((inverse[r] >> c & 1) == 1) { result[c] |= 1u << r; } } }
            return result;
        }

        /// <summary>Rabin's test: g of degree 4 over GF(2^8) is irreducible if x^(256^4) = x mod g and g has no common factor with x^(256^2) - x.</summary>
        public static bool IsIrreducible(byte[] g)
        {
            Func<byte[], byte[]> frobenius = a => { for (int i = 0; i < M; i++) { a = Gf.SquareMod(a, g); } return a; };
            byte[] x = Gf.Mod(new byte[] { 0, 1 }, g), x2 = frobenius(frobenius(x)), x4 = frobenius(frobenius(x2));
            return Gf.Add(x4, x).Length == 0 && Gf.Degree(Gf.Gcd(g, Gf.Add(x2, x))) == 0;
        }

        /// <summary>The syndrome of the errors at the positions of the Goppa code.</summary>
        public uint Syndrome(IEnumerable<int> positions) => positions.Aggregate(0u, (s, p) => s ^ _columns[p]);

        /// <summary>Patterson's algorithm: the error positions (at most T) with the given syndrome, or null if it is not the syndrome of such an error.</summary>
        public List<int> Decode(uint syndrome)
        {
            if (syndrome == 0) { return new List<int>(); }
            var s = Gf.Trim(Enumerable.Range(0, T).Select(k => (byte)(syndrome >> (8 * k))).ToArray());
            byte[] x = Gf.Mod(new byte[] { 0, 1 }, Goppa);
            byte[] sigma;
            byte[] inverse = Gf.InverseMod(s, Goppa);
            if (Gf.Add(inverse, x).Length == 0) { sigma = new byte[] { 0, 1 }; }
            else
            {
                byte[] r = Gf.Add(inverse, x);
                for (int i = 0; i < SyndromeBits - 1; i++) { r = Gf.SquareMod(r, Goppa); }   // the square root in GF(2^32) is the 31-fold square
                // a = b * r mod g with deg a <= T/2 and deg b <= (T-1)/2: stop the Euclidean algorithm as soon as the remainder is small enough
                byte[] r0 = Gf.Trim(Goppa), r1 = Gf.Trim(r), u0 = new byte[0], u1 = new byte[] { 1 };
                while (Gf.Degree(r1) > T / 2)
                {
                    Gf.DivMod(r0, r1, out var q, out var r2);
                    var u2 = Gf.Add(u0, Gf.Multiply(q, u1));
                    r0 = r1; r1 = r2; u0 = u1; u1 = u2;
                }
                sigma = Gf.Add(Gf.Square(r1), Gf.Multiply(new byte[] { 0, 1 }, Gf.Square(u1)));
            }
            var positions = Enumerable.Range(0, Length).Where(i => Gf.Evaluate(sigma, (byte)i) == 0).ToList();
            if (positions.Count != Gf.Degree(sigma) || positions.Count > T || Syndrome(positions) != syndrome) { return null; }
            return positions;
        }

        private static uint Hash(string message, int counter)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(message).Concat(BitConverter.GetBytes(counter)).ToArray());
                return BitConverter.ToUInt32(h, 0);
            }
        }

        /// <summary>The signature: the positions (of the public matrix) of at most T ones and the counter for which the hash value is a decodable syndrome.</summary>
        public int[] Sign(string message, out int counter)
        {
            for (counter = 0; counter < 100000; counter++)
            {
                uint target = Hash(message, counter);
                uint goppaSyndrome = Apply(_basisColumns, target);   // the change of basis back: M^-1 * s
                var positions = Decode(goppaSyndrome);
                if (positions != null) { return positions.Select(p => _inverse[p]).OrderBy(p => p).ToArray(); }
            }
            throw new InvalidOperationException("No decodable hash value was found.");
        }

        public static bool Verify(string message, int[] positions, int counter, uint[] publicColumns)
        {
            if (positions.Length > T || positions.Distinct().Count() != positions.Length || positions.Any(p => p < 0 || p >= Length)) { return false; }
            return positions.Aggregate(0u, (s, p) => s ^ publicColumns[p]) == Hash(message, counter);
        }
    }

}

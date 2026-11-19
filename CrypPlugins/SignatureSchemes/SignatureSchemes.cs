using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Numerics;

namespace SignatureSchemes
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("SignatureSchemes.Properties.Resources", "PluginCaption", "PluginTooltip", "SignatureSchemes/DetailedDescription/doc.xml", "SignatureSchemes/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class SignatureSchemes : ICrypComponent
    {
        private SignatureSchemesSettings _settings = new SignatureSchemesSettings();

        [PropertyInfo(Direction.InputData, "MessageCaption", "MessageTooltip", true)]
        public string Message { get; set; }

        [PropertyInfo(Direction.InputData, "ReceivedCaption", "ReceivedTooltip", false)]
        public string Received { get; set; }

        [PropertyInfo(Direction.InputData, "HideCaption", "HideTooltip", false)]
        public string Hide { get; set; }

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
            set => _settings = (SignatureSchemesSettings)value;
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
                string message = Message;
                string received = string.IsNullOrEmpty(Received) ? message : Received;
                var report = new StringBuilder();
                switch (_settings.Scheme)
                {
                    case 0:
                        var gq = new GuillouQuisquater(_settings.Seed, "alice");
                        BigInteger d, big;
                        gq.Sign(message, out d, out big);
                        Signature = d + ":" + big;
                        bool gqValid = gq.Verify(received, d, big);
                        Valid = gqValid.ToString();
                        report.AppendLine("n = " + gq.N + ", e = " + gq.E);
                        report.AppendLine("J (from the identity) = " + gq.J);
                        report.AppendLine("secret B with B^e * J = 1: " + BigInteger.ModPow(gq.B, gq.E, gq.N) * gq.J % gq.N);
                        report.AppendLine("signature (d, D): d = " + d + ", D = " + big);
                        report.AppendLine("check: T' = D^e * J^d, H(m, T') mod e = d: " + (gqValid ? "equal" : "different"));
                        break;
                    case 1:
                        var redactable = new RedactableSignature(_settings.Seed);
                        redactable.Sign(message);
                        var hide = (Hide ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                        var redacted = redactable.Redact(hide);
                        if (!string.IsNullOrEmpty(Received))
                        {
                            var changed = Received.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < redacted.Words.Length && i < changed.Length; i++) { if (redacted.Words[i] != null) { redacted.Words[i] = changed[i]; } }
                        }
                        Signature = redactable.Signature.ToString();
                        bool redactedValid = redactable.Verify(redacted);
                        Valid = redactedValid.ToString();
                        report.AppendLine("Redacted message: " + string.Join(" ", redacted.Words.Select(w => w ?? "[...]")));
                        report.AppendLine("Signature of the Merkle root (RSA, " + redactable.N.GetBitLength() + " bits): " + Signature);
                        report.AppendLine("Hidden words are replaced by their leaf hashes: " + string.Join(" ", Enumerable.Range(0, redacted.Words.Length).Where(i => redacted.Words[i] == null).Select(i => (i + 1) + ":" + Hashing.Hex(redacted.Hashes[i]).Substring(0, 8))));
                        report.AppendLine(redactedValid ? "The signature is valid for the redacted message." : "The signature is NOT valid: a revealed word was changed.");
                        break;
                    case 2:
                        var tree = new MultiTreeSignature(_settings.Seed, _settings.Layers, _settings.Height);
                        long index = _settings.Index % (long)tree.Capacity;
                        byte[] signature = tree.Sign(message, index);
                        Signature = Convert.ToBase64String(signature);
                        bool treeValid = MultiTreeSignature.Verify(received, signature, tree.PublicKey, _settings.Layers, _settings.Height);
                        Valid = treeValid.ToString();
                        report.AppendLine("Public key (root of the top tree): " + Hashing.Hex(tree.PublicKey));
                        report.AppendLine(_settings.Layers + " layers of trees of height " + _settings.Height + ": " + tree.Capacity + " signatures; this is number " + index);
                        report.AppendLine("Signature: " + signature.Length + " bytes (per layer a Winternitz signature of 67 x 32 bytes and an authentication path)");
                        report.AppendLine(treeValid ? "The signature is valid: the chain of roots leads to the public key." : "The signature is NOT valid.");
                        break;
                    default:
                        var uov = new OilAndVinegar(_settings.Seed, 4, 8);
                        int[] s = uov.Sign(message);
                        Signature = string.Join(" ", s);
                        bool uovValid = uov.Verify(received, s);
                        Valid = uovValid.ToString();
                        report.AppendLine("Field GF(" + OilAndVinegar.Field + "), " + uov.Oil + " oil and " + uov.Vinegar + " vinegar variables, " + uov.Oil + " public quadratic polynomials in " + uov.Variables + " variables");
                        report.AppendLine("Target values H(m): " + string.Join(" ", uov.Target(received)));
                        report.AppendLine("Signature (" + uov.Variables + " numbers): " + Signature);
                        report.AppendLine(uovValid ? "All public polynomials give the target values: the signature is valid." : "The public polynomials do not give the target values: the signature is NOT valid.");
                        break;
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
            Message = Received = Hide = Signature = Valid = Report = null;
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


    public static class Hashing
    {
        public static byte[] H(params byte[][] parts)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) { return sha.ComputeHash(parts.SelectMany(p => p).ToArray()); }
        }

        public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        public static byte[] Bytes(int value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        public static BigInteger ToNumber(byte[] data) => new BigInteger(data.Concat(new byte[] { 0 }).ToArray());

        public static string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        public static BigInteger Inverse(BigInteger a, BigInteger m)
        {
            BigInteger g = m, x = 0, y = 1, r = ((a % m) + m) % m;
            while (r != 0) { BigInteger q = g / r; BigInteger t = g - q * r; g = r; r = t; t = x - q * y; x = y; y = t; }
            return (x % m + m) % m;
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

        public static BigInteger RandomPrime(Random random, int bits)
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

        public static BigInteger RandomBelow(Random random, BigInteger limit)
        {
            var bytes = new byte[limit.ToByteArray().Length + 1];
            random.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            return new BigInteger(bytes) % (limit - 1) + 1;
        }
    }

    /// <summary>
    /// The signature of Guillou and Quisquater (1988): an identification protocol of the Fiat-Shamir type made into a signature. The public key is n, e and the number J of the signer
    /// (made from the identity), the secret is B with B^e * J = 1 mod n. The signature of m is (d, D): T = r^e, d = H(m, T) mod e, D = r * B^d; it is checked by T' = D^e * J^d and d = H(m, T') mod e.
    /// </summary>
    public sealed class GuillouQuisquater
    {
        public BigInteger N, E = 65537, J, B;
        private readonly Random _random;

        public GuillouQuisquater(int seed, string identity)
        {
            _random = new Random(seed);
            BigInteger p = Hashing.RandomPrime(_random, 160), q, phi;
            do { q = Hashing.RandomPrime(_random, 160); phi = (p - 1) * (q - 1); } while (q == p || BigInteger.GreatestCommonDivisor(E, phi) != 1);
            N = p * q;
            phi = (p - 1) * (q - 1);
            J = Hashing.ToNumber(Hashing.H(Hashing.Bytes("identity: " + identity))) % N;
            B = BigInteger.ModPow(Hashing.Inverse(J, N), Hashing.Inverse(E, phi), N);
        }

        private BigInteger Challenge(string message, BigInteger t) => Hashing.ToNumber(Hashing.H(Hashing.Bytes(message), t.ToByteArray())) % E;

        public void Sign(string message, out BigInteger d, out BigInteger big)
        {
            BigInteger r;
            do { r = Hashing.RandomBelow(_random, N); } while (BigInteger.GreatestCommonDivisor(r, N) != 1);
            BigInteger t = BigInteger.ModPow(r, E, N);
            d = Challenge(message, t);
            big = r * BigInteger.ModPow(B, d, N) % N;
        }

        public bool Verify(string message, BigInteger d, BigInteger big)
        {
            BigInteger t = BigInteger.ModPow(big, E, N) * BigInteger.ModPow(J, d, N) % N;
            return Challenge(message, t) == d;
        }
    }

    /// <summary>
    /// A redactable signature on a Merkle hash tree: the words of the message are the leaves (hashed with an index and a random salt), the root is signed with RSA. A word can be removed (redacted)
    /// and replaced by the hash of its leaf, and the signature is still valid for the rest; the salt keeps a hidden word from being guessed from its hash.
    /// </summary>
    public sealed class RedactableSignature
    {
        public BigInteger N, E = 65537, D;
        public string[] Words;
        public byte[][] Salts;
        public BigInteger Signature;
        private readonly Random _random;

        public RedactableSignature(int seed)
        {
            _random = new Random(seed);
            BigInteger p = Hashing.RandomPrime(_random, 256), q;
            do { q = Hashing.RandomPrime(_random, 256); } while (q == p || BigInteger.GreatestCommonDivisor(E, (p - 1) * (q - 1)) != 1);
            N = p * q;
            D = Hashing.Inverse(E, (p - 1) * (q - 1));
        }

        public static byte[] Leaf(int index, byte[] salt, string word) => Hashing.H(Hashing.Bytes(index), salt, Hashing.Bytes(word));

        /// <summary>The root of the Merkle tree over the leaf hashes (the last level is filled up with a fixed hash).</summary>
        public static byte[] Root(IList<byte[]> leaves)
        {
            var level = leaves.ToList();
            int size = 1;
            while (size < level.Count) { size *= 2; }
            while (level.Count < size) { level.Add(Hashing.H(Hashing.Bytes("empty"))); }
            while (level.Count > 1) { level = Enumerable.Range(0, level.Count / 2).Select(i => Hashing.H(level[2 * i], level[2 * i + 1])).ToList(); }
            return level[0];
        }

        private BigInteger Digest(byte[] root) => Hashing.ToNumber(Hashing.H(root).Concat(Hashing.H(Hashing.H(root))).ToArray()) % N;

        public void Sign(string message)
        {
            Words = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Salts = Words.Select(w => { var salt = new byte[16]; _random.NextBytes(salt); return salt; }).ToArray();
            byte[] root = Root(Enumerable.Range(0, Words.Length).Select(i => Leaf(i, Salts[i], Words[i])).ToList());
            Signature = BigInteger.ModPow(Digest(root), D, N);
        }

        public sealed class Redacted { public string[] Words; public byte[][] Salts; public byte[][] Hashes; }

        /// <summary>Hides the words at the positions (1-based): their words and salts are removed and only their leaf hashes stay.</summary>
        public Redacted Redact(IEnumerable<int> hide)
        {
            var hidden = new HashSet<int>(hide.Select(p => p - 1));
            var r = new Redacted { Words = new string[Words.Length], Salts = new byte[Words.Length][], Hashes = new byte[Words.Length][] };
            for (int i = 0; i < Words.Length; i++)
            {
                if (hidden.Contains(i)) { r.Hashes[i] = Leaf(i, Salts[i], Words[i]); }
                else { r.Words[i] = Words[i]; r.Salts[i] = Salts[i]; r.Hashes[i] = Leaf(i, Salts[i], Words[i]); }
            }
            return r;
        }

        /// <summary>Hidden words are recognised by the missing salt: their hash is taken as it is, the other leaves are computed again from word and salt.</summary>
        public bool Verify(Redacted r)
        {
            var leaves = Enumerable.Range(0, r.Words.Length).Select(i => r.Words[i] == null ? r.Hashes[i] : Leaf(i, r.Salts[i], r.Words[i])).ToList();
            return BigInteger.ModPow(Signature, E, N) == Digest(Root(leaves));
        }
    }

    /// <summary>
    /// A Merkle signature scheme with several layers of trees (the idea of CMSS and GMSS, Buchmann et al.): the Winternitz one-time keys (w = 16, SHA-256) of the leaves of a tree sign messages or, in an upper layer,
    /// the root of a tree of the layer below. Only the root of the top tree is public, and with L layers of height h there are 2^(L h) signatures, while only the small trees on the path are computed.
    /// </summary>
    public sealed class MultiTreeSignature
    {
        private const int Len1 = 64, Len = 67, Hash = 32;
        public int Layers { get; }
        public int Height { get; }
        public BigInteger Capacity => BigInteger.One << (Layers * Height);
        private readonly byte[] _seed;
        private readonly Dictionary<string, List<byte[][]>> _trees = new Dictionary<string, List<byte[][]>>();

        public MultiTreeSignature(int seed, int layers, int height)
        {
            Layers = layers; Height = height;
            _seed = Hashing.H(Hashing.Bytes("multi-tree seed"), Hashing.Bytes(seed));
        }

        private byte[] Secret(int layer, long tree, int leaf, int chain) => Hashing.H(Hashing.Bytes("sk"), _seed, Hashing.Bytes(layer), Hashing.Bytes((int)tree), Hashing.Bytes(leaf), Hashing.Bytes(chain));

        private static byte[] Step(byte[] x, int position) => Hashing.H(Hashing.Bytes("chain"), Hashing.Bytes(position), x);

        private static byte[] Chain(byte[] x, int from, int steps)
        {
            for (int i = 0; i < steps; i++) { x = Step(x, from + i); }
            return x;
        }

        /// <summary>The 64 hexadecimal digits of the hash and the 3 digits of the checksum.</summary>
        private static int[] Digits(byte[] hash)
        {
            var digits = new int[Len];
            for (int i = 0; i < Hash; i++) { digits[2 * i] = hash[i] >> 4; digits[2 * i + 1] = hash[i] & 15; }
            int checksum = digits.Take(Len1).Sum(d => 15 - d);
            digits[Len1] = checksum >> 8 & 15; digits[Len1 + 1] = checksum >> 4 & 15; digits[Len1 + 2] = checksum & 15;
            return digits;
        }

        private byte[] LeafHash(IEnumerable<byte[]> ends) => Hashing.H(ends.SelectMany(e => e).ToArray());

        private byte[] OneTimePublic(int layer, long tree, int leaf) => LeafHash(Enumerable.Range(0, Len).Select(c => Chain(Secret(layer, tree, leaf, c), 0, 15)));

        private List<byte[][]> Tree(int layer, long tree)
        {
            string key = layer + ":" + tree;
            if (_trees.TryGetValue(key, out var cached)) { return cached; }
            var levels = new List<byte[][]>();
            levels.Add(Enumerable.Range(0, 1 << Height).Select(l => OneTimePublic(layer, tree, l)).ToArray());
            while (levels[levels.Count - 1].Length > 1)
            {
                var below = levels[levels.Count - 1];
                levels.Add(Enumerable.Range(0, below.Length / 2).Select(i => Hashing.H(below[2 * i], below[2 * i + 1])).ToArray());
            }
            _trees[key] = levels;
            return levels;
        }

        public byte[] PublicKey => Tree(0, 0)[Height][0];

        /// <summary>The signature of the message with the one-time keys of the numbered signature (0 to 2^(L h) - 1): per layer the Winternitz signature and the authentication path.</summary>
        public byte[] Sign(string message, long index)
        {
            if (index < 0 || index >= (long)Capacity) { throw new ArgumentException("The number of the signature must be between 0 and " + (Capacity - 1) + "."); }
            var result = new List<byte>(Hashing.Bytes((int)index));
            byte[] signed = Hashing.H(Hashing.Bytes(message));
            long tree = index >> Height;
            int leaf = (int)(index & ((1 << Height) - 1));
            for (int layer = Layers - 1; layer >= 0; layer--)
            {
                int[] digits = Digits(signed);
                for (int c = 0; c < Len; c++) { result.AddRange(Chain(Secret(layer, tree, leaf, c), 0, digits[c])); }
                var levels = Tree(layer, tree);
                for (int level = 0; level < Height; level++) { result.AddRange(levels[level][(leaf >> level) ^ 1]); }
                signed = levels[Height][0];   // the root of this tree is signed in the layer above
                leaf = (int)(tree & ((1 << Height) - 1));
                tree >>= Height;
            }
            return result.ToArray();
        }

        public static bool Verify(string message, byte[] signature, byte[] publicKey, int layers, int height)
        {
            int perLayer = (Len + height) * Hash;
            if (signature.Length != 4 + layers * perLayer) { return false; }
            long index = signature[0] << 24 | signature[1] << 16 | signature[2] << 8 | signature[3];
            byte[] signed = Hashing.H(Hashing.Bytes(message));
            int leaf = (int)(index & ((1 << height) - 1));
            long tree = index >> height;
            for (int layer = layers - 1; layer >= 0; layer--)
            {
                int offset = 4 + (layers - 1 - layer) * perLayer;
                int[] digits = Digits(signed);
                var ends = Enumerable.Range(0, Len).Select(c => Chain(signature.Skip(offset + c * Hash).Take(Hash).ToArray(), digits[c], 15 - digits[c])).ToList();
                byte[] node = Hashing.H(ends.SelectMany(e => e).ToArray());
                for (int level = 0; level < height; level++)
                {
                    byte[] sibling = signature.Skip(offset + (Len + level) * Hash).Take(Hash).ToArray();
                    node = ((leaf >> level) & 1) == 0 ? Hashing.H(node, sibling) : Hashing.H(sibling, node);
                }
                signed = node;
                leaf = (int)(tree & ((1 << height) - 1));
                tree >>= height;
            }
            return signed.SequenceEqual(publicKey);
        }
    }

    /// <summary>
    /// The (homogeneous) Unbalanced Oil and Vinegar signature of Kipnis and Patarin over GF(251): the secret polynomials have no products of two oil variables, so for fixed random vinegar values the oil
    /// values follow from a linear system. The public polynomials are these composed with a secret linear map T, so the equations look random. A signature of m is a vector s with P_k(s) = H(m)_k.
    /// </summary>
    public sealed class OilAndVinegar
    {
        public const int Field = 251;
        public int Oil { get; }
        public int Vinegar { get; }
        public int Variables => Oil + Vinegar;
        public int[][,] Public { get; }
        private readonly int[,] _t, _tInverse;
        private readonly int[][,] _central;
        private readonly Random _random;

        public OilAndVinegar(int seed, int oil, int vinegar)
        {
            Oil = oil; Vinegar = vinegar;
            _random = new Random(seed);
            int n = Variables;
            _central = new int[oil][,];
            for (int k = 0; k < oil; k++)
            {
                _central[k] = new int[n, n];
                for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) { if (i < vinegar || j < vinegar) { _central[k][i, j] = _random.Next(Field); } } }
            }
            do { _t = new int[n, n]; for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) { _t[i, j] = _random.Next(Field); } } _tInverse = Invert(_t); } while (_tInverse == null);
            Public = new int[oil][,];
            for (int k = 0; k < oil; k++) { Public[k] = Multiply(Multiply(Transpose(_t), _central[k]), _t); }
        }

        private static int Mod(long x) => (int)(((x % Field) + Field) % Field);

        private static int Power(int a, int e) { long result = 1, b = a % Field; for (; e > 0; e >>= 1) { if ((e & 1) == 1) { result = result * b % Field; } b = b * b % Field; } return (int)result; }

        public static int[,] Transpose(int[,] a) { int n = a.GetLength(0), m = a.GetLength(1); var r = new int[m, n]; for (int i = 0; i < n; i++) { for (int j = 0; j < m; j++) { r[j, i] = a[i, j]; } } return r; }

        public static int[,] Multiply(int[,] a, int[,] b)
        {
            int n = a.GetLength(0), m = b.GetLength(1), k = a.GetLength(1);
            var r = new int[n, m];
            for (int i = 0; i < n; i++) { for (int j = 0; j < m; j++) { long s = 0; for (int t = 0; t < k; t++) { s += (long)a[i, t] * b[t, j]; } r[i, j] = Mod(s); } }
            return r;
        }

        /// <summary>Gauss-Jordan elimination modulo the prime 251; null if the matrix is singular.</summary>
        public static int[,] Invert(int[,] matrix)
        {
            int n = matrix.GetLength(0);
            var a = new int[n, 2 * n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) { a[i, j] = matrix[i, j]; } a[i, n + i] = 1; }
            for (int c = 0; c < n; c++)
            {
                int pivot = -1;
                for (int r = c; r < n; r++) { if (a[r, c] != 0) { pivot = r; break; } }
                if (pivot < 0) { return null; }
                for (int j = 0; j < 2 * n; j++) { int t = a[c, j]; a[c, j] = a[pivot, j]; a[pivot, j] = t; }
                int inverse = Power(a[c, c], Field - 2);
                for (int j = 0; j < 2 * n; j++) { a[c, j] = Mod((long)a[c, j] * inverse); }
                for (int r = 0; r < n; r++) { if (r == c || a[r, c] == 0) { continue; } int f = a[r, c]; for (int j = 0; j < 2 * n; j++) { a[r, j] = Mod(a[r, j] - (long)f * a[c, j]); } }
            }
            var result = new int[n, n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) { result[i, j] = a[i, n + j]; } }
            return result;
        }

        public int[] Target(string message) => Enumerable.Range(0, Oil).Select(k => Hashing.H(Hashing.Bytes(message), Hashing.Bytes(k))).Select(h => (h[0] << 8 | h[1]) % Field).ToArray();

        private static long Form(int[,] a, int[] x)
        {
            long s = 0;
            for (int i = 0; i < x.Length; i++) { for (int j = 0; j < x.Length; j++) { s += (long)a[i, j] * x[i] % Field * x[j]; } }
            return ((s % Field) + Field) % Field;
        }

        /// <summary>Chooses random vinegar values, solves the linear system for the oil values and applies the inverse of the secret map.</summary>
        public int[] Sign(string message)
        {
            int[] target = Target(message);
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var x = new int[Variables];
                for (int i = 0; i < Vinegar; i++) { x[i] = _random.Next(Field); }
                var matrix = new int[Oil, Oil];
                var rhs = new int[Oil];
                for (int k = 0; k < Oil; k++)
                {
                    long constant = Form(_central[k], x);
                    for (int j = 0; j < Oil; j++)
                    {
                        long coefficient = 0;
                        for (int i = 0; i < Vinegar; i++) { coefficient += (_central[k][i, Vinegar + j] + _central[k][Vinegar + j, i]) * (long)x[i]; }
                        matrix[k, j] = Mod(coefficient);
                    }
                    rhs[k] = Mod(target[k] - constant);
                }
                var inverse = Invert(matrix);
                if (inverse == null) { continue; }
                for (int j = 0; j < Oil; j++) { long s = 0; for (int k = 0; k < Oil; k++) { s += (long)inverse[j, k] * rhs[k]; } x[Vinegar + j] = Mod(s); }
                var signature = new int[Variables];
                for (int i = 0; i < Variables; i++) { long s = 0; for (int j = 0; j < Variables; j++) { s += (long)_tInverse[i, j] * x[j]; } signature[i] = Mod(s); }
                return signature;
            }
            throw new InvalidOperationException("No signature was found.");
        }

        public bool Verify(string message, int[] signature)
        {
            if (signature.Length != Variables || signature.Any(v => v < 0 || v >= Field)) { return false; }
            int[] target = Target(message);
            return Enumerable.Range(0, Oil).All(k => Form(Public[k], signature) == target[k]);
        }
    }

}

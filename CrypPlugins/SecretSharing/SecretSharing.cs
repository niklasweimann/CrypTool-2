using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;

namespace SecretSharing
{
    /// <summary>
    /// Shamir's threshold secret sharing (1979) with Feldman's verifiable extension (1987).
    /// The secret s is the constant term of a random polynomial f of degree k-1 over Z_q; share i is (i, f(i)). Any k shares give
    /// s = f(0) by Lagrange interpolation, k-1 shares tell nothing about s. For verification the dealer also publishes the
    /// commitments C_j = g^(a_j) mod P of the coefficients: share (i, y) is correct if g^y = product of C_j^(i^j) mod P.
    /// The group is the 1024-bit safe prime P = 2q+1 of RFC 2409 with g = 4 (of order q).
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("SecretSharing.Properties.Resources", "PluginCaption", "PluginTooltip", "SecretSharing/DetailedDescription/doc.xml", "SecretSharing/icon.png")]
    [ComponentCategory(ComponentCategory.Protocols)]
    public class SecretSharing : ICrypComponent
    {
        private SecretSharingSettings _settings = new SecretSharingSettings();

        [PropertyInfo(Direction.InputData, "SecretCaption", "SecretTooltip", false)]
        public BigInteger Secret { get; set; }

        [PropertyInfo(Direction.InputData, "ThresholdCaption", "ThresholdTooltip", false)]
        public int Threshold { get; set; }

        [PropertyInfo(Direction.InputData, "NumberCaption", "NumberTooltip", false)]
        public int Number { get; set; }

        [PropertyInfo(Direction.InputData, "InputSharesCaption", "InputSharesTooltip", false)]
        public string InputShares { get; set; }

        [PropertyInfo(Direction.InputData, "InputCommitmentsCaption", "InputCommitmentsTooltip", false)]
        public string InputCommitments { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputSharesCaption", "OutputSharesTooltip", false)]
        public string OutputShares { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputCommitmentsCaption", "OutputCommitmentsTooltip", false)]
        public string OutputCommitments { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputSecretCaption", "OutputSecretTooltip", false)]
        public BigInteger OutputSecret { get; set; }

        [PropertyInfo(Direction.OutputData, "ValidCaption", "ValidTooltip", false)]
        public bool Valid { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (SecretSharingSettings)value;
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
                switch (_settings.Action)
                {
                    case 1:
                        OutputSecret = Sharing.Reconstruct(Sharing.ParseShares(InputShares));
                        OnPropertyChanged("OutputSecret");
                        break;
                    case 2:
                        var shares = Sharing.ParseShares(InputShares);
                        Valid = shares.All(share => Sharing.VerifyShare(share.Key, share.Value, Sharing.ParseCommitments(InputCommitments)));
                        OnPropertyChanged("Valid");
                        GuiLogMessage(Valid ? "All given shares are valid." : "At least one share is NOT valid.", Valid ? NotificationLevel.Info : NotificationLevel.Warning);
                        break;
                    default:
                        var polynomial = Sharing.RandomPolynomial(Secret, Threshold);
                        OutputShares = string.Join("\n", Enumerable.Range(1, Number).Select(i => i + "," + Sharing.Evaluate(polynomial, i)));
                        OutputCommitments = _settings.Verifiable ? string.Join("\n", polynomial.Select(a => BigInteger.ModPow(Sharing.G, a, Sharing.P))) : null;
                        OnPropertyChanged("OutputShares");
                        OnPropertyChanged("OutputCommitments");
                        break;
                }
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
            Dispose();
        }

        public void PostExecution()
        {
            Dispose();
        }

        public void Dispose()
        {
            Secret = BigInteger.Zero;
            Threshold = Number = 0;
            InputShares = InputCommitments = OutputShares = OutputCommitments = null;
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

    /// <summary>Shamir's scheme and Feldman's commitments, free of plugin infrastructure; invalid input throws ArgumentException.</summary>
    public static class Sharing
    {
        /// <summary>The safe prime of RFC 2409, group 2 (1024 bits).</summary>
        public static readonly BigInteger P = BigInteger.Parse("0" + "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE65381FFFFFFFFFFFFFFFF", NumberStyles.HexNumber);

        /// <summary>The prime order q = (P-1)/2 of the subgroup; shares and coefficients are numbers modulo q.</summary>
        public static readonly BigInteger Q = (P - 1) / 2;

        /// <summary>4 = 2^2 generates the subgroup of order q.</summary>
        public static readonly BigInteger G = 4;

        private static BigInteger Mod(BigInteger a, BigInteger n) => ((a % n) + n) % n;

        private static BigInteger RandomBelow(BigInteger limit)
        {
            var bytes = new byte[limit.ToByteArray().Length + 8];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())   // fully qualified: the random number generator plugin has a namespace of the same name
            {
                random.GetBytes(bytes);
            }
            bytes[bytes.Length - 1] = 0;   // not negative
            return new BigInteger(bytes) % limit;
        }

        /// <summary>The coefficients a_0 = secret, a_1, ..., a_(k-1) of a random polynomial.</summary>
        public static BigInteger[] RandomPolynomial(BigInteger secret, int threshold)
        {
            if (threshold < 1) { throw new ArgumentException("The threshold must be at least 1."); }
            if (secret < 0 || secret >= Q) { throw new ArgumentException("The secret must be a number from 0 to q-1, where q is the 1023-bit prime (P-1)/2 of RFC 2409."); }
            var coefficients = new BigInteger[threshold];
            coefficients[0] = secret;
            for (int j = 1; j < threshold; j++) { coefficients[j] = RandomBelow(Q); }
            return coefficients;
        }

        /// <summary>f(x) mod q by Horner's rule.</summary>
        public static BigInteger Evaluate(BigInteger[] coefficients, BigInteger x)
        {
            BigInteger result = 0;
            for (int j = coefficients.Length - 1; j >= 0; j--) { result = (result * x + coefficients[j]) % Q; }
            return result;
        }

        /// <summary>Lagrange interpolation at 0: s = sum of y_i * product over j != i of x_j / (x_j - x_i), modulo q.</summary>
        public static BigInteger Reconstruct(Dictionary<BigInteger, BigInteger> shares)
        {
            BigInteger secret = 0;
            foreach (var share in shares)
            {
                BigInteger numerator = 1, denominator = 1;
                foreach (BigInteger other in shares.Keys.Where(x => x != share.Key))
                {
                    numerator = numerator * other % Q;
                    denominator = denominator * Mod(other - share.Key, Q) % Q;
                }
                secret = (secret + share.Value * numerator % Q * BigInteger.ModPow(denominator, Q - 2, Q)) % Q;
            }
            return Mod(secret, Q);
        }

        /// <summary>g^y = product of C_j^(x^j) mod P.</summary>
        public static bool VerifyShare(BigInteger x, BigInteger y, BigInteger[] commitments)
        {
            BigInteger expected = 1;
            BigInteger power = 1;
            foreach (BigInteger commitment in commitments)
            {
                expected = expected * BigInteger.ModPow(commitment, power, P) % P;
                power = power * x % Q;
            }
            return BigInteger.ModPow(G, y, P) == expected;
        }

        /// <summary>Lines "x,y" (or "x y", "x:y").</summary>
        public static Dictionary<BigInteger, BigInteger> ParseShares(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("No shares were given."); }
            var shares = new Dictionary<BigInteger, BigInteger>();
            foreach (string line in text.Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split(new[] { ',', ':', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) { continue; }
                if (parts.Length != 2 || !BigInteger.TryParse(parts[0], out BigInteger x) || !BigInteger.TryParse(parts[1], out BigInteger y)) { throw new ArgumentException("A share must have the form \"x,y\", but found: " + line.Trim()); }
                if (x.IsZero || shares.ContainsKey(x)) { throw new ArgumentException("The x values of the shares must be different and not 0."); }
                shares[x] = y;
            }
            return shares;
        }

        public static BigInteger[] ParseCommitments(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("No commitments were given."); }
            try
            {
                return text.Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(BigInteger.Parse).ToArray();
            }
            catch (FormatException)
            {
                throw new ArgumentException("The commitments must be integers, one per line.");
            }
        }
    }
}

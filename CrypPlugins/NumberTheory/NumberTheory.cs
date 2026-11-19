using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Controls;

namespace NumberTheory
{
    /// <summary>
    /// Three algorithms of elementary number theory that show their steps: modular exponentiation by repeated squaring
    /// (square and multiply), Shanks' baby-step giant-step algorithm for the discrete logarithm, and the Chinese remainder theorem.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("NumberTheory.Properties.Resources", "PluginCaption", "PluginTooltip", "NumberTheory/DetailedDescription/doc.xml", "NumberTheory/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class NumberTheory : ICrypComponent
    {
        private NumberTheorySettings _settings = new NumberTheorySettings();

        [PropertyInfo(Direction.InputData, "Input1Caption", "Input1Tooltip", false)]
        public BigInteger Input1 { get; set; }

        [PropertyInfo(Direction.InputData, "Input2Caption", "Input2Tooltip", false)]
        public BigInteger Input2 { get; set; }

        [PropertyInfo(Direction.InputData, "ModCaption", "ModTooltip", false)]
        public BigInteger Mod { get; set; }

        [PropertyInfo(Direction.InputData, "ResiduesCaption", "ResiduesTooltip", false)]
        public string Residues { get; set; }

        [PropertyInfo(Direction.InputData, "ModuliCaption", "ModuliTooltip", false)]
        public string Moduli { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputCaption", "OutputTooltip", false)]
        public BigInteger Output { get; set; }

        [PropertyInfo(Direction.OutputData, "StepsCaption", "StepsTooltip", false)]
        public string Steps { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (NumberTheorySettings)value;
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
                var steps = new StringBuilder();
                BigInteger result;
                switch (_settings.Operation)
                {
                    case 1:
                        result = Algorithms.BabyStepGiantStep(Input1, Input2, Mod, steps);
                        break;
                    case 2:
                        result = Algorithms.ChineseRemainder(Residues, Moduli, steps);
                        break;
                    default:
                        result = Algorithms.SquareAndMultiply(Input1, Input2, Mod, steps);
                        break;
                }
                Output = result;
                Steps = steps.ToString();
                OnPropertyChanged("Output");
                OnPropertyChanged("Steps");
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
            Input1 = Input2 = Mod = BigInteger.Zero;
            Residues = Moduli = null;
            Steps = null;
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

    /// <summary>The algorithms, free of plugin infrastructure. Each writes its steps to the given builder; invalid input throws ArgumentException.</summary>
    public static class Algorithms
    {
        private static BigInteger Mod(BigInteger a, BigInteger n) => ((a % n) + n) % n;

        // (BigInteger.GetBitLength does not exist on .NET Framework, which the desktop version of CrypTool 2 runs on)
        private static int BitLength(BigInteger value)
        {
            int length = 0;
            for (; value > 0; value >>= 1) { length++; }
            return length;
        }

        /// <summary>x^e mod n by the binary method: for every bit of e, from the highest, square; for a 1 bit also multiply by x.</summary>
        public static BigInteger SquareAndMultiply(BigInteger x, BigInteger e, BigInteger n, StringBuilder steps)
        {
            if (n < 2) { throw new ArgumentException("The modulus must be at least 2."); }
            if (e < 0) { throw new ArgumentException("The exponent must not be negative."); }
            x = Mod(x, n);
            string bits = e.IsZero ? "0" : string.Concat(Enumerable.Range(0, BitLength(e)).Reverse().Select(i => ((e >> i) & 1).ToString()));
            steps.AppendLine(string.Format("Exponent {0} in binary: {1}", e, bits));
            BigInteger result = BigInteger.One;
            foreach (char bit in bits)
            {
                result = result * result % n;
                string line = string.Format("bit {0}: square -> {1}", bit, result);
                if (bit == '1')
                {
                    result = result * x % n;
                    line += string.Format(", multiply by {0} -> {1}", x, result);
                }
                steps.AppendLine(line);
            }
            steps.AppendLine(string.Format("Result: {0}", result));
            return result;
        }

        /// <summary>Shanks' algorithm: finds the smallest e with g^e = h (mod p), using about sqrt(p) multiplications and as many table entries.</summary>
        public static BigInteger BabyStepGiantStep(BigInteger g, BigInteger h, BigInteger p, StringBuilder steps)
        {
            if (p < 2) { throw new ArgumentException("The modulus must be at least 2."); }
            g = Mod(g, p);
            h = Mod(h, p);
            if (BigInteger.GreatestCommonDivisor(g, p) != 1) { throw new ArgumentException("The base must be coprime to the modulus."); }
            if (p > 100000000000000) { throw new ArgumentException("The modulus is too large for this algorithm (more than 10^14)."); }

            // m = ceiling of the square root of the group size
            BigInteger m = (BigInteger)Math.Ceiling(Math.Sqrt((double)p)) + 1;
            steps.AppendLine(string.Format("Find e with {0}^e = {1} (mod {2}); m = {3}", g, h, p, m));

            var babySteps = new Dictionary<BigInteger, BigInteger>();
            BigInteger power = BigInteger.One;
            for (BigInteger j = 0; j < m; j++)
            {
                if (!babySteps.ContainsKey(power)) { babySteps[power] = j; }
                if (j < 10) { steps.AppendLine(string.Format("baby step j={0}: {1}^{0} = {2}", j, g, power)); }
                power = power * g % p;
            }
            steps.AppendLine(string.Format("({0} baby steps stored)", babySteps.Count));

            // giant steps: h * g^(-m*i)
            BigInteger factor = BigInteger.ModPow(BigInteger.ModPow(g, m, p), p - 2, p);   // g^-m, p prime; for composite p use the modular inverse below
            if (BigInteger.ModPow(g, m, p) * factor % p != 1) { factor = ModularInverse(BigInteger.ModPow(g, m, p), p); }
            BigInteger gamma = h;
            for (BigInteger i = 0; i <= m; i++)
            {
                if (babySteps.TryGetValue(gamma, out BigInteger j))
                {
                    BigInteger e = i * m + j;
                    steps.AppendLine(string.Format("giant step i={0}: {1} equals the baby step j={2}", i, gamma, j));
                    steps.AppendLine(string.Format("e = i*m + j = {0}*{1} + {2} = {3}", i, m, j, e));
                    return e;
                }
                if (i < 10) { steps.AppendLine(string.Format("giant step i={0}: {1} * g^(-m*{0}) = {2}: not in the table", i, h, gamma)); }
                gamma = gamma * factor % p;
            }
            throw new ArgumentException(string.Format("There is no e with {0}^e = {1} (mod {2}).", g, h, p));
        }

        private static BigInteger ModularInverse(BigInteger a, BigInteger n)
        {
            // extended Euclid
            BigInteger t = 0, newT = 1, r = n, newR = Mod(a, n);
            while (!newR.IsZero)
            {
                BigInteger q = r / newR;
                (t, newT) = (newT, t - q * newT);
                (r, newR) = (newR, r - q * newR);
            }
            if (r != 1) { throw new ArgumentException(string.Format("{0} has no inverse modulo {1}.", a, n)); }
            return Mod(t, n);
        }

        private static BigInteger[] ParseList(string text, string what)
        {
            if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("The list of " + what + " is empty."); }
            try
            {
                return text.Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(BigInteger.Parse).ToArray();
            }
            catch (FormatException)
            {
                throw new ArgumentException("The list of " + what + " must contain integers separated by commas.");
            }
        }

        /// <summary>The solution x modulo M = m1*m2*... of x = a_i (mod m_i) for pairwise coprime moduli.</summary>
        public static BigInteger ChineseRemainder(string residues, string moduli, StringBuilder steps)
        {
            BigInteger[] a = ParseList(residues, "residues");
            BigInteger[] m = ParseList(moduli, "moduli");
            if (a.Length != m.Length) { throw new ArgumentException("There must be as many residues as moduli."); }
            if (m.Any(v => v < 2)) { throw new ArgumentException("Every modulus must be at least 2."); }
            for (int i = 0; i < m.Length; i++)
            {
                for (int j = i + 1; j < m.Length; j++)
                {
                    if (BigInteger.GreatestCommonDivisor(m[i], m[j]) != 1) { throw new ArgumentException(string.Format("The moduli {0} and {1} are not coprime.", m[i], m[j])); }
                }
            }

            BigInteger product = m.Aggregate(BigInteger.One, (x, y) => x * y);
            steps.AppendLine(string.Format("M = {0} = {1}", string.Join(" * ", m), product));
            BigInteger x = 0;
            for (int i = 0; i < m.Length; i++)
            {
                BigInteger mi = product / m[i];
                BigInteger yi = ModularInverse(mi, m[i]);
                x += Mod(a[i], m[i]) * mi * yi;
                steps.AppendLine(string.Format("i={0}: M_i = M / {1} = {2}, y_i = M_i^-1 mod {1} = {3}, term a_i * M_i * y_i = {4}", i + 1, m[i], mi, yi, Mod(a[i], m[i]) * mi * yi));
            }
            x = Mod(x, product);
            steps.AppendLine(string.Format("x = sum mod M = {0}", x));
            return x;
        }
    }
}

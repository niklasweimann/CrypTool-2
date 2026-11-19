using CrypTool.PluginBase;
using System;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;

namespace RandomnessTests
{
    /// <summary>
    /// Statistical tests of the randomness of a bit sequence: the four tests of FIPS 140-1 and the poker, serial and runs tests.
    /// The formulas are those of FIPS 140-1, Knuth (The Art of Computer Programming, vol. 2, 3.3.2) and NIST SP 800-22.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("RandomnessTests.Properties.Resources", "PluginCaption", "PluginTooltip", "RandomnessTests/DetailedDescription/doc.xml", "RandomnessTests/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsRandomNumbers)]
    public class RandomnessTests : ICrypComponent
    {
        private RandomnessTestsSettings _settings = new RandomnessTestsSettings();

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public byte[] InputData
        {
            get;
            set;
        }

        [PropertyInfo(Direction.OutputData, "PassedCaption", "PassedTooltip", false)]
        public bool Passed
        {
            get;
            set;
        }

        [PropertyInfo(Direction.OutputData, "ReportCaption", "ReportTooltip", false)]
        public string Report
        {
            get;
            set;
        }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (RandomnessTestsSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            bool[] bits = Statistics.ToBits(InputData);
            TestResult result;
            switch (_settings.Test)
            {
                case 1:
                    result = Statistics.Poker(bits, _settings.PokerBlockLength);
                    break;
                case 2:
                    result = Statistics.Serial(bits, _settings.SerialBlockLength);
                    break;
                case 3:
                    result = Statistics.Runs(bits);
                    break;
                default:
                    result = Statistics.Fips1401(bits);
                    break;
            }

            Passed = result.Passed;
            Report = result.Report;
            OnPropertyChanged("Passed");
            OnPropertyChanged("Report");
            GuiLogMessage(result.Passed ? "The sequence passed the test." : "The sequence did not pass the test.", result.Passed ? NotificationLevel.Info : NotificationLevel.Warning);
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
            InputData = null;
            Report = null;
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

    /// <summary>The outcome of a test: passed or not, and a report with the statistic, its limits and the p-value.</summary>
    public sealed class TestResult
    {
        public bool Passed;
        public string Report;
        /// <summary>The test statistic (for the FIPS 140-1 test: of the poker test).</summary>
        public double Statistic;
        /// <summary>The p-value of the chi-square based tests; NaN where the test uses fixed intervals.</summary>
        public double PValue = double.NaN;
    }

    /// <summary>The tests themselves, free of any plugin infrastructure.</summary>
    public static class Statistics
    {
        /// <summary>A test fails when its p-value is below this significance level.</summary>
        public const double Alpha = 0.01;

        /// <summary>The bits of the bytes, most significant bit first.</summary>
        public static bool[] ToBits(byte[] data)
        {
            data = data ?? new byte[0];
            var bits = new bool[data.Length * 8];
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = (data[i / 8] >> (7 - i % 8) & 1) == 1;
            }
            return bits;
        }

        /// <summary>The four tests of FIPS 140-1 on the first 20000 bits: monobit, poker (4 bits), runs and long run.</summary>
        public static TestResult Fips1401(bool[] bits)
        {
            const int N = 20000;
            if (bits.Length < N)
            {
                return new TestResult { Passed = false, Report = string.Format("FIPS 140-1 needs {0} bits, but only {1} were given.", N, bits.Length) };
            }

            var report = new StringBuilder();
            bool passed = true;

            // monobit test: the number of ones must lie in (9654, 10346)
            int ones = 0;
            for (int i = 0; i < N; i++) { if (bits[i]) ones++; }
            bool monobit = ones > 9654 && ones < 10346;
            passed &= monobit;
            report.AppendLine(string.Format("Monobit test: {0} ones (required: 9654 < n1 < 10346): {1}", ones, monobit ? "passed" : "FAILED"));

            // poker test: 5000 segments of 4 bits, X = 16/5000 * sum(f(i)^2) - 5000 must lie in (1.03, 57.4)
            var f = new int[16];
            for (int i = 0; i < N; i += 4)
            {
                f[(bits[i] ? 8 : 0) + (bits[i + 1] ? 4 : 0) + (bits[i + 2] ? 2 : 0) + (bits[i + 3] ? 1 : 0)]++;
            }
            double sum = 0;
            foreach (int count in f) { sum += (double)count * count; }
            double x = 16.0 / 5000.0 * sum - 5000.0;
            bool poker = x > 1.03 && x < 57.4;
            passed &= poker;
            report.AppendLine(string.Format("Poker test: X = {0:F2} (required: 1.03 < X < 57.4): {1}", x, poker ? "passed" : "FAILED"));

            // runs test: number of runs of each length, separately for zeros and ones; lengths of 6 and more are counted together
            int[] lower = { 2267, 1079, 502, 223, 90, 90 };
            int[] upper = { 2733, 1421, 748, 402, 223, 223 };
            var runs = new int[2, 6];
            int longest = 0;
            int length = 1;
            for (int i = 1; i <= N; i++)
            {
                if (i < N && bits[i] == bits[i - 1])
                {
                    length++;
                    continue;
                }
                runs[bits[i - 1] ? 1 : 0, Math.Min(length, 6) - 1]++;
                longest = Math.Max(longest, length);
                length = 1;
            }
            bool runsPassed = true;
            for (int value = 0; value < 2; value++)
            {
                for (int k = 0; k < 6; k++)
                {
                    runsPassed &= runs[value, k] >= lower[k] && runs[value, k] <= upper[k];
                }
            }
            passed &= runsPassed;
            report.AppendLine(string.Format("Runs test: zeros {0}, ones {1} (runs of length 1, 2, 3, 4, 5, 6 or more; required: {2}): {3}",
                string.Join("/", new[] { runs[0, 0], runs[0, 1], runs[0, 2], runs[0, 3], runs[0, 4], runs[0, 5] }),
                string.Join("/", new[] { runs[1, 0], runs[1, 1], runs[1, 2], runs[1, 3], runs[1, 4], runs[1, 5] }),
                string.Join(" ", new[] { "2267-2733", "1079-1421", "502-748", "223-402", "90-223", "90-223" }), runsPassed ? "passed" : "FAILED"));

            // long run test: no run of 34 or more equal bits
            bool longRun = longest < 34;
            passed &= longRun;
            report.AppendLine(string.Format("Long run test: longest run {0} (required: < 34): {1}", longest, longRun ? "passed" : "FAILED"));
            report.AppendLine(passed ? "The sequence passed all four tests." : "The sequence failed at least one test.");

            return new TestResult { Passed = passed, Report = report.ToString(), Statistic = x };
        }

        /// <summary>
        /// Poker test with blocks of m bits (Knuth): chi-square = 2^m / k * sum(f^2) - k over the k blocks, 2^m - 1 degrees of freedom.
        /// k should be at least 5 * 2^m.
        /// </summary>
        public static TestResult Poker(bool[] bits, int m)
        {
            int k = bits.Length / m;
            int classes = 1 << m;
            if (k < 5 * classes)
            {
                return new TestResult { Passed = false, Report = string.Format("The poker test with {0}-bit blocks needs at least {1} bits, but only {2} were given.", m, 5 * classes * m, bits.Length) };
            }

            var f = new int[classes];
            for (int block = 0; block < k; block++)
            {
                int value = 0;
                for (int j = 0; j < m; j++) { value = value << 1 | (bits[block * m + j] ? 1 : 0); }
                f[value]++;
            }
            double sum = 0;
            foreach (int count in f) { sum += (double)count * count; }
            double chiSquare = (double)classes / k * sum - k;
            return ChiSquareResult("Poker test", string.Format("{0} blocks of {1} bits", k, m), chiSquare, classes - 1);
        }

        /// <summary>
        /// Serial test (NIST SP 800-22, 2.11) with overlapping blocks of m bits (the sequence is continued cyclically):
        /// the difference of psi^2(m) and psi^2(m-1) is chi-square distributed with 2^(m-1) degrees of freedom.
        /// </summary>
        public static TestResult Serial(bool[] bits, int m, int minimumBits = -1)
        {
            int n = bits.Length;
            if (minimumBits < 0) { minimumBits = 5 * (1 << m); }
            if (n < minimumBits)
            {
                return new TestResult { Passed = false, Report = string.Format("The serial test with {0}-bit blocks needs at least {1} bits, but only {2} were given.", m, minimumBits, n) };
            }

            double chiSquare = SerialChiSquare(bits, m);
            return ChiSquareResult("Serial test", string.Format("{0} overlapping blocks of {1} bits", n, m), chiSquare, 1 << (m - 1));
        }

        /// <summary>psi^2(m) - psi^2(m-1) of the serial test.</summary>
        public static double SerialChiSquare(bool[] bits, int m)
        {
            return Psi(bits, m) - Psi(bits, m - 1);
        }

        static double Psi(bool[] bits, int m)
        {
            if (m <= 0) { return 0; }
            int n = bits.Length;
            var counts = new int[1 << m];
            for (int i = 0; i < n; i++)
            {
                int value = 0;
                for (int j = 0; j < m; j++) { value = value << 1 | (bits[(i + j) % n] ? 1 : 0); }
                counts[value]++;
            }
            double sum = 0;
            foreach (int count in counts) { sum += (double)count * count; }
            return (double)(1 << m) / n * sum - n;
        }

        /// <summary>Runs test (NIST SP 800-22, 2.3): the total number of runs of zeros and ones must be as expected for the proportion of ones.</summary>
        public static TestResult Runs(bool[] bits, int minimumBits = 100)
        {
            int n = bits.Length;
            if (n < minimumBits)
            {
                return new TestResult { Passed = false, Report = string.Format("The runs test needs at least {0} bits, but only {1} were given.", minimumBits, n) };
            }

            int ones = 0;
            foreach (bool bit in bits) { if (bit) ones++; }
            double pi = (double)ones / n;
            if (Math.Abs(pi - 0.5) >= 2.0 / Math.Sqrt(n))
            {
                return new TestResult { Passed = false, PValue = 0, Report = string.Format("The proportion of ones ({0:F4}) is too far from 1/2 for the runs test: the sequence is not random.", pi) };
            }

            int runs = 1;
            for (int i = 1; i < n; i++) { if (bits[i] != bits[i - 1]) runs++; }
            double p = ChiSquare.Erfc(Math.Abs(runs - 2.0 * n * pi * (1 - pi)) / (2.0 * Math.Sqrt(2.0 * n) * pi * (1 - pi)));
            bool passed = p >= Alpha;
            return new TestResult
            {
                Passed = passed,
                PValue = p,
                Statistic = runs,
                Report = string.Format("Runs test: {0} runs in {1} bits, proportion of ones {2:F4}, p-value {3:F6} (significance level {4}): {5}", runs, n, pi, p, Alpha, passed ? "passed" : "FAILED")
            };
        }

        static TestResult ChiSquareResult(string name, string what, double chiSquare, int degreesOfFreedom)
        {
            double p = ChiSquare.PValue(chiSquare, degreesOfFreedom);
            bool passed = p >= Alpha;
            return new TestResult
            {
                Passed = passed,
                Statistic = chiSquare,
                PValue = p,
                Report = string.Format("{0}: {1}, chi-square = {2:F4} with {3} degrees of freedom, p-value {4:F6} (significance level {5}): {6}", name, what, chiSquare, degreesOfFreedom, p, Alpha, passed ? "passed" : "FAILED")
            };
        }
    }

    /// <summary>The chi-square distribution and the complementary error function, via the regularized incomplete gamma function.</summary>
    public static class ChiSquare
    {
        /// <summary>The probability that a chi-square distributed variable with the given degrees of freedom is at least x.</summary>
        public static double PValue(double x, int degreesOfFreedom)
        {
            return x <= 0 ? 1 : GammaQ(degreesOfFreedom / 2.0, x / 2.0);
        }

        /// <summary>erfc(x) for x &gt;= 0.</summary>
        public static double Erfc(double x)
        {
            return x <= 0 ? 1 : GammaQ(0.5, x * x);
        }

        /// <summary>The regularized upper incomplete gamma function Q(a, x) (series for x &lt; a + 1, continued fraction otherwise).</summary>
        public static double GammaQ(double a, double x)
        {
            if (x < a + 1)
            {
                double term = 1 / a;
                double sum = term;
                for (int n = 1; n < 1000 && Math.Abs(term) > Math.Abs(sum) * 1e-15; n++)
                {
                    term *= x / (a + n);
                    sum += term;
                }
                return 1 - sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
            }

            const double tiny = 1e-300;
            double b = x + 1 - a;
            double c = 1 / tiny;
            double d = 1 / b;
            double h = d;
            for (int i = 1; i < 1000; i++)
            {
                double an = -i * (i - a);
                b += 2;
                d = an * d + b;
                if (Math.Abs(d) < tiny) { d = tiny; }
                c = b + an / c;
                if (Math.Abs(c) < tiny) { c = tiny; }
                d = 1 / d;
                double delta = d * c;
                h *= delta;
                if (Math.Abs(delta - 1) < 1e-15) { break; }
            }
            return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
        }

        /// <summary>ln(Gamma(x)) by the Lanczos approximation.</summary>
        static double LogGamma(double x)
        {
            double[] coefficients =
            {
                76.18009172947146, -86.50532032941677, 24.01409824083091,
                -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5
            };
            double y = x;
            double tmp = x + 5.5;
            tmp -= (x + 0.5) * Math.Log(tmp);
            double ser = 1.000000000190015;
            foreach (double coefficient in coefficients) { ser += coefficient / ++y; }
            return -tmp + Math.Log(2.5066282746310005 * ser / x);
        }
    }
}

// The ISAP bit commitment scheme (Martin Schmidt, "Bit commitment based on the inhomogeneous simultaneous
// approximation problem") in managed code, for the ISAPBitCommitmentScheme plugin in the browser.
//
// On the desktop the plugin calls a C++/CLI wrapper (Lib/ISAPCommitmentScheme.dll, source in
// LibSource/ISAPCommitmentScheme) around C++ code that uses GMP and MPFR. This is a port of that code with the same
// interface (namespace ISAPCommitmentSchemeWrapper, Wrapper.Run, ISAPResult):
//   * GMP integers are BigInteger, GMP rationals are exact fractions of BigIntegers;
//   * the accuracy eps = N^(-delta) with N = 2^s is not stored as a float: since N is a power of two,
//     eps = 2^(-s * delta), and every comparison with eps (or 1 / sqrt(eps)) is done with base-2 logarithms.
// The log text has the same lines as the original's output.
using System;
using System.Globalization;
using System.Numerics;
// (RandomNumberGenerator is written with its namespace: a plugin namespace of that name exists in this assembly)
using System.Text;

namespace ISAPCommitmentSchemeWrapper
{
    /// <summary>The values of one commitment (see Wrapper.h of the original).</summary>
    public class ISAPResult
    {
        public BigInteger[] p;
        public BigInteger q;
        public double[] alpha;
        public BigInteger[] a;
        public BigInteger[] b;
        public double[] eta;
        public string log;
    }

    public class Wrapper
    {
        /// <summary>Commits to bit <paramref name="m"/> and opens the commitment again; the parameters as in the original wrapper.</summary>
        public ISAPResult Run(bool m, long dimension, long s)
        {
            double epsilonPrime = 1.0 / (3 * dimension * s);
            double delta = 1.5 + epsilonPrime;
            double deltaPrime = 1.0 + epsilonPrime;

            var scheme = new ISAPCommitmentScheme();
            scheme.Setup(dimension, s, delta, deltaPrime);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            scheme.Commit(m);
            double commitTime = clock.Elapsed.TotalSeconds;
            scheme.Open();

            StringBuilder log = scheme.Log;
            log.Append("\nRestarts log\n");
            log.Append("------------\n");
            log.Append("Number of restart due to equation (19)\t\t\t= ").Append(scheme.RestartCounter1).Append('\n');
            log.Append("Number of restart due to equation (20)\t\t\t= ").Append(scheme.RestartCounter2).Append('\n');
            log.Append("Time to compute the commitment (inner measurement)\t= ").Append(Number(scheme.CommitmentCpuTime)).Append("s\n");
            log.Append("Time to compute the commitment (outer measurement)\t= ").Append(Number(commitTime)).Append("s\n");
            log.Append("Time to compute the verification\t\t\t= ").Append(Number(scheme.OpeningCpuTime)).Append("s\n");

            var result = new ISAPResult
            {
                log = log.ToString(),
                q = scheme.Q,
                p = new BigInteger[dimension],
                alpha = new double[dimension],
                a = new BigInteger[dimension],
                b = new BigInteger[dimension],
                eta = new double[dimension],
            };
            for (int i = 0; i < dimension; i++)
            {
                result.p[i] = scheme.P[i];
                result.a[i] = scheme.A[i];
                result.b[i] = scheme.B[i];
                result.alpha[i] = Fraction.ToDouble(scheme.A[i], scheme.B[i]);
                result.eta[i] = Fraction.ToDouble(scheme.EtaNumerator[i], scheme.EtaDenominator[i]);
            }
            return result;
        }

        internal static string Number(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
    }

    /// <summary>Helpers for fractions of BigIntegers.</summary>
    internal static class Fraction
    {
        /// <summary>numerator / denominator as a double (also when both are far beyond the double range).</summary>
        internal static double ToDouble(BigInteger numerator, BigInteger denominator)
        {
            if (numerator.IsZero)
            {
                return 0;
            }
            double log = Log2(BigInteger.Abs(numerator)) - Log2(BigInteger.Abs(denominator));
            return numerator.Sign * denominator.Sign * Math.Pow(2, log);
        }

        internal static double Log2(BigInteger value) => BigInteger.Log(value, 2);

        /// <summary>A positive number given by its base-2 logarithm in scientific notation, like GMP prints floats.</summary>
        internal static string Scientific(double log2)
        {
            double log10 = log2 * Math.Log10(2);
            double exponent = Math.Floor(log10);
            double mantissa = Math.Pow(10, log10 - exponent);
            return mantissa.ToString("0.#####", CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The scheme itself, following ISAPCommitmentScheme.cpp step by step.</summary>
    internal sealed class ISAPCommitmentScheme
    {
        private long _dimension;
        private long _s;
        private double _delta;
        private double _deltaPrime;

        /// <summary>log2 of the accuracy eps = N^(-delta) = 2^(-s * delta).</summary>
        private double _epsLog2;

        private BigInteger _qUpperBound, _aUpperBound, _bUpperBound, _pLowerBound, _pUpperBound;
        private BigInteger[] _c, _d;
        private bool _m;

        internal StringBuilder Log { get; } = new StringBuilder();
        internal BigInteger Q { get; private set; }
        internal BigInteger[] A { get; private set; }
        internal BigInteger[] B { get; private set; }
        internal BigInteger[] P { get; private set; }
        internal BigInteger[] EtaNumerator { get; private set; }
        internal BigInteger[] EtaDenominator { get; private set; }
        internal long RestartCounter1 { get; private set; }
        internal long RestartCounter2 { get; private set; }
        internal double CommitmentCpuTime { get; private set; }
        internal double OpeningCpuTime { get; private set; }

        internal void Setup(long dimension, long s, double delta, double deltaPrime)
        {
            _dimension = dimension;
            A = new BigInteger[dimension];
            B = new BigInteger[dimension];
            _c = new BigInteger[dimension];
            _d = new BigInteger[dimension];
            P = new BigInteger[dimension];
            EtaNumerator = new BigInteger[dimension];
            EtaDenominator = new BigInteger[dimension];
            _delta = delta;
            _deltaPrime = deltaPrime;
            _s = s;

            _aUpperBound = BigInteger.Pow(2, (int)s);
            _bUpperBound = new BigInteger(Math.Pow(2, deltaPrime * s));
            _pLowerBound = BigInteger.Zero;
            _pUpperBound = BigInteger.Pow(2, (int)s);
            _qUpperBound = BigInteger.Pow(2, (int)s);       // also named N in the paper
            _epsLog2 = -s * delta;

            Log.Append("Setup phase log\n");
            Log.Append("---------------\n");
            Log.Append("Dimension n\t\t= ").Append(_dimension).Append('\n');
            Log.Append("Security parameter s\t= ").Append(_s).Append('\n');
            Log.Append("Delta\t\t\t= ").Append(Wrapper.Number(_delta)).Append('\n');
            Log.Append("Delta'\t\t\t= ").Append(Wrapper.Number(_deltaPrime)).Append('\n');
            Log.Append("N (upper bound on q)\t= ").Append(_qUpperBound).Append('\n');
            Log.Append("Precision eps\t\t= ").Append(Fraction.Scientific(_epsLog2)).Append('\n');
            Log.Append("Lower bound on a_i\t= 0\n");
            Log.Append("Upper bound on a_i\t= ").Append(_aUpperBound).Append('\n');
            Log.Append("Lower bound on b_i\t= 0\n");
            Log.Append("Upper bound on b_i\t= ").Append(_bUpperBound).Append('\n');
            Log.Append("Lower bound on p_i\t= ").Append(_pLowerBound).Append('\n');
            Log.Append("Upper bound on p_i\t= ").Append(_pUpperBound).Append('\n');
        }

        /// <summary>The commitment phase; restarts (as the original does recursively) until the conditions hold.</summary>
        internal void Commit(bool m)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _m = m;
            while (true)
            {
                MapMessage(_m);
                GenerateRationals();
                if (!GenerateApproximations())
                {
                    RestartCounter1++;      // approximations don't satisfy the denominator conditions, equation (19)
                    continue;
                }
                if (!CheckAdditionalConditions())
                {
                    RestartCounter2++;      // approximations don't satisfy the additional conditions, equation (20)
                    continue;
                }
                break;
            }
            GeneratePAndEta();
            CommitmentCpuTime += clock.Elapsed.TotalSeconds;
        }

        internal bool Open()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool valid = true;
            Log.Append("\nOpening phase log\n");
            Log.Append("---------------\n");
            Log.Append("eps (right hand side)\t= ").Append(Fraction.Scientific(_epsLog2)).Append('\n');
            Log.Append("q (mapped message)\t= ").Append(Q).Append('\n');
            for (long i = 0; i != _dimension; ++i)
            {
                // lhs = q * a_i / b_i - p_i - eta_i, exactly: a fraction with denominator b_i * etaDenominator_i
                BigInteger denominator = B[i] * EtaDenominator[i];
                BigInteger numerator = Q * A[i] * EtaDenominator[i] - P[i] * denominator - EtaNumerator[i] * B[i];
                // as in the original, the result of the last index decides
                string quality = "0";
                valid = true;
                if (!numerator.IsZero)
                {
                    double log2 = Fraction.Log2(BigInteger.Abs(numerator)) - Fraction.Log2(BigInteger.Abs(denominator));
                    valid = log2 < _epsLog2;
                    quality = Fraction.Scientific(log2);
                }
                Log.Append("i = ").Append(i).Append(",\tAchieved approximation quality (left hand side) = ").Append(quality).Append('\n');
            }
            Log.Append(valid ? "Verifier: accept\n" : "Verifier: reject\n");
            OpeningCpuTime = clock.Elapsed.TotalSeconds;
            return valid;
        }

        /// <summary>q: an s-bit random number whose least significant bit is the message.</summary>
        private void MapMessage(bool m)
        {
            var bits = new StringBuilder();
            for (long i = 0; i != _s - 1; ++i)
            {
                bits.Append(global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(2) == 1 ? '1' : '0');
            }
            bits.Append(m ? '1' : '0');
            Q = BigInteger.Zero;
            foreach (char bit in bits.ToString())
            {
                Q = Q * 2 + (bit - '0');
            }
            Log.Append("\nMap message log\n");
            Log.Append("---------------\n");
            Log.Append("Message m\t\t= ").Append(m ? 1 : 0).Append('\n');
            Log.Append("Mapped message, [q]_2\t= (").Append(bits).Append(")\n");
            Log.Append("Mapped message, [q]_10\t= ").Append(Q).Append('\n');
        }

        /// <summary>Random fractions a_i / b_i with b_i odd and coprime with q, and a_i coprime with b_i.</summary>
        private void GenerateRationals()
        {
            for (long i = 0; i != _dimension; ++i)
            {
                BigInteger a, b;
                do
                {
                    a = RandomBelow(_aUpperBound);
                    do
                    {
                        b = RandomBelow(_bUpperBound);
                        if (b.IsEven)
                        {
                            b -= 1;
                        }
                    }
                    while (BigInteger.GreatestCommonDivisor(b, Q) != 1);
                }
                while (BigInteger.GreatestCommonDivisor(a, b) != 1);
                A[i] = a;
                B[i] = b;
            }
        }

        /// <summary>
        /// The convergents c_i / d_i of the continued fraction of q * a_i / b_i, until d_i lies between
        /// 1 / sqrt(eps) and b_i (equation (22)); false when a series ends without that (restart).
        /// </summary>
        private bool GenerateApproximations()
        {
            double dLowerBoundLog2 = -_epsLog2 / 2;     // log2(1 / sqrt(eps))
            for (long i = 0; i != _dimension; ++i)
            {
                BigInteger numerator = Q * A[i];
                BigInteger denominator = B[i];
                BigInteger oldConvergentNumerator = 0;  // k = -1
                BigInteger oldConvergentDenominator = 1;
                _c[i] = 1;                              // k = 0
                _d[i] = 0;
                BigInteger remainder = -1;
                bool boundCondition = false;
                while (!remainder.IsZero && _d[i] < B[i] && !boundCondition)
                {
                    BigInteger quotient = BigInteger.DivRem(numerator, denominator, out remainder);

                    BigInteger convergentNumerator = _c[i];
                    _c[i] = _c[i] * quotient + oldConvergentNumerator;     // recurrence (4)
                    oldConvergentNumerator = convergentNumerator;
                    BigInteger convergentDenominator = _d[i];
                    _d[i] = _d[i] * quotient + oldConvergentDenominator;   // recurrence (5)
                    oldConvergentDenominator = convergentDenominator;

                    boundCondition = _d[i] > 0 && Fraction.Log2(_d[i]) > dLowerBoundLog2 && _d[i] < B[i];
                    numerator = denominator;
                    denominator = remainder;
                }
                if (!boundCondition)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Equation (23): some index with sqrt(2 * b_i) &lt; d_i and N &lt; b_i.</summary>
        private bool CheckAdditionalConditions()
        {
            for (int i = 0; i != _dimension; ++i)
            {
                // the original compares the integer square root: floor(sqrt(2b)) < d  <=>  2b < d^2
                if (2 * B[i] < _d[i] * _d[i] && _qUpperBound < B[i])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Random p_i and eta_i = c_i / d_i - p_i.</summary>
        private void GeneratePAndEta()
        {
            Log.Append("\nGenerate p and eta log\n");
            Log.Append("----------------------\n");
            for (long i = 0; i != _dimension; ++i)
            {
                P[i] = RandomBelow(_pUpperBound - _pLowerBound) + _pLowerBound;
                BigInteger numerator = _c[i] - P[i] * _d[i];
                BigInteger denominator = _d[i];
                BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
                if (!gcd.IsZero && !gcd.IsOne)
                {
                    numerator /= gcd;
                    denominator /= gcd;
                }
                EtaNumerator[i] = numerator;
                EtaDenominator[i] = denominator;
                Log.Append("p_").Append(i).Append("\t= ").Append(P[i]).Append('\n');
                Log.Append("eta_").Append(i).Append("\t= ").Append(numerator).Append('/').Append(denominator).Append('\n');
            }
        }

        /// <summary>A uniformly random number in 0 .. limit - 1 (GMP's get_z_range).</summary>
        private static BigInteger RandomBelow(BigInteger limit)
        {
            if (limit <= 1)
            {
                return BigInteger.Zero;
            }
            // random numbers with the bit length of the limit, the ones that are too large drawn again
            long bitLength = (long)limit.GetBitLength();
            var bytes = new byte[(bitLength + 7) / 8 + 1];
            int topBits = (int)(bitLength % 8);
            BigInteger value;
            do
            {
                global::System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
                bytes[^1] = 0;                                          // sign byte: non-negative
                if (topBits != 0)
                {
                    bytes[^2] &= (byte)((1 << topBits) - 1);
                }
                value = new BigInteger(bytes);
            }
            while (value >= limit);
            return value;
        }
    }
}

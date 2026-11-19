using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Numerics;

namespace VerySmoothHash
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("VerySmoothHash.Properties.Resources", "PluginCaption", "PluginTooltip", "VerySmoothHash/DetailedDescription/doc.xml", "VerySmoothHash/icon.png")]
    [ComponentCategory(ComponentCategory.HashFunctions)]
    public class VerySmoothHash : ICrypComponent
    {
        private VerySmoothHashSettings _settings = new VerySmoothHashSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.OutputData, "HashCaption", "HashTooltip", false)]
        public string Hash
        {
            get => _hash;
            set { _hash = value; OnPropertyChanged("Hash"); }
        }
        private string _hash;

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
            set => _settings = (VerySmoothHashSettings)value;
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
                var random = new Random(_settings.Seed);
                var vsh = new VshFunction(PrimeNumbers.Prime(random, _settings.ModulusBits / 2) * PrimeNumbers.Prime(random, _settings.ModulusBits / 2));
                var hash = vsh.Hash(Encoding.UTF8.GetBytes(Input), out var steps);
                Hash = hash.ToString("X");
                Report = string.Format(Properties.Resources.VshReport, vsh.N.GetBitLength(), vsh.BlockBits, vsh.Primes[0] + ", " + vsh.Primes[1] + ", ..., " + vsh.Primes[vsh.Primes.Length - 1], steps.Count);
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
            Input = Hash = Report = null;
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
    /// The Very Smooth Hash of Contini, Lenstra and Steinfeld (2006): the message is cut into blocks of k bits; the hash value starts with x = 1 and for every block x becomes x^2 * p_1^m_1 * ... * p_k^m_k mod n,
    /// where p_1, ..., p_k are the first k primes and m_j is the j-th bit of the block. Finding a collision is as hard as finding a modular square root of a very smooth number, which is related to factoring n.
    /// The block size is the largest k for which the product of the first k primes stays below n^(2/3); the message is padded with zero bits and a final block holds its length.
    /// </summary>
    public sealed class VshFunction
    {
        public BigInteger N { get; }
        public int[] Primes { get; }
        public int BlockBits => Primes.Length;

        public VshFunction(BigInteger n)
        {
            N = n;
            var primes = new List<int>();
            BigInteger product = 1;
            // n^(2/3) as a bound: product^3 < n^2
            for (int candidate = 2; ; candidate++)
            {
                if (primes.Any(p => candidate % p == 0)) { continue; }
                if ((product * candidate) * (product * candidate) * (product * candidate) >= n * n) { break; }
                primes.Add(candidate);
                product *= candidate;
            }
            Primes = primes.ToArray();
        }

        public BigInteger Hash(byte[] message, out List<BigInteger> steps)
        {
            steps = new List<BigInteger>();
            var bits = new List<int>();
            foreach (byte b in message) { for (int i = 7; i >= 0; i--) { bits.Add(b >> i & 1); } }
            long length = bits.Count;
            while (bits.Count % BlockBits != 0) { bits.Add(0); }
            // the final block: the length of the message in bits (as many blocks as needed)
            var lengthBits = new List<int>();
            for (int i = 63; i >= 0; i--) { lengthBits.Add((int)(length >> i & 1)); }
            while (lengthBits.Count % BlockBits != 0) { lengthBits.Insert(0, 0); }
            bits.AddRange(lengthBits);
            BigInteger x = 1;
            for (int block = 0; block < bits.Count; block += BlockBits)
            {
                BigInteger factor = 1;
                for (int j = 0; j < BlockBits; j++) { if (bits[block + j] == 1) { factor *= Primes[j]; } }
                x = x * x % N * factor % N;
                steps.Add(x);
            }
            return x;
        }
    }

    public static class PrimeNumbers
    {
        public static BigInteger Prime(Random random, int bits)
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
    }

}

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Numerics;
using System.Security.Cryptography;

namespace Kleptography
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("Kleptography.Properties.Resources", "PluginCaption", "PluginTooltip", "Kleptography/DetailedDescription/doc.xml", "Kleptography/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class Kleptography : ICrypComponent
    {
        private KleptographySettings _settings = new KleptographySettings();
        private KleptographyPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (KleptographySettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new KleptographyPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>Starts a new game when the workspace runs; the game then goes on in the presentation.</summary>
        public void Execute()
        {
            ((KleptographyPresentation)Presentation).Start(_settings.Seed);
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A SETUP attack (secretly embedded trapdoor with universal protection) on RSA key generation, after the idea of Young and Yung: the key generator of the
    /// victim is changed so that the modulus n = p * q carries the prime p, encrypted with the public key of the attacker, in its upper bits.
    /// Only the attacker can read it; the keys look like ordinary RSA keys. (A demonstration with small numbers: the attacker's modulus has 128 bits.)
    /// </summary>
    public sealed class SetupAttack
    {
        public const int PrimeBits = 160;
        public const int ShiftBits = 192;   // n = p * q has 320 bits; the upper 128 bits carry the encrypted seed

        public BigInteger AttackerN, AttackerE, AttackerD;
        public BigInteger Seed, P, Q, N, Encrypted;
        public BigInteger RecoveredSeed, RecoveredP, RecoveredQ;
        public bool Recovered;

        public static readonly int[] SmallPrimes = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97 };

        public static bool IsPrime(BigInteger n)
        {
            if (n < 2) { return false; }
            foreach (int p in SmallPrimes) { if (n == p) { return true; } if (n % p == 0) { return false; } }
            BigInteger d = n - 1;
            int s = 0;
            while (d.IsEven) { d >>= 1; s++; }
            foreach (int a in SmallPrimes.Take(12))
            {
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) { continue; }
                bool composite = true;
                for (int r = 1; r < s && composite; r++) { x = BigInteger.ModPow(x, 2, n); if (x == n - 1) { composite = false; } }
                if (composite) { return false; }
            }
            return true;
        }

        public static BigInteger NextPrime(BigInteger start)
        {
            if (start.IsEven) { start++; }
            while (!IsPrime(start)) { start += 2; }
            return start;
        }

        private static BigInteger FromBytes(byte[] bytes) => new BigInteger(bytes.Concat(new byte[] { 0 }).ToArray());

        /// <summary>A prime of PrimeBits bits that depends only on the seed: SHA-256 blocks of the seed, the top bit set, then the next prime.</summary>
        public static BigInteger PrimeFromSeed(BigInteger seed)
        {
            var bytes = new List<byte>();
            using (var sha = SHA256.Create())
            {
                for (byte block = 0; bytes.Count < PrimeBits / 8; block++)
                {
                    bytes.AddRange(sha.ComputeHash(seed.ToByteArray().Concat(new[] { block }).ToArray()));
                }
            }
            BigInteger start = FromBytes(bytes.Take(PrimeBits / 8).ToArray());
            start |= BigInteger.One << (PrimeBits - 1);
            return NextPrime(start);
        }

        /// <summary>The attacker makes a key pair (two 64-bit primes, e = 65537); the victim's seed and everything follows from the numbers of the run.</summary>
        public static SetupAttack Run(int number)
        {
            var random = new Random(number);
            var attack = new SetupAttack();
            BigInteger a, b;
            do
            {
                a = NextPrime(RandomBits(random, 64));
                b = NextPrime(RandomBits(random, 64));
                attack.AttackerN = a * b;
                attack.AttackerE = 65537;
            }
            while (a == b || BigInteger.GreatestCommonDivisor(attack.AttackerE, (a - 1) * (b - 1)) != 1 || attack.AttackerN.GetBitLength() < 127);
            attack.AttackerD = ModInverse(attack.AttackerE, (a - 1) * (b - 1));

            // the victim's (changed) key generation
            attack.Seed = RandomBits(random, 100);
            attack.P = PrimeFromSeed(attack.Seed);
            attack.Encrypted = BigInteger.ModPow(attack.Seed, attack.AttackerE, attack.AttackerN);
            BigInteger target = attack.Encrypted << ShiftBits;
            attack.Q = NextPrime(target / attack.P);
            attack.N = attack.P * attack.Q;

            // the attacker reads the upper bits of the public key n
            for (int carry = 0; carry < 2 && !attack.Recovered; carry++)
            {
                BigInteger candidate = (attack.N >> ShiftBits) - carry;
                BigInteger seed = BigInteger.ModPow(candidate, attack.AttackerD, attack.AttackerN);
                BigInteger p = PrimeFromSeed(seed);
                if (attack.N % p == 0) { attack.RecoveredSeed = seed; attack.RecoveredP = p; attack.RecoveredQ = attack.N / p; attack.Recovered = true; }
            }
            return attack;
        }

        public static BigInteger RandomBits(Random random, int bits)
        {
            var bytes = new byte[(bits + 7) / 8 + 1];
            random.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            BigInteger value = new BigInteger(bytes) & ((BigInteger.One << bits) - 1);
            return value | (BigInteger.One << (bits - 1));
        }

        public static BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger g = m, x = 0, y = 1, r = a;
            while (r != 0) { BigInteger q = g / r; BigInteger t = g - q * r; g = r; r = t; t = x - q * y; x = y; y = t; }
            return (x % m + m) % m;
        }
    }

    public class KleptographyPresentation : UserControl
    {
        public KleptographyPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public SetupAttack Attack { get; private set; }

        public void Start(int number)
        {
            Action start = () =>
            {
                Attack = SetupAttack.Run(number);
                Show();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private static string Short(BigInteger value)
        {
            string text = value.ToString();
            return text.Length <= 40 ? text : text.Substring(0, 18) + "..." + text.Substring(text.Length - 18) + " (" + text.Length + " digits)";
        }

        private void Show()
        {
            var a = Attack;
            var panel = new StackPanel { Margin = new Thickness(10) };
            Action<string, bool> line = (text, bold) => panel.Children.Add(new TextBlock { Text = text, FontSize = bold ? 14 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, bold ? 10 : 2, 0, 2) });
            line(Properties.Resources.Title, true);
            line(Properties.Resources.Intro, false);
            line(Properties.Resources.StepAttacker, true);
            line(string.Format(Properties.Resources.AttackerKey, Short(a.AttackerN), a.AttackerE), false);
            line(Properties.Resources.StepVictim, true);
            line(string.Format(Properties.Resources.VictimSeed, Short(a.Seed)), false);
            line(string.Format(Properties.Resources.VictimP, Short(a.P)), false);
            line(string.Format(Properties.Resources.VictimEncrypted, Short(a.Encrypted)), false);
            line(string.Format(Properties.Resources.VictimQ, Short(a.Q)), false);
            line(string.Format(Properties.Resources.VictimN, Short(a.N), a.N.GetBitLength()), false);
            line(Properties.Resources.StepAttack, true);
            line(string.Format(Properties.Resources.AttackRead, Short(a.N >> SetupAttack.ShiftBits)), false);
            if (a.Recovered)
            {
                line(string.Format(Properties.Resources.AttackSeed, Short(a.RecoveredSeed)), false);
                line(string.Format(Properties.Resources.AttackP, Short(a.RecoveredP), Short(a.RecoveredQ)), false);
                line(string.Format(Properties.Resources.AttackFactors, a.RecoveredP * a.RecoveredQ == a.N), true);
            }
            else { line(Properties.Resources.AttackFailed, true); }
            line(Properties.Resources.Conclusion, false);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

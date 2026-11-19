using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Numerics;

namespace MultipartyKeyExchange
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("MultipartyKeyExchange.Properties.Resources", "PluginCaption", "PluginTooltip", "MultipartyKeyExchange/DetailedDescription/doc.xml", "MultipartyKeyExchange/icon.png")]
    [ComponentCategory(ComponentCategory.Protocols)]
    public class MultipartyKeyExchange : ICrypComponent
    {
        private MultipartyKeyExchangeSettings _settings = new MultipartyKeyExchangeSettings();

        [PropertyInfo(Direction.InputData, "ParametersCaption", "ParametersTooltip", true)]
        public string Parameters { get; set; }

        [PropertyInfo(Direction.OutputData, "KeyCaption", "KeyTooltip", false)]
        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged("Key"); }
        }
        private string _key;

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
            set => _settings = (MultipartyKeyExchangeSettings)value;
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
                var numbers = Parameters.Split(new[] { ' ', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(BigInteger.Parse).ToList();
                if (numbers.Count < 5) { throw new ArgumentException("The parameters are p g r1 r2 r3 [r4 ...]: prime, generator and the secret numbers of at least three parties."); }
                var result = BurmesterDesmedt.Run(numbers[0], numbers[1], numbers.Skip(2).ToList());
                Key = result.Parties[0].Key.ToString();
                var report = new StringBuilder();
                for (int i = 0; i < result.Parties.Count; i++)
                {
                    var party = result.Parties[i];
                    report.AppendLine("Party " + (i + 1) + ": secret " + party.Secret + ", z = g^r = " + party.Z + ", X = (z_next / z_previous)^r = " + party.X + ", key K = " + party.Key);
                }
                report.AppendLine(result.Agree ? string.Format(Properties.Resources.Agree, result.Expected) : Properties.Resources.Disagree);
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
            Parameters = Key = Report = null;
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
    /// The conference key distribution of Burmester and Desmedt (1994) for n parties in a ring: every party i publishes z_i = g^r_i and then X_i = (z_(i+1) / z_(i-1))^r_i.
    /// Party i computes K = z_(i-1)^(n r_i) * X_i^(n-1) * X_(i+1)^(n-2) * ... * X_(i-2) mod p, and all parties get g^(r_1 r_2 + r_2 r_3 + ... + r_n r_1).
    /// </summary>
    public static class BurmesterDesmedt
    {
        public sealed class Party { public BigInteger Secret, Z, X, Key; }

        public sealed class Result { public BigInteger P, G, Expected; public List<Party> Parties = new List<Party>(); public bool Agree; }

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

        public static Result Run(BigInteger p, BigInteger g, IList<BigInteger> secrets)
        {
            if (secrets.Count < 3) { throw new ArgumentException("At least three parties are needed."); }
            if (!IsPrime(p)) { throw new ArgumentException("p must be a prime."); }
            if (g < 2 || g > p - 2) { throw new ArgumentException("g must be between 2 and p - 2."); }
            if (secrets.Any(r => r < 1 || r > p - 2)) { throw new ArgumentException("The secret numbers must be between 1 and p - 2."); }
            int n = secrets.Count;
            var result = new Result { P = p, G = g };
            Func<int, int> at = i => ((i % n) + n) % n;
            foreach (var r in secrets) { result.Parties.Add(new Party { Secret = r, Z = BigInteger.ModPow(g, r, p) }); }
            Func<BigInteger, BigInteger> inverse = a => BigInteger.ModPow(a, p - 2, p);
            for (int i = 0; i < n; i++)
            {
                BigInteger ratio = result.Parties[at(i + 1)].Z * inverse(result.Parties[at(i - 1)].Z) % p;
                result.Parties[i].X = BigInteger.ModPow(ratio, result.Parties[i].Secret, p);
            }
            for (int i = 0; i < n; i++)
            {
                BigInteger key = BigInteger.ModPow(result.Parties[at(i - 1)].Z, n * result.Parties[i].Secret, p);
                for (int j = 0; j < n - 1; j++) { key = key * BigInteger.ModPow(result.Parties[at(i + j)].X, n - 1 - j, p) % p; }
                result.Parties[i].Key = key;
            }
            BigInteger exponent = 0;
            for (int i = 0; i < n; i++) { exponent += secrets[i] * secrets[at(i + 1)]; }
            result.Expected = BigInteger.ModPow(g, exponent, p);
            result.Agree = result.Parties.All(party => party.Key == result.Expected);
            return result;
        }
    }

}

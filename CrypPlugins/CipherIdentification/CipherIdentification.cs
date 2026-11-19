using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace CipherIdentification
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("CipherIdentification.Properties.Resources", "PluginCaption", "PluginTooltip", "CipherIdentification/DetailedDescription/doc.xml", "CipherIdentification/icon.png")]
    [ComponentCategory(ComponentCategory.CryptanalysisGeneric)]
    public class CipherIdentification : ICrypComponent
    {
        private CipherIdentificationSettings _settings = new CipherIdentificationSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.OutputData, "GuessCaption", "GuessTooltip", false)]
        public string Guess
        {
            get => _guess;
            set { _guess = value; OnPropertyChanged("Guess"); }
        }
        private string _guess;

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
            set => _settings = (CipherIdentificationSettings)value;
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
                var a = CipherIdentifier.Analyse(Input);
                Guess = a.Guess;
                var report = new StringBuilder();
                report.AppendLine(string.Format(Properties.Resources.Basic, a.Length, a.Distinct, a.Alphabet, a.Entropy.ToString("F2")));
                if (a.Alphabet == "letters") { report.AppendLine(string.Format(Properties.Resources.Statistics, a.IndexOfCoincidence.ToString("F4"), a.ChiSquare.ToString("F1"), a.BestPeriod > 1 ? a.BestPeriod + " (" + a.BestPeriodIc.ToString("F4") + ")" : "-")); }
                report.AppendLine(string.Format(Properties.Resources.GuessLine, a.Guess));
                foreach (string candidate in a.Candidates) { report.AppendLine("- " + candidate); }
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
            Input = Guess = Report = null;
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


    public static class CipherIdentifier
    {
        private static readonly double[] English = { 8.167, 1.492, 2.782, 4.253, 12.702, 2.228, 2.015, 6.094, 6.966, 0.153, 0.772, 4.025, 2.406, 6.749, 7.507, 1.929, 0.095, 5.987, 6.327, 9.056, 2.758, 0.978, 2.360, 0.150, 1.974, 0.074 };

        public sealed class Analysis
        {
            public int Length, Distinct, BestPeriod;
            public double IndexOfCoincidence, Entropy, ChiSquare, BestPeriodIc;
            public string Alphabet, Guess;
            public List<string> Candidates = new List<string>();
        }

        public static double Ic(string letters)
        {
            if (letters.Length < 2) { return 0; }
            var counts = new int[26];
            foreach (char c in letters) { counts[c - 'A']++; }
            return counts.Sum(n => (double)n * (n - 1)) / ((double)letters.Length * (letters.Length - 1));
        }

        /// <summary>The average index of coincidence of the columns when the text is written in rows of the given length; for a Vigenere cipher with this key length it is close to that of the language.</summary>
        public static double PeriodIc(string letters, int period) => Enumerable.Range(0, period).Average(c => Ic(new string(letters.Where((x, i) => i % period == c).ToArray())));

        public static Analysis Analyse(string text)
        {
            string trimmed = text.Trim();
            var a = new Analysis { Length = trimmed.Length };
            string compact = new string(trimmed.Where(c => !char.IsWhiteSpace(c)).ToArray());
            a.Distinct = compact.Distinct().Count();
            var counts = compact.GroupBy(c => c).Select(g => (double)g.Count()).ToList();
            a.Entropy = -counts.Sum(n => n / compact.Length * Math.Log(n / compact.Length, 2));
            if (compact.Length == 0) { a.Alphabet = "empty"; a.Guess = "nothing to analyse"; return a; }

            bool binary = compact.All(c => c == '0' || c == '1'), digits = compact.All(char.IsDigit), hex = compact.All(Uri.IsHexDigit) && compact.Length % 2 == 0;
            bool base64 = compact.Length % 4 == 0 && compact.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=') && compact.Any(char.IsLower) && compact.Any(char.IsUpper) && compact.Any(char.IsDigit);
            string letters = new string(compact.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());
            bool onlyLetters = letters.Length == compact.Length;

            if (binary) { a.Alphabet = "binary digits"; a.Guess = "a bit string (a binary encoding or the output of a modern cipher or a stream cipher)"; a.Candidates.Add("bit string: split it into 8-bit bytes, look at the bytes as ASCII or as data of a modern cipher"); return a; }
            if (digits) { a.Alphabet = "decimal digits"; a.Guess = "a number code (a book code, a Polybius or straddling checkerboard cipher, a numeric encoding)"; a.Candidates.Add("Polybius square or checkerboard: pairs of digits"); a.Candidates.Add("a code with numbers: a book cipher or telephone keypad code"); return a; }
            if (hex) { a.Alphabet = "hexadecimal digits"; a.Guess = "hexadecimal data: the output of a modern cipher (AES, DES) or a hash value"; a.Candidates.Add("a hash value if the length is 32, 40, 64 or 128 digits"); a.Candidates.Add("block cipher output if the number of bytes is a multiple of 8 or 16"); return a; }
            if (base64) { a.Alphabet = "Base64"; a.Guess = "Base64 encoded data (decode it first: the content is a modern cipher or a file)"; return a; }
            if (!onlyLetters && letters.Length < compact.Length / 2) { a.Alphabet = "mixed characters"; a.Guess = "a symbol cipher or an encoding with special characters"; return a; }

            a.Alphabet = "letters";
            a.IndexOfCoincidence = Ic(letters);
            var frequencies = new int[26];
            foreach (char c in letters) { frequencies[c - 'A']++; }
            a.ChiSquare = Enumerable.Range(0, 26).Sum(i => { double expected = English[i] / 100 * letters.Length; return (frequencies[i] - expected) * (frequencies[i] - expected) / expected; }) / Math.Max(1, letters.Length) * 100;
            double best = 0; int bestPeriod = 1;
            for (int period = 2; period <= 16 && letters.Length >= 40 * period / 4; period++) { double ic = PeriodIc(letters, period); if (ic > best + 0.001 && ic > 0.055) { best = ic; bestPeriod = period; break; } }
            a.BestPeriod = bestPeriod; a.BestPeriodIc = best;

            if (a.IndexOfCoincidence >= 0.060)
            {
                if (a.ChiSquare < 25) { a.Guess = "plaintext or a transposition cipher: the letter frequencies are those of the language"; a.Candidates.Add("a transposition cipher (columnar, rail fence, scytale): the letters are only rearranged"); a.Candidates.Add("plaintext"); }
                else { a.Guess = "a monoalphabetic substitution (Caesar, Atbash, affine, simple substitution): the frequencies are those of a language, but the letters are exchanged"; a.Candidates.Add("Caesar or shift cipher (try all 26 shifts)"); a.Candidates.Add("simple substitution: analyse the letter frequencies"); }
            }
            else if (bestPeriod > 1) { a.Guess = "a polyalphabetic substitution with a key of length " + bestPeriod + " (Vigenere or similar): the columns are monoalphabetic"; a.Candidates.Add("Vigenere cipher with a key of " + bestPeriod + " letters (or a multiple of it)"); }
            else if (a.IndexOfCoincidence >= 0.045) { a.Guess = "a polyalphabetic substitution with a short key, or a digraph cipher (Playfair, Hill)"; a.Candidates.Add("Vigenere cipher: look for the key length with the Kasiski test"); a.Candidates.Add("Playfair or Hill cipher: letters are encrypted in pairs"); }
            else { a.Guess = "a polyalphabetic cipher with a long key, a one-time pad, or random letters: no statistics of a language are left"; a.Candidates.Add("Vigenere with a long key or an autokey cipher"); a.Candidates.Add("a modern cipher with a letter encoding, or a one-time pad"); }
            return a;
        }
    }

}

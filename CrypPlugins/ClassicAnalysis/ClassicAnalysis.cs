using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ClassicAnalysis
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ClassicAnalysis.Properties.Resources", "PluginCaption", "PluginTooltip", "ClassicAnalysis/DetailedDescription/doc.xml", "ClassicAnalysis/icon.png")]
    [ComponentCategory(ComponentCategory.CryptanalysisSpecific)]
    public class ClassicAnalysis : ICrypComponent
    {
        private ClassicAnalysisSettings _settings = new ClassicAnalysisSettings();

        [PropertyInfo(Direction.InputData, "PlaintextCaption", "PlaintextTooltip", false)]
        public string Plaintext { get; set; }

        [PropertyInfo(Direction.InputData, "CiphertextCaption", "CiphertextTooltip", true)]
        public string Ciphertext { get; set; }

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
            set => _settings = (ClassicAnalysisSettings)value;
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
                if (_settings.Mode == 0)
                {
                    int[,] key = ClassicAnalyses.HillKey(Plaintext ?? "", Ciphertext, _settings.Size);
                    Key = ClassicAnalyses.MatrixText(key);
                    int[,] inverse = ClassicAnalyses.Inverse(key);
                    Report = string.Format(Properties.Resources.HillReport, Key, inverse == null ? "-" : ClassicAnalyses.MatrixText(inverse));
                }
                else
                {
                    var candidates = ClassicAnalyses.FleissnerAnalysis(Ciphertext, _settings.Size, 5);
                    Key = candidates[0].Text;
                    Report = string.Join(Environment.NewLine, candidates.Select(c => c.Score + ": " + c.Text));
                }
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
            Plaintext = Ciphertext = Key = Report = null;
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


    public static class ClassicAnalyses
    {
        public static string Letters(string text) => new string(text.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());

        // ---- Hill cipher: ciphertext block = K * plaintext block (column vectors, mod 26)

        public static int[,] Multiply(int[,] a, int[,] b)
        {
            int n = a.GetLength(0), m = b.GetLength(1), k = a.GetLength(1);
            var result = new int[n, m];
            for (int i = 0; i < n; i++) { for (int j = 0; j < m; j++) { int sum = 0; for (int t = 0; t < k; t++) { sum += a[i, t] * b[t, j]; } result[i, j] = ((sum % 26) + 26) % 26; } }
            return result;
        }

        private static int Determinant(int[,] m)
        {
            int n = m.GetLength(0);
            if (n == 1) { return m[0, 0]; }
            int det = 0;
            for (int c = 0; c < n; c++) { det += (c % 2 == 0 ? 1 : -1) * m[0, c] * Determinant(Minor(m, 0, c)); }
            return det;
        }

        private static int[,] Minor(int[,] m, int row, int column)
        {
            int n = m.GetLength(0);
            var result = new int[n - 1, n - 1];
            for (int i = 0, ri = 0; i < n; i++) { if (i == row) { continue; } for (int j = 0, rj = 0; j < n; j++) { if (j == column) { continue; } result[ri, rj++] = m[i, j]; } ri++; }
            return result;
        }

        /// <summary>The inverse matrix modulo 26, or null if the determinant is not coprime to 26.</summary>
        public static int[,] Inverse(int[,] m)
        {
            int n = m.GetLength(0);
            int det = ((Determinant(m) % 26) + 26) % 26, inverse = -1;
            for (int d = 1; d < 26; d++) { if (det * d % 26 == 1) { inverse = d; } }
            if (inverse < 0) { return null; }
            var result = new int[n, n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) { int cofactor = ((i + j) % 2 == 0 ? 1 : -1) * (n == 1 ? 1 : Determinant(Minor(m, j, i))); result[i, j] = (((cofactor * inverse) % 26) + 26) % 26; } }
            return result;
        }

        public static string HillEncrypt(string text, int[,] key)
        {
            int n = key.GetLength(0);
            text = Letters(text);
            var result = new StringBuilder();
            for (int b = 0; b + n <= text.Length; b += n)
            {
                var block = new int[n, 1];
                for (int i = 0; i < n; i++) { block[i, 0] = text[b + i] - 'A'; }
                var c = Multiply(key, block);
                for (int i = 0; i < n; i++) { result.Append((char)('A' + c[i, 0])); }
            }
            return result.ToString();
        }

        /// <summary>The known-plaintext attack: n blocks of plaintext with an invertible matrix and their ciphertext give the key K = C * P^-1; it is checked on all blocks.</summary>
        public static int[,] HillKey(string plaintext, string ciphertext, int n)
        {
            plaintext = Letters(plaintext); ciphertext = Letters(ciphertext);
            int blocks = Math.Min(plaintext.Length, ciphertext.Length) / n;
            if (blocks < n) { throw new ArgumentException("At least " + n + " blocks of " + n + " letters are needed."); }
            Func<string, int, int[]> block = (s, b) => Enumerable.Range(0, n).Select(i => s[b * n + i] - 'A').ToArray();
            foreach (var choice in Choices(Math.Min(blocks, 12), n))
            {
                var p = new int[n, n]; var c = new int[n, n];
                for (int column = 0; column < n; column++)
                {
                    int[] pb = block(plaintext, choice[column]), cb = block(ciphertext, choice[column]);
                    for (int row = 0; row < n; row++) { p[row, column] = pb[row]; c[row, column] = cb[row]; }
                }
                var inverse = Inverse(p);
                if (inverse == null) { continue; }
                var key = Multiply(c, inverse);
                if (HillEncrypt(plaintext.Substring(0, blocks * n), key) == ciphertext.Substring(0, blocks * n)) { return key; }
            }
            throw new ArgumentException("No key fits: the plaintext blocks are not independent or the text is not a Hill ciphertext of this size.");
        }

        private static IEnumerable<int[]> Choices(int total, int n)
        {
            var current = new int[n];
            return Combine(current, 0, 0, total, n);
        }

        private static IEnumerable<int[]> Combine(int[] current, int position, int start, int total, int n)
        {
            if (position == n) { yield return (int[])current.Clone(); yield break; }
            for (int i = start; i < total; i++) { current[position] = i; foreach (var result in Combine(current, position + 1, i + 1, total, n)) { yield return result; } }
        }

        public static string MatrixText(int[,] m)
        {
            int n = m.GetLength(0);
            return string.Join("; ", Enumerable.Range(0, n).Select(i => string.Join(" ", Enumerable.Range(0, n).Select(j => m[i, j].ToString()))));
        }

        // ---- Fleissner grille (turning grille)

        /// <summary>The holes of the grille at a quarter turn (0 to 3), in reading order; choice[orbit] tells which of the four cells of an orbit is a hole.</summary>
        public static List<int> Holes(int n, int[] choice, int turn, int direction)
        {
            int half = n / 2;
            var holes = new List<int>();
            for (int r = 0; r < half; r++)
            {
                for (int c = 0; c < half; c++)
                {
                    int rotation = (((choice[r * half + c] + direction * turn) % 4) + 4) % 4;
                    int row = r, column = c;
                    for (int k = 0; k < rotation; k++) { int t = row; row = column; column = n - 1 - t; }
                    holes.Add(row * n + column);
                }
            }
            holes.Sort();
            return holes;
        }

        /// <summary>The plaintext is written into the holes (four quarter turns), the ciphertext is the grid read row by row; short texts are filled up with X.</summary>
        public static string FleissnerEncrypt(string text, int n, int[] choice, int direction = 1)
        {
            text = Letters(text);
            text = text.PadRight((text.Length + n * n - 1) / (n * n) * n * n, 'X');
            var result = new StringBuilder();
            for (int b = 0; b < text.Length; b += n * n)
            {
                var grid = new char[n * n];
                int index = b;
                for (int turn = 0; turn < 4; turn++) { foreach (int hole in Holes(n, choice, turn, direction)) { grid[hole] = text[index++]; } }
                result.Append(grid);
            }
            return result.ToString();
        }

        public static string FleissnerDecrypt(string cipher, int n, int[] choice, int direction = 1)
        {
            var result = new StringBuilder();
            for (int b = 0; b + n * n <= cipher.Length; b += n * n)
            {
                for (int turn = 0; turn < 4; turn++) { foreach (int hole in Holes(n, choice, turn, direction)) { result.Append(cipher[b + hole]); } }
            }
            return result.ToString();
        }

        private static readonly HashSet<string> Bigrams = new HashSet<string>("TH HE IN ER AN RE ON AT EN ND TI ES OR TE OF ED IS IT AL AR ST TO NT NG SE HA AS OU IO LE VE CO ME DE HI RI RO IC NE EA RA CE LI CH LL BE MA SI OM UR EI IE GE UN DI AU SC".Split(' '));

        private static readonly HashSet<string> Trigrams = new HashSet<string>("THE AND ING ENT ION HER FOR THA NTH INT ERE TIO VER ALL ETH TER ATI HAT ATE ERS HIS RES EST OUR ONE OUL ULD OFT STH ITH EAR TED NDE EIN ICH DER CHE SCH DIE UND NGE END GEN".Split(' '));

        /// <summary>The number of common English and German bigrams in the text, plus four times the number of common trigrams.</summary>
        public static int BigramScore(string text)
        {
            int score = 0;
            for (int i = 0; i + 1 < text.Length; i++) { if (Bigrams.Contains(text.Substring(i, 2))) { score++; } }
            for (int i = 0; i + 2 < text.Length; i++) { if (Trigrams.Contains(text.Substring(i, 3))) { score += 4; } }
            return score;
        }

        public sealed class Candidate { public int Score; public string Text; public int[] Choice; public int Direction; }

        /// <summary>Tries all 4^(n*n/4) grilles (in both turning directions) and returns the best candidates by bigram score.</summary>
        public static List<Candidate> FleissnerAnalysis(string cipher, int n, int top)
        {
            cipher = Letters(cipher);
            if (n % 2 != 0 || n < 2 || n > 6) { throw new ArgumentException("The size of the grille must be 2, 4 or 6."); }
            if (cipher.Length % (n * n) != 0) { throw new ArgumentException("The length of the ciphertext must be a multiple of " + n * n + "."); }
            int orbits = n * n / 4;
            long total = 1L << (2 * orbits);
            var best = new List<Candidate>();
            for (int direction = -1; direction <= 1; direction += 2)
            {
                var choice = new int[orbits];
                for (long number = 0; number < total; number++)
                {
                    for (int o = 0; o < orbits; o++) { choice[o] = (int)(number >> (2 * o) & 3); }
                    string plain = FleissnerDecrypt(cipher, n, choice, direction);
                    int score = BigramScore(plain);
                    if (best.Count < top || score > best[best.Count - 1].Score)
                    {
                        best.Add(new Candidate { Score = score, Text = plain, Choice = (int[])choice.Clone(), Direction = direction });
                        best = best.OrderByDescending(c => c.Score).Take(top).ToList();
                    }
                }
            }
            return best;
        }
    }

}

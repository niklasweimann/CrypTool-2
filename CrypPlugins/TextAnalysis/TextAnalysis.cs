using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace TextAnalysis
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("TextAnalysis.Properties.Resources", "PluginCaption", "PluginTooltip", "TextAnalysis/DetailedDescription/doc.xml", "TextAnalysis/icon.png")]
    [ComponentCategory(ComponentCategory.CryptanalysisGeneric)]
    public class TextAnalysis : ICrypComponent
    {
        private TextAnalysisSettings _settings = new TextAnalysisSettings();

        [PropertyInfo(Direction.InputData, "InputACaption", "InputATooltip", true)]
        public string InputA { get; set; }

        [PropertyInfo(Direction.InputData, "InputBCaption", "InputBTooltip", false)]
        public string InputB { get; set; }

        [PropertyInfo(Direction.OutputData, "ReportCaption", "ReportTooltip", false)]
        public string Report
        {
            get => _report;
            set { _report = value; OnPropertyChanged("Report"); }
        }
        private string _report;

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged("Result"); }
        }
        private string _result;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (TextAnalysisSettings)value;
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
                switch (_settings.Mode)
                {
                    case 0:
                        var grams = TextStatistics.NGrams(InputA, _settings.N, _settings.Top);
                        Report = string.Join(Environment.NewLine, grams.Select(g => g.Text + "  " + g.Count + "  " + g.Percent.ToString("F2") + " %"));
                        Result = grams.Count == 0 ? "" : grams[0].Text;
                        break;
                    case 1:
                        if (string.IsNullOrEmpty(InputB)) { throw new ArgumentException("The second text is needed."); }
                        double distance = TextStatistics.Ncd(InputA, InputB);
                        Result = distance.ToString("F4");
                        Report = string.Format(Properties.Resources.NcdReport, Result);
                        break;
                    default:
                        var shifts = TextStatistics.ByteAddition(TextStatistics.FromHex(InputA));
                        Result = shifts[0].Value.ToString();
                        Report = string.Join(Environment.NewLine, shifts.Take(3).Select(s => s.Value + ": " + new string(s.Plaintext.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray())));
                        break;
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
            InputA = InputB = Report = Result = null;
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


    public static class TextStatistics
    {
        public sealed class Gram { public string Text; public int Count; public double Percent; }

        /// <summary>The n-grams of the letters of the text (upper case, everything else removed), most frequent first.</summary>
        public static List<Gram> NGrams(string text, int n, int top)
        {
            string letters = new string(text.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
            var counts = new Dictionary<string, int>();
            for (int i = 0; i + n <= letters.Length; i++)
            {
                string gram = letters.Substring(i, n);
                counts[gram] = counts.TryGetValue(gram, out int c) ? c + 1 : 1;
            }
            int total = Math.Max(1, letters.Length - n + 1);
            return counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(top).Select(p => new Gram { Text = p.Key, Count = p.Value, Percent = 100.0 * p.Value / total }).ToList();
        }

        /// <summary>The size of the data after compression (Deflate): the stand-in for the Kolmogorov complexity.</summary>
        public static int Compressed(byte[] data)
        {
            using (var buffer = new System.IO.MemoryStream())
            {
                using (var deflate = new System.IO.Compression.DeflateStream(buffer, System.IO.Compression.CompressionLevel.Optimal, true)) { deflate.Write(data, 0, data.Length); }
                return (int)buffer.Length;
            }
        }

        /// <summary>The normalized compression distance of Cilibrasi and Vitanyi: (C(xy) - min(C(x), C(y))) / max(C(x), C(y)); 0 for equal, about 1 for unrelated data.</summary>
        public static double Ncd(string x, string y)
        {
            byte[] a = Encoding.UTF8.GetBytes(x), b = Encoding.UTF8.GetBytes(y);
            int cx = Compressed(a), cy = Compressed(b), cxy = Compressed(a.Concat(b).ToArray());
            return (double)(cxy - Math.Min(cx, cy)) / Math.Max(cx, cy);
        }

        private static readonly double[] LetterFrequency = { 8.2, 1.5, 2.8, 4.3, 12.7, 2.2, 2.0, 6.1, 7.0, 0.15, 0.77, 4.0, 2.4, 6.7, 7.5, 1.9, 0.095, 6.0, 6.3, 9.1, 2.8, 0.98, 2.4, 0.15, 2.0, 0.074 };

        /// <summary>The log-likelihood of the bytes as English text (letters by their frequency, space, rare other characters, control characters very unlikely).</summary>
        public static double Score(byte[] text)
        {
            double score = 0;
            foreach (byte b in text)
            {
                if (b == ' ') { score += Math.Log(18); }
                else if (b >= 'a' && b <= 'z') { score += Math.Log(LetterFrequency[b - 'a']); }
                else if (b >= 'A' && b <= 'Z') { score += Math.Log(LetterFrequency[b - 'A'] / 4); }
                else if (b == '\n' || b == '\r' || b == '\t' || (b >= 33 && b < 127)) { score += Math.Log(0.5); }
                else { score += Math.Log(0.0001); }
            }
            return score;
        }

        public sealed class Shift { public int Value; public double Score; public byte[] Plaintext; }

        /// <summary>Subtracts every value from 0 to 255 from all bytes (the cipher adds a constant byte) and ranks the results by their likelihood as text.</summary>
        public static List<Shift> ByteAddition(byte[] ciphertext)
        {
            return Enumerable.Range(0, 256).Select(s => { var p = ciphertext.Select(c => (byte)(c - s)).ToArray(); return new Shift { Value = s, Plaintext = p, Score = Score(p) }; }).OrderByDescending(s => s.Score).ToList();
        }

        public static byte[] FromHex(string hex)
        {
            hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (hex.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }
    }

}

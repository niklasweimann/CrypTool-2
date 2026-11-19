using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ClassicVariants
{
    /// <summary>Four classic ciphers: Porta, the rail fence cipher (with the redefence variant), the Kamasutra cipher and the two-square cipher (Doppelkasten).</summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ClassicVariants.Properties.Resources", "PluginCaption", "PluginTooltip", "ClassicVariants/DetailedDescription/doc.xml", "ClassicVariants/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersClassic)]
    public class ClassicVariants : ICrypComponent
    {
        private ClassicVariantsSettings _settings = new ClassicVariantsSettings();

        [PropertyInfo(Direction.InputData, "InputTextCaption", "InputTextTooltip", true)]
        public string InputText { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", false)]
        public string Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (ClassicVariantsSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (InputText == null)
            {
                GuiLogMessage("There is no input text.", NotificationLevel.Error);
                return;
            }

            bool decrypt = _settings.Action == 1;
            try
            {
                switch (_settings.Cipher)
                {
                    case 1: OutputText = Ciphers.RailFence(InputText, _settings.Rails, _settings.Offset, Key, decrypt); break;
                    case 2: OutputText = Ciphers.Kamasutra(InputText, Key); break;
                    case 3: OutputText = Ciphers.TwoSquare(InputText, Key, decrypt); break;
                    default: OutputText = Ciphers.Porta(InputText, Key); break;
                }
                OnPropertyChanged("OutputText");
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
            InputText = Key = OutputText = null;
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

    /// <summary>The ciphers, free of plugin infrastructure; invalid keys throw ArgumentException.</summary>
    public static class Ciphers
    {
        private static string Letters(string text) => new string(text.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());

        /// <summary>
        /// Porta: the key letters come in pairs (A/B, C/D, ...) that select one of 13 reciprocal alphabets. Letters A to M are replaced by
        /// letters from N to Z and the other way round; the shift depends on the key letter. The same operation encrypts and decrypts.
        /// Letters other than A to Z are kept and do not use up a key letter; the result is in upper case.
        /// </summary>
        public static string Porta(string text, string key)
        {
            string k = Letters(key ?? "");
            if (k.Length == 0) { throw new ArgumentException("The key must contain at least one letter."); }
            var result = new StringBuilder();
            int position = 0;
            foreach (char c in text.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') { result.Append(c); continue; }
                int shift = (k[position++ % k.Length] - 'A') / 2;
                if (c <= 'M') { result.Append((char)('N' + (c - 'A' + shift) % 13)); }
                else { result.Append((char)('A' + ((c - 'N' - shift) % 13 + 13) % 13)); }
            }
            return result.ToString();
        }

        /// <summary>
        /// Rail fence (zigzag) cipher: the text is written on a zigzag over the given number of rails, starting at the given step of the zigzag
        /// (offset), and read rail by rail. With a key of one letter per rail (redefence) the rails are read in the alphabetical order of the key
        /// letters instead of from top to bottom. All characters are used, spaces included.
        /// </summary>
        public static string RailFence(string text, int rails, int offset, string key, bool decrypt)
        {
            if (rails < 2) { throw new ArgumentException("There must be at least 2 rails."); }
            int[] order = Enumerable.Range(0, rails).ToArray();
            if (!string.IsNullOrEmpty(key))
            {
                string k = key.ToUpperInvariant();
                if (k.Length != rails) { throw new ArgumentException("The key of the redefence cipher needs one letter for each of the " + rails + " rails."); }
                order = Enumerable.Range(0, rails).OrderBy(i => k[i]).ThenBy(i => i).ToArray();
            }

            int period = 2 * (rails - 1);
            int[] railOf = Enumerable.Range(0, text.Length).Select(i => { int s = (i + offset) % period; return s < rails ? s : period - s; }).ToArray();
            var read = new List<int>();   // positions in the order in which the rails are read
            foreach (int rail in order) { for (int i = 0; i < text.Length; i++) { if (railOf[i] == rail) { read.Add(i); } } }

            var result = new char[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                if (decrypt) { result[read[i]] = text[i]; } else { result[i] = text[read[i]]; }
            }
            return new string(result);
        }

        /// <summary>
        /// Kamasutra: the key is a permutation of the alphabet (or part of it), cut into pairs of consecutive letters; each letter is replaced by
        /// the other letter of its pair. The cipher is reciprocal. Letters that are not in the key and other characters are kept.
        /// </summary>
        public static string Kamasutra(string text, string key)
        {
            string k = Letters(key ?? "");
            if (k.Length == 0 || k.Length % 2 != 0) { throw new ArgumentException("The key must have an even number of letters (pairs)."); }
            if (k.Distinct().Count() != k.Length) { throw new ArgumentException("A letter occurs twice in the key."); }
            var partner = new Dictionary<char, char>();
            for (int i = 0; i < k.Length; i += 2) { partner[k[i]] = k[i + 1]; partner[k[i + 1]] = k[i]; }
            return new string(text.ToUpperInvariant().Select(c => partner.TryGetValue(c, out char p) ? p : c).ToArray());
        }

        /// <summary>The 5x5 square of a keyword: the letters of the keyword without repeats, then the rest of the alphabet; J is I.</summary>
        public static string Square(string keyword)
        {
            string k = Letters(keyword).Replace('J', 'I');
            var seen = new List<char>();
            foreach (char c in k + "ABCDEFGHIKLMNOPQRSTUVWXYZ") { if (!seen.Contains(c)) { seen.Add(c); } }
            return new string(seen.ToArray());
        }

        /// <summary>
        /// Two-square cipher (Doppelkasten), horizontal: two 5x5 squares side by side, made of two keywords (separated by a space or a comma).
        /// A pair of plain letters (p from the left square, q from the right one) forms a rectangle; the cipher letters are the other two
        /// corners, the one in the right square first. Only letters are used (J is I, other characters are dropped); an odd text gets an X.
        /// </summary>
        public static string TwoSquare(string text, string keys, bool decrypt)
        {
            string[] parts = (keys ?? "").Split(new[] { ' ', ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) { throw new ArgumentException("The key needs two keywords, separated by a space or a comma."); }
            string left = Square(parts[0]), right = Square(parts[1]);
            string letters = Letters(text).Replace('J', 'I');
            if (letters.Length % 2 == 1) { letters += "X"; }
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i += 2)
            {
                if (!decrypt)
                {
                    int p = left.IndexOf(letters[i]), q = right.IndexOf(letters[i + 1]);
                    result.Append(right[p / 5 * 5 + q % 5]).Append(left[q / 5 * 5 + p % 5]);
                }
                else
                {
                    int a = right.IndexOf(letters[i]), b = left.IndexOf(letters[i + 1]);   // a = (row of p, column of q), b = (row of q, column of p)
                    result.Append(left[a / 5 * 5 + b % 5]).Append(right[b / 5 * 5 + a % 5]);
                }
            }
            return result.ToString();
        }
    }
}

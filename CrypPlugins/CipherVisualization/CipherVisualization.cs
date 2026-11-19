using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CipherVisualization
{
    /// <summary>
    /// Shows the inner calculation of three classic ciphers while it encrypts or decrypts: the shifted alphabet of the Caesar cipher, the
    /// Vigenere square with the cells that are used, and the Polybius square and the additions of the Nihilist cipher.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("CipherVisualization.Properties.Resources", "PluginCaption", "PluginTooltip", "CipherVisualization/DetailedDescription/doc.xml", "CipherVisualization/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersClassic)]
    public class CipherVisualization : ICrypComponent
    {
        private CipherVisualizationSettings _settings = new CipherVisualizationSettings();
        private CipherVisualizationPresentation _presentation;

        [PropertyInfo(Direction.InputData, "InputTextCaption", "InputTextTooltip", true)]
        public string InputText { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", true)]
        public string Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (CipherVisualizationSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new CipherVisualizationPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (InputText == null || Key == null)
            {
                GuiLogMessage("Both the text and the key are needed.", NotificationLevel.Error);
                return;
            }

            bool decrypt = _settings.Action == 1;
            try
            {
                switch (_settings.Cipher)
                {
                    case 1:
                        OutputText = Calculations.Vigenere(InputText, Key, decrypt, out List<Calculations.Step> vigenereSteps);
                        ((CipherVisualizationPresentation)Presentation).ShowVigenere(InputText, Key, OutputText, decrypt, vigenereSteps);
                        break;
                    case 2:
                        OutputText = Calculations.Nihilist(InputText, Key, decrypt, out Calculations.NihilistTrace trace);
                        ((CipherVisualizationPresentation)Presentation).ShowNihilist(trace, decrypt);
                        break;
                    default:
                        int shift = Calculations.CaesarShift(Key);
                        OutputText = Calculations.Caesar(InputText, shift, decrypt);
                        ((CipherVisualizationPresentation)Presentation).ShowCaesar(InputText, OutputText, shift, decrypt);
                        break;
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
            InputText = Key = OutputText = null;
        }

        public void PostExecution()
        {
            InputText = Key = OutputText = null;
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

    /// <summary>The calculations of the ciphers, free of plugin infrastructure; invalid keys throw ArgumentException.</summary>
    public static class Calculations
    {
        /// <summary>One letter of the Vigenere cipher: plaintext letter, key letter and ciphertext letter (all 0 to 25).</summary>
        public sealed class Step
        {
            public char Plain, Key, Cipher;
        }

        /// <summary>The Nihilist cipher letter by letter: the coordinates in the square, those of the key letter and their sum.</summary>
        public sealed class NihilistTrace
        {
            public string Square;
            public string KeyWord, AdditiveKey;
            public List<char> Letters = new List<char>();
            public List<char> KeyLetters = new List<char>();
            public List<int> Numbers = new List<int>();
            public List<int> KeyNumbers = new List<int>();
            public List<int> Sums = new List<int>();
            public bool Decrypt;
        }

        private static string Letters(string text) => new string(text.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());

        /// <summary>The shift of the Caesar cipher: a number, or a letter (A = 0, B = 1, ...).</summary>
        public static int CaesarShift(string key)
        {
            key = key.Trim();
            if (int.TryParse(key, out int number)) { return ((number % 26) + 26) % 26; }
            string letters = Letters(key);
            if (letters.Length == 0) { throw new ArgumentException("The key must be a number or a letter."); }
            return letters[0] - 'A';
        }

        public static string Caesar(string text, int shift, bool decrypt)
        {
            int s = decrypt ? 26 - shift : shift;
            return new string(text.ToUpperInvariant().Select(c => c >= 'A' && c <= 'Z' ? (char)('A' + (c - 'A' + s) % 26) : c).ToArray());
        }

        /// <summary>Vigenere cipher; other characters than A to Z are kept and do not use up a key letter.</summary>
        public static string Vigenere(string text, string key, bool decrypt, out List<Step> steps)
        {
            string k = Letters(key);
            if (k.Length == 0) { throw new ArgumentException("The key must contain at least one letter."); }
            steps = new List<Step>();
            var result = new StringBuilder();
            int position = 0;
            foreach (char c in text.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') { result.Append(c); continue; }
                char keyLetter = k[position++ % k.Length];
                int shift = keyLetter - 'A';
                char other = (char)('A' + ((c - 'A') + (decrypt ? 26 - shift : shift)) % 26);
                steps.Add(decrypt ? new Step { Plain = other, Key = keyLetter, Cipher = c } : new Step { Plain = c, Key = keyLetter, Cipher = other });
                result.Append(other);
            }
            return result.ToString();
        }

        /// <summary>The 5x5 square of a keyword (J is I): the letters of the keyword without repeats, then the rest of the alphabet.</summary>
        public static string Square(string keyword)
        {
            var seen = new List<char>();
            foreach (char c in Letters(keyword).Replace('J', 'I') + "ABCDEFGHIKLMNOPQRSTUVWXYZ") { if (!seen.Contains(c)) { seen.Add(c); } }
            return new string(seen.ToArray());
        }

        /// <summary>
        /// Nihilist cipher with a Polybius square: the key has two words, the first makes the square, the second is the additive key. Every
        /// letter becomes the number of its row and column (11 to 55), the number of the key letter is added. The result of encryption is
        /// the list of the sums separated by spaces; decryption takes such a list.
        /// </summary>
        public static string Nihilist(string text, string key, bool decrypt, out NihilistTrace trace)
        {
            string[] words = (key ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 2 || Letters(words[1]).Length == 0) { throw new ArgumentException("The key needs two words: the keyword of the square and the additive key, separated by a space."); }
            var t = new NihilistTrace { Square = Square(words[0]), KeyWord = words[0].ToUpperInvariant(), AdditiveKey = Letters(words[1]), Decrypt = decrypt };
            trace = t;
            string additive = t.AdditiveKey.Replace('J', 'I');
            Func<char, int> number = c => { int i = t.Square.IndexOf(c); return (i / 5 + 1) * 10 + i % 5 + 1; };

            var result = new StringBuilder();
            if (!decrypt)
            {
                foreach (char c in Letters(text).Replace('J', 'I'))
                {
                    char keyLetter = additive[trace.Letters.Count % additive.Length];
                    trace.Letters.Add(c);
                    trace.KeyLetters.Add(keyLetter);
                    trace.Numbers.Add(number(c));
                    trace.KeyNumbers.Add(number(keyLetter));
                    trace.Sums.Add(trace.Numbers[trace.Numbers.Count - 1] + trace.KeyNumbers[trace.KeyNumbers.Count - 1]);
                }
                return string.Join(" ", trace.Sums);
            }

            foreach (string token in text.Split(new[] { ' ', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, out int sum)) { throw new ArgumentException("The ciphertext must be numbers separated by spaces, found: " + token); }
                char keyLetter = additive[trace.Sums.Count % additive.Length];
                int plain = sum - number(keyLetter);
                int row = plain / 10, column = plain % 10;
                if (row < 1 || row > 5 || column < 1 || column > 5) { throw new ArgumentException("The number " + sum + " does not fit the key: the difference is not a row and a column from 1 to 5."); }
                char letter = trace.Square[(row - 1) * 5 + column - 1];
                trace.Sums.Add(sum);
                trace.KeyLetters.Add(keyLetter);
                trace.KeyNumbers.Add(number(keyLetter));
                trace.Numbers.Add(plain);
                trace.Letters.Add(letter);
                result.Append(letter);
            }
            return result.ToString();
        }
    }

    /// <summary>The picture of the calculation, built from plain WPF elements (grids, borders, text).</summary>
    public class CipherVisualizationPresentation : UserControl
    {
        private static readonly Brush Used = new SolidColorBrush(Color.FromRgb(255, 235, 130));
        private static readonly Brush Result = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush Header = new SolidColorBrush(Color.FromRgb(225, 232, 245));
        private static readonly Brush Plain = Brushes.White;

        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        public CipherVisualizationPresentation()
        {
            Content = _scroll;
            Background = Brushes.White;
        }

        private static TextBlock Text(string text, double size = 12, FontWeight? weight = null, bool mono = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return block;
        }

        private static Border Cell(string text, Brush background, double width = 22, double height = 22, bool bold = false)
        {
            return new Border
            {
                Width = width, Height = height, Background = background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5),
                Child = new TextBlock { Text = text, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
        }

        private static Grid MakeGrid(int rows, int columns, double cellWidth = 22, double cellHeight = 22)
        {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
            for (int r = 0; r < rows; r++) { grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellHeight) }); }
            for (int c = 0; c < columns; c++) { grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellWidth) }); }
            return grid;
        }

        private static void Put(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private void Show(Action build)
        {
            Action show = () =>
            {
                var panel = new StackPanel { Margin = new Thickness(10) };
                _panel = panel;
                build();
                _scroll.Content = panel;
            };
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
        }

        private StackPanel _panel;

        private void Add(UIElement element) => _panel.Children.Add(element);

        private static string Pretty(string text, int limit = 60) => text.Length > limit ? text.Substring(0, limit) + "..." : text;

        // ------------------------------------------------------------------ Caesar

        public void ShowCaesar(string text, string result, int shift, bool decrypt)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.CaesarTitle, 16, FontWeights.Bold));
                Add(Text(string.Format(Properties.Resources.CaesarShiftLine, shift, (char)('A' + shift)), 12));
                var used = new HashSet<char>(text.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z'));
                var grid = MakeGrid(2, 27);
                Put(grid, Cell(decrypt ? Properties.Resources.CipherLabelShort : Properties.Resources.PlainLabelShort, Header, 22, 22, true), 0, 0);
                Put(grid, Cell(decrypt ? Properties.Resources.PlainLabelShort : Properties.Resources.CipherLabelShort, Header, 22, 22, true), 1, 0);
                for (int i = 0; i < 26; i++)
                {
                    char top = (char)('A' + i), bottom = (char)('A' + (i + (decrypt ? 26 - shift : shift)) % 26);
                    bool highlighted = used.Contains(decrypt ? bottom : top);
                    Put(grid, Cell(top.ToString(), highlighted ? Used : Plain), 0, i + 1);
                    Put(grid, Cell(bottom.ToString(), highlighted ? Result : Plain), 1, i + 1);
                }
                Add(grid);
                Add(Text(Properties.Resources.CaesarExplanation, 12));
                var lines = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
                lines.Children.Add(Text((decrypt ? Properties.Resources.CipherLabel : Properties.Resources.PlainLabel) + ":  " + Pretty(text.ToUpperInvariant(), 80), 13, null, true));
                lines.Children.Add(Text((decrypt ? Properties.Resources.PlainLabel : Properties.Resources.CipherLabel) + ":  " + Pretty(result, 80), 13, FontWeights.Bold, true));
                Add(lines);
            });
        }

        // ------------------------------------------------------------------ Vigenere

        public void ShowVigenere(string text, string key, string result, bool decrypt, List<Calculations.Step> steps)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.VigenereTitle, 16, FontWeights.Bold));
                Add(Text(Properties.Resources.VigenereExplanation, 12));
                // the square: row = key letter, column = plaintext letter, cell = ciphertext letter
                var grid = MakeGrid(27, 27, 20, 20);
                Put(grid, Cell("", Header, 20, 20), 0, 0);
                var keyRows = new HashSet<int>(steps.Select(s => s.Key - 'A'));
                var plainColumns = new HashSet<int>(steps.Select(s => s.Plain - 'A'));
                var pairs = new HashSet<int>(steps.Select(s => (s.Key - 'A') * 26 + (s.Plain - 'A')));
                for (int i = 0; i < 26; i++)
                {
                    Put(grid, Cell(((char)('A' + i)).ToString(), plainColumns.Contains(i) ? Used : Header, 20, 20, true), 0, i + 1);
                    Put(grid, Cell(((char)('A' + i)).ToString(), keyRows.Contains(i) ? Used : Header, 20, 20, true), i + 1, 0);
                }
                for (int r = 0; r < 26; r++)
                {
                    for (int c = 0; c < 26; c++)
                    {
                        Brush background = pairs.Contains(r * 26 + c) ? Result : keyRows.Contains(r) || plainColumns.Contains(c) ? new SolidColorBrush(Color.FromRgb(255, 248, 215)) : Plain;
                        Put(grid, Cell(((char)('A' + (r + c) % 26)).ToString(), background, 20, 20), r + 1, c + 1);
                    }
                }
                Add(grid);

                var table = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
                table.Children.Add(Text(Properties.Resources.StepsLabel, 13, FontWeights.Bold));
                foreach (var step in steps.Take(30))
                {
                    table.Children.Add(Text(decrypt
                        ? string.Format(Properties.Resources.VigenereStepDecrypt, step.Cipher, step.Key, step.Plain)
                        : string.Format(Properties.Resources.VigenereStepEncrypt, step.Plain, step.Key, step.Cipher), 12, null, true));
                }
                if (steps.Count > 30) { table.Children.Add(Text("...", 12)); }
                table.Children.Add(Text(Environment.NewLine + (decrypt ? Properties.Resources.PlainLabel : Properties.Resources.CipherLabel) + ":  " + Pretty(result, 80), 13, FontWeights.Bold, true));
                Add(table);
            });
        }

        // ------------------------------------------------------------------ Nihilist

        public void ShowNihilist(Calculations.NihilistTrace trace, bool decrypt)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.NihilistTitle, 16, FontWeights.Bold));
                Add(Text(string.Format(Properties.Resources.NihilistExplanation, trace.KeyWord, trace.AdditiveKey), 12));
                var square = MakeGrid(6, 6, 26, 24);
                Put(square, Cell("", Header, 26, 24), 0, 0);
                var usedNumbers = new HashSet<int>(trace.Numbers.Concat(trace.KeyNumbers));
                for (int i = 1; i <= 5; i++)
                {
                    Put(square, Cell(i.ToString(), Header, 26, 24, true), 0, i);
                    Put(square, Cell(i.ToString(), Header, 26, 24, true), i, 0);
                }
                for (int i = 0; i < 25; i++)
                {
                    int number = (i / 5 + 1) * 10 + i % 5 + 1;
                    Put(square, Cell(trace.Square[i].ToString(), usedNumbers.Contains(number) ? Used : Plain, 26, 24, true), i / 5 + 1, i % 5 + 1);
                }
                Add(square);

                int count = Math.Min(trace.Letters.Count, 20);
                var table = MakeGrid(5, count + 1, 34, 22);
                string[] labels = { Properties.Resources.PlainLabelShort, "#", Properties.Resources.KeyLabelShort, "#", "+" };
                for (int r = 0; r < 5; r++) { Put(table, Cell(labels[r], Header, 34, 22, true), r, 0); }
                for (int i = 0; i < count; i++)
                {
                    Put(table, Cell(trace.Letters[i].ToString(), Plain, 34, 22), 0, i + 1);
                    Put(table, Cell(trace.Numbers[i].ToString(), Plain, 34, 22), 1, i + 1);
                    Put(table, Cell(trace.KeyLetters[i].ToString(), Plain, 34, 22), 2, i + 1);
                    Put(table, Cell(trace.KeyNumbers[i].ToString(), Plain, 34, 22), 3, i + 1);
                    Put(table, Cell(trace.Sums[i].ToString(), decrypt ? Plain : Result, 34, 22, true), 4, i + 1);
                }
                var holder = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
                holder.Children.Add(table);
                if (trace.Letters.Count > count) { holder.Children.Add(Text("...", 12)); }
                holder.Children.Add(Text(Environment.NewLine + (decrypt ? Properties.Resources.NihilistDecryptNote : Properties.Resources.NihilistEncryptNote), 12));
                Add(holder);
            });
        }
    }
}

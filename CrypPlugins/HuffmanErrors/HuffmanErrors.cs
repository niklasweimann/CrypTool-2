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
using System.Text;

namespace HuffmanErrors
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("HuffmanErrors.Properties.Resources", "PluginCaption", "PluginTooltip", "HuffmanErrors/DetailedDescription/doc.xml", "HuffmanErrors/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class HuffmanErrors : ICrypComponent
    {
        private HuffmanErrorsSettings _settings = new HuffmanErrorsSettings();
        private HuffmanErrorsPresentation _presentation;

        [PropertyInfo(Direction.InputData, "InputTextCaption", "InputTextTooltip", true)]
        public string InputText { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (HuffmanErrorsSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new HuffmanErrorsPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>Starts a new game when the workspace runs; the game then goes on in the presentation.</summary>
        public void Execute()
        {
            OutputText = ((HuffmanErrorsPresentation)Presentation).Start(InputText ?? "", _settings.Protect, _settings.ErrorPosition);
            OnPropertyChanged("OutputText");
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            InputText = OutputText = null;
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Huffman coding of a text with an optional Hamming(7,4) code: a single bit error in the Huffman code makes the decoder lose its place and
    /// garble everything after it, and the Hamming code corrects it at the price of 75 percent more bits.
    /// </summary>
    public static class HuffmanCoding
    {
        private sealed class Node
        {
            public int Weight;
            public char First;     // the smallest character below the node (breaks ties, so that the code is the same every time)
            public Node Left, Right;
            public char Symbol;
        }

        /// <summary>The Huffman code: the two lightest nodes are joined until one tree is left; 0 is the left branch.</summary>
        public static Dictionary<char, string> Build(string text)
        {
            var nodes = text.GroupBy(c => c).Select(g => new Node { Weight = g.Count(), Symbol = g.Key, First = g.Key }).ToList();
            var codes = new Dictionary<char, string>();
            if (nodes.Count == 0) { return codes; }
            if (nodes.Count == 1) { codes[nodes[0].Symbol] = "0"; return codes; }
            while (nodes.Count > 1)
            {
                nodes = nodes.OrderBy(n => n.Weight).ThenBy(n => n.First).ToList();
                var joined = new Node { Left = nodes[0], Right = nodes[1], Weight = nodes[0].Weight + nodes[1].Weight, First = (char)Math.Min(nodes[0].First, nodes[1].First) };
                nodes.RemoveRange(0, 2);
                nodes.Add(joined);
            }
            Assign(nodes[0], "", codes);
            return codes;
        }

        private static void Assign(Node node, string prefix, Dictionary<char, string> codes)
        {
            if (node.Left == null) { codes[node.Symbol] = prefix; return; }
            Assign(node.Left, prefix + "0", codes);
            Assign(node.Right, prefix + "1", codes);
        }

        public static string Encode(string text, Dictionary<char, string> codes) => string.Concat(text.Select(c => codes[c]));

        /// <summary>Decodes the bits; bits at the end that are not a whole code word give a question mark.</summary>
        public static string Decode(string bits, Dictionary<char, string> codes)
        {
            var symbols = codes.ToDictionary(p => p.Value, p => p.Key);
            var result = new StringBuilder();
            string word = "";
            foreach (char bit in bits)
            {
                word += bit;
                if (symbols.TryGetValue(word, out char symbol)) { result.Append(symbol); word = ""; }
            }
            if (word != "") { result.Append('?'); }
            return result.ToString();
        }

        /// <summary>Hamming(7,4): the bits d1 d2 d3 d4 become p1 p2 d1 p3 d2 d3 d4; every nibble is protected against one bit error.</summary>
        public static string HammingEncode(string bits)
        {
            bits = bits.PadRight((bits.Length + 3) / 4 * 4, '0');
            var result = new StringBuilder();
            for (int i = 0; i < bits.Length; i += 4)
            {
                int d1 = bits[i] - '0', d2 = bits[i + 1] - '0', d3 = bits[i + 2] - '0', d4 = bits[i + 3] - '0';
                int p1 = d1 ^ d2 ^ d4, p2 = d1 ^ d3 ^ d4, p3 = d2 ^ d3 ^ d4;
                result.Append(p1).Append(p2).Append(d1).Append(p3).Append(d2).Append(d3).Append(d4);
            }
            return result.ToString();
        }

        /// <summary>Corrects up to one error in every block of 7 bits; fixed counts the corrections.</summary>
        public static string HammingDecode(string bits, out int fixedErrors)
        {
            fixedErrors = 0;
            var result = new StringBuilder();
            for (int i = 0; i + 7 <= bits.Length; i += 7)
            {
                int[] c = Enumerable.Range(0, 7).Select(k => bits[i + k] - '0').ToArray();
                int syndrome = (c[0] ^ c[2] ^ c[4] ^ c[6]) | (c[1] ^ c[2] ^ c[5] ^ c[6]) << 1 | (c[3] ^ c[4] ^ c[5] ^ c[6]) << 2;
                if (syndrome != 0) { c[syndrome - 1] ^= 1; fixedErrors++; }
                result.Append(c[2]).Append(c[4]).Append(c[5]).Append(c[6]);
            }
            return result.ToString();
        }

        public static string Flip(string bits, int position)
        {
            if (position < 1 || position > bits.Length) { return bits; }
            var chars = bits.ToCharArray();
            chars[position - 1] = chars[position - 1] == '0' ? '1' : '0';
            return new string(chars);
        }

        public sealed class Transmission
        {
            public Dictionary<char, string> Codes;
            public string Bits, Sent, Received, Decoded;
            public int Corrected, Wrong;
        }

        public static Transmission Transmit(string text, bool protect, int errorPosition)
        {
            var t = new Transmission { Codes = Build(text) };
            t.Bits = Encode(text, t.Codes);
            t.Sent = protect ? HammingEncode(t.Bits) : t.Bits;
            t.Received = Flip(t.Sent, errorPosition);
            string bits = protect ? HammingDecode(t.Received, out t.Corrected).Substring(0, t.Bits.Length) : t.Received;
            t.Decoded = Decode(bits, t.Codes);
            t.Wrong = Enumerable.Range(0, Math.Max(text.Length, t.Decoded.Length)).Count(i => i >= text.Length || i >= t.Decoded.Length || text[i] != t.Decoded[i]);
            return t;
        }
    }

    public class HuffmanErrorsPresentation : UserControl
    {
        public HuffmanErrorsPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public HuffmanCoding.Transmission Last { get; private set; }

        /// <summary>Codes the text, flips the bit at the error position (1-based, 0 = no error) and decodes; returns the decoded text.</summary>
        public string Start(string text, int protect, int errorPosition)
        {
            if (text.Length == 0) { return ""; }
            var t = HuffmanCoding.Transmit(text, protect == 1, errorPosition);
            Last = t;
            Action show = () => Show(text, protect == 1, errorPosition, t);
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
            return t.Decoded;
        }

        private static string Shorten(string bits) => bits.Length <= 160 ? bits : bits.Substring(0, 160) + "...";

        private void Show(string text, bool protect, int errorPosition, HuffmanCoding.Transmission t)
        {
            var panel = new StackPanel { Margin = new Thickness(10) };
            Action<string, bool> line = (content, bold) => panel.Children.Add(new TextBlock { Text = content, FontSize = bold ? 14 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, bold ? 10 : 2, 0, 2) });
            line(Properties.Resources.Title, true);
            line(Properties.Resources.Intro, false);
            line(Properties.Resources.CodeTitle, true);
            line(string.Join("   ", t.Codes.OrderBy(p => p.Value.Length).ThenBy(p => p.Key).Select(p => (p.Key == ' ' ? "_" : p.Key.ToString()) + " = " + p.Value)), false);
            line(string.Format(Properties.Resources.Sizes, text.Length * 8, t.Bits.Length, t.Sent.Length), false);
            line(Properties.Resources.SentTitle, true);
            line(Shorten(t.Sent), false);
            if (errorPosition >= 1 && errorPosition <= t.Sent.Length)
            {
                line(string.Format(Properties.Resources.ErrorAt, errorPosition), true);
                line(Shorten(t.Received), false);
            }
            else { line(Properties.Resources.NoError, false); }
            line(Properties.Resources.DecodedTitle, true);
            line(t.Decoded, false);
            if (protect) { line(string.Format(Properties.Resources.Corrected, t.Corrected), false); }
            line(t.Wrong == 0 ? Properties.Resources.Intact : string.Format(Properties.Resources.Garbled, t.Wrong, Math.Max(text.Length, t.Decoded.Length)), true);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

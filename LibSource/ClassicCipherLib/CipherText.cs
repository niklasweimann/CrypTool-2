using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>Helpers shared by all ciphers of this component: alphabets, keywords, keyed squares, Morse code.</summary>
    public static class CipherText
    {
        public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>The 25 letters of a Polybius square (J is written as I).</summary>
        public const string SquareAlphabet = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

        public static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

        /// <summary>The letters A to Z of the text in upper case; everything else is dropped.</summary>
        public static string Letters(string text)
        {
            return new string((text ?? "").ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());
        }

        /// <summary>Like <see cref="Letters"/>, with J replaced by I.</summary>
        public static string SquareLetters(string text) => Letters(text).Replace('J', 'I');

        public static string Digits(string text) => new string((text ?? "").Where(c => c >= '0' && c <= '9').ToArray());

        /// <summary>The key letters, each only once (first occurrence), in the order of the key.</summary>
        public static string Distinct(string letters) => new string(letters.Distinct().ToArray());

        /// <summary>
        /// A keyed alphabet: the letters of the key without repeats, followed by the remaining letters of the alphabet in their normal order.
        /// Characters of the key that are not in the alphabet are ignored.
        /// </summary>
        public static string KeyedAlphabet(string key, string alphabet = Alphabet)
        {
            var result = new StringBuilder();
            foreach (char c in (key ?? "").ToUpperInvariant().Concat(alphabet))
            {
                if (alphabet.IndexOf(c) >= 0 && result.ToString().IndexOf(c) < 0) { result.Append(c); }
            }
            return result.ToString();
        }

        /// <summary>A keyed 5x5 Polybius square as 25 letters, row by row (J is I).</summary>
        public static string KeyedSquare(string key) => KeyedAlphabet((key ?? "").ToUpperInvariant().Replace('J', 'I'), SquareAlphabet);

        /// <summary>The alphabet moved to the right by "shift" places: the last letters move to the front.</summary>
        public static string RotateRight(string alphabet, int shift)
        {
            shift = Mod(shift, alphabet.Length);
            return alphabet.Substring(alphabet.Length - shift) + alphabet.Substring(0, alphabet.Length - shift);
        }

        /// <summary>
        /// The rank (0, 1, 2, ...) of every key character in alphabetical order. Equal characters get consecutive ranks from left to right,
        /// or the same rank if shareTies is set (the lowest ranks are 0, 1, 2 without gaps). The digit 0 counts as larger than 9.
        /// </summary>
        public static int[] Ranks(string key, bool shareTies = false)
        {
            Func<char, int> value = c => c == '0' ? 1000 : char.ToUpperInvariant(c);
            var order = Enumerable.Range(0, key.Length).OrderBy(i => value(key[i])).ThenBy(i => i).ToArray();
            var ranks = new int[key.Length];
            int rank = -1;
            for (int n = 0; n < order.Length; n++)
            {
                if (!shareTies || n == 0 || value(key[order[n]]) != value(key[order[n - 1]])) { rank++; }
                ranks[order[n]] = shareTies ? rank : n;
            }
            return ranks;
        }

        /// <summary>The digits 1 2 ... 9 0 that a keyword of up to ten letters stands for (the letters in alphabetical order, equal letters from left to right).</summary>
        public static int[] KeyDigits(string key) => Ranks(key).Select(r => (r + 1) % 10).ToArray();

        public static string Required(string value, string what)
        {
            if (string.IsNullOrEmpty(value)) { throw new ArgumentException("The " + what + " must not be empty."); }
            return value;
        }

        /// <summary>Appends the pad letter until the text fills whole blocks of the given size.</summary>
        public static string Pad(string text, int blockSize, char pad = 'X')
        {
            int rest = text.Length % blockSize;
            return rest == 0 ? text : text + new string(pad, blockSize - rest);
        }

        // ---------------------------------------------------------------- Morse code

        private static readonly Dictionary<char, string> MorseTable = new Dictionary<char, string>
        {
            ['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".", ['F'] = "..-.", ['G'] = "--.", ['H'] = "....",
            ['I'] = "..", ['J'] = ".---", ['K'] = "-.-", ['L'] = ".-..", ['M'] = "--", ['N'] = "-.", ['O'] = "---", ['P'] = ".--.",
            ['Q'] = "--.-", ['R'] = ".-.", ['S'] = "...", ['T'] = "-", ['U'] = "..-", ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-",
            ['Y'] = "-.--", ['Z'] = "--..", ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-",
            ['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----."
        };

        /// <summary>
        /// The text in Morse code as the ACA writes it for fractionating ciphers: '.' and '-' for dot and dash, one 'x' between the letters of
        /// a word and "xx" between words. Letters and digits are encoded, white space separates words, everything else is dropped.
        /// </summary>
        public static string ToMorse(string text)
        {
            var words = new List<string>();
            foreach (string word in (text ?? "").ToUpperInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string morse = string.Join("x", word.Where(MorseTable.ContainsKey).Select(c => MorseTable[c]));
                if (morse.Length > 0) { words.Add(morse); }
            }
            return string.Join("xx", words);
        }

        /// <summary>The inverse of <see cref="ToMorse"/>; words are separated by a space. Unknown groups of symbols give '?'.</summary>
        public static string FromMorse(string morse)
        {
            var reverse = MorseTable.ToDictionary(p => p.Value, p => p.Key);
            var words = new List<string>();
            foreach (string word in morse.Trim('x').Split(new[] { "xx" }, StringSplitOptions.None))
            {
                words.Add(new string(word.Split('x').Where(s => s.Length > 0).Select(s => reverse.TryGetValue(s, out char c) ? c : '?').ToArray()));
            }
            return string.Join(" ", words.Where(w => w.Length > 0));
        }

        /// <summary>The numbers 0 (dot), 1 (dash) and 2 (separator) for the Morse symbols '.', '-' and 'x'.</summary>
        public static int MorseSymbol(char symbol) => symbol == '.' ? 0 : symbol == '-' ? 1 : 2;

        public static char MorseChar(int symbol) => ".-x"[symbol];

        /// <summary>The numbers in the text (groups of digits) as a list.</summary>
        public static List<int> Numbers(string text)
        {
            var result = new List<int>();
            int current = -1;
            foreach (char c in (text ?? "") + " ")
            {
                if (c >= '0' && c <= '9') { current = (current < 0 ? 0 : current * 10) + (c - '0'); }
                else if (current >= 0) { result.Add(current); current = -1; }
            }
            return result;
        }
    }
}

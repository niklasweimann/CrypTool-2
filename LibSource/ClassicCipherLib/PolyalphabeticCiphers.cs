using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>The three tables of the periodic ciphers: Vigenère (C = P + K), Variant (C = P - K) and Beaufort (C = K - P).</summary>
    public enum Table { Vigenere = 0, Variant = 1, Beaufort = 2 }

    /// <summary>The polyalphabetic ciphers: all of them replace a letter by a letter that depends on the position in the text.</summary>
    public static class PolyalphabeticCiphers
    {
        private static int Combine(Table table, int plain, int key, bool decrypt)
        {
            switch (table)
            {
                case Table.Vigenere: return decrypt ? plain - key : plain + key;
                case Table.Variant: return decrypt ? plain + key : plain - key;
                default: return key - plain;   // Beaufort is reciprocal
            }
        }

        /// <summary>Applies a table with a key letter (as number 0..25) for every position of the text.</summary>
        private static string Apply(string letters, Func<int, int> keyAt, Table table, bool decrypt)
        {
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i++)
            {
                result.Append((char)('A' + CipherText.Mod(Combine(table, letters[i] - 'A', keyAt(i), decrypt), 26)));
            }
            return result.ToString();
        }

        /// <summary>Periodic encryption with a key word (Vigenère, Variant or Beaufort); only the letters of the text are used.</summary>
        public static string Periodic(string text, string key, Table table, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            return Apply(CipherText.Letters(text), i => k[i % k.Length] - 'A', table, decrypt);
        }

        /// <summary>
        /// Progressive key: the text is enciphered with the key word, then a second time with a key letter that moves on by "index" letters
        /// after every group of one period (A, B, C, ... for index 1; A, C, E, ... for index 2).
        /// </summary>
        public static string Progressive(string text, string key, int index, Table table, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            Func<int, int> progressive = i => CipherText.Mod(i / k.Length * index, 26);
            string letters = CipherText.Letters(text);
            if (decrypt) { return Periodic(Apply(letters, progressive, table, true), k, table, true); }
            return Apply(Periodic(letters, k, table, false), progressive, table, false);
        }

        /// <summary>
        /// Interrupted key: the key word is used from its first letter, but only for the number of letters given in the list "interruptions";
        /// then it starts again. The list is repeated if the text is longer.
        /// </summary>
        public static string Interrupted(string text, string key, string interruptions, Table table, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            var lengths = CipherText.Numbers(interruptions);
            if (lengths.Count == 0 || lengths.Any(l => l < 1 || l > k.Length))
            {
                throw new ArgumentException("The interruptions are lengths between 1 and the length of the key, e.g. \"4 6 2 3\".");
            }
            string letters = CipherText.Letters(text);
            var stream = new StringBuilder();
            for (int segment = 0; stream.Length < letters.Length; segment++) { stream.Append(k, 0, lengths[segment % lengths.Count]); }
            return Apply(letters, i => stream[i] - 'A', table, decrypt);
        }

        /// <summary>
        /// Slidefair: pairs of letters, the first one is looked up in the top alphabet, the second one in the row of the table (Vigenère, Variant
        /// or Beaufort) that the key letter selects; the cipher letters are the other corners of the rectangle (the top one first). A pair in the
        /// same column is replaced by the pair to its right. An odd text gets an X.
        /// </summary>
        public static string Slidefair(string text, string key, Table table, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            string letters = CipherText.Pad(CipherText.Letters(text), 2);
            var result = new StringBuilder();
            for (int pair = 0; pair < letters.Length / 2; pair++)
            {
                int shift = k[pair % k.Length] - 'A';
                Func<int, int> rowAt = column => CipherText.Mod(table == Table.Vigenere ? column + shift : table == Table.Variant ? column - shift : shift - column, 26);
                Func<int, int> columnOf = letter => CipherText.Mod(table == Table.Vigenere ? letter - shift : table == Table.Variant ? letter + shift : shift - letter, 26);
                int a = letters[2 * pair] - 'A', b = letters[2 * pair + 1] - 'A';
                int first, second;
                if (!decrypt)
                {
                    int i = a, j = columnOf(b);
                    if (i == j) { first = CipherText.Mod(i + 1, 26); second = rowAt(first); }
                    else { first = j; second = rowAt(i); }
                }
                else
                {
                    int top = a, row = columnOf(b);
                    if (top == row) { first = CipherText.Mod(top - 1, 26); second = rowAt(first); }
                    else { first = row; second = rowAt(top); }
                }
                result.Append((char)('A' + first)).Append((char)('A' + second));
            }
            return result.ToString();
        }

        /// <summary>
        /// Portax: the text is written under the key word and every vertical pair is enciphered with the slide that the key letter of its column
        /// sets (see the documentation). The same operation encrypts and decrypts. The text is filled up with X to complete blocks of two lines.
        /// </summary>
        public static string Portax(string text, string key)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            int period = k.Length;
            string letters = CipherText.Pad(CipherText.Letters(text), 2 * period);
            var result = new char[letters.Length];
            for (int block = 0; block < letters.Length; block += 2 * period)
            {
                for (int column = 0; column < period; column++)
                {
                    int slide = (k[column] - 'A') / 2;
                    // upper alphabet: fixed A..M above the sliding N..Z; lower alphabet: pairs AB, CD, ... under the slide
                    Func<int, int, char> upperAt = (row, col) => row == 0 ? (char)('A' + col) : (char)('N' + (col + slide) % 13);
                    Func<int, int, char> lowerAt = (row, col) => (char)('A' + 2 * ((col + slide) % 13) + row);
                    char top = letters[block + column], bottom = letters[block + period + column];
                    int upperRow = top < 'N' ? 0 : 1;
                    int upperCol = upperRow == 0 ? top - 'A' : CipherText.Mod(top - 'N' - slide, 13);
                    int lowerRow = (bottom - 'A') % 2;
                    int lowerCol = CipherText.Mod((bottom - 'A') / 2 - slide, 13);
                    char first, second;
                    if (upperCol == lowerCol) { first = upperAt(1 - upperRow, upperCol); second = lowerAt(1 - lowerRow, lowerCol); }
                    else { first = upperAt(upperRow, lowerCol); second = lowerAt(lowerRow, upperCol); }
                    result[block + column] = first;
                    result[block + period + column] = second;
                }
            }
            return new string(result);
        }

        /// <summary>The plain and the cipher alphabet of a Quagmire (type 1 to 4): see <see cref="Quagmire"/>.</summary>
        public static void QuagmireAlphabets(int type, string key, string cipherKey, out string plainAlphabet, out string cipherAlphabet)
        {
            plainAlphabet = type == 2 ? CipherText.Alphabet : CipherText.KeyedAlphabet(key);
            cipherAlphabet = type == 1 ? CipherText.Alphabet : CipherText.KeyedAlphabet(type == 4 ? cipherKey : key);
        }

        /// <summary>
        /// Quagmire I to IV: the keyed plain alphabet (type 1 and 3, 4) and the keyed cipher alphabet (type 2, 3, 4) are run against each other.
        /// The indicator key gives the period and the setting of the cipher alphabet: its letter stands under the plain letter "under".
        /// Type 1: keyed plain alphabet, straight cipher alphabet; 2: the other way round; 3: both keyed with the same key; 4: keyed with two keys.
        /// </summary>
        public static string Quagmire(int type, string text, string key, string indicator, string cipherKey, char under, bool decrypt)
        {
            string plainAlphabet, cipherAlphabet;
            QuagmireAlphabets(type, key, cipherKey, out plainAlphabet, out cipherAlphabet);
            string ind = CipherText.Required(CipherText.Letters(indicator), "indicator key");
            char u = char.ToUpperInvariant(under);
            if (u < 'A' || u > 'Z') { throw new ArgumentException("The letter above the indicator must be a letter."); }
            int underPosition = plainAlphabet.IndexOf(u);
            string letters = CipherText.Letters(text);
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i++)
            {
                int setting = cipherAlphabet.IndexOf(ind[i % ind.Length]) - underPosition;
                if (!decrypt) { result.Append(cipherAlphabet[CipherText.Mod(setting + plainAlphabet.IndexOf(letters[i]), 26)]); }
                else { result.Append(plainAlphabet[CipherText.Mod(cipherAlphabet.IndexOf(letters[i]) - setting, 26)]); }
            }
            return result.ToString();
        }

        /// <summary>The key word's alphabet written in a block as wide as the key; the columns are taken in the alphabetical order of the key letters.</summary>
        public static string TranspositionAlphabet(string distinctKey)
        {
            string alphabet = CipherText.KeyedAlphabet(distinctKey);
            int width = distinctKey.Length;
            var result = new StringBuilder();
            foreach (int column in CipherText.Ranks(distinctKey).Select((rank, index) => new { rank, index }).OrderBy(x => x.rank).Select(x => x.index))
            {
                for (int i = column; i < alphabet.Length; i += width) { result.Append(alphabet[i]); }
            }
            return result.ToString();
        }

        /// <summary>The mixed alphabet of the Headlines cipher: the alphabet of the key word written in a block as wide as the hat word, the columns taken in the order of the hat letters.</summary>
        public static string HeadlinesAlphabet(string hat, string key)
        {
            string hatLetters = CipherText.Required(CipherText.Letters(hat), "hat word");
            string alphabet = CipherText.KeyedAlphabet(key);
            var mixed = new StringBuilder();
            foreach (int column in CipherText.Ranks(hatLetters).Select((rank, index) => new { rank, index }).OrderBy(x => x.rank).Select(x => x.index))
            {
                for (int i = column; i < alphabet.Length; i += hatLetters.Length) { mixed.Append(alphabet[i]); }
            }
            return mixed.ToString();
        }

        /// <summary>
        /// Headlines: the headlines (lines of the text) are enciphered by simple substitution with the same mixed alphabet at different settings.
        /// The mixed alphabet is the alphabet of the key word, written in a block as wide as the hat word is long; its columns are taken in the
        /// order of the letters of the hat word. The letter of the setting word for the line is the letter of the mixed alphabet that stands
        /// under the first letter of it (like Quagmire III). Other characters, also the asterisk of proper nouns, are kept.
        /// </summary>
        public static string Headlines(string text, string hat, string key, string setting, bool decrypt)
        {
            string settings = CipherText.Required(CipherText.Letters(setting), "setting word");
            string m = HeadlinesAlphabet(hat, key);
            var lines = (text ?? "").Replace("\r", "").Split('\n');
            for (int line = 0; line < lines.Length; line++)
            {
                int offset = m.IndexOf(settings[line % settings.Length]);
                lines[line] = new string(lines[line].ToUpperInvariant().Select(c =>
                    c < 'A' || c > 'Z' ? c : m[CipherText.Mod(m.IndexOf(c) + (decrypt ? -offset : offset), 26)]).ToArray());
            }
            return string.Join("\n", lines);
        }

        /// <summary>The digit that is added to the position of a letter: digits[i] = digits[i - n] + digits[i - n + 1] (mod 10) for a primer of n digits.</summary>
        public static int[] RunningDigits(int[] primer, int length)
        {
            var digits = new int[Math.Max(length, primer.Length)];
            Array.Copy(primer, digits, primer.Length);
            for (int i = primer.Length; i < digits.Length; i++) { digits[i] = (digits[i - primer.Length] + digits[i - primer.Length + 1]) % 10; }
            return digits;
        }

        /// <summary>
        /// Gromark: a Gronsfeld-like cipher with a mixed cipher alphabet (the key word's alphabet, columns of the transposition block taken in
        /// alphabetical order) and a running key of digits that is made from a primer by adding pairs of digits.
        /// </summary>
        public static string Gromark(string text, string key, string primer, bool decrypt)
        {
            string distinct = CipherText.Distinct(CipherText.Required(CipherText.Letters(key), "key"));
            int[] p = CipherText.Digits(primer).Select(c => c - '0').ToArray();
            if (p.Length < 2) { throw new ArgumentException("The primer needs at least two digits."); }
            string cipherAlphabet = TranspositionAlphabet(distinct);
            string letters = CipherText.Letters(text);
            int[] digits = RunningDigits(p, letters.Length);
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i++)
            {
                if (!decrypt) { result.Append(cipherAlphabet[(letters[i] - 'A' + digits[i]) % 26]); }
                else { result.Append((char)('A' + CipherText.Mod(cipherAlphabet.IndexOf(letters[i]) - digits[i], 26))); }
            }
            return result.ToString();
        }

        /// <summary>
        /// Periodic Gromark: like Gromark, but the primer is the numbering of the key word's letters, and every group of one period starts at
        /// another position of the cipher alphabet: the position of the next key letter in it.
        /// </summary>
        public static string PeriodicGromark(string text, string key, bool decrypt)
        {
            string distinct = CipherText.Distinct(CipherText.Required(CipherText.Letters(key), "key"));
            if (distinct.Length < 2) { throw new ArgumentException("The key needs at least two different letters."); }
            string cipherAlphabet = TranspositionAlphabet(distinct);
            string letters = CipherText.Letters(text);
            int[] digits = RunningDigits(CipherText.KeyDigits(distinct), letters.Length);
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i++)
            {
                int start = cipherAlphabet.IndexOf(distinct[i / distinct.Length % distinct.Length]) + digits[i];
                if (!decrypt) { result.Append(cipherAlphabet[CipherText.Mod(letters[i] - 'A' + start, 26)]); }
                else { result.Append((char)('A' + CipherText.Mod(cipherAlphabet.IndexOf(letters[i]) - start, 26))); }
            }
            return result.ToString();
        }

        /// <summary>
        /// Condi: a keyed alphabet (moved to the right by "shift") is used as a table. A letter is replaced by the letter that lies as many places
        /// further as the position (1 to 26) of the plaintext letter before it; the first letter uses the start value "offset".
        /// Word divisions and punctuation are kept.
        /// </summary>
        public static string Condi(string text, string key, int shift, int offset, bool decrypt)
        {
            string alphabet = CipherText.RotateRight(CipherText.KeyedAlphabet(key), shift);
            int previous = offset;
            var result = new StringBuilder();
            foreach (char c in (text ?? "").ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') { result.Append(c); continue; }
                int plain, cipher;
                if (!decrypt) { plain = alphabet.IndexOf(c); cipher = CipherText.Mod(plain + previous, 26); }
                else { cipher = alphabet.IndexOf(c); plain = CipherText.Mod(cipher - previous, 26); }
                result.Append(alphabet[decrypt ? plain : cipher]);
                previous = plain + 1;
            }
            return result.ToString();
        }

        /// <summary>
        /// Ragbaby: a 24-letter keyed alphabet (I/J and W/X share a place). Every letter moves to the right by its number: 1 for the first
        /// letter of the first word, 2 for the second letter, ...; the next word starts with the number of the word. The numbers go up to 24 and
        /// start again. Words are separated by white space; other characters are kept.
        /// </summary>
        public static string Ragbaby(string text, string key, bool decrypt)
        {
            const string letters24 = "ABCDEFGHIKLMNOPQRSTUVWYZ";
            string alphabet = CipherText.KeyedAlphabet((key ?? "").ToUpperInvariant().Replace('J', 'I').Replace('X', 'W'), letters24);
            var result = new StringBuilder();
            int word = 0, number = 0;
            bool inWord = false;
            foreach (char original in (text ?? "").ToUpperInvariant())
            {
                if (char.IsWhiteSpace(original)) { inWord = false; result.Append(original); continue; }
                if (!inWord) { inWord = true; word++; number = word - 1; }
                char c = original == 'J' ? 'I' : original == 'X' ? 'W' : original;
                int index = alphabet.IndexOf(c);
                if (index < 0) { result.Append(original); continue; }
                number++;
                result.Append(alphabet[CipherText.Mod(index + (decrypt ? -number : number), 24)]);
            }
            return result.ToString();
        }

        /// <summary>
        /// The eight Polybius squares of the Phillips cipher (rc: of the Phillips-RC cipher). The key word is written into a 5x5 square in
        /// alternating directions. Squares 1 to 5 move the first row down one row at a time, squares 6 to 8 do this with the second row
        /// (the Phillips-RC cipher moves columns in the same way).
        /// </summary>
        public static string[][] PhillipsSquares(string key, bool rc)
        {
            string square = CipherText.KeyedSquare(key);
            var rows = new List<string>();
            for (int r = 0; r < 5; r++)
            {
                string row = square.Substring(5 * r, 5);
                rows.Add(r % 2 == 1 ? new string(row.Reverse().ToArray()) : row);
            }
            var squares = new List<string[]>();
            for (int n = 0; n < 8; n++)
            {
                // the order of the rows (for Phillips-RC also of the columns) of square n + 1
                var order = new List<int>();
                if (n < 5) { order.AddRange(new[] { 1, 2, 3, 4 }); order.Insert(n, 0); }      // row 1 moves down: 1, 2, 3, 4, 5
                else { order.AddRange(new[] { 2, 3, 4 }); order.Insert(n - 4, 1); order.Add(0); } // then row 2 moves down, row 1 stays at the bottom
                squares.Add(Enumerable.Range(0, 5).Select(r => new string(Enumerable.Range(0, 5).Select(c => rows[order[r]][rc ? order[c] : c]).ToArray())).ToArray());
            }
            return squares.ToArray();
        }

        /// <summary>Phillips (rc = false) and Phillips-RC (rc = true): five letters are enciphered with each of the eight squares in turn, every letter by the one diagonally down to the right.</summary>
        public static string Phillips(string text, string key, bool rc, bool decrypt)
        {
            var squares = PhillipsSquares(key, rc);
            string letters = CipherText.SquareLetters(text);
            var result = new StringBuilder();
            for (int i = 0; i < letters.Length; i++)
            {
                var square = squares[i / 5 % 8];
                int row = Enumerable.Range(0, 5).First(r => square[r].IndexOf(letters[i]) >= 0);
                int column = square[row].IndexOf(letters[i]);
                int step = decrypt ? -1 : 1;
                result.Append(square[CipherText.Mod(row + step, 5)][CipherText.Mod(column + step, 5)]);
            }
            return result.ToString();
        }
    }
}

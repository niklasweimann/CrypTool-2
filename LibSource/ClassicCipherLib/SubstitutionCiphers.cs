using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>Which letter of every word of a null cipher is the message.</summary>
    public enum NullPosition { First = 0, Middle = 1, Last = 2 }

    /// <summary>Substitution ciphers of the ACA that replace a letter by a letter, a number or a group of digits.</summary>
    public static class SubstitutionCiphers
    {
        /// <summary>
        /// Polybius cipher: every letter is replaced by the number of its row and of its column in a 5x5 square (I and J together). Without
        /// a key the square is the plain alphabet; a key word gives a keyed square. The pairs of digits are separated by spaces.
        /// </summary>
        public static string Polybius(string text, string key, bool decrypt)
        {
            string square = CipherText.KeyedSquare(key);
            if (!decrypt)
            {
                return string.Join(" ", CipherText.SquareLetters(text).Select(c => (square.IndexOf(c) / 5 + 1).ToString() + (square.IndexOf(c) % 5 + 1)));
            }
            string digits = CipherText.Digits(text);
            var result = new StringBuilder();
            for (int i = 0; i + 1 < digits.Length; i += 2)
            {
                int row = digits[i] - '1', column = digits[i + 1] - '1';
                result.Append(row < 0 || row > 4 || column < 0 || column > 4 ? '?' : square[5 * row + column]);
            }
            return result.ToString();
        }

        public const string KnockSquare = "ABCDEFGHIJLMNOPQRSTUVWXYZ";

        /// <summary>
        /// Tap code (Klopfcode): the Polybius square of the prisoners, with C and K together (K is written as C). A letter is knocked as the
        /// number of its row, a pause, and the number of its column; the output is the pair of numbers, pairs separated by spaces.
        /// </summary>
        public static string TapCode(string text, bool decrypt)
        {
            if (!decrypt)
            {
                return string.Join(" ", CipherText.Letters(text).Replace('K', 'C').Select(c => (KnockSquare.IndexOf(c) / 5 + 1).ToString() + (KnockSquare.IndexOf(c) % 5 + 1)));
            }
            string digits = CipherText.Digits(text);
            var result = new StringBuilder();
            for (int i = 0; i + 1 < digits.Length; i += 2)
            {
                int row = digits[i] - '1', column = digits[i + 1] - '1';
                result.Append(row < 0 || row > 4 || column < 0 || column > 4 ? '?' : KnockSquare[5 * row + column]);
            }
            return result.ToString();
        }

        // the 20 alphabet bars of Le Sphinx: 1 to 10 ascending, 11 to 20 descending (numbers as printed on the bars)
        private static readonly string[] SphinxBars =
        {
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "CDEFGHIJKLMNOPQRSTUVWXYZAB", "EFGHIJKLMNOPQRSTUVWXYZABCD", "GHIJKLMNOPQRSTUVWXYZABCDEF", "IJKLMNOPQRSTUVWXYZABCDEFGH",
            "MNOPQRSTUVWXYZABCDEFGHIJKL", "PQRSTUVWXYZABCDEFGHIJKLMNO", "RSTUVWXYZABCDEFGHIJKLMNOPQ", "TUVWXYZABCDEFGHIJKLMNOPQRS", "VWXYZABCDEFGHIJKLMNOPQRSTU",
            "BAZYXWVUTSRQPONMLKJIHGFEDC", "DCBAZYXWVUTSRQPONMLKJIHGFE", "FEDCBAZYXWVUTSRQPONMLKJIHG", "HGFEDCBAZYXWVUTSRQPONMLKJI", "LKJIHGFEDCBAZYXWVUTSRQPONM",
            "ONMLKJIHGFEDCBAZYXWVUTSRQP", "SRQPONMLKJIHGFEDCBAZYXWVUT", "UTSRQPONMLKJIHGFEDCBAZYXWV", "WVUTSRQPONMLKJIHGFEDCBAZYX", "YXWVUTSRQPONMLKJIHGFEDCBAZ"
        };

        /// <summary>
        /// Le Sphinx (Lugagne, about 1930): ten lanes, each with an upper bar for the plaintext and a lower bar for the ciphertext that move together; the key is
        /// the arrangement of the 20 bars: the numbers of the 10 upper bars, then of the 10 lower bars (without a key 1 2 ... 10 and 11 12 ... 20). Ten letters are
        /// enciphered at a time: a letter in the upper bar of a lane is replaced by the letter at the same place of the lower bar. A last group may be shorter.
        /// </summary>
        public static string LeSphinx(string text, string key, bool decrypt)
        {
            var numbers = CipherText.Numbers(key);
            if (numbers.Count == 0) { numbers = Enumerable.Range(1, 20).ToList(); }
            if (numbers.Count != 20 || numbers.Distinct().Count() != 20 || numbers.Any(n => n < 1 || n > 20))
            {
                throw new ArgumentException("The key lists the numbers 1 to 20 once: first the 10 upper bars, then the 10 lower bars of the lanes.");
            }
            var result = new StringBuilder();
            string letters = CipherText.Letters(text);
            for (int i = 0; i < letters.Length; i++)
            {
                int lane = i % 10;
                string upper = SphinxBars[numbers[lane] - 1], lower = SphinxBars[numbers[10 + lane] - 1];
                result.Append(decrypt ? upper[lower.IndexOf(letters[i])] : lower[upper.IndexOf(letters[i])]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Numbered key: the key phrase is extended by the letters that are missing (in alphabetical order); its letters are numbered 0, 1, ...
        /// starting at the place "offset" and going around. A plaintext letter is replaced by one of the numbers of its places (every number of a
        /// letter is used in turn). The numbers are written with two digits and separated by spaces.
        /// </summary>
        public static string NumberedKey(string text, string phrase, int offset, bool decrypt)
        {
            string extended = CipherText.Required(CipherText.Letters(phrase), "key phrase");
            extended += new string(CipherText.Alphabet.Where(c => extended.IndexOf(c) < 0).ToArray());
            Func<int, char> letterOf = number => extended[(offset + number) % extended.Length];
            if (decrypt)
            {
                return new string(CipherText.Numbers(text).Select(n => n < extended.Length ? letterOf(n) : '?').ToArray());
            }
            var used = new Dictionary<char, int>();
            var numbers = new List<string>();
            foreach (char c in CipherText.Letters(text))
            {
                var places = Enumerable.Range(0, extended.Length).Where(n => letterOf(n) == c).ToArray();
                used.TryGetValue(c, out int count);
                used[c] = count + 1;
                numbers.Add(places[count % places.Length].ToString("00"));
            }
            return string.Join(" ", numbers);
        }

        /// <summary>
        /// Homophonic: the 25 letters (I/J together) have the numbers 01 to 00 in four rows. The four letters of the key word say where the
        /// numbers of the rows (01-25, 26-50, 51-75, 76-00) start: the key letter of a row has its first number. The four rows are used in turn.
        /// </summary>
        public static string Homophonic(string text, string key, bool decrypt)
        {
            string k = CipherText.SquareLetters(key);
            if (k.Length != 4) { throw new ArgumentException("The key must have 4 letters."); }
            Func<int, char, int> number = (row, letter) =>
                1 + 25 * row + CipherText.Mod(CipherText.SquareAlphabet.IndexOf(letter) - CipherText.SquareAlphabet.IndexOf(k[row]), 25);
            if (!decrypt)
            {
                var numbers = new List<string>();
                string letters = CipherText.SquareLetters(text);
                for (int i = 0; i < letters.Length; i++) { numbers.Add((number(i % 4, letters[i]) % 100).ToString("00")); }
                return string.Join(" ", numbers);
            }
            var result = new StringBuilder();
            foreach (int n in CipherText.Numbers(text))
            {
                int value = n == 0 ? 100 : n, row = (value - 1) / 25;
                if (value > 100 || row > 3) { result.Append('?'); continue; }
                result.Append(CipherText.SquareAlphabet[CipherText.Mod(CipherText.SquareAlphabet.IndexOf(k[row]) + (value - 1) % 25, 25)]);
            }
            return result.ToString();
        }

        private static char Pick(string word, NullPosition position)
        {
            return position == NullPosition.First ? word[0] : position == NullPosition.Last ? word[word.Length - 1] : word[(word.Length - 1) / 2];
        }

        /// <summary>
        /// Null cipher: every letter of the message is hidden in a word (its first, middle or last letter). The words come from the cover text;
        /// for every message letter the next word that has the right letter in the right place is taken, and the cover text starts again if needed.
        /// Decryption reads the letters back from the words.
        /// </summary>
        public static string Null(string text, string coverText, NullPosition position, bool decrypt)
        {
            Func<string, string[]> words = s => (s ?? "").ToUpperInvariant().Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (decrypt) { return new string(words(text).Select(w => Pick(w, position)).ToArray()); }
            var cover = words(CipherText.Required(coverText, "cover text")).Where(w => w.All(c => c >= 'A' && c <= 'Z')).ToArray();
            if (cover.Length == 0) { throw new ArgumentException("The cover text needs words."); }
            var result = new List<string>();
            int next = 0;
            foreach (char c in CipherText.Letters(text))
            {
                int found = -1;
                for (int i = 0; i < cover.Length && found < 0; i++)
                {
                    int candidate = (next + i) % cover.Length;
                    if (Pick(cover[candidate], position) == c) { found = candidate; }
                }
                if (found < 0) { throw new ArgumentException("No word of the cover text has the letter " + c + " at the chosen place."); }
                result.Add(cover[found]);
                next = found + 1;
            }
            return string.Join(" ", result);
        }

        /// <summary>
        /// Checkerboard: a 5x5 Polybius square with a key word of five different letters at its left (rows) and one above (columns). A letter
        /// is replaced by the letter of its row and the letter of its column. In the complex case there are two words at each side (ten letters,
        /// all different); the two words are used in turn.
        /// </summary>
        public static string Checkerboard(string text, string square, string rowLabels, string columnLabels, bool decrypt)
        {
            string s = CipherText.KeyedSquare(square);
            string rows = CipherText.Letters(rowLabels), columns = CipherText.Letters(columnLabels);
            Func<string, bool> valid = labels => (labels.Length == 5 || labels.Length == 10) && CipherText.Distinct(labels).Length == labels.Length;
            if (!valid(rows) || !valid(columns))
            {
                throw new ArgumentException("The words at the left and above the square need five different letters each (or ten, for two words).");
            }
            var result = new StringBuilder();
            if (!decrypt)
            {
                int position = 0;
                foreach (char c in CipherText.SquareLetters(text))
                {
                    int index = s.IndexOf(c);
                    int rowSet = position % (rows.Length / 5), columnSet = position / 2 % (columns.Length / 5);
                    result.Append(rows[5 * rowSet + index / 5]).Append(columns[5 * columnSet + index % 5]);
                    position++;
                }
                return result.ToString();
            }
            string pairs = CipherText.Letters(text);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                int row = rows.IndexOf(pairs[i]), column = columns.IndexOf(pairs[i + 1]);
                result.Append(row < 0 || column < 0 ? '?' : s[5 * (row % 5) + column % 5]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Monome-Dinome: a 3x8 box of the 24 letters (I/J and Y/Z share a place). The letters of the top row are replaced by one digit (their
        /// column), those of the other rows by two digits (row, column). The ten digits come from a key word of ten letters: the first two are the
        /// rows 2 and 3, the other eight are the columns.
        /// </summary>
        public static string MonomeDinome(string text, string boxKey, string numberKey, bool decrypt)
        {
            const string alphabet24 = "ABCDEFGHIKLMNOPQRSTUVWXY";
            string box = CipherText.KeyedAlphabet(CipherText.Letters(boxKey).Replace('J', 'I').Replace('Z', 'Y'), alphabet24);
            string nk = CipherText.Letters(numberKey);
            if (nk.Length != 10) { throw new ArgumentException("The key for the digits needs 10 letters."); }
            int[] digits = CipherText.KeyDigits(nk);
            var result = new StringBuilder();
            if (!decrypt)
            {
                foreach (char c in CipherText.Letters(text).Replace('J', 'I').Replace('Z', 'Y'))
                {
                    int index = box.IndexOf(c), row = index / 8;
                    if (row > 0) { result.Append(digits[row - 1]); }
                    result.Append(digits[2 + index % 8]);
                }
                return result.ToString();
            }
            string d = CipherText.Digits(text);
            for (int i = 0; i < d.Length; i++)
            {
                int row = 0, value = d[i] - '0';
                if (value == digits[0] || value == digits[1])
                {
                    row = value == digits[0] ? 1 : 2;
                    if (++i >= d.Length) { break; }
                    value = d[i] - '0';
                }
                int column = Array.IndexOf(digits, value) - 2;
                result.Append(column < 0 ? '?' : box[8 * row + column]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Tridigital: the keyed alphabet fills a block of ten columns row by row (the last column stays empty); a number key from ten letters is
        /// written above the columns. A letter is replaced by the digit above its column, a space by the digit above the empty column.
        /// Because three letters share a digit, decryption writes them in brackets.
        /// </summary>
        public static string Tridigital(string text, string alphabetKey, string numberKey, bool decrypt)
        {
            string alphabet = CipherText.KeyedAlphabet(CipherText.Letters(alphabetKey));
            string nk = CipherText.Letters(numberKey);
            if (nk.Length != 10) { throw new ArgumentException("The key for the digits needs 10 letters."); }
            int[] digits = CipherText.KeyDigits(nk);
            var result = new StringBuilder();
            if (!decrypt)
            {
                bool space = false;
                foreach (char c in (text ?? "").ToUpperInvariant())
                {
                    if (char.IsWhiteSpace(c)) { space = result.Length > 0; continue; }
                    if (c < 'A' || c > 'Z') { continue; }
                    if (space) { result.Append(digits[9]); space = false; }
                    result.Append(digits[alphabet.IndexOf(c) % 9]);
                }
                return result.ToString();
            }
            foreach (char c in CipherText.Digits(text))
            {
                int column = Array.IndexOf(digits, c - '0');
                if (column == 9) { result.Append(' '); continue; }
                result.Append('[');
                for (int i = column; i < 26; i += 9) { result.Append(alphabet[i]); }
                result.Append(']');
            }
            return result.ToString();
        }

        // Compressocrat: the Huffman-like code of the ACA; the digits are 1, 2 and 3
        public static readonly string[] CompressocratCodes =
        {
            "13", "32112", "1112", "213", "31", "3213", "32113", "113", "322", "321112", "11112", "212", "2111",
            "23", "22", "3212", "11113", "323", "112", "12", "1113", "11111", "2112", "321111", "2113", "321113"
        };

        /// <summary>
        /// Compressocrat: every plaintext letter is replaced by a code of 1 to 6 digits (1, 2, 3; frequent letters have short codes), the digits
        /// are taken in groups of three (the digit 1 is added to complete the last group) and every group is replaced by a letter of the keyed
        /// alphabet, moved to the right by "shift". Only the letters of the text are used.
        /// </summary>
        public static string Compressocrat(string text, string key, int shift, bool decrypt)
        {
            string alphabet = CipherText.RotateRight(CipherText.KeyedAlphabet(key), shift);
            // the 26 groups 111 to 332 in this order stand for the letters of the alphabet
            var triples = new List<string>();
            foreach (char a in "123") foreach (char b in "123") foreach (char c in "123") triples.Add("" + a + b + c);
            if (!decrypt)
            {
                var digits = new StringBuilder();
                foreach (char c in CipherText.Letters(text)) { digits.Append(CompressocratCodes[c - 'A']); }
                while (digits.Length % 3 != 0) { digits.Append('1'); }
                var result = new StringBuilder();
                for (int i = 0; i < digits.Length; i += 3) { result.Append(alphabet[triples.IndexOf(digits.ToString(i, 3))]); }
                return result.ToString();
            }
            var numbers = new StringBuilder();
            foreach (char c in CipherText.Letters(text)) { numbers.Append(triples[alphabet.IndexOf(c)]); }
            var plain = new StringBuilder();
            string all = numbers.ToString();
            int position = 0;
            while (position < all.Length)
            {
                int code = Array.FindIndex(CompressocratCodes, x => string.CompareOrdinal(all, position, x, 0, x.Length) == 0);
                if (code < 0) { break; }   // what is left is the filling with ones
                plain.Append((char)('A' + code));
                position += CompressocratCodes[code].Length;
            }
            return plain.ToString();
        }
    }
}

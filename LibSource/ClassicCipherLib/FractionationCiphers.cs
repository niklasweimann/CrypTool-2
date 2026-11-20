using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>
    /// Fractionating and digraphic ciphers of the ACA: letters are split into parts (coordinates, Morse symbols) that are mixed over a
    /// group of letters, or they are enciphered in pairs.
    /// </summary>
    public static class FractionationCiphers
    {
        /// <summary>The second square of the CM Bifid cipher: the key word is written down the first column, up the second, down the third, ...</summary>
        public static string AlternatingVerticals(string key)
        {
            string letters = CipherText.KeyedSquare(key);
            var square = new char[25];
            for (int i = 0; i < 25; i++)
            {
                int column = i / 5, row = column % 2 == 0 ? i % 5 : 4 - i % 5;
                square[5 * row + column] = letters[i];
            }
            return new string(square);
        }

        /// <summary>
        /// CM Bifid (conjugated matrix bifid): the Bifid cipher with two squares. The plaintext letters are looked up in the first square, the
        /// row and column numbers of every group of "period" letters are written one after the other, and every two numbers give a letter of the
        /// second square (the key word of which is written in alternating verticals). A period of 0 uses the whole text as one group.
        /// </summary>
        public static string CmBifid(string text, string key1, string key2, int period, bool decrypt)
        {
            string plainSquare = CipherText.KeyedSquare(key1), cipherSquare = AlternatingVerticals(key2);
            string letters = CipherText.SquareLetters(text);
            if (period <= 0) { period = Math.Max(1, letters.Length); }
            string from = decrypt ? cipherSquare : plainSquare, to = decrypt ? plainSquare : cipherSquare;
            var result = new StringBuilder();
            for (int start = 0; start < letters.Length; start += period)
            {
                string group = letters.Substring(start, Math.Min(period, letters.Length - start));
                var numbers = new List<int>();
                if (!decrypt)
                {
                    numbers.AddRange(group.Select(c => plainSquare.IndexOf(c) / 5));
                    numbers.AddRange(group.Select(c => plainSquare.IndexOf(c) % 5));
                    for (int i = 0; i < numbers.Count; i += 2) { result.Append(cipherSquare[5 * numbers[i] + numbers[i + 1]]); }
                }
                else
                {
                    foreach (char c in group) { int place = cipherSquare.IndexOf(c); numbers.Add(place / 5); numbers.Add(place % 5); }
                    for (int i = 0; i < group.Length; i++) { result.Append(plainSquare[5 * numbers[i] + numbers[group.Length + i]]); }
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Digrafid: two 27-place alphabets (key word 1 across the rows of a 3x9 table, key word 2 down the columns of a 9x3 table; # is the 27th
        /// symbol). A pair of letters gives three digits; the digits of "period" pairs are written under each other and taken by lines, and
        /// every three digits give a new pair. An odd text gets a #.
        /// </summary>
        public static string Digrafid(string text, string horizontalKey, string verticalKey, int period, bool decrypt)
        {
            const string symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#";
            string horizontal = CipherText.KeyedAlphabet(CipherText.Letters(horizontalKey), symbols), vertical = CipherText.KeyedAlphabet(CipherText.Letters(verticalKey), symbols);
            if (period < 1) { throw new ArgumentException("The period must be at least 1."); }
            string pairs = CipherText.Pad(new string((text ?? "").ToUpperInvariant().Where(c => symbols.IndexOf(c) >= 0).ToArray()), 2, '#');
            // the digits of a pair: column in the horizontal alphabet, (row of it, column in the vertical alphabet) and row in the vertical alphabet
            Func<char, char, int[]> digits = (a, b) =>
            {
                int h = horizontal.IndexOf(a), v = vertical.IndexOf(b);
                return new[] { h % 9, 3 * (h / 9) + v / 9, v % 9 };
            };
            Func<int[], string> letters = d => "" + horizontal[(d[1] / 3) * 9 + d[0]] + vertical[(d[1] % 3) * 9 + d[2]];
            var result = new StringBuilder();
            for (int start = 0; start < pairs.Length; start += 2 * period)
            {
                string group = pairs.Substring(start, Math.Min(2 * period, pairs.Length - start));
                int count = group.Length / 2;
                var trigits = Enumerable.Range(0, count).Select(i => digits(group[2 * i], group[2 * i + 1])).ToArray();
                var sequence = new List<int>();
                if (!decrypt)
                {
                    for (int line = 0; line < 3; line++) { sequence.AddRange(trigits.Select(t => t[line])); }
                    for (int i = 0; i < count; i++) { result.Append(letters(sequence.Skip(3 * i).Take(3).ToArray())); }
                }
                else
                {
                    foreach (var t in trigits) { sequence.AddRange(t); }
                    for (int i = 0; i < count; i++) { result.Append(letters(new[] { sequence[i], sequence[count + i], sequence[2 * count + i] })); }
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Fractionated Morse: the text in Morse code ('x' between letters, "xx" between words) is taken in groups of three symbols; each of
        /// the 26 possible groups (all but xxx) is a letter of the keyed alphabet in the order ... -, x of the symbols.
        /// </summary>
        public static string FractionatedMorse(string text, string key, bool decrypt)
        {
            string alphabet = CipherText.KeyedAlphabet(key);
            if (!decrypt)
            {
                string morse = CipherText.ToMorse(text);
                while (morse.Length % 3 != 0) { morse += "x"; }
                var result = new StringBuilder();
                for (int i = 0; i < morse.Length; i += 3)
                {
                    result.Append(alphabet[9 * CipherText.MorseSymbol(morse[i]) + 3 * CipherText.MorseSymbol(morse[i + 1]) + CipherText.MorseSymbol(morse[i + 2])]);
                }
                return result.ToString();
            }
            var symbols = new StringBuilder();
            foreach (char c in CipherText.Letters(text))
            {
                int index = alphabet.IndexOf(c);
                symbols.Append(CipherText.MorseChar(index / 9)).Append(CipherText.MorseChar(index / 3 % 3)).Append(CipherText.MorseChar(index % 3));
            }
            return CipherText.FromMorse(symbols.ToString());
        }

        /// <summary>
        /// Morbit: the text in Morse code is taken in pairs of symbols; the nine possible pairs get the digits 1 to 9 that a key word of nine
        /// letters gives (in the order ..  .-  .x  -.  --  -x  x.  x-  xx).
        /// </summary>
        public static string Morbit(string text, string key, bool decrypt)
        {
            string k = CipherText.Letters(key);
            if (k.Length != 9) { throw new ArgumentException("The key needs 9 letters."); }
            int[] digits = CipherText.KeyDigits(k);
            if (!decrypt)
            {
                string morse = CipherText.ToMorse(text);
                if (morse.Length % 2 == 1) { morse += "x"; }
                var result = new StringBuilder();
                for (int i = 0; i < morse.Length; i += 2) { result.Append(digits[3 * CipherText.MorseSymbol(morse[i]) + CipherText.MorseSymbol(morse[i + 1])]); }
                return result.ToString();
            }
            var symbols = new StringBuilder();
            foreach (char c in CipherText.Digits(text))
            {
                int pair = Array.IndexOf(digits, c - '0');
                if (pair < 0) { continue; }
                symbols.Append(CipherText.MorseChar(pair / 3)).Append(CipherText.MorseChar(pair % 3));
            }
            return CipherText.FromMorse(symbols.ToString());
        }

        /// <summary>
        /// Pollux: every digit stands for a dot, a dash or a separator. The key gives the symbol of the digits 1 2 3 4 5 6 7 8 9 0 (for example
        /// "x-..x.--x." with . - x). When there are several digits for a symbol they are used in turn.
        /// </summary>
        public static string Pollux(string text, string key, bool decrypt)
        {
            string k = new string((key ?? "").Replace('•', '.').Replace('–', '-').Replace('X', 'x').Where(c => c == '.' || c == '-' || c == 'x').ToArray());
            if (k.Length != 10) { throw new ArgumentException("The key gives the symbol (. - or x) of ten digits."); }
            Func<int, int> digitAt = i => (i + 1) % 10;     // digit 1 is the first symbol of the key, digit 0 the last
            if (!decrypt)
            {
                var result = new StringBuilder();
                var used = new Dictionary<char, int>();
                foreach (char symbol in CipherText.ToMorse(text))
                {
                    var digits = Enumerable.Range(0, 10).Where(i => k[i] == symbol).Select(digitAt).ToArray();
                    if (digits.Length == 0) { throw new ArgumentException("No digit of the key stands for the symbol " + symbol + "."); }
                    used.TryGetValue(symbol, out int count);
                    used[symbol] = count + 1;
                    result.Append(digits[count % digits.Length]);
                }
                return result.ToString();
            }
            var morse = new StringBuilder();
            foreach (char c in CipherText.Digits(text)) { morse.Append(k[(c - '0' + 9) % 10]); }
            return CipherText.FromMorse(morse.ToString());
        }

        /// <summary>
        /// Tri-Square: three Polybius squares (from three key words). The plaintext pair is looked up in squares 1 and 2; the first cipher letter
        /// is a letter of the column of the first one in square 1 (the rows are used in turn), the second the letter of square 3 in the row of
        /// the first and the column of the second plaintext letter, the third a letter of the row of the second one in square 2. An odd text gets an X.
        /// </summary>
        public static string TriSquare(string text, string key1, string key2, string key3, bool decrypt)
        {
            string s1 = CipherText.KeyedSquare(key1), s2 = CipherText.KeyedSquare(key2), s3 = CipherText.KeyedSquare(key3);
            var result = new StringBuilder();
            if (!decrypt)
            {
                string pairs = CipherText.Pad(CipherText.SquareLetters(text), 2);
                for (int i = 0; i < pairs.Length; i += 2)
                {
                    int a = s1.IndexOf(pairs[i]), b = s2.IndexOf(pairs[i + 1]);
                    result.Append(s1[5 * (i / 2 % 5) + a % 5]).Append(s3[5 * (a / 5) + b % 5]).Append(s2[5 * (b / 5) + (i / 2 + 2) % 5]);
                }
                return result.ToString();
            }
            string triples = CipherText.SquareLetters(text);
            for (int i = 0; i + 2 < triples.Length; i += 3)
            {
                int column1 = s1.IndexOf(triples[i]) % 5, place3 = s3.IndexOf(triples[i + 1]), row2 = s2.IndexOf(triples[i + 2]) / 5;
                result.Append(s1[5 * (place3 / 5) + column1]).Append(s2[5 * row2 + place3 % 5]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Seriated Playfair: the text is written in groups of two lines of "period" letters; every vertical pair is enciphered by the Playfair
        /// rules (same row: letter to the right; same column: letter below; else the other corners). If a vertical pair would be a double letter,
        /// an X is put into the lower line. The cipher text is the two lines of every group one after the other.
        /// </summary>
        public static string SeriatedPlayfair(string text, string key, int period, bool decrypt)
        {
            if (period < 1) { throw new ArgumentException("The period must be at least 1."); }
            string square = CipherText.KeyedSquare(key);
            string letters = CipherText.SquareLetters(text);
            var result = new StringBuilder();
            Func<char, char, string> playfair = (a, b) =>
            {
                int pa = square.IndexOf(a), pb = square.IndexOf(b), step = decrypt ? 4 : 1;
                if (pa == pb) { throw new ArgumentException("A pair of the same letter cannot be enciphered."); }
                if (pa / 5 == pb / 5) { return "" + square[5 * (pa / 5) + (pa % 5 + step) % 5] + square[5 * (pb / 5) + (pb % 5 + step) % 5]; }
                if (pa % 5 == pb % 5) { return "" + square[5 * ((pa / 5 + step) % 5) + pa % 5] + square[5 * ((pb / 5 + step) % 5) + pb % 5]; }
                return "" + square[5 * (pa / 5) + pb % 5] + square[5 * (pb / 5) + pa % 5];
            };
            if (decrypt)
            {
                for (int start = 0; start < letters.Length; start += 2 * period)
                {
                    int count = Math.Min(2 * period, letters.Length - start) / 2;
                    var pairs = Enumerable.Range(0, count).Select(i => playfair(letters[start + i], letters[start + count + i])).ToArray();
                    result.Append(string.Concat(pairs.Select(p => p[0]))).Append(string.Concat(pairs.Select(p => p[1])));
                }
                return result.ToString();
            }
            int position = 0;
            while (position < letters.Length)
            {
                int count = Math.Min(period, letters.Length - position);
                string top = letters.Substring(position, count);
                position += count;
                var bottom = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    if (position < letters.Length && letters[position] != top[i]) { bottom.Append(letters[position++]); }
                    else { bottom.Append(top[i] == 'X' ? 'Q' : 'X'); }
                }
                var pairs = Enumerable.Range(0, count).Select(i => playfair(top[i], bottom[i])).ToArray();
                result.Append(string.Concat(pairs.Select(p => p[0]))).Append(string.Concat(pairs.Select(p => p[1])));
            }
            return result.ToString();
        }
    }
}

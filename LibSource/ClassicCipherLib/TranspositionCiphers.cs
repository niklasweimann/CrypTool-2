using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>The paths through a rectangle of letters that a route transposition can write along and read along.</summary>
    public enum Route
    {
        Rows = 0, Columns, AlternatingRows, AlternatingColumns, Diagonals, AlternatingDiagonals,
        SpiralInClockwise, SpiralInCounterclockwise, SpiralOutClockwise, SpiralOutCounterclockwise
    }

    /// <summary>The transposition ciphers of the ACA: the letters stay the same, only their order changes.</summary>
    public static class TranspositionCiphers
    {
        /// <summary>
        /// The places of a text of the given length, written in rows of the given width, in the order in which the columns are read:
        /// the columns of the lowest rank first, from the top to the bottom. Columns of the same rank (Myszkowski) are read together,
        /// row by row, from left to right.
        /// </summary>
        public static List<int> ColumnOrder(int length, int[] ranks)
        {
            int width = ranks.Length, rows = (length + width - 1) / width;
            var order = new List<int>();
            foreach (int rank in ranks.Distinct().OrderBy(r => r))
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        if (ranks[column] == rank && row * width + column < length) { order.Add(row * width + column); }
                    }
                }
            }
            return order;
        }

        /// <summary>Writes the text by the order given (encryption: reads the places in this order; decryption: writes into them).</summary>
        private static string ByOrder(string letters, List<int> order, bool decrypt)
        {
            var result = new char[letters.Length];
            for (int i = 0; i < order.Count; i++)
            {
                if (!decrypt) { result[i] = letters[order[i]]; } else { result[order[i]] = letters[i]; }
            }
            return new string(result);
        }

        /// <summary>
        /// Columnar transposition: the text is written in rows under the key and taken off by columns in the alphabetical order of the key
        /// letters (or the numbers of a numeric key). A complete block is filled up with X; an incomplete block is not.
        /// With "tiesTogether" columns of equal key letters are read together row by row (Myszkowski).
        /// </summary>
        public static string Columnar(string text, string key, bool complete, bool tiesTogether, bool decrypt)
        {
            string k = CipherText.Required(new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()), "key");
            string letters = CipherText.Letters(text);
            if (complete && !decrypt) { letters = CipherText.Pad(letters, k.Length); }
            return ByOrder(letters, ColumnOrder(letters.Length, CipherText.Ranks(k, tiesTogether)), decrypt);
        }

        /// <summary>
        /// ABC cipher: the letters are enciphered in groups of three, the first unchanged, the second moved one letter on, the third two (this is
        /// a Vigenère cipher with the key ABC), then the result goes through an incomplete columnar transposition with the key word.
        /// </summary>
        public static string Abc(string text, string key, bool decrypt)
        {
            if (!decrypt) { return Columnar(PolyalphabeticCiphers.Periodic(text, "ABC", Table.Vigenere, false), key, false, false, false); }
            return PolyalphabeticCiphers.Periodic(Columnar(text, key, false, false, true), "ABC", Table.Vigenere, true);
        }

        /// <summary>
        /// AMSCO: like an incomplete columnar transposition, but every place of the block holds one letter or two letters alternately; the first
        /// place is a digraph or a single letter. The pattern alternates both along rows and down the columns.
        /// </summary>
        public static string Amsco(string text, string key, bool startWithDigraph, bool decrypt)
        {
            string k = CipherText.Required(new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()), "key");
            string letters = CipherText.Letters(text);
            int width = k.Length;
            // the cells of the block in reading order: row, column, start and length in the text
            var cells = new List<int[]>();
            for (int position = 0, cell = 0; position < letters.Length; cell++)
            {
                int size = ((cell / width + cell % width) % 2 == 0) == startWithDigraph ? 2 : 1;
                size = Math.Min(size, letters.Length - position);
                cells.Add(new[] { cell / width, cell % width, position, size });
                position += size;
            }
            int[] ranks = CipherText.Ranks(k);
            var inOrder = cells.OrderBy(c => ranks[c[1]]).ThenBy(c => c[0]).ToList();
            var result = new char[letters.Length];
            int next = 0;
            foreach (var cell in inOrder)
            {
                for (int i = 0; i < cell[3]; i++)
                {
                    if (!decrypt) { result[next++] = letters[cell[2] + i]; } else { result[cell[2] + i] = letters[next++]; }
                }
            }
            return new string(result);
        }

        private const string Alphabet25 = "ABCDEFGHIJKLMNOPQRSTUVXYZ";

        /// <summary>
        /// Cadenus: a columnar transposition of a block of 25 rows. A letter of the 25-letter alphabet (V and W together), going down from A, is
        /// attached to every row; every column starts at the row that has its key letter and goes on around the block.
        /// The text needs exactly 25 letters for every key letter.
        /// </summary>
        public static string Cadenus(string text, string key, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key).Replace('W', 'V'), "key");
            string letters = CipherText.Letters(text);
            int width = k.Length;
            if (letters.Length != 25 * width) { throw new ArgumentException("The text needs " + 25 * width + " letters (25 for every letter of the key), it has " + letters.Length + "."); }
            int[] ranks = CipherText.Ranks(k);
            var result = new char[letters.Length];
            for (int column = 0; column < width; column++)
            {
                int start = CipherText.Mod(-Alphabet25.IndexOf(k[column]), 25);
                for (int i = 0; i < 25; i++)
                {
                    // the plain column starts at its starting row in the cipher column
                    int plainPlace = CipherText.Mod(start + i, 25) * width + column;
                    int cipherPlace = i * width + ranks[column];
                    if (!decrypt) { result[cipherPlace] = letters[plainPlace]; } else { result[plainPlace] = letters[cipherPlace]; }
                }
            }
            return new string(result);
        }

        /// <summary>
        /// Nicodemus: a columnar transposition, then a Vigenère encipherment of every column with the key letter of its place in the sorted
        /// key, then the letters are taken off five at a time from every column in turn (what is left of a column at the end is taken as it is).
        /// </summary>
        public static string Nicodemus(string text, string key, bool decrypt)
        {
            string k = CipherText.Required(CipherText.Letters(key), "key");
            string letters = CipherText.Letters(text);
            int width = k.Length;
            int[] ranks = CipherText.Ranks(k);
            int[] columnAt = Enumerable.Range(0, width).OrderBy(c => ranks[c]).ToArray();   // plain column that is at this place of the block
            // the places (as index into the text) in the order in which the columns are taken off five at a time
            var columns = columnAt.Select(c => Enumerable.Range(0, (letters.Length + width - 1) / width).Select(r => r * width + c).Where(p => p < letters.Length).ToList()).ToList();
            var order = new List<int>();
            var keyLetter = new Dictionary<int, char>();
            for (int taken = 0; order.Count < letters.Length; taken += 5)
            {
                for (int c = 0; c < width; c++)
                {
                    foreach (int place in columns[c].Skip(taken).Take(5)) { order.Add(place); keyLetter[place] = k[columnAt[c]]; }
                }
            }
            var result = new char[letters.Length];
            for (int i = 0; i < order.Count; i++)
            {
                int shift = keyLetter[order[i]] - 'A';
                if (!decrypt) { result[i] = (char)('A' + (letters[order[i]] - 'A' + shift) % 26); }
                else { result[order[i]] = (char)('A' + CipherText.Mod(letters[i] - 'A' - shift, 26)); }
            }
            return new string(result);
        }

        /// <summary>
        /// Nihilist transposition: the text fills a square (as wide as the key is long); the columns and then the rows are moved by the key
        /// (the key says where every column and row goes to); the result is read by rows or by columns. The text is filled up with X.
        /// </summary>
        public static string NihilistTransposition(string text, string key, bool byColumns, bool decrypt)
        {
            string k = CipherText.Required(new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()), "key");
            int n = k.Length;
            int[] target = CipherText.Ranks(k);
            string letters = CipherText.Pad(CipherText.Letters(text), n * n);
            var result = new StringBuilder();
            for (int block = 0; block < letters.Length; block += n * n)
            {
                var square = new char[n, n];
                for (int i = 0; i < n * n; i++)
                {
                    int row = i / n, column = i % n;
                    if (!decrypt) { square[target[row], target[column]] = letters[block + i]; }
                    else
                    {
                        // the cipher text is read by rows or by columns from the moved square
                        int r = byColumns ? i % n : i / n, c = byColumns ? i / n : i % n;
                        square[r, c] = letters[block + i];
                    }
                }
                for (int i = 0; i < n * n; i++)
                {
                    int row = i / n, column = i % n;
                    if (!decrypt) { result.Append(byColumns ? square[column, row] : square[row, column]); }
                    else { result.Append(square[target[row], target[column]]); }
                }
            }
            return result.ToString();
        }

        /// <summary>The places (row, column) of a rectangle in the order of a route; all routes start at the top left corner.</summary>
        public static List<int[]> RoutePath(Route route, int rows, int columns)
        {
            var path = new List<int[]>();
            switch (route)
            {
                case Route.Rows:
                    for (int r = 0; r < rows; r++) for (int c = 0; c < columns; c++) path.Add(new[] { r, c });
                    break;
                case Route.Columns:
                    for (int c = 0; c < columns; c++) for (int r = 0; r < rows; r++) path.Add(new[] { r, c });
                    break;
                case Route.AlternatingRows:
                    for (int r = 0; r < rows; r++) for (int i = 0; i < columns; i++) path.Add(new[] { r, r % 2 == 0 ? i : columns - 1 - i });
                    break;
                case Route.AlternatingColumns:
                    for (int c = 0; c < columns; c++) for (int i = 0; i < rows; i++) path.Add(new[] { c % 2 == 0 ? i : rows - 1 - i, c });
                    break;
                case Route.Diagonals:
                case Route.AlternatingDiagonals:
                    for (int d = 0; d < rows + columns - 1; d++)
                    {
                        var diagonal = new List<int[]>();
                        for (int r = Math.Max(0, d - columns + 1); r <= Math.Min(d, rows - 1); r++) diagonal.Add(new[] { r, d - r });
                        // down to the left; the alternating route goes up to the right on every second diagonal
                        if (route == Route.AlternatingDiagonals && d % 2 == 0) { diagonal.Reverse(); }
                        path.AddRange(diagonal);
                    }
                    break;
                case Route.SpiralInClockwise:
                case Route.SpiralInCounterclockwise:
                    path = Spiral(rows, columns, route == Route.SpiralInClockwise);
                    break;
                default:
                    // outward from the middle is the way back of an inward spiral that turns the other way
                    path = Spiral(rows, columns, route == Route.SpiralOutCounterclockwise);
                    path.Reverse();
                    break;
            }
            return path;
        }

        private static List<int[]> Spiral(int rows, int columns, bool clockwise)
        {
            var path = new List<int[]>();
            int top = 0, bottom = rows - 1, left = 0, right = columns - 1;
            while (top <= bottom && left <= right)
            {
                if (clockwise)
                {
                    for (int c = left; c <= right; c++) path.Add(new[] { top, c });
                    for (int r = top + 1; r <= bottom; r++) path.Add(new[] { r, right });
                    if (top < bottom) for (int c = right - 1; c >= left; c--) path.Add(new[] { bottom, c });
                    if (left < right) for (int r = bottom - 1; r > top; r--) path.Add(new[] { r, left });
                }
                else
                {
                    for (int r = top; r <= bottom; r++) path.Add(new[] { r, left });
                    for (int c = left + 1; c <= right; c++) path.Add(new[] { bottom, c });
                    if (left < right) for (int r = bottom - 1; r >= top; r--) path.Add(new[] { r, right });
                    if (top < bottom) for (int c = right - 1; c > left; c--) path.Add(new[] { top, c });
                }
                top++; bottom--; left++; right--;
            }
            return path;
        }

        /// <summary>Route transposition: the text is written into a block of the given width along one route and read along another. The block is filled up with X.</summary>
        public static string RouteTransposition(string text, int columns, Route write, Route read, bool decrypt)
        {
            if (columns < 1) { throw new ArgumentException("The block needs at least one column."); }
            string letters = CipherText.Pad(CipherText.Letters(text), columns);
            int rows = letters.Length / columns;
            var from = RoutePath(decrypt ? read : write, rows, columns);
            var to = RoutePath(decrypt ? write : read, rows, columns);
            var block = new char[rows, columns];
            for (int i = 0; i < letters.Length; i++) { block[from[i][0], from[i][1]] = letters[i]; }
            return new string(to.Select(p => block[p[0], p[1]]).ToArray());
        }

        /// <summary>
        /// Sequence transposition: a key of ten letters gives the numbers 1 2 ... 9 0 to its columns; a primer of digits is extended to the length of
        /// the text by adding successive pairs of digits (mod 10). The plaintext letter at a position goes into the column that has the digit of
        /// this position; the columns are written one after the other from left to right.
        /// </summary>
        public static string Sequence(string text, string key, string primer, bool decrypt)
        {
            string k = CipherText.Letters(key);
            if (k.Length != 10) { throw new ArgumentException("The key needs 10 letters."); }
            int[] p = CipherText.Digits(primer).Select(c => c - '0').ToArray();
            if (p.Length < 2) { throw new ArgumentException("The primer needs at least two digits."); }
            string letters = CipherText.Letters(text);
            var sequence = new int[Math.Max(letters.Length, p.Length)];
            Array.Copy(p, sequence, p.Length);
            for (int i = p.Length; i < sequence.Length; i++) { sequence[i] = (sequence[i - p.Length] + sequence[i - p.Length + 1]) % 10; }
            // the places in the order of the columns
            var order = new List<int>();
            foreach (int digit in CipherText.KeyDigits(k))
            {
                for (int i = 0; i < letters.Length; i++) { if (sequence[i] == digit) { order.Add(i); } }
            }
            return ByOrder(letters, order, decrypt);
        }

        /// <summary>
        /// Swagman: the text is written into a rectangle with as many rows as the key square has; the key square (a Latin square of the numbers
        /// 1 to n, rows separated by spaces) is repeated along the columns. Every letter moves within its column to the row that the number of the
        /// key square says. The result is read by columns. The text is filled up with X.
        /// </summary>
        public static string Swagman(string text, string keySquare, bool decrypt)
        {
            var rows = (keySquare ?? "").Split(new[] { ' ', ',', ';', '\n', '\r', '/' }, StringSplitOptions.RemoveEmptyEntries).Select(r => CipherText.Digits(r)).Where(r => r.Length > 0).ToArray();
            int n = rows.Length;
            if (n < 2 || n > 9 || rows.Any(r => r.Length != n)) { throw new ArgumentException("The key square has n rows of n digits each (n from 2 to 9), separated by spaces."); }
            var square = rows.Select(r => r.Select(c => c - '1').ToArray()).ToArray();
            for (int i = 0; i < n; i++)
            {
                if (square[i].Distinct().Count() != n || square[i].Any(v => v < 0 || v >= n) || Enumerable.Range(0, n).Select(r => square[r][i]).Distinct().Count() != n)
                {
                    throw new ArgumentException("No number may repeat in a row or a column of the key square (it uses the numbers 1 to " + n + ").");
                }
            }
            string letters = CipherText.Pad(CipherText.Letters(text), n);
            int width = letters.Length / n;
            var result = new char[letters.Length];
            for (int row = 0; row < n; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int moved = square[row][column % n];
                    // plain text lies by rows, cipher text by columns (of the rectangle)
                    int plainPlace = row * width + column, cipherPlace = column * n + moved;
                    if (!decrypt) { result[cipherPlace] = letters[plainPlace]; } else { result[plainPlace] = letters[cipherPlace]; }
                }
            }
            return new string(result);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicCiphers
{
    /// <summary>Hand ciphers of the German forces in the Second World War (pencil and paper, key tables on printed forms).</summary>
    public static class HandCiphers
    {
        /// <summary>The grid of the Rasterschlüssel 44: one line per row, '#' for a black cell, '.' for a cell that can be written in.</summary>
        public static bool[][] ParseGrid(string grid)
        {
            var rows = (grid ?? "").Split(new[] { '\n', '\r', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).Where(r => r.Length > 0).ToArray();
            if (rows.Length == 0) { throw new ArgumentException("The grid is empty. It has one line for each row: # for a black cell, . for a white one."); }
            int width = rows[0].Length;
            if (rows.Any(r => r.Length != width)) { throw new ArgumentException("All rows of the grid must have the same number of cells."); }
            return rows.Select(r => r.Select(c => c == '.' || c == '_' || c == 'o' || c == 'O').ToArray()).ToArray();
        }

        /// <summary>
        /// Rasterschlüssel 44: the plaintext is written row by row into the white cells of the grid, starting at the cell (row, column) or at the first white
        /// cell after it, and goes on in the next row at the left. The grid is read column by column: the columns have the numbers 1 to n (printed above them),
        /// reading starts at the column with the number "readNumber" and goes on with the next numbers (after n comes 1). The ciphertext has as many letters
        /// as the plaintext. The encoding of the start cell and the reading column by the letter swap table (the message header) is not part of this.
        /// </summary>
        public static string Raster44(string text, string grid, string columnNumbers, int startRow, int startColumn, int readNumber, bool decrypt)
        {
            var cells = ParseGrid(grid);
            int rows = cells.Length, columns = cells[0].Length;
            var numbers = CipherText.Numbers(columnNumbers);
            if (numbers.Count != columns || numbers.Distinct().Count() != columns || numbers.Any(n => n < 1 || n > columns))
            {
                throw new ArgumentException("The column numbers are a list of the numbers 1 to " + columns + ", one for each column of the grid.");
            }
            if (startRow < 1 || startRow > rows || startColumn < 1 || startColumn > columns) { throw new ArgumentException("The start cell is outside the grid."); }
            if (readNumber < 1 || readNumber > columns) { throw new ArgumentException("The number of the reading column must be between 1 and " + columns + "."); }
            string letters = CipherText.Letters(text);
            // the cells in the order of writing
            var written = new List<int[]>();
            for (int row = startRow - 1, column = startColumn - 1; row < rows && written.Count < letters.Length; column++)
            {
                if (column == columns) { column = 0; row++; if (row == rows) { break; } }
                if (cells[row][column]) { written.Add(new[] { row, column }); }
            }
            if (written.Count < letters.Length) { throw new ArgumentException("The text is too long: from the start cell the grid has room for " + written.Count + " letters."); }
            // the cells in the order of reading
            var read = new List<int[]>();
            for (int k = 0; k < columns; k++)
            {
                int number = (readNumber - 1 + k) % columns + 1, column = numbers.IndexOf(number);
                read.AddRange(written.Where(w => w[1] == column).OrderBy(w => w[0]));
            }
            var result = new char[letters.Length];
            var position = written.Select((w, i) => new { w, i }).ToDictionary(x => x.w[0] * columns + x.w[1], x => x.i);
            for (int i = 0; i < read.Count; i++)
            {
                int writtenIndex = position[read[i][0] * columns + read[i][1]];
                if (!decrypt) { result[i] = letters[writtenIndex]; } else { result[writtenIndex] = letters[i]; }
            }
            return new string(result);
        }

        /// <summary>The table of the Werftschlüssel: pairs written as AB=CD (any separators); every pair works in both directions.</summary>
        private static Dictionary<string, string> ParseSwapTable(string table)
        {
            var map = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches((table ?? "").ToUpperInvariant(), @"([A-Z]{2})\s*=\s*([A-Z]{2})"))
            {
                string a = m.Groups[1].Value, b = m.Groups[2].Value;
                foreach (var pair in new[] { new[] { a, b }, new[] { b, a } })
                {
                    if (map.TryGetValue(pair[0], out string existing) && existing != pair[1]) { throw new ArgumentException("The table is not consistent: " + pair[0] + " is replaced by both " + existing + " and " + pair[1] + "."); }
                    map[pair[0]] = pair[1];
                }
            }
            if (map.Count == 0) { throw new ArgumentException("The table has no entries. Write the pairs like KV=TC, separated by spaces or lines."); }
            return map;
        }

        /// <summary>
        /// Werftschlüssel (Reservehandverfahren of the navy): the plaintext is cut into groups of five letters (filled up with X to an even number of groups) and the
        /// groups are put into the rows of a box with five columns in the order of "rowOrder" (the k-th group goes into the row with the k-th number). Two rows
        /// at a time are taken column by column as pairs of letters, every pair is replaced by another pair from the double-letter table (the table works in both
        /// directions, so the same table decrypts), and the first letters of the new pairs form one group, the second letters the next one.
        /// Without a row order the odd rows come first, then the even rows (1 3 5 2 4 6 for six groups).
        /// </summary>
        public static string Werftschluessel(string text, string table, string rowOrder, bool decrypt)
        {
            var swap = ParseSwapTable(table);
            string letters = CipherText.Letters(text);
            if (!decrypt)
            {
                letters = CipherText.Pad(letters, 5);
                if ((letters.Length / 5) % 2 == 1) { letters += "XXXXX"; }
            }
            else if (letters.Length % 10 != 0) { throw new ArgumentException("The ciphertext needs a multiple of 10 letters (two groups of five for every two rows)."); }
            int groups = letters.Length / 5;
            var order = CipherText.Numbers(rowOrder);
            if (order.Count == 0) { order = Enumerable.Range(1, groups).Where(i => i % 2 == 1).Concat(Enumerable.Range(1, groups).Where(i => i % 2 == 0)).ToList(); }
            if (order.Count != groups || order.Distinct().Count() != groups || order.Any(r => r < 1 || r > groups))
            {
                throw new ArgumentException("The row order must list each of the numbers 1 to " + groups + " once (the text has " + groups + " groups of five letters).");
            }
            var result = new StringBuilder();
            Func<string, string, string> replace = (a, b) =>
            {
                string pair = "" + a + b;
                if (!swap.TryGetValue(pair, out string replacement)) { throw new ArgumentException("The table has no entry for the pair " + pair + "."); }
                return replacement;
            };
            if (!decrypt)
            {
                var rows = new string[groups];
                for (int k = 0; k < groups; k++) { rows[order[k] - 1] = letters.Substring(5 * k, 5); }
                for (int pair = 0; pair < groups; pair += 2)
                {
                    var replaced = Enumerable.Range(0, 5).Select(c => replace(rows[pair][c].ToString(), rows[pair + 1][c].ToString())).ToArray();
                    result.Append(string.Concat(replaced.Select(r => r[0]))).Append(string.Concat(replaced.Select(r => r[1])));
                }
                return result.ToString();
            }
            var plainRows = new string[groups];
            for (int pair = 0; pair < groups; pair += 2)
            {
                string first = letters.Substring(5 * pair, 5), second = letters.Substring(5 * pair + 5, 5);
                var replaced = Enumerable.Range(0, 5).Select(c => replace(first[c].ToString(), second[c].ToString())).ToArray();
                plainRows[pair] = string.Concat(replaced.Select(r => r[0]));
                plainRows[pair + 1] = string.Concat(replaced.Select(r => r[1]));
            }
            for (int k = 0; k < groups; k++) { result.Append(plainRows[order[k] - 1]); }
            return result.ToString();
        }
    }
}

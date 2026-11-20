using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicCiphers
{
    /// <summary>A table of a picture: cells of text, some of them marked (yellow: used, green: result). Free of any user interface code.</summary>
    public class TraceSection
    {
        public string Title;
        public string Note;
        public string[][] Cells = new string[0][];
        public int HeaderRows, HeaderColumns;
        public readonly HashSet<string> Marked = new HashSet<string>();
        public readonly HashSet<string> Result = new HashSet<string>();

        public static string Key(int row, int column) => row + "," + column;
    }

    /// <summary>What a component shows while it runs: a title, an explanation and the tables of the calculation (the key tables and the letter by letter steps).</summary>
    public class CipherTrace
    {
        public string Title;
        public string Summary;
        public readonly List<TraceSection> Sections = new List<TraceSection>();
    }

    /// <summary>Builds the tables of the pictures. The texts of the pictures are English or German (lang is "de" or anything else).</summary>
    public static class TraceBuilder
    {
        public static string T(string lang, string en, string de) => lang == "de" ? de : en;

        public static string Language() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        /// <summary>A table of rows (the first rows and columns are headers).</summary>
        public static TraceSection Block(string title, string note, string[][] cells, int headerRows = 0, int headerColumns = 0)
        {
            return new TraceSection { Title = title, Note = note, Cells = cells, HeaderRows = headerRows, HeaderColumns = headerColumns };
        }

        /// <summary>A square (Polybius, keyed alphabet in rows) with the numbers 1 to n at the top and at the left; the letters in "used" are marked.</summary>
        public static TraceSection Square(string title, string note, string letters, int columns, string used = "", string[] rowLabels = null, string[] columnLabels = null)
        {
            int rows = (letters.Length + columns - 1) / columns;
            var cells = new string[rows + 1][];
            cells[0] = new string[columns + 1];
            cells[0][0] = "";
            for (int c = 0; c < columns; c++) { cells[0][c + 1] = columnLabels != null ? columnLabels[c] : (c + 1).ToString(); }
            var section = new TraceSection { Title = title, Note = note, Cells = cells, HeaderRows = 1, HeaderColumns = 1 };
            for (int r = 0; r < rows; r++)
            {
                cells[r + 1] = new string[columns + 1];
                cells[r + 1][0] = rowLabels != null ? rowLabels[r] : (r + 1).ToString();
                for (int c = 0; c < columns; c++)
                {
                    int index = r * columns + c;
                    string letter = index < letters.Length ? letters[index].ToString() : "";
                    cells[r + 1][c + 1] = letter == "I" && columns == 5 && letters.Length == 25 ? "I/J" : letter;
                    if (letter.Length > 0 && used.IndexOf(letter[0]) >= 0) { section.Marked.Add(TraceSection.Key(r + 1, c + 1)); }
                }
            }
            return section;
        }

        /// <summary>A row of letters under a row of 1..26 or A..Z (an alphabet), as a table with labelled rows.</summary>
        public static TraceSection Rows(string title, string note, string[] labels, string[] rows)
        {
            var cells = new string[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                cells[r] = new string[rows[r].Length + 1];
                cells[r][0] = labels[r];
                for (int c = 0; c < rows[r].Length; c++) { cells[r][c + 1] = rows[r][c].ToString(); }
            }
            return new TraceSection { Title = title, Note = note, Cells = cells, HeaderColumns = 1 };
        }

        /// <summary>The steps letter by letter: a table with one column for every step and one row for every kind of value (plain letter, key, cipher letter, ...). Long texts are cut into several tables.</summary>
        public static IEnumerable<TraceSection> Steps(string title, string[] labels, IList<string>[] values, int chunk = 18, int maxChunks = 4)
        {
            int count = values.Length == 0 ? 0 : values.Max(v => v.Count);
            for (int start = 0, part = 0; start < count && part < maxChunks; start += chunk, part++)
            {
                int length = Math.Min(chunk, count - start);
                var cells = new string[values.Length][];
                for (int r = 0; r < values.Length; r++)
                {
                    cells[r] = new string[length + 1];
                    cells[r][0] = labels[r];
                    for (int c = 0; c < length; c++) { cells[r][c + 1] = start + c < values[r].Count ? values[r][start + c] : ""; }
                }
                var section = new TraceSection { Title = part == 0 ? title : null, Cells = cells, HeaderColumns = 1 };
                for (int c = 1; c <= length; c++) { section.Result.Add(TraceSection.Key(values.Length - 1, c)); }
                yield return section;
            }
        }

        /// <summary>Each character of a text as an item (letters of the text; other characters are left out when onlyLetters is set).</summary>
        public static List<string> Characters(string text, bool onlyLetters = true)
        {
            return (text ?? "").ToUpperInvariant().Where(c => !onlyLetters || (c >= 'A' && c <= 'Z')).Select(c => c.ToString()).ToList();
        }

        /// <summary>The text cut into items of the given length.</summary>
        public static List<string> Pieces(string text, int length)
        {
            var result = new List<string>();
            for (int i = 0; i < (text ?? "").Length; i += length) { result.Add(text.Substring(i, Math.Min(length, text.Length - i))); }
            return result;
        }

        /// <summary>A trace with the plain text and the result of the steps, as far as nothing else is known about the cipher.</summary>
        public static CipherTrace Generic(string title, string input, string output, bool decrypt, string lang, string explanation = null)
        {
            var trace = new CipherTrace { Title = title, Summary = explanation };
            string from = T(lang, decrypt ? "Cipher text" : "Plain text", decrypt ? "Geheimtext" : "Klartext");
            string to = T(lang, decrypt ? "Plain text" : "Cipher text", decrypt ? "Klartext" : "Geheimtext");
            var a = Characters(input);
            var b = Characters(output);
            if (a.Count == b.Count && a.Count > 0)
            {
                trace.Sections.AddRange(Steps(T(lang, "Letter by letter", "Buchstabe für Buchstabe"), new[] { from, to }, new IList<string>[] { a, b }));
            }
            else
            {
                trace.Sections.Add(Block(T(lang, "Result", "Ergebnis"), null, new[] { new[] { from, input ?? "" }, new[] { to, output ?? "" } }, 0, 1));
            }
            return trace;
        }
    }
}

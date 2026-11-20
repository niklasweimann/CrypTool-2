using System;
using System.Collections.Generic;
using System.Linq;
using static ClassicCiphers.TraceBuilder;

namespace ClassicCiphers
{
    /// <summary>
    /// The pictures of the classic cipher components: for each cipher the key tables (squares, alphabets, the box) and the steps letter by letter.
    /// The tables are made from the same keys as the cipher itself and from its result. Ciphers without a special picture use <see cref="TraceBuilder.Generic"/>.
    /// </summary>
    public static class CipherTraces
    {
        private static CipherTrace Start(string title, string lang, string en, string de) => new CipherTrace { Title = title, Summary = T(lang, en, de) };

        private static string From(string lang, bool decrypt) => T(lang, decrypt ? "Cipher letter" : "Plain letter", decrypt ? "Geheimtextbuchstabe" : "Klartextbuchstabe");

        private static string To(string lang, bool decrypt) => T(lang, decrypt ? "Plain letter" : "Cipher letter", decrypt ? "Klartextbuchstabe" : "Geheimtextbuchstabe");

        private static string Step(string lang) => T(lang, "Step by step", "Schritt für Schritt");

        // ------------------------------------------------------------------------------------------------ Polybius square and tap code
        private static CipherTrace Coordinates(string title, string lang, string square, string used, string input, string output, bool decrypt, bool tap)
        {
            var trace = Start(title, lang,
                tap ? "The tap code is a Polybius square: a letter is knocked as the number of its row, a pause, and the number of its column (C and K share a cell)."
                    : "Every letter is replaced by the number of its row and of its column in the square (I and J share a cell).",
                tap ? "Der Klopfcode ist ein Polybios-Quadrat: Ein Buchstabe wird als Zahl der Zeile, eine Pause und Zahl der Spalte geklopft (C und K teilen sich ein Feld)."
                    : "Jeder Buchstabe wird durch die Nummer seiner Zeile und seiner Spalte im Quadrat ersetzt (I und J teilen sich ein Feld).");
            trace.Sections.Add(Square(T(lang, "The square (row, column)", "Das Quadrat (Zeile, Spalte)"), null, square, 5, used));
            var pairs = decrypt ? Pieces(CipherText.Digits(input), 2) : (output ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var letters = decrypt ? Characters(output) : Characters(input);
            trace.Sections.AddRange(Steps(Step(lang), new[] { T(lang, "Letter", "Buchstabe"), T(lang, "Row, column", "Zeile, Spalte") }, new IList<string>[] { letters, pairs }));
            return trace;
        }

        public static CipherTrace Polybius(string title, string key, string input, string output, bool decrypt, string lang)
        {
            string used = decrypt ? CipherText.SquareLetters(output) : CipherText.SquareLetters(input);
            return Coordinates(title, lang, CipherText.KeyedSquare(key), used, input, output, decrypt, false);
        }

        public static CipherTrace TapCode(string title, string input, string output, bool decrypt, string lang)
        {
            string used = (decrypt ? CipherText.Letters(output) : CipherText.Letters(input)).Replace('K', 'C');
            return Coordinates(title, lang, SubstitutionCiphers.KnockSquare, used, input, output, decrypt, true);
        }

        // ------------------------------------------------------------------------------------------------ Phillips
        public static CipherTrace Phillips(string title, string key, bool rc, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Eight 5x5 squares are used in turn, five letters with each. Every letter is replaced by the letter diagonally down to the right of it (the square is closed into a ring). The squares are made from the first one by moving rows" + (rc ? " and columns" : "") + ".",
                "Acht 5x5-Quadrate werden nacheinander benutzt, mit jedem fünf Buchstaben. Jeder Buchstabe wird durch den schräg rechts darunter ersetzt (das Quadrat ist zu einem Ring geschlossen). Die Quadrate entstehen aus dem ersten durch Verschieben von Zeilen" + (rc ? " und Spalten" : "") + ".");
            string plain = decrypt ? CipherText.SquareLetters(output) : CipherText.SquareLetters(input);
            var squares = PolyalphabeticCiphers.PhillipsSquares(key, rc);
            for (int i = 0; i < Math.Min(8, (plain.Length + 4) / 5); i++)
            {
                string block = plain.Substring(5 * i, Math.Min(5, plain.Length - 5 * i));
                trace.Sections.Add(Square(T(lang, "Square ", "Quadrat ") + (i + 1), null, string.Concat(squares[i]), 5, block));
            }
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { Characters(input), Characters(output) }));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ Quagmire
        public static CipherTrace Quagmire(string title, int type, string input, string key, string indicator, string cipherKey, char under, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The plain alphabet (top) is run against the cipher alphabet that the letters of the indicator select: the indicator letter stands under the plain letter " + char.ToUpperInvariant(under) + ". The period is the length of the indicator.",
                "Das Klartextalphabet (oben) läuft gegen das Geheimtextalphabet, das die Buchstaben des Indikators wählen: Der Indikatorbuchstabe steht unter dem Klartextbuchstaben " + char.ToUpperInvariant(under) + ". Die Periode ist die Länge des Indikators.");
            string plainAlphabet, cipherAlphabet;
            PolyalphabeticCiphers.QuagmireAlphabets(type, key, cipherKey, out plainAlphabet, out cipherAlphabet);
            string ind = CipherText.Letters(indicator);
            if (ind.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            int underPosition = plainAlphabet.IndexOf(char.ToUpperInvariant(under));
            var labels = new List<string> { T(lang, "Plain", "Klar") };
            var rows = new List<string> { plainAlphabet };
            foreach (char c in ind.Distinct())
            {
                labels.Add(c.ToString());
                rows.Add(new string(Enumerable.Range(0, 26).Select(i => cipherAlphabet[CipherText.Mod(cipherAlphabet.IndexOf(c) - underPosition + i, 26)]).ToArray()));
            }
            var table = Rows(T(lang, "The alphabets", "Die Alphabete"), null, labels.ToArray(), rows.ToArray());
            table.HeaderRows = 1;
            string plain = CipherText.Letters(decrypt ? output : input);
            var distinct = ind.Distinct().ToList();
            for (int i = 0; i < plain.Length; i++)
            {
                table.Result.Add(TraceSection.Key(1 + distinct.IndexOf(ind[i % ind.Length]), 1 + plainAlphabet.IndexOf(plain[i])));
            }
            trace.Sections.Add(table);
            var indicatorRow = Enumerable.Range(0, CipherText.Letters(input).Length).Select(i => ind[i % ind.Length].ToString()).ToList();
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Indicator", "Indikator"), To(lang, decrypt) }, new IList<string>[] { Characters(input), indicatorRow, Characters(output) }));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ Gromark
        public static CipherTrace Gromark(string title, string key, string primer, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A mixed cipher alphabet (the columns of the key word's block, taken in alphabetical order) and a running key of digits: every digit says how many places to the right of the plain letter the cipher letter stands. The digits are the primer, then the sum of two digits at a time.",
                "Ein gemischtes Geheimtextalphabet (die Spalten des Blocks des Schlüsselworts in alphabetischer Reihenfolge) und ein laufender Ziffernschlüssel: Jede Ziffer sagt, wie viele Stellen rechts vom Klartextbuchstaben der Geheimtextbuchstabe steht. Die Ziffern sind der Primer, dann die Summe je zweier Ziffern.");
            string alphabet = PolyalphabeticCiphers.TranspositionAlphabet(CipherText.Distinct(CipherText.Letters(key)));
            trace.Sections.Add(Rows(T(lang, "The alphabets", "Die Alphabete"), null, new[] { T(lang, "Plain", "Klar"), T(lang, "Cipher", "Geheim") }, new[] { CipherText.Alphabet, alphabet }));
            var digits = PolyalphabeticCiphers.RunningDigits(CipherText.Digits(primer).Select(c => c - '0').ToArray(), CipherText.Letters(input).Length);
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Digit", "Ziffer"), To(lang, decrypt) },
                new IList<string>[] { Characters(input), digits.Take(CipherText.Letters(input).Length).Select(d => d.ToString()).ToList(), Characters(output) }));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ Condi and Ragbaby
        public static CipherTrace Condi(string title, string key, int shift, int offset, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A keyed alphabet is the table. The step from a plaintext letter to its substitute is the position (1 to 26) of the plaintext letter before it; the first letter uses the start value.",
                "Ein gemischtes Alphabet ist die Tabelle. Die Schrittweite vom Klartextbuchstaben zu seinem Ersatz ist die Position (1 bis 26) des vorhergehenden Klartextbuchstabens; der erste Buchstabe benutzt den Startwert.");
            string alphabet = CipherText.RotateRight(CipherText.KeyedAlphabet(key), shift);
            var cells = new[] { Enumerable.Range(1, 26).Select(i => i.ToString()).ToArray(), alphabet.Select(c => c.ToString()).ToArray() };
            trace.Sections.Add(Block(T(lang, "The alphabet with its positions", "Das Alphabet mit seinen Positionen"), null, new[] { new[] { "#" }.Concat(cells[0]).ToArray(), new[] { "" }.Concat(cells[1]).ToArray() }, 1, 1));
            var plain = Characters(decrypt ? output : input);
            var steps = new List<string>();
            int previous = offset;
            foreach (var letter in plain) { steps.Add(previous.ToString()); previous = alphabet.IndexOf(letter[0]) + 1; }
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Step", "Schritt"), To(lang, decrypt) },
                new IList<string>[] { Characters(input), steps, Characters(output) }));
            return trace;
        }

        public static CipherTrace Ragbaby(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A 24-letter keyed alphabet (I/J and W/X together). Every letter moves to the right by its number: 1 for the first letter of the first word, 2 for the second letter, ...; every word starts with the number of the word.",
                "Ein gemischtes Alphabet aus 24 Buchstaben (I/J und W/X zusammen). Jeder Buchstabe rückt um seine Nummer nach rechts: 1 für den ersten Buchstaben des ersten Wortes, 2 für den zweiten, ...; jedes Wort beginnt mit der Nummer des Wortes.");
            string alphabet = CipherText.KeyedAlphabet((key ?? "").ToUpperInvariant().Replace('J', 'I').Replace('X', 'W'), "ABCDEFGHIKLMNOPQRSTUVWYZ");
            trace.Sections.Add(Rows(T(lang, "The alphabet", "Das Alphabet"), null, new[] { "" }, new[] { alphabet }));
            var numbers = new List<string>();
            int word = 0, number = 0;
            bool inWord = false;
            foreach (char c in (input ?? "").ToUpperInvariant())
            {
                if (char.IsWhiteSpace(c)) { inWord = false; continue; }
                if (!inWord) { inWord = true; word++; number = word - 1; }
                if (c >= 'A' && c <= 'Z') { number++; numbers.Add(number.ToString()); }
            }
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Number", "Nummer"), To(lang, decrypt) }, new IList<string>[] { Characters(input), numbers, Characters(output) }));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ Headlines
        public static CipherTrace Headlines(string title, string hat, string key, string setting, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The mixed alphabet is made from the key word (written in a block as wide as the hat word, columns taken in the order of the hat letters). Every headline (line) uses the next letter of the setting word: the cipher alphabet starts with that letter.",
                "Das gemischte Alphabet entsteht aus dem Schlüsselwort (in einen Block so breit wie das Hutwort geschrieben, die Spalten in der Reihenfolge der Hutbuchstaben genommen). Jede Schlagzeile (Zeile) benutzt den nächsten Buchstaben des Einstellungswortes: Das Geheimtextalphabet beginnt mit diesem Buchstaben.");
            string mixed = PolyalphabeticCiphers.HeadlinesAlphabet(hat, key);
            string settings = CipherText.Letters(setting);
            var labels = new List<string> { T(lang, "Plain", "Klar") };
            var rows = new List<string> { mixed };
            int lines = (input ?? "").Split('\n').Length;
            for (int i = 0; i < Math.Min(lines, settings.Length == 0 ? 0 : 10); i++)
            {
                char s = settings[i % settings.Length];
                labels.Add((i + 1) + ": " + s);
                rows.Add(mixed.Substring(mixed.IndexOf(s)) + mixed.Substring(0, mixed.IndexOf(s)));
            }
            trace.Sections.Add(Rows(T(lang, "The alphabets of the headlines", "Die Alphabete der Schlagzeilen"), null, labels.ToArray(), rows.ToArray()));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ squares of the digraphic ciphers
        public static CipherTrace CmBifid(string title, string key1, string key2, int period, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The Bifid cipher with two squares: the letters are looked up in the first square, the row numbers and column numbers of a group of letters are written one after the other, and every two numbers give a letter of the second square.",
                "Die Bifid-Chiffre mit zwei Quadraten: Die Buchstaben werden im ersten Quadrat gesucht, die Zeilen- und Spaltennummern einer Gruppe von Buchstaben werden hintereinander geschrieben, und je zwei Zahlen ergeben einen Buchstaben des zweiten Quadrats.");
            string plain = CipherText.SquareLetters(decrypt ? output : input);
            trace.Sections.Add(Square(T(lang, "Plain square", "Klartextquadrat"), null, CipherText.KeyedSquare(key1), 5, plain));
            trace.Sections.Add(Square(T(lang, "Cipher square", "Geheimtextquadrat"), null, FractionationCiphers.AlternatingVerticals(key2), 5, CipherText.SquareLetters(decrypt ? input : output)));
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { Characters(input), Characters(output) }));
            return trace;
        }

        public static CipherTrace TriSquare(string title, string key1, string key2, string key3, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Three squares: the pair of plaintext letters is looked up in squares 1 and 2; the three cipher letters are a letter of the column of the first one (square 1), the letter of square 3 where row and column meet, and a letter of the row of the second one (square 2).",
                "Drei Quadrate: Das Klartextpaar wird in den Quadraten 1 und 2 gesucht; die drei Geheimtextbuchstaben sind ein Buchstabe der Spalte des ersten (Quadrat 1), der Buchstabe von Quadrat 3, an dem sich Zeile und Spalte treffen, und ein Buchstabe der Zeile des zweiten (Quadrat 2).");
            string plain = CipherText.SquareLetters(decrypt ? output : input);
            trace.Sections.Add(Square(T(lang, "Square 1", "Quadrat 1"), null, CipherText.KeyedSquare(key1), 5, plain.Where((c, i) => i % 2 == 0).Aggregate("", (a, c) => a + c)));
            trace.Sections.Add(Square(T(lang, "Square 2", "Quadrat 2"), null, CipherText.KeyedSquare(key2), 5, plain.Where((c, i) => i % 2 == 1).Aggregate("", (a, c) => a + c)));
            trace.Sections.Add(Square(T(lang, "Square 3", "Quadrat 3"), null, CipherText.KeyedSquare(key3), 5));
            return trace;
        }

        public static CipherTrace Checkerboard(string title, string square, string rowLabels, string columnLabels, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A Polybius square with a word at the left (rows) and one above (columns). A letter is replaced by the letter of its row and the letter of its column.",
                "Ein Polybios-Quadrat mit einem Wort links (Zeilen) und einem darüber (Spalten). Ein Buchstabe wird durch den Buchstaben seiner Zeile und den Buchstaben seiner Spalte ersetzt.");
            string rows = CipherText.Letters(rowLabels), columns = CipherText.Letters(columnLabels);
            if (rows.Length < 5 || columns.Length < 5) { return Generic(title, input, output, decrypt, lang); }
            trace.Sections.Add(Square(T(lang, "The square", "Das Quadrat"), null, CipherText.KeyedSquare(square), 5, CipherText.SquareLetters(decrypt ? output : input),
                rows.Substring(0, 5).Select(c => c.ToString()).ToArray(), columns.Substring(0, 5).Select(c => c.ToString()).ToArray()));
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { decrypt ? Pieces(CipherText.Letters(input), 2) : Characters(input), decrypt ? Characters(output) : Pieces(CipherText.Letters(output), 2) }));
            return trace;
        }

        public static CipherTrace MonomeDinome(string title, string boxKey, string numberKey, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A box of 3 rows and 8 columns. The letters of the top row are replaced by the digit of their column, the letters of the other rows by the digit of their row and then of their column.",
                "Ein Kasten mit 3 Zeilen und 8 Spalten. Die Buchstaben der obersten Zeile werden durch die Ziffer ihrer Spalte ersetzt, die der anderen Zeilen durch die Ziffer ihrer Zeile und dann ihrer Spalte.");
            string nk = CipherText.Letters(numberKey);
            if (nk.Length != 10) { return Generic(title, input, output, decrypt, lang); }
            var digits = CipherText.KeyDigits(nk);
            string box = CipherText.KeyedAlphabet(CipherText.Letters(boxKey).Replace('J', 'I').Replace('Z', 'Y'), "ABCDEFGHIKLMNOPQRSTUVWXY");
            trace.Sections.Add(Square(T(lang, "The box", "Der Kasten"), null, box, 8, CipherText.Letters(decrypt ? output : input).Replace('J', 'I').Replace('Z', 'Y'),
                new[] { "", digits[0].ToString(), digits[1].ToString() }, Enumerable.Range(2, 8).Select(i => digits[i].ToString()).ToArray()));
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { Characters(input), Characters(output) }));
            return trace;
        }

        public static CipherTrace Tridigital(string title, string alphabetKey, string numberKey, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The keyed alphabet fills a block of ten columns (the last stays empty). A letter is replaced by the digit above its column; the digit above the empty column separates the words.",
                "Das gemischte Alphabet füllt einen Block mit zehn Spalten (die letzte bleibt leer). Ein Buchstabe wird durch die Ziffer über seiner Spalte ersetzt; die Ziffer über der leeren Spalte trennt die Wörter.");
            string nk = CipherText.Letters(numberKey);
            if (nk.Length != 10) { return Generic(title, input, output, decrypt, lang); }
            var digits = CipherText.KeyDigits(nk);
            string alphabet = CipherText.KeyedAlphabet(CipherText.Letters(alphabetKey));
            var cells = new string[4][];
            cells[0] = new[] { "" }.Concat(digits.Select(d => d.ToString())).ToArray();
            for (int r = 0; r < 3; r++)
            {
                cells[r + 1] = new[] { "" }.Concat(Enumerable.Range(0, 10).Select(c => c < 9 && r * 9 + c < 26 ? alphabet[r * 9 + c].ToString() : "")).ToArray();
            }
            trace.Sections.Add(Block(T(lang, "The block with its digits", "Der Block mit seinen Ziffern"), null, cells, 1, 1));
            return trace;
        }


        // ------------------------------------------------------------------------------------------------ more tables
        public static CipherTrace Digrafid(string title, string horizontalKey, string verticalKey, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Two mixed alphabets of 27 symbols (the letters and #). A pair of letters gives three digits (column of the first letter, row-and-column part, row of the second letter); the digits of a group of pairs are written under each other, read by lines and turned back into pairs.",
                "Zwei gemischte Alphabete aus 27 Zeichen (den Buchstaben und #). Ein Buchstabenpaar ergibt drei Ziffern (Spalte des ersten Buchstabens, Zeilen-und-Spalten-Teil, Zeile des zweiten Buchstabens); die Ziffern einer Gruppe von Paaren werden untereinander geschrieben, zeilenweise gelesen und wieder zu Paaren gemacht.");
            const string symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#";
            string h = CipherText.KeyedAlphabet(CipherText.Letters(horizontalKey), symbols), v = CipherText.KeyedAlphabet(CipherText.Letters(verticalKey), symbols);
            trace.Sections.Add(Square(T(lang, "Horizontal alphabet (3 rows of 9)", "Waagerechtes Alphabet (3 Zeilen zu 9)"), null, h, 9));
            var cells = new string[10][];
            cells[0] = new[] { "", "1", "2", "3" };
            for (int g = 0; g < 9; g++) { cells[g + 1] = new[] { (g + 1).ToString(), v[g].ToString(), v[9 + g].ToString(), v[18 + g].ToString() }; }
            trace.Sections.Add(Block(T(lang, "Vertical alphabet (9 rows of 3)", "Senkrechtes Alphabet (9 Zeilen zu 3)"), null, cells, 1, 1));
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { Characters(input), Characters(output) }));
            return trace;
        }

        public static CipherTrace Compressocrat(string title, string key, int shift, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Every letter has a code of the digits 1, 2 and 3 (frequent letters are short). The digits are taken in groups of three, and every group is a letter of the keyed alphabet.",
                "Jeder Buchstabe hat einen Code aus den Ziffern 1, 2 und 3 (häufige Buchstaben sind kurz). Die Ziffern werden in Dreiergruppen genommen, und jede Gruppe ist ein Buchstabe des gemischten Alphabets.");
            string alphabet = CipherText.RotateRight(CipherText.KeyedAlphabet(key), shift);
            var triples = new List<string>();
            foreach (char a in "123") foreach (char b in "123") foreach (char c in "123") triples.Add("" + a + b + c);
            var codes = new[]
            {
                new[] { T(lang, "Letter", "Buchstabe") }.Concat(CipherText.Alphabet.Select(c => c.ToString())).ToArray(),
                new[] { "Code" }.Concat(SubstitutionCiphers.CompressocratCodes).ToArray()
            };
            trace.Sections.Add(Block(T(lang, "Codes of the letters", "Codes der Buchstaben"), null, codes, 1, 1));
            trace.Sections.Add(Block(T(lang, "The groups of three digits and their letters", "Die Dreiergruppen und ihre Buchstaben"), null,
                new[] { new[] { T(lang, "Group", "Gruppe") }.Concat(triples.Take(26)).ToArray(), new[] { T(lang, "Letter", "Buchstabe") }.Concat(alphabet.Select(c => c.ToString())).ToArray() }, 1, 1));
            return trace;
        }

        private static CipherTrace MorseSteps(string title, string lang, string en, string de, string morse, IList<string> parts, string partsLabel, IList<string> results, bool decrypt)
        {
            var trace = Start(title, lang, en, de);
            trace.Sections.Add(Block(T(lang, "The text in Morse code", "Der Text im Morsecode"), T(lang, "(. dot, - dash, x separator: x between letters, xx between words)", "(. Punkt, - Strich, x Trenner: x zwischen Buchstaben, xx zwischen Wörtern)"), new[] { new[] { morse } }));
            trace.Sections.AddRange(Steps(Step(lang), new[] { partsLabel, T(lang, decrypt ? "Plain letter" : "Cipher", decrypt ? "Klar" : "Geheim") }, new IList<string>[] { parts, results }));
            return trace;
        }

        public static CipherTrace FractionatedMorse(string title, string input, string output, bool decrypt, string lang)
        {
            string morse = decrypt ? "" : CipherText.ToMorse(input);
            while (!decrypt && morse.Length % 3 != 0) { morse += "x"; }
            var parts = decrypt ? Characters(input) : Pieces(morse, 3);
            return MorseSteps(title, lang, "The text in Morse code is taken in groups of three symbols; every group is a letter of the keyed alphabet.", "Der Text im Morsecode wird in Dreiergruppen genommen; jede Gruppe ist ein Buchstabe des gemischten Alphabets.",
                morse, parts, T(lang, decrypt ? "Cipher" : "Group of three", decrypt ? "Geheim" : "Dreiergruppe"), decrypt ? Characters(output) : Characters(output), decrypt);
        }

        public static CipherTrace Morbit(string title, string input, string output, bool decrypt, string lang)
        {
            string morse = decrypt ? "" : CipherText.ToMorse(input);
            if (!decrypt && morse.Length % 2 == 1) { morse += "x"; }
            var parts = decrypt ? Pieces(CipherText.Digits(input), 1) : Pieces(morse, 2);
            var results = decrypt ? Characters(output, false) : Pieces(CipherText.Digits(output), 1);
            return MorseSteps(title, lang, "The text in Morse code is taken in pairs of symbols; every pair is replaced by a digit from 1 to 9.", "Der Text im Morsecode wird in Zeichenpaare zerlegt; jedes Paar wird durch eine Ziffer von 1 bis 9 ersetzt.",
                morse, parts, T(lang, decrypt ? "Digit" : "Pair", decrypt ? "Ziffer" : "Paar"), results, decrypt);
        }

        public static CipherTrace Pollux(string title, string input, string output, bool decrypt, string lang)
        {
            string morse = decrypt ? "" : CipherText.ToMorse(input);
            var parts = decrypt ? Pieces(CipherText.Digits(input), 1) : Pieces(morse, 1);
            var results = decrypt ? Characters(output, false) : Pieces(CipherText.Digits(output), 1);
            return MorseSteps(title, lang, "Every symbol of the Morse code (dot, dash, separator) is replaced by one of several digits that stand for it.", "Jedes Zeichen des Morsecodes (Punkt, Strich, Trenner) wird durch eine von mehreren Ziffern ersetzt, die dafür stehen.",
                morse, parts, T(lang, decrypt ? "Digit" : "Symbol", decrypt ? "Ziffer" : "Zeichen"), results, decrypt);
        }

        public static CipherTrace ProgressiveKey(string title, string key, int index, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is enciphered twice: with the key word, and then with a key letter that moves on after every group of one period (A, then A plus the progression index, ...).",
                "Der Text wird zweimal verschlüsselt: mit dem Schlüsselwort und dann mit einem Schlüsselbuchstaben, der nach jeder Gruppe einer Periode weiterrückt (A, dann A plus der Fortschreitungsindex, ...).");
            string k = CipherText.Letters(key);
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            int n = CipherText.Letters(input).Length;
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Key letter", "Schlüsselbuchstabe"), T(lang, "Progressive letter", "Fortschreitender Buchstabe"), To(lang, decrypt) },
                new IList<string>[] { Characters(input), Enumerable.Range(0, n).Select(i => k[i % k.Length].ToString()).ToList(), Enumerable.Range(0, n).Select(i => ((char)('A' + CipherText.Mod(i / k.Length * index, 26))).ToString()).ToList(), Characters(output) }));
            return trace;
        }

        public static CipherTrace InterruptedKey(string title, string key, string interruptions, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The key word is interrupted: after the number of letters in the list it starts again from its first letter.",
                "Das Schlüsselwort wird unterbrochen: Nach der Zahl von Buchstaben der Liste beginnt es wieder beim ersten Buchstaben.");
            string k = CipherText.Letters(key);
            var lengths = CipherText.Numbers(interruptions);
            if (k.Length == 0 || lengths.Count == 0 || lengths.Any(l => l < 1 || l > k.Length)) { return Generic(title, input, output, decrypt, lang); }
            int n = CipherText.Letters(input).Length;
            var stream = new System.Text.StringBuilder();
            for (int segment = 0; stream.Length < n; segment++) { stream.Append(k, 0, lengths[segment % lengths.Count]); }
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Key letter", "Schlüsselbuchstabe"), To(lang, decrypt) },
                new IList<string>[] { Characters(input), stream.ToString().Take(n).Select(c => c.ToString()).ToList(), Characters(output) }));
            return trace;
        }

        public static CipherTrace SeriatedPlayfair(string title, string key, int period, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is written in groups of two lines; every vertical pair is enciphered with the Playfair rules in the square. The ciphertext is the two lines of every group one after the other.",
                "Der Text wird in Gruppen aus zwei Zeilen geschrieben; jedes senkrechte Paar wird mit den Playfair-Regeln im Quadrat verschlüsselt. Der Geheimtext besteht aus den beiden Zeilen jeder Gruppe nacheinander.");
            trace.Sections.Add(Square(T(lang, "The square", "Das Quadrat"), null, CipherText.KeyedSquare(key), 5, CipherText.SquareLetters(decrypt ? output : input)));
            var plain = CipherText.SquareLetters(decrypt ? output : input);
            var cipher = CipherText.Letters(decrypt ? input : output);
            var rows = new List<string[]>();
            for (int start = 0; start < cipher.Length && period > 0; start += 2 * period)
            {
                int count = Math.Min(2 * period, cipher.Length - start) / 2;
                rows.Add(new[] { T(lang, "Cipher", "Geheim") }.Concat(cipher.Substring(start, count).Select(c => c.ToString())).ToArray());
                rows.Add(new[] { "" }.Concat(cipher.Substring(start + count, count).Select(c => c.ToString())).ToArray());
            }
            if (rows.Count > 0) { trace.Sections.Add(Block(T(lang, "The groups of the ciphertext (two lines each)", "Die Gruppen des Geheimtextes (je zwei Zeilen)"), null, rows.ToArray(), 0, 1)); }
            return trace;
        }

        public static CipherTrace NumberedKey(string title, string key, int offset, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The key phrase, extended by the letters that are missing, is numbered; a letter has the numbers of all its places. Every plaintext letter is replaced by one of them (in turn).",
                "Der Schlüsselsatz, um die fehlenden Buchstaben erweitert, wird nummeriert; ein Buchstabe hat die Nummern aller seiner Stellen. Jeder Klartextbuchstabe wird durch eine davon ersetzt (der Reihe nach).");
            string extended = CipherText.Letters(key);
            if (extended.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            extended += new string(CipherText.Alphabet.Where(c => extended.IndexOf(c) < 0).ToArray());
            var table = new string[2][];
            table[0] = new[] { "#" }.Concat(Enumerable.Range(0, extended.Length).Select(n => n.ToString())).ToArray();
            table[1] = new[] { "" }.Concat(Enumerable.Range(0, extended.Length).Select(n => extended[(offset + n) % extended.Length].ToString())).ToArray();
            trace.Sections.Add(Block(T(lang, "The numbered key", "Der nummerierte Schlüssel"), null, table, 1, 1));
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), To(lang, decrypt) }, new IList<string>[] { decrypt ? Pieces(CipherText.Digits(input).Length % 2 == 0 ? CipherText.Digits(input) : "", 2) : Characters(input), decrypt ? Characters(output) : (output ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList() }));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ the remaining ciphers
        private static string[][] Letters2D(string letters, int width, string[] header, bool rowNumbers = true)
        {
            int rows = (letters.Length + width - 1) / width;
            var cells = new List<string[]>();
            if (header != null) { cells.Add(new[] { "" }.Concat(header).ToArray()); }
            for (int r = 0; r < rows; r++)
            {
                cells.Add(new[] { rowNumbers ? (r + 1).ToString() : "" }.Concat(Enumerable.Range(0, width).Select(c => r * width + c < letters.Length ? letters[r * width + c].ToString() : "")).ToArray());
            }
            return cells.ToArray();
        }

        public static CipherTrace Portax(string title, string key, string input, string output, string lang)
        {
            var trace = Start(title, lang,
                "The text is written in rows under the key word (filled up with X); two rows at a time are enciphered column by column with the slide that the key letter sets. The same operation decrypts.",
                "Der Text wird zeilenweise unter das Schlüsselwort geschrieben (mit X aufgefüllt); je zwei Zeilen werden spaltenweise mit dem Schieber verschlüsselt, den der Schlüsselbuchstabe einstellt. Dieselbe Operation entschlüsselt.");
            string k = CipherText.Letters(key);
            if (k.Length == 0) { return Generic(title, input, output, false, lang); }
            string a = CipherText.Pad(CipherText.Letters(input), 2 * k.Length), b = CipherText.Letters(output);
            var header = k.Select(c => c.ToString()).ToArray();
            trace.Sections.Add(Block(T(lang, "Text", "Text"), null, Letters2D(a, k.Length, header), 1, 1));
            trace.Sections.Add(Block(T(lang, "Result", "Ergebnis"), null, Letters2D(b, k.Length, header), 1, 1));
            return trace;
        }

        public static CipherTrace Slidefair(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is cut into pairs; every pair uses the next letter of the key word. The two letters are opposite corners of a rectangle in the table (top alphabet and the row of the key letter); the other corners are the cipher letters.",
                "Der Text wird in Paare zerlegt; jedes Paar benutzt den nächsten Buchstaben des Schlüsselworts. Die beiden Buchstaben sind gegenüberliegende Ecken eines Rechtecks in der Tabelle (oberes Alphabet und Zeile des Schlüsselbuchstabens); die anderen Ecken sind die Geheimtextbuchstaben.");
            string k = CipherText.Letters(key);
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            var from = Pieces(CipherText.Pad(CipherText.Letters(input), 2), 2);
            var to = Pieces(CipherText.Letters(output), 2);
            var keys = Enumerable.Range(0, from.Count).Select(i => k[i % k.Length].ToString()).ToList();
            trace.Sections.AddRange(Steps(Step(lang), new[] { T(lang, decrypt ? "Cipher pair" : "Plain pair", decrypt ? "Geheimtextpaar" : "Klartextpaar"), T(lang, "Key letter", "Schlüsselbuchstabe"), T(lang, decrypt ? "Plain pair" : "Cipher pair", decrypt ? "Klartextpaar" : "Geheimtextpaar") }, new IList<string>[] { from, keys, to }, 12));
            return trace;
        }

        public static CipherTrace PeriodicGromark(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The mixed cipher alphabet comes from the key word; the numbering of the key letters is the primer of the running key of digits. Every group of one period starts at the place of the next key letter in the cipher alphabet.",
                "Das gemischte Geheimtextalphabet kommt aus dem Schlüsselwort; die Nummerierung der Schlüsselbuchstaben ist der Primer des laufenden Ziffernschlüssels. Jede Gruppe einer Periode beginnt an der Stelle des nächsten Schlüsselbuchstabens im Geheimtextalphabet.");
            string distinct = CipherText.Distinct(CipherText.Letters(key));
            if (distinct.Length < 2) { return Generic(title, input, output, decrypt, lang); }
            string alphabet = PolyalphabeticCiphers.TranspositionAlphabet(distinct);
            trace.Sections.Add(Rows(T(lang, "The alphabets", "Die Alphabete"), null, new[] { T(lang, "Plain", "Klar"), T(lang, "Cipher", "Geheim") }, new[] { CipherText.Alphabet, alphabet }));
            int n = CipherText.Letters(input).Length;
            var digits = PolyalphabeticCiphers.RunningDigits(CipherText.KeyDigits(distinct), n);
            var starts = Enumerable.Range(0, n).Select(i => alphabet.IndexOf(distinct[i / distinct.Length % distinct.Length]).ToString()).ToList();
            trace.Sections.AddRange(Steps(Step(lang), new[] { From(lang, decrypt), T(lang, "Start of the group", "Start der Gruppe"), T(lang, "Digit", "Ziffer"), To(lang, decrypt) },
                new IList<string>[] { Characters(input), starts, digits.Take(n).Select(d => d.ToString()).ToList(), Characters(output) }));
            return trace;
        }

        public static CipherTrace Amsco(string title, string key, bool startWithDigraph, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is written in rows under the key; the places alternate between one letter and two letters (like a checkerboard). The columns are read in the order of the key.",
                "Der Text wird zeilenweise unter den Schlüssel geschrieben; die Plätze wechseln zwischen einem und zwei Buchstaben (wie ein Schachbrett). Die Spalten werden in der Reihenfolge des Schlüssels gelesen.");
            string k = new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            string letters = CipherText.Letters(decrypt ? output : input);
            int width = k.Length;
            var ranks = CipherText.Ranks(k);
            var rows = new List<string[]>();
            for (int position = 0, cell = 0; position < letters.Length; cell++)
            {
                if (cell % width == 0) { rows.Add(new string[width + 1]); rows[rows.Count - 1][0] = rows.Count.ToString(); }
                int size = ((cell / width + cell % width) % 2 == 0) == startWithDigraph ? 2 : 1;
                size = Math.Min(size, letters.Length - position);
                rows[rows.Count - 1][cell % width + 1] = letters.Substring(position, size);
                position += size;
            }
            foreach (var row in rows) { for (int c = 0; c < row.Length; c++) { row[c] = row[c] ?? ""; } }
            var table = new List<string[]> { new[] { "" }.Concat(k.Select(c => c.ToString())).ToArray(), new[] { "#" }.Concat(ranks.Select(r => (r + 1).ToString())).ToArray() };
            table.AddRange(rows);
            trace.Sections.Add(Block(T(lang, "The block (key letters and their order)", "Der Block (Schlüsselbuchstaben und ihre Reihenfolge)"), null, table.ToArray(), 2, 1));
            return trace;
        }

        public static CipherTrace Cadenus(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A columnar transposition of 25 rows. A letter of the 25-letter alphabet is attached to every row; every column starts at the row whose letter is its key letter. The plaintext block (left) and the cipher block (right) are shown.",
                "Eine Spaltentransposition aus 25 Zeilen. An jeder Zeile steht ein Buchstabe des 25-Buchstaben-Alphabets; jede Spalte beginnt in der Zeile, deren Buchstabe ihr Schlüsselbuchstabe ist. Gezeigt werden der Klartextblock (links) und der Geheimtextblock (rechts).");
            string k = CipherText.Letters(key).Replace('W', 'V');
            string plain = CipherText.Letters(decrypt ? output : input), cipher = CipherText.Letters(decrypt ? input : output);
            if (k.Length == 0 || plain.Length != 25 * k.Length || cipher.Length != plain.Length) { return Generic(title, input, output, decrypt, lang); }
            const string alphabet25 = "ABCDEFGHIJKLMNOPQRSTUVXYZ";
            var header = new[] { "" }.Concat(k.Select(c => c.ToString())).ToArray();
            var rows = new List<string[]> { header };
            var cipherHeader = new[] { "" }.Concat(k.OrderBy(c => c).Select(c => c.ToString())).ToArray();
            var cipherRows = new List<string[]> { cipherHeader };
            for (int r = 0; r < 25; r++)
            {
                string label = alphabet25[CipherText.Mod(-r, 25)].ToString();
                rows.Add(new[] { label }.Concat(Enumerable.Range(0, k.Length).Select(c => plain[r * k.Length + c].ToString())).ToArray());
                cipherRows.Add(new[] { (r + 1).ToString() }.Concat(Enumerable.Range(0, k.Length).Select(c => cipher[r * k.Length + c].ToString())).ToArray());
            }
            trace.Sections.Add(Block(T(lang, "Plaintext block (with the attached letters)", "Klartextblock (mit den angehängten Buchstaben)"), null, rows.ToArray(), 1, 1));
            trace.Sections.Add(Block(T(lang, "Cipher block (columns in the order of the key)", "Geheimtextblock (Spalten in der Reihenfolge des Schlüssels)"), null, cipherRows.ToArray(), 1, 1));
            return trace;
        }

        public static CipherTrace Nicodemus(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is written in rows under the key word; the columns are put in the alphabetical order of the key letters, enciphered with the key letters over them (Vigenère) and read off five letters at a time.",
                "Der Text wird zeilenweise unter das Schlüsselwort geschrieben; die Spalten werden in die alphabetische Reihenfolge der Schlüsselbuchstaben gebracht, mit den Schlüsselbuchstaben darüber (Vigenère) verschlüsselt und je fünf Buchstaben auf einmal gelesen.");
            string k = CipherText.Letters(key);
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            string plain = CipherText.Letters(decrypt ? output : input);
            var ranks = CipherText.Ranks(k);
            var columnAt = Enumerable.Range(0, k.Length).OrderBy(c => ranks[c]).ToArray();
            int rows = (plain.Length + k.Length - 1) / k.Length;
            trace.Sections.Add(Block(T(lang, "The plaintext under the key", "Der Klartext unter dem Schlüssel"), null, Letters2D(plain, k.Length, k.Select(c => c.ToString()).ToArray()), 1, 1));
            var sorted = new List<string[]> { new[] { "" }.Concat(columnAt.Select(c => k[c].ToString())).ToArray() };
            for (int r = 0; r < rows; r++) { sorted.Add(new[] { (r + 1).ToString() }.Concat(columnAt.Select(c => r * k.Length + c < plain.Length ? plain[r * k.Length + c].ToString() : "")).ToArray()); }
            trace.Sections.Add(Block(T(lang, "The columns in alphabetical order", "Die Spalten in alphabetischer Reihenfolge"), null, sorted.ToArray(), 1, 1));
            trace.Sections.Add(Block(T(lang, "The result in groups of five", "Das Ergebnis in Fünfergruppen"), null, new[] { Pieces(CipherText.Letters(output), 5).ToArray() }));
            return trace;
        }

        public static CipherTrace NihilistTransposition(string title, string key, bool byColumns, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text fills a square; its columns and then its rows are moved by the key; the result is read by rows or by columns.",
                "Der Text füllt ein Quadrat; seine Spalten und dann seine Zeilen werden durch den Schlüssel verschoben; das Ergebnis wird zeilen- oder spaltenweise gelesen.");
            string k = new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (k.Length < 2) { return Generic(title, input, output, decrypt, lang); }
            int n = k.Length;
            var target = CipherText.Ranks(k);
            string plain = CipherText.Pad(CipherText.Letters(decrypt ? output : input), n * n);
            if (plain.Length > n * n) { plain = plain.Substring(0, n * n); }
            var moved = new string[n, n];
            for (int i = 0; i < Math.Min(plain.Length, n * n); i++) { moved[target[i / n], target[i % n]] = plain[i].ToString(); }
            var header = k.Select(c => (target[k.IndexOf(c)] + 1).ToString()).ToArray();
            trace.Sections.Add(Block(T(lang, "The text in the square", "Der Text im Quadrat"), null, Letters2D(plain, n, k.Select(c => c.ToString()).ToArray()), 1, 1));
            trace.Sections.Add(Block(T(lang, "After moving the columns and the rows", "Nach dem Verschieben der Spalten und Zeilen"), null,
                Enumerable.Range(0, n).Select(r => new[] { (r + 1).ToString() }.Concat(Enumerable.Range(0, n).Select(c => moved[r, c] ?? "")).ToArray()).ToArray(), 0, 1));
            return trace;
        }

        public static CipherTrace Sequence(string title, string key, string primer, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "A number sequence made from the primer decides which column every letter goes into; the columns are numbered by the key word and written one after the other.",
                "Eine aus dem Primer gebildete Zahlenfolge entscheidet, in welche Spalte jeder Buchstabe kommt; die Spalten werden durch das Schlüsselwort nummeriert und hintereinander geschrieben.");
            string k = CipherText.Letters(key);
            var p = CipherText.Digits(primer).Select(c => c - '0').ToArray();
            string plain = CipherText.Letters(decrypt ? output : input);
            if (k.Length != 10 || p.Length < 2) { return Generic(title, input, output, decrypt, lang); }
            var sequence = new int[Math.Max(plain.Length, p.Length)];
            Array.Copy(p, sequence, p.Length);
            for (int i = p.Length; i < sequence.Length; i++) { sequence[i] = (sequence[i - p.Length] + sequence[i - p.Length + 1]) % 10; }
            trace.Sections.AddRange(Steps(T(lang, "The sequence", "Die Folge"), new[] { T(lang, "Plain letter", "Klartextbuchstabe"), T(lang, "Column number", "Spaltennummer") },
                new IList<string>[] { Characters(plain), sequence.Take(plain.Length).Select(d => d.ToString()).ToList() }));
            var digits = CipherText.KeyDigits(k);
            var columns = new List<string[]> { new[] { "" }.Concat(k.Select(c => c.ToString())).ToArray(), new[] { "#" }.Concat(digits.Select(d => d.ToString())).ToArray() };
            var content = digits.Select(d => new string(Enumerable.Range(0, plain.Length).Where(i => sequence[i] == d).Select(i => plain[i]).ToArray())).ToArray();
            columns.Add(new[] { T(lang, "Letters", "Buchstaben") }.Concat(content).ToArray());
            trace.Sections.Add(Block(T(lang, "The columns", "Die Spalten"), null, columns.ToArray(), 2, 1));
            return trace;
        }

        public static CipherTrace Homophonic(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Every letter has four two-digit numbers, one in each row; the key letters say where the numbers of the rows start (the key letter of a row has its first number).",
                "Jeder Buchstabe hat vier zweistellige Zahlen, eine in jeder Zeile; die Schlüsselbuchstaben sagen, wo die Zahlen der Zeilen beginnen (der Schlüsselbuchstabe einer Zeile hat ihre erste Zahl).");
            string k = CipherText.SquareLetters(key);
            if (k.Length != 4) { return Generic(title, input, output, decrypt, lang); }
            string square = CipherText.SquareAlphabet;
            var cells = new List<string[]> { new[] { "" }.Concat(square.Select(c => c.ToString())).ToArray() };
            for (int row = 0; row < 4; row++)
            {
                cells.Add(new[] { (row + 1).ToString() }.Concat(square.Select(c => ((1 + 25 * row + CipherText.Mod(square.IndexOf(c) - square.IndexOf(k[row]), 25)) % 100).ToString("00"))).ToArray());
            }
            trace.Sections.Add(Block(T(lang, "The numbers of the letters", "Die Zahlen der Buchstaben"), null, cells.ToArray(), 1, 1));
            var numbers = decrypt ? CipherText.Numbers(input).Select(n => n.ToString("00")).ToList() : (output ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            trace.Sections.AddRange(Steps(Step(lang), new[] { T(lang, "Letter", "Buchstabe"), T(lang, "Number", "Zahl") }, new IList<string>[] { decrypt ? Characters(output) : Characters(input), numbers }));
            return trace;
        }

        public static CipherTrace Null(string title, NullPosition position, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Every letter of the message is hidden in a word of the cover text: its first, middle or last letter. The letter that carries the message is marked.",
                "Jeder Buchstabe der Nachricht ist in einem Wort des Deckungstextes versteckt: seinem ersten, mittleren oder letzten Buchstaben. Der Buchstabe, der die Nachricht trägt, ist markiert.");
            var words = (decrypt ? input : output ?? "").ToUpperInvariant().Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var cells = new List<string[]>();
            var section = new TraceSection { Title = T(lang, "The words", "Die Wörter") };
            int width = words.Length == 0 ? 1 : words.Max(w => w.Length) + 1;
            for (int r = 0; r < words.Length; r++)
            {
                var row = new string[width];
                row[0] = (r + 1).ToString();
                for (int c = 0; c < words[r].Length; c++) { row[c + 1] = words[r][c].ToString(); }
                for (int c = 0; c < width; c++) { row[c] = row[c] ?? ""; }
                cells.Add(row);
                int index = position == NullPosition.First ? 0 : position == NullPosition.Last ? words[r].Length - 1 : (words[r].Length - 1) / 2;
                section.Result.Add(TraceSection.Key(r, index + 1));
            }
            section.Cells = cells.ToArray();
            section.HeaderColumns = 1;
            trace.Sections.Add(section);
            return trace;
        }

        public static CipherTrace Abc(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Two stages: in groups of three letters the first stays, the second moves one letter on, the third two; then the result goes through a columnar transposition with the key word.",
                "Zwei Stufen: In Dreiergruppen bleibt der erste Buchstabe, der zweite rückt um einen Buchstaben weiter, der dritte um zwei; dann durchläuft das Ergebnis eine Spaltentransposition mit dem Schlüsselwort.");
            string k = new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            string plain = CipherText.Letters(decrypt ? output : input);
            string substituted = PolyalphabeticCiphers.Periodic(plain, "ABC", Table.Vigenere, false);
            trace.Sections.AddRange(Steps(T(lang, "Stage 1: substitution", "Stufe 1: Substitution"), new[] { T(lang, "Plain", "Klar"), "+", T(lang, "Result", "Ergebnis") },
                new IList<string>[] { Characters(plain), Enumerable.Range(0, plain.Length).Select(i => (i % 3).ToString()).ToList(), Characters(substituted) }));
            var ranks = CipherText.Ranks(k);
            var table = new List<string[]> { new[] { "" }.Concat(k.Select(c => c.ToString())).ToArray(), new[] { "#" }.Concat(ranks.Select(r => (r + 1).ToString())).ToArray() };
            table.AddRange(Letters2D(substituted, k.Length, null));
            trace.Sections.Add(Block(T(lang, "Stage 2: transposition (the columns are read in the order of the numbers)", "Stufe 2: Transposition (die Spalten werden in der Reihenfolge der Nummern gelesen)"), null, table.ToArray(), 2, 1));
            return trace;
        }

        // ------------------------------------------------------------------------------------------------ transposition
        private static TraceSection Numbered(string title, string note, string[] header, IList<string[]> rows)
        {
            var cells = new List<string[]>();
            if (header != null) { cells.Add(header); }
            cells.AddRange(rows);
            return Block(title, note, cells.ToArray(), header != null ? 1 : 0, 0);
        }

        public static CipherTrace Myszkowski(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The plaintext is written in rows under the key. The columns are read in the order of the numbers of the key letters; columns with the same letter get the same number and are read together, row by row.",
                "Der Klartext wird zeilenweise unter den Schlüssel geschrieben. Die Spalten werden in der Reihenfolge der Nummern der Schlüsselbuchstaben gelesen; Spalten mit demselben Buchstaben erhalten dieselbe Nummer und werden zusammen, Zeile für Zeile, gelesen.");
            string k = new string((key ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (k.Length == 0) { return Generic(title, input, output, decrypt, lang); }
            string plain = CipherText.Letters(decrypt ? output : input);
            var ranks = CipherText.Ranks(k, true);
            int width = k.Length, rows = (plain.Length + width - 1) / width;
            var header = new[] { "" }.Concat(k.Select(c => c.ToString())).ToArray();
            var numbers = new[] { "" }.Concat(ranks.Select(r => (r + 1).ToString())).ToArray();
            var block = new List<string[]> { numbers };
            var order = TranspositionCiphers.ColumnOrder(plain.Length, CipherText.Ranks(k, true));
            var position = new Dictionary<int, int>();
            for (int i = 0; i < order.Count; i++) { position[order[i]] = i + 1; }
            for (int r = 0; r < rows; r++) { block.Add(new[] { (r + 1).ToString() }.Concat(Enumerable.Range(0, width).Select(c => r * width + c < plain.Length ? plain[r * width + c].ToString() : "")).ToArray()); }
            trace.Sections.Add(Numbered(T(lang, "The plaintext in the block", "Der Klartext im Block"), null, header, block));
            var read = new List<string[]>();
            for (int r = 0; r < rows; r++) { read.Add(new[] { (r + 1).ToString() }.Concat(Enumerable.Range(0, width).Select(c => position.ContainsKey(r * width + c) ? position[r * width + c].ToString() : "")).ToArray()); }
            trace.Sections.Add(Numbered(T(lang, "The order of reading", "Die Reihenfolge des Lesens"), null, header, read));
            return trace;
        }

        public static CipherTrace RouteTransposition(string title, int columns, Route write, Route read, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is written into a block along one route and read from it along another. The numbers show the order of the places.",
                "Der Text wird entlang einer Route in einen Block geschrieben und entlang einer anderen aus ihm gelesen. Die Zahlen zeigen die Reihenfolge der Plätze.");
            string letters = CipherText.Pad(CipherText.Letters(decrypt ? output : input), Math.Max(1, columns));
            int rows = Math.Max(1, letters.Length / Math.Max(1, columns));
            for (int pass = 0; pass < 2; pass++)
            {
                var path = TranspositionCiphers.RoutePath(pass == 0 ? write : read, rows, columns);
                var grid = new string[rows][];
                for (int r = 0; r < rows; r++) { grid[r] = new string[columns]; }
                for (int i = 0; i < path.Count; i++) { grid[path[i][0]][path[i][1]] = (i + 1).ToString(); }
                trace.Sections.Add(Block(pass == 0 ? T(lang, "Writing", "Schreiben") : T(lang, "Reading", "Lesen"), null, grid));
            }
            return trace;
        }

        public static CipherTrace Swagman(string title, string keySquare, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The text is written into a rectangle with as many rows as the key square has. Every letter moves within its column to the row that the number of the key square (repeated along the columns) says. The result is read by columns.",
                "Der Text wird in ein Rechteck mit so vielen Zeilen geschrieben, wie das Schlüsselquadrat hat. Jeder Buchstabe wandert in seiner Spalte in die Zeile, die die Zahl des Schlüsselquadrats (entlang der Spalten wiederholt) angibt. Das Ergebnis wird spaltenweise gelesen.");
            var rows = (keySquare ?? "").Split(new[] { ' ', ',', ';', '\n', '\r', '/' }, StringSplitOptions.RemoveEmptyEntries).Select(CipherText.Digits).Where(r => r.Length > 0).ToArray();
            if (rows.Length < 2) { return Generic(title, input, output, decrypt, lang); }
            trace.Sections.Add(Block(T(lang, "The key square", "Das Schlüsselquadrat"), null, rows.Select(r => r.Select(c => c.ToString()).ToArray()).ToArray()));
            string letters = CipherText.Pad(CipherText.Letters(decrypt ? output : input), rows.Length);
            int n = rows.Length, width = letters.Length / n;
            trace.Sections.Add(Block(T(lang, "The plaintext in the rectangle", "Der Klartext im Rechteck"), null,
                Enumerable.Range(0, n).Select(r => Enumerable.Range(0, Math.Min(width, 30)).Select(c => letters[r * width + c].ToString()).ToArray()).ToArray()));
            return trace;
        }

        public static CipherTrace Raster44(string title, string grid, string columnNumbers, int startRow, int startColumn, int readNumber, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The plaintext is written row by row into the white cells of the grid (black cells are skipped), starting at the start cell. The columns are read in the order of the numbers above them, starting at the reading column.",
                "Der Klartext wird zeilenweise in die weißen Felder des Rasters geschrieben (schwarze Felder werden übersprungen), beginnend an der Startzelle. Die Spalten werden in der Reihenfolge der Nummern über ihnen gelesen, beginnend bei der Herauslesespalte.");
            var cells = HandCiphers.ParseGrid(grid);
            int rows = cells.Length, columns = cells[0].Length;
            var numbers = CipherText.Numbers(columnNumbers);
            string letters = CipherText.Letters(decrypt ? output : input);
            var table = new string[rows + 1][];
            table[0] = new[] { "" }.Concat(Enumerable.Range(0, columns).Select(c => c < numbers.Count ? numbers[c].ToString() : "")).ToArray();
            var section = new TraceSection { Title = T(lang, "The grid with the plaintext", "Das Raster mit dem Klartext"), Cells = table, HeaderRows = 1, HeaderColumns = 1 };
            for (int r = 0; r < rows; r++) { table[r + 1] = new string[columns + 1]; table[r + 1][0] = (r + 1).ToString(); for (int c = 0; c < columns; c++) { table[r + 1][c + 1] = cells[r][c] ? "" : "■"; } }
            int written = 0;
            for (int r = startRow - 1, c = startColumn - 1; r < rows && written < letters.Length; c++)
            {
                if (c == columns) { c = 0; r++; if (r == rows) { break; } }
                if (cells[r][c]) { table[r + 1][c + 1] = letters[written++].ToString(); section.Marked.Add(TraceSection.Key(r + 1, c + 1)); }
            }
            trace.Sections.Add(section);
            return trace;
        }

        public static CipherTrace Werftschluessel(string title, string rowOrder, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "The groups of five letters go into the rows of the box in the order of the row numbers. Two rows at a time are replaced pair by pair (letters above each other) from the double-letter table; the first letters of the new pairs form one group of the ciphertext, the second letters the next.",
                "Die Fünfergruppen kommen in der Reihenfolge der Zeilennummern in die Zeilen des Kastens. Je zwei Zeilen werden Paar für Paar (übereinanderstehende Buchstaben) mithilfe der Doppelbuchstabentafel ersetzt; die ersten Buchstaben der neuen Paare bilden eine Gruppe des Geheimtextes, die zweiten die nächste.");
            string plain = CipherText.Letters(decrypt ? output : input);
            plain = CipherText.Pad(plain, 5);
            if ((plain.Length / 5) % 2 == 1) { plain += "XXXXX"; }
            int groups = plain.Length / 5;
            var order = CipherText.Numbers(rowOrder);
            if (order.Count != groups) { order = Enumerable.Range(1, groups).Where(i => i % 2 == 1).Concat(Enumerable.Range(1, groups).Where(i => i % 2 == 0)).ToList(); }
            var rows = new string[groups];
            for (int k = 0; k < groups; k++) { rows[order[k] - 1] = plain.Substring(5 * k, 5); }
            var table = new List<string[]>();
            for (int r = 0; r < groups; r++) { table.Add(new[] { (r + 1).ToString() }.Concat(rows[r].Select(c => c.ToString())).ToArray()); }
            trace.Sections.Add(Block(T(lang, "The box (rows)", "Der Kasten (Zeilen)"), null, table.ToArray(), 0, 1));
            string cipher = CipherText.Letters(decrypt ? input : output);
            var pairs = new List<string>();
            var replaced = new List<string>();
            for (int p = 0; p + 1 < groups && (p + 1) * 5 + 5 <= cipher.Length; p += 2)
            {
                for (int c = 0; c < 5; c++)
                {
                    pairs.Add("" + rows[p][c] + rows[p + 1][c]);
                    replaced.Add("" + cipher[5 * p + c] + cipher[5 * p + 5 + c]);
                }
            }
            trace.Sections.AddRange(Steps(T(lang, "The pairs", "Die Paare"), new[] { T(lang, "Plain pair", "Klartextpaar"), T(lang, "Cipher pair", "Geheimtextpaar") }, new IList<string>[] { pairs, replaced }, 15));
            return trace;
        }

        public static CipherTrace LeSphinx(string title, string key, string input, string output, bool decrypt, string lang)
        {
            var trace = Start(title, lang,
                "Ten lanes, each with an upper bar (plaintext) and a lower bar (ciphertext) that move together. A letter in the upper bar of a lane is replaced by the letter at the same place of the lower bar. Ten letters are enciphered at a time.",
                "Zehn Bahnen, jede mit einer oberen Schiene (Klartext) und einer unteren (Geheimtext), die sich zusammen bewegen. Ein Buchstabe in der oberen Schiene einer Bahn wird durch den Buchstaben an derselben Stelle der unteren ersetzt. Zehn Buchstaben werden auf einmal verschlüsselt.");
            var numbers = CipherText.Numbers(key);
            if (numbers.Count != 20) { numbers = Enumerable.Range(1, 20).ToList(); }
            var plain = Characters(decrypt ? output : input);
            var cipher = Characters(decrypt ? input : output);
            var table = new List<string[]> { new[] { T(lang, "Lane", "Bahn"), T(lang, "Upper bar", "Obere Schiene"), T(lang, "Lower bar", "Untere Schiene"), T(lang, "Plain", "Klar"), T(lang, "Cipher", "Geheim") } };
            for (int lane = 0; lane < 10; lane++)
            {
                table.Add(new[] { (lane + 1).ToString(), numbers[lane].ToString(), numbers[10 + lane].ToString(), lane < plain.Count ? plain[lane] : "", lane < cipher.Count ? cipher[lane] : "" });
            }
            trace.Sections.Add(Block(T(lang, "The lanes (first group of ten)", "Die Bahnen (erste Zehnergruppe)"), null, table.ToArray(), 1, 1));
            return trace;
        }
    }
}

"""What the single-cipher components of the classic ciphers look like: folder and class name, inputs, settings and the call into
LibSource/ClassicCipherLib. Used by tools-gen-classic-plugins.py (writes CrypPlugins/<Folder>) and tools-gen-aca-templates.py (the templates).

The keys of PLUGINS are the numbers the ciphers had in the first, combined component; the templates generator uses them too.
"""

# ---------------------------------------------------------------------------------------------------------------- the inputs
def inp(name, cap_en, cap_de, tip_en, tip_de, mandatory=True):
    return dict(name=name, cap=(cap_en, cap_de), tip=(tip_en, tip_de), mandatory=mandatory)


def key(tip_en="The key word", tip_de="Das Schlüsselwort", mandatory=True):
    return inp("Key", "Key", "Schlüssel", tip_en, tip_de, mandatory)


INDICATOR = inp("Indicator", "Indicator", "Indikator", "The indicator key: it gives the period and the setting of the cipher alphabet",
                "Der Indikator: Er bestimmt die Periode und die Stellung des Geheimtextalphabets")
PRIMER = inp("Primer", "Primer", "Primer", "The primer, a number of digits (5 for Gromark and the sequence transposition)",
             "Der Primer, eine Ziffernfolge (5 Ziffern bei Gromark und der Sequenz-Transposition)")
DIGIT_KEY = inp("DigitKey", "Key for the digits", "Schlüssel für die Ziffern", "A key word of 10 letters; its letters in alphabetical order give the digits 1 2 ... 9 0",
                "Ein Schlüsselwort aus 10 Buchstaben; seine Buchstaben in alphabetischer Reihenfolge ergeben die Ziffern 1 2 ... 9 0")

# ---------------------------------------------------------------------------------------------------------------- the settings
ACTION = dict(name="Action", kind="combo", items=(("Encrypt", "Verschlüsseln"), ("Decrypt", "Entschlüsseln")), cap=("Action", "Aktion"),
              tip=("Encrypt or decrypt", "Verschlüsseln oder Entschlüsseln"))
TABLE = dict(name="Table", kind="combo", items=(("Vigenère (C = P + K)", "Vigenère (C = P + K)"), ("Variant (C = P - K)", "Variant (C = P - K)"),
                                                 ("Beaufort (C = K - P)", "Beaufort (C = K - P)")), cap=("Table", "Tabelle"),
             tip=("The table of the encipherment: Vigenère, Variant or Beaufort", "Die Tabelle der Verschlüsselung: Vigenère, Variant oder Beaufort"))
ROUTES = (("Rows", "Zeilen"), ("Columns", "Spalten"), ("Alternating rows", "Zeilen im Wechsel"), ("Alternating columns", "Spalten im Wechsel"),
          ("Diagonals", "Diagonalen"), ("Alternating diagonals", "Diagonalen im Wechsel"), ("Spiral inwards, clockwise", "Spirale nach innen, im Uhrzeigersinn"),
          ("Spiral inwards, counterclockwise", "Spirale nach innen, gegen den Uhrzeigersinn"), ("Spiral outwards, clockwise", "Spirale nach außen, im Uhrzeigersinn"),
          ("Spiral outwards, counterclockwise", "Spirale nach außen, gegen den Uhrzeigersinn"))
WRITE_ROUTE = dict(name="WriteRoute", kind="combo", items=ROUTES, cap=("Write route", "Schreibroute"), tip=("The path along which the text is written into the block", "Der Weg, auf dem der Text in den Block geschrieben wird"))
READ_ROUTE = dict(name="ReadRoute", kind="combo", items=ROUTES, cap=("Read route", "Leseroute"), tip=("The path along which the text is read from the block", "Der Weg, auf dem der Text aus dem Block gelesen wird"))
NULL_POSITION = dict(name="NullPosition", kind="combo", items=(("First letter", "Erster Buchstabe"), ("Middle letter", "Mittlerer Buchstabe"), ("Last letter", "Letzter Buchstabe")),
                     cap=("Null position", "Stelle der Null"), tip=("Which letter of every word is the message", "Welcher Buchstabe jedes Wortes die Nachricht ist"), default=1)


def number(name, cap, tip, minimum, maximum, default):
    return dict(name=name, kind="int", cap=cap, tip=tip, min=minimum, max=maximum, default=default)


def flag(name, cap, tip, default):
    return dict(name=name, kind="bool", cap=cap, tip=tip, default=default)


COLUMNS = number("Columns", ("Columns", "Spalten"), ("Width of the block", "Breite des Blocks"), 1, 50, 5)
PROGRESSION = number("Progression", ("Progression index", "Fortschreitungsindex"), ("How far the key letter of the second encipherment moves on after every group",
                     "Um wie viele Buchstaben der Schlüsselbuchstabe der zweiten Verschlüsselung nach jeder Gruppe weiterrückt"), 1, 25, 1)
SHIFT = number("Shift", ("Alphabet shift", "Alphabetverschiebung"), ("How many places the keyed alphabet is moved to the right", "Um wie viele Stellen das Schlüsselalphabet nach rechts verschoben wird"), 0, 25, 0)
UNDER = dict(name="Under", kind="string", cap=("Indicator under", "Indikator unter"), default="A",
             tip=("The plain letter above which the first letter of the indicator stands (A; for Quagmire IV often the first letter of the key word)",
                  "Der Klartextbuchstabe, über dem der Indikatorbuchstabe steht (A; bei Quagmire IV oft der erste Buchstabe des Schlüsselworts)"))
START_DIGRAPH = flag("StartWithDigraph", ("Start with a digraph", "Mit einem Digraph beginnen"), ("The first place of the block holds two letters (otherwise one)", "Der erste Platz des Blocks enthält zwei Buchstaben (sonst einen)"), True)
BY_COLUMNS = flag("ByColumns", ("Read by columns", "Spaltenweise lesen"), ("The square is read by columns (otherwise by rows)", "Das Quadrat wird spaltenweise gelesen (sonst zeilenweise)"), False)


def period(tip_en, tip_de, minimum=1, maximum=50, default=5):
    return number("Period", ("Period", "Periode"), (tip_en, tip_de), minimum, maximum, default)


def offset(tip_en, tip_de, maximum=200):
    return number("Offset", ("Offset", "Versatz"), (tip_en, tip_de), 0, maximum, 0)


# ---------------------------------------------------------------------------------------------------------------- the components
L = "ClassicCiphers."          # the namespace of the library
P, S, T, F, H = L + "PolyalphabeticCiphers.", L + "SubstitutionCiphers.", L + "TranspositionCiphers.", L + "FractionationCiphers.", L + "HandCiphers."
TABLE_ARG = "(ClassicCiphers.Table)_settings.Table"


def plugin(folder, en, de, inputs, settings, call, reciprocal=False):
    return dict(folder=folder, name=(en, de), inputs=[inp("InputText", "Text", "Text", "The text to encrypt or decrypt", "Der Text, der ver- oder entschlüsselt werden soll")] + inputs,
                settings=([] if reciprocal else [ACTION]) + settings, call=call)


PLUGINS = {
    1: plugin("Quagmire1Cipher", "Quagmire I Cipher", "Quagmire-I-Chiffre", [key("The key word of the keyed plain alphabet", "Das Schlüsselwort des gemischten Klartextalphabets"), INDICATOR], [UNDER],
              P + "Quagmire(1, InputText, Key, Indicator, null, _settings.UnderLetter, decrypt)"),
    2: plugin("Quagmire2Cipher", "Quagmire II Cipher", "Quagmire-II-Chiffre", [key("The key word of the keyed cipher alphabet", "Das Schlüsselwort des gemischten Geheimtextalphabets"), INDICATOR], [UNDER],
              P + "Quagmire(2, InputText, Key, Indicator, null, _settings.UnderLetter, decrypt)"),
    3: plugin("Quagmire3Cipher", "Quagmire III Cipher", "Quagmire-III-Chiffre", [key("The key word of both alphabets", "Das Schlüsselwort beider Alphabete"), INDICATOR], [UNDER],
              P + "Quagmire(3, InputText, Key, Indicator, null, _settings.UnderLetter, decrypt)"),
    4: plugin("Quagmire4Cipher", "Quagmire IV Cipher", "Quagmire-IV-Chiffre", [key("The key word of the plain alphabet", "Das Schlüsselwort des Klartextalphabets"), INDICATOR,
                                                                              inp("CipherKey", "Cipher alphabet key", "Schlüssel des Geheimtextalphabets", "The key word of the cipher alphabet", "Das Schlüsselwort des Geheimtextalphabets")], [UNDER],
              P + "Quagmire(4, InputText, Key, Indicator, CipherKey, _settings.UnderLetter, decrypt)"),
    5: plugin("PortaxCipher", "Portax Cipher", "Portax-Chiffre", [key()], [], P + "Portax(InputText, Key)", reciprocal=True),
    6: plugin("SlidefairCipher", "Slidefair Cipher", "Slidefair-Chiffre", [key()], [TABLE], P + "Slidefair(InputText, Key, " + TABLE_ARG + ", decrypt)"),
    7: plugin("ProgressiveKeyCipher", "Progressive Key Cipher", "Chiffre mit fortschreitendem Schlüssel", [key()], [TABLE, PROGRESSION],
              P + "Progressive(InputText, Key, _settings.Progression, " + TABLE_ARG + ", decrypt)"),
    8: plugin("InterruptedKeyCipher", "Interrupted Key Cipher", "Chiffre mit unterbrochenem Schlüssel",
              [key(), inp("Interruptions", "Interruptions", "Unterbrechungen", "The lengths of the pieces of the key word that are used, e.g. \"4 6 2 3\" (the list is repeated)",
                          "Die Längen der benutzten Stücke des Schlüsselworts, z. B. \"4 6 2 3\" (die Liste wird wiederholt)")], [TABLE],
              P + "Interrupted(InputText, Key, Interruptions, " + TABLE_ARG + ", decrypt)"),
    10: plugin("GromarkCipher", "Gromark Cipher", "Gromark-Chiffre", [key(), PRIMER], [], P + "Gromark(InputText, Key, Primer, decrypt)"),
    11: plugin("PeriodicGromarkCipher", "Periodic Gromark Cipher", "Periodische Gromark-Chiffre", [key("The key word (its numbering is the primer)", "Das Schlüsselwort (seine Nummerierung ist der Primer)")], [],
               P + "PeriodicGromark(InputText, Key, decrypt)"),
    12: plugin("CondiCipher", "Condi Cipher", "Condi-Chiffre", [key()], [SHIFT, offset("The start value of the step for the first letter", "Der Startwert der Schrittweite für den ersten Buchstaben", 25)],
               P + "Condi(InputText, Key, _settings.Shift, _settings.Offset, decrypt)"),
    13: plugin("RagbabyCipher", "Ragbaby Cipher", "Ragbaby-Chiffre", [key("The key word (24-letter alphabet, I/J and W/X together)", "Das Schlüsselwort (24-Buchstaben-Alphabet, I/J und W/X zusammen)")], [],
               P + "Ragbaby(InputText, Key, decrypt)"),
    14: plugin("PhillipsCipher", "Phillips Cipher", "Phillips-Chiffre", [key("The key word of the first 5x5 square", "Das Schlüsselwort des ersten 5x5-Quadrats")], [], P + "Phillips(InputText, Key, false, decrypt)"),
    15: plugin("PhillipsRCCipher", "Phillips-RC Cipher", "Phillips-RC-Chiffre", [key("The key word of the first 5x5 square", "Das Schlüsselwort des ersten 5x5-Quadrats")], [], P + "Phillips(InputText, Key, true, decrypt)"),
    17: plugin("NumberedKeyCipher", "Numbered Key Cipher", "Chiffre mit nummeriertem Schlüssel", [key("The key phrase", "Der Schlüsselsatz")],
               [offset("The place of the extended key where the numbering starts", "Die Stelle des erweiterten Schlüssels, an der die Nummerierung beginnt")], S + "NumberedKey(InputText, Key, _settings.Offset, decrypt)"),
    18: plugin("HomophonicCipher", "Homophonic Cipher (ACA)", "Homophone Chiffre (ACA)", [key("4 letters: they say where the numbers of the four rows start", "4 Buchstaben: Sie sagen, wo die Zahlen der vier Zeilen beginnen")], [], S + "Homophonic(InputText, Key, decrypt)"),
    19: plugin("NullCipher", "Null Cipher", "Nullen-Chiffre", [inp("CoverText", "Cover text", "Deckungstext", "Words from which the cipher takes its words", "Wörter, aus denen die Chiffre ihre Wörter nimmt")], [NULL_POSITION],
               S + "Null(InputText, CoverText, (ClassicCiphers.NullPosition)_settings.NullPosition, decrypt)"),
    22: plugin("MyszkowskiTransposition", "Myszkowski Transposition", "Myszkowski-Transposition", [key("A key word with repeated letters", "Ein Schlüsselwort mit wiederholten Buchstaben")], [], T + "Columnar(InputText, Key, false, true, decrypt)"),
    23: plugin("AmscoCipher", "AMSCO Cipher", "AMSCO-Chiffre", [key("The key word or number", "Das Schlüsselwort oder die Zahl")], [START_DIGRAPH], T + "Amsco(InputText, Key, _settings.StartWithDigraph, decrypt)"),
    24: plugin("CadenusCipher", "Cadenus Cipher", "Cadenus-Chiffre", [key("The key word; the text needs 25 letters for every key letter", "Das Schlüsselwort; der Text braucht 25 Buchstaben je Schlüsselbuchstabe")], [], T + "Cadenus(InputText, Key, decrypt)"),
    25: plugin("NicodemusCipher", "Nicodemus Cipher", "Nicodemus-Chiffre", [key()], [], T + "Nicodemus(InputText, Key, decrypt)"),
    26: plugin("NihilistTransposition", "Nihilist Transposition", "Nihilisten-Transposition", [key("The key (word or number); the text fills squares as wide as the key is long", "Der Schlüssel (Wort oder Zahl); der Text füllt Quadrate, die so breit sind wie der Schlüssel lang ist")],
               [BY_COLUMNS], T + "NihilistTransposition(InputText, Key, _settings.ByColumns, decrypt)"),
    27: plugin("RouteTransposition", "Route Transposition", "Routentransposition", [], [COLUMNS, WRITE_ROUTE, READ_ROUTE],
               T + "RouteTransposition(InputText, _settings.Columns, (ClassicCiphers.Route)_settings.WriteRoute, (ClassicCiphers.Route)_settings.ReadRoute, decrypt)"),
    28: plugin("SequenceTransposition", "Sequence Transposition", "Sequenz-Transposition", [key("A key word of 10 letters", "Ein Schlüsselwort aus 10 Buchstaben"), PRIMER], [], T + "Sequence(InputText, Key, Primer, decrypt)"),
    30: plugin("SwagmanCipher", "Swagman Cipher", "Swagman-Chiffre", [inp("KeySquare", "Key square", "Schlüsselquadrat", "The key square, its rows separated by spaces, e.g. \"32145 15324 24531 53412 41253\"",
                                                                       "Das Schlüsselquadrat, die Zeilen durch Leerzeichen getrennt, z. B. \"32145 15324 24531 53412 41253\"")], [], T + "Swagman(InputText, KeySquare, decrypt)"),
    31: plugin("CMBifidCipher", "CM Bifid Cipher", "CM-Bifid-Chiffre", [key("The key word of the plain square", "Das Schlüsselwort des Klartextquadrats"),
                                                                       inp("CipherKey", "Cipher square key", "Schlüssel des Geheimtextquadrats", "The key word of the cipher square (written in alternating verticals)", "Das Schlüsselwort des Geheimtextquadrats (in abwechselnden Senkrechten geschrieben)")],
               [period("Number of letters in a group; 0 uses the whole text", "Anzahl der Buchstaben in einer Gruppe; 0 verwendet den ganzen Text", 0, 50, 7)], F + "CmBifid(InputText, Key, CipherKey, _settings.Period, decrypt)"),
    32: plugin("DigrafidCipher", "Digrafid Cipher", "Digrafid-Chiffre", [key("The key word of the horizontal alphabet", "Das Schlüsselwort des waagerechten Alphabets"),
                                                                     inp("VerticalKey", "Key of the vertical alphabet", "Schlüssel des senkrechten Alphabets", "The key word of the vertical alphabet", "Das Schlüsselwort des senkrechten Alphabets")],
               [period("Number of pairs of letters in a group", "Anzahl der Buchstabenpaare in einer Gruppe", 1, 50, 3)], F + "Digrafid(InputText, Key, VerticalKey, _settings.Period, decrypt)"),
    33: plugin("FractionatedMorseCipher", "Fractionated Morse Cipher", "Fraktioniertes Morse", [key("The key word of the alphabet", "Das Schlüsselwort des Alphabets")], [], F + "FractionatedMorse(InputText, Key, decrypt)"),
    34: plugin("MorbitCipher", "Morbit Cipher", "Morbit-Chiffre", [key("A key word of 9 letters", "Ein Schlüsselwort aus 9 Buchstaben")], [], F + "Morbit(InputText, Key, decrypt)"),
    35: plugin("PolluxCipher", "Pollux Cipher", "Pollux-Chiffre", [key("The symbols (. - x) of the digits 1 2 3 4 5 6 7 8 9 0, e.g. \"x-..x.--x.\"", "Die Zeichen (. - x) der Ziffern 1 2 3 4 5 6 7 8 9 0, z. B. \"x-..x.--x.\"")], [], F + "Pollux(InputText, Key, decrypt)"),
    36: plugin("TridigitalCipher", "Tridigital Cipher", "Tridigital-Chiffre", [key("The key word of the alphabet", "Das Schlüsselwort des Alphabets"), DIGIT_KEY], [], S + "Tridigital(InputText, Key, DigitKey, decrypt)"),
    37: plugin("TriSquareCipher", "Tri-Square Cipher", "Tri-Square-Chiffre", [inp("Key", "Key 1", "Schlüssel 1", "The key word of square 1", "Das Schlüsselwort des Quadrats 1"),
                                                                           inp("Key2", "Key 2", "Schlüssel 2", "The key word of square 2", "Das Schlüsselwort des Quadrats 2"),
                                                                           inp("Key3", "Key 3", "Schlüssel 3", "The key word of square 3", "Das Schlüsselwort des Quadrats 3")], [], F + "TriSquare(InputText, Key, Key2, Key3, decrypt)"),
    38: plugin("SeriatedPlayfairCipher", "Seriated Playfair", "Seriated Playfair", [key("The key word of the square", "Das Schlüsselwort des Quadrats")],
               [period("Number of letters in a line of a group", "Anzahl der Buchstaben in einer Zeile einer Gruppe", 1, 50, 6)], F + "SeriatedPlayfair(InputText, Key, _settings.Period, decrypt)"),
    39: plugin("CheckerboardCipher", "Checkerboard Cipher", "Checkerboard-Chiffre", [key("The key word of the square", "Das Schlüsselwort des Quadrats"),
                                                                                   inp("RowLabels", "Row labels", "Zeilenbeschriftung", "The 5 letters at the left of the square (10 for two words)", "Die 5 Buchstaben links vom Quadrat (10 für zwei Wörter)"),
                                                                                   inp("ColumnLabels", "Column labels", "Spaltenbeschriftung", "The 5 letters above the square (10 for two words)", "Die 5 Buchstaben über dem Quadrat (10 für zwei Wörter)")], [],
               S + "Checkerboard(InputText, Key, RowLabels, ColumnLabels, decrypt)"),
    40: plugin("MonomeDinomeCipher", "Monome-Dinome Cipher", "Monome-Dinome-Chiffre", [key("The key word of the 3x8 box", "Das Schlüsselwort des 3x8-Kastens"), DIGIT_KEY], [], S + "MonomeDinome(InputText, Key, DigitKey, decrypt)"),
    41: plugin("CompressocratCipher", "Compressocrat Cipher", "Compressocrat-Chiffre", [key("The key word of the alphabet", "Das Schlüsselwort des Alphabets")], [SHIFT], S + "Compressocrat(InputText, Key, _settings.Shift, decrypt)"),
    42: plugin("HeadlinesCipher", "Headlines Cipher", "Headlines-Chiffre", [inp("HatWord", "Hat word", "Hutwort", "The hat word: its letters give the order of the columns", "Das Hutwort: Seine Buchstaben bestimmen die Reihenfolge der Spalten"),
                                                                         key("The key word of the alphabet", "Das Schlüsselwort des Alphabets"),
                                                                         inp("SettingWord", "Setting word", "Einstellungswort", "The setting word: every line (headline) uses the next letter of it", "Das Einstellungswort: Jede Zeile (Schlagzeile) benutzt den nächsten Buchstaben")], [],
               P + "Headlines(InputText, HatWord, Key, SettingWord, decrypt)"),
    43: plugin("PolybiusCipher", "Polybius Cipher", "Polybios-Chiffre", [key("Optional key word of the square; without it the plain alphabet is used (I and J together)", "Optionales Schlüsselwort des Quadrats; ohne wird das einfache Alphabet benutzt (I und J zusammen)", False)], [],
               S + "Polybius(InputText, Key, decrypt)"),
    44: plugin("TapCode", "Tap Code", "Klopfcode", [], [], S + "TapCode(InputText, decrypt)"),
    45: plugin("AbcCipher", "ABC Cipher", "ABC-Chiffre", [key("The key word of the columnar transposition (the substitution has the fixed key ABC)", "Das Schlüsselwort der Spaltentransposition (die Substitution hat den festen Schlüssel ABC)")], [], T + "Abc(InputText, Key, decrypt)"),
    46: plugin("Rasterschluessel44Cipher", "Rasterschlüssel 44 (RS 44)", "Rasterschlüssel 44 (RS 44)",
               [inp("Grid", "Grid", "Raster", "The grid, one line for each row: # for a black cell, . for a cell that can be written in",
                    "Das Raster, eine Zeile je Reihe: # für ein schwarzes Feld, . für ein Feld, in das geschrieben wird"),
                inp("ColumnNumbers", "Column numbers", "Spaltennummern", "The numbers 1 to 25 printed above the columns, in the order of the columns of the grid",
                    "Die über den Spalten gedruckten Nummern 1 bis 25, in der Reihenfolge der Spalten des Rasters")],
               [number("StartRow", ("Start row", "Startzeile"), ("The row of the cell where writing starts", "Die Zeile der Zelle, in der das Schreiben beginnt"), 1, 50, 1),
                number("StartColumn", ("Start column", "Startspalte"), ("The column of the cell where writing starts (the first white cell from there is used)", "Die Spalte der Zelle, in der das Schreiben beginnt (ab dort wird die erste weiße Zelle benutzt)"), 1, 50, 1),
                number("ReadNumber", ("Reading column", "Herauslesespalte"), ("The number of the column where reading starts", "Die Nummer der Spalte, in der das Herauslesen beginnt"), 1, 50, 1)],
               H + "Raster44(InputText, Grid, ColumnNumbers, _settings.StartRow, _settings.StartColumn, _settings.ReadNumber, decrypt)"),
    47: plugin("WerftschluesselCipher", "Werftschlüssel (Reservehandverfahren)", "Werftschlüssel (Reservehandverfahren)",
               [inp("Table", "Double-letter table", "Doppelbuchstabentauschtafel", "The pairs of the table written like KV=TC, separated by spaces or lines (every pair works in both directions)",
                    "Die Paare der Tafel in der Form KV=TC, durch Leerzeichen oder Zeilen getrennt (jedes Paar gilt in beiden Richtungen)"),
                inp("RowOrder", "Row order", "Zeilenreihenfolge", "The row of the box for each group of five letters, e.g. \"1 3 5 2 4 6\"; without it the odd rows come first, then the even rows",
                    "Die Zeile des Kastens für jede Fünfergruppe, z. B. \"1 3 5 2 4 6\"; ohne Angabe kommen erst die ungeraden, dann die geraden Zeilen", False)],
               [], H + "Werftschluessel(InputText, Table, RowOrder, decrypt)"),
    48: plugin("LeSphinxCipher", "Le Sphinx", "Le Sphinx", [inp("Key", "Key", "Schlüssel", "The numbers 1 to 20 of the bars: first the 10 upper bars, then the 10 lower bars of the lanes; without a key 1 2 ... 20",
                                                           "Die Nummern 1 bis 20 der Schienen: erst die 10 oberen, dann die 10 unteren Schienen der Bahnen; ohne Schlüssel 1 2 ... 20", False)], [],
               S + "LeSphinx(InputText, Key, decrypt)"),
}


# ---------------------------------------------------------------------------------------------------------------- the pictures
# C# expressions that make the picture (CipherTrace) of a component; title, lang and decrypt are variables of Execute. What is not listed uses the
# generic picture (the steps letter by letter).
C = L + "CipherTraces."
TRACES = {
    1: C + "Quagmire(title, 1, InputText, Key, Indicator, null, _settings.UnderLetter, OutputText, decrypt, lang)",
    2: C + "Quagmire(title, 2, InputText, Key, Indicator, null, _settings.UnderLetter, OutputText, decrypt, lang)",
    3: C + "Quagmire(title, 3, InputText, Key, Indicator, null, _settings.UnderLetter, OutputText, decrypt, lang)",
    4: C + "Quagmire(title, 4, InputText, Key, Indicator, CipherKey, _settings.UnderLetter, OutputText, decrypt, lang)",
    10: C + "Gromark(title, Key, Primer, InputText, OutputText, decrypt, lang)",
    12: C + "Condi(title, Key, _settings.Shift, _settings.Offset, InputText, OutputText, decrypt, lang)",
    13: C + "Ragbaby(title, Key, InputText, OutputText, decrypt, lang)",
    14: C + "Phillips(title, Key, false, InputText, OutputText, decrypt, lang)",
    15: C + "Phillips(title, Key, true, InputText, OutputText, decrypt, lang)",
    22: C + "Myszkowski(title, Key, InputText, OutputText, decrypt, lang)",
    27: C + "RouteTransposition(title, _settings.Columns, (ClassicCiphers.Route)_settings.WriteRoute, (ClassicCiphers.Route)_settings.ReadRoute, InputText, OutputText, decrypt, lang)",
    30: C + "Swagman(title, KeySquare, InputText, OutputText, decrypt, lang)",
    31: C + "CmBifid(title, Key, CipherKey, _settings.Period, InputText, OutputText, decrypt, lang)",
    36: C + "Tridigital(title, Key, DigitKey, InputText, OutputText, decrypt, lang)",
    37: C + "TriSquare(title, Key, Key2, Key3, InputText, OutputText, decrypt, lang)",
    39: C + "Checkerboard(title, Key, RowLabels, ColumnLabels, InputText, OutputText, decrypt, lang)",
    40: C + "MonomeDinome(title, Key, DigitKey, InputText, OutputText, decrypt, lang)",
    42: C + "Headlines(title, HatWord, Key, SettingWord, InputText, OutputText, decrypt, lang)",
    43: C + "Polybius(title, Key, InputText, OutputText, decrypt, lang)",
    44: C + "TapCode(title, InputText, OutputText, decrypt, lang)",
    46: C + "Raster44(title, Grid, ColumnNumbers, _settings.StartRow, _settings.StartColumn, _settings.ReadNumber, InputText, OutputText, decrypt, lang)",
    47: C + "Werftschluessel(title, RowOrder, InputText, OutputText, decrypt, lang)",
    48: C + "LeSphinx(title, Key, InputText, OutputText, decrypt, lang)",
}
TRACES.update({
    7: C + "ProgressiveKey(title, Key, _settings.Progression, InputText, OutputText, decrypt, lang)",
    8: C + "InterruptedKey(title, Key, Interruptions, InputText, OutputText, decrypt, lang)",
    17: C + "NumberedKey(title, Key, _settings.Offset, InputText, OutputText, decrypt, lang)",
    32: C + "Digrafid(title, Key, VerticalKey, InputText, OutputText, decrypt, lang)",
    33: C + "FractionatedMorse(title, InputText, OutputText, decrypt, lang)",
    34: C + "Morbit(title, InputText, OutputText, decrypt, lang)",
    35: C + "Pollux(title, InputText, OutputText, decrypt, lang)",
    38: C + "SeriatedPlayfair(title, Key, _settings.Period, InputText, OutputText, decrypt, lang)",
    41: C + "Compressocrat(title, Key, _settings.Shift, InputText, OutputText, decrypt, lang)",
})
TRACES.update({
    5: C + "Portax(title, Key, InputText, OutputText, lang)",
    6: C + "Slidefair(title, Key, InputText, OutputText, decrypt, lang)",
    11: C + "PeriodicGromark(title, Key, InputText, OutputText, decrypt, lang)",
    18: C + "Homophonic(title, Key, InputText, OutputText, decrypt, lang)",
    19: C + "Null(title, (ClassicCiphers.NullPosition)_settings.NullPosition, InputText, OutputText, decrypt, lang)",
    23: C + "Amsco(title, Key, _settings.StartWithDigraph, InputText, OutputText, decrypt, lang)",
    24: C + "Cadenus(title, Key, InputText, OutputText, decrypt, lang)",
    25: C + "Nicodemus(title, Key, InputText, OutputText, decrypt, lang)",
    26: C + "NihilistTransposition(title, Key, _settings.ByColumns, InputText, OutputText, decrypt, lang)",
    28: C + "Sequence(title, Key, Primer, InputText, OutputText, decrypt, lang)",
    45: C + "Abc(title, Key, InputText, OutputText, decrypt, lang)",
})
GENERIC_TRACE = L + "TraceBuilder.Generic(title, InputText, OutputText, decrypt, lang)"
for number, spec in PLUGINS.items():
    spec["trace"] = TRACES.get(number, GENERIC_TRACE)

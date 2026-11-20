#!/usr/bin/env python3
"""One template for each cipher of the American Cryptogram Association (ACA) that the ACA Ciphers component (CrypPlugins/AcaClassic) offers,
plus Twin Bifid and Twin Trifid (two Bifid or Trifid components with different periods). Written next to the other classic templates in
../Templates/Cryptography/Classic as .cwm and .xml (English and German), with the machinery of tools-gen-derived-templates.py.

  python3 tools-gen-aca-templates.py

Each template encrypts a text with the cipher and decrypts the result again; the examples are those of the ACA's descriptions
(https://www.cryptogram.org/resource-area/cipher-types/). Needs the built CrypWeb.Tests.
"""
import importlib

from tools_classic_specs import PLUGINS, TRACES

derived = importlib.import_module("tools-gen-derived-templates")

CLASSIC = "Cryptography/Classic"
RECORDS = {}   # cipher number -> description texts of its template; the plugin generator takes the documentation of a component from them

TEXT_INPUT, TEXT_OUTPUT = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
# existing components that do what a cipher of the list needs: (type, input port, output port, key ports)
VIGENERE = ("CrypTool.Vigenere.Vigenere", "InputString", "OutputString", ["ShiftValue"], "Vigenère", "Vigenère")
FLEISSNER = ("CrypTool.Plugins.FleißnerGrille.FleißnerGrille", "InputString", "OutputString", ["StencilString"], "Fleißner Grille", "Fleißner-Gitter")
SUBSTITUTION = ("CrypTool.Substitution.Substitution", "InputString", "OutputString", ["SourceAlphabet", "DestinationAlphabet"], "Substitution", "Substitution")


def chain(file, component, in_port, out_port, key_ports, text, keys, step_settings, title, title_de, description, memo, keywords, plugin, view=False):
    """The text goes through the component once for every entry of step_settings (the next step takes the result of the one before); the result of every
    step is shown. The outputs are below the note of the base template (x 405 to 870, y 170 to 380)."""
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=TEXT_INPUT, x=40, y=40, w=260, h=140, settings=dict(Text=text)))]
    for index, key_text in enumerate(keys):
        edits.append(dict(add=dict(type=TEXT_INPUT, x=40, y=220 + index * 110, w=260, h=90, settings=dict(Text=key_text))))
    for step, settings in enumerate(step_settings):
        x = 380 + 340 * step
        if view and step == 0:
            # the component is shown with its picture: large, below the note of the base template; its output goes under the inputs
            below = 220 + 110 * len(keys) + 20
            box = dict(add=dict(type=component, x=380, y=430, w=640, h=440, viewState=1, settings=dict(settings)))
            output = dict(add=dict(type=TEXT_OUTPUT, x=40, y=below, w=300, h=160))
        elif view:
            box = dict(add=dict(type=component, x=1060, y=40, w=180, h=110, settings=dict(settings)))
            output = dict(add=dict(type=TEXT_OUTPUT, x=1060, y=220, w=300, h=160))
        else:
            box = dict(add=dict(type=component, x=x, y=40, w=180, h=110, settings=dict(settings)))
            output = dict(add=dict(type=TEXT_OUTPUT, x=x, y=430, w=300, h=180))
        edits += [box, output,
                  dict(connect={"from": f"{component}#{step - 1}.{out_port}" if step else f"{TEXT_INPUT}#0.TextOutput", "to": f"{component}#{step}.{in_port}"}),
                  dict(connect={"from": f"{component}#{step}.{out_port}", "to": f"{TEXT_OUTPUT}#{step}.Input"})]
        for index in range(len(keys)):
            edits.append(dict(connect={"from": f"{TEXT_INPUT}#{index + 1}.TextOutput", "to": f"{component}#{step}.{key_ports[index]}"}))
    return dict(
        out=f"{CLASSIC}/{file}", base="Tools/RandomNumberGenerators", edits=edits, plugin=plugin,
        en=dict(title=title, summary=f"<i>{title}</i>", description=description[0], memo=memo[0]),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description[1], memo=memo[1]),
        keywords=keywords,
    )


def aca(file, cipher, text, keys, title, titles_de, description, memo, keywords, settings=None, steps=(0, 1)):
    """A template of the component of a cipher: the text is encrypted, and the result decrypted again (ciphers that are their own inverse get no
    action setting). keys: the texts of the key inputs in the order of the component; description and memo: (English, German)."""
    spec = PLUGINS[cipher]
    folder = spec["folder"]
    RECORDS[cipher] = dict(description=description)
    has_action = any(setting["name"] == "Action" for setting in spec["settings"])
    step_settings = [dict(Action=str(action), **(settings or {})) if has_action else dict(settings or {}) for action in steps]
    key_ports = [i["name"] for i in spec["inputs"][1:]]
    memo = tuple(m.replace("{NAME}", "{\\b " + name + "}") for m, name in zip(memo, spec["name"]))
    RECORDS[cipher]["memo"] = memo
    return chain(file, f"{folder}.{folder}", "InputText", "OutputText", key_ports, text, keys, step_settings, title, titles_de, description, memo, keywords, folder, view=cipher in TRACES)


def reuse(file, component, text, keys, step_settings, title, title_de, description, memo, keywords):
    """A template of a cipher that an existing component does (see the table in ACA-CIPHERS.md): no component of its own."""
    kind, in_port, out_port, key_ports, name_en, name_de = component
    memo = tuple(m.replace("{NAME}", "{\\b " + name + "}") for m, name in zip(memo, (name_en, name_de)))
    return chain(file, kind, in_port, out_port, key_ports, text, keys, step_settings, title, title_de, description, memo, keywords, kind.split(".")[-2])


def twin(file, plugin, kind, columns, text, key, periods, title, title_de, description, memo, keywords):
    """Twin Bifid / Twin Trifid: the same text enciphered with the same key and two different periods."""
    component = f"CrypTool.Plugins.{plugin}.{plugin}"
    text_input, output = TEXT_INPUT, TEXT_OUTPUT
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=260, h=140, settings=dict(Text=text))),
             dict(add=dict(type=text_input, x=40, y=220, w=260, h=90, settings=dict(Text=key)))]
    for index, period in enumerate(periods):
        x = 380 + index * 340
        edits += [dict(add=dict(type=component, x=x, y=40, w=180, h=110, settings=dict(Action="0", EnablePeriod="True", Period=str(period)))),
                  dict(add=dict(type=output, x=x, y=430, w=320, h=180)),
                  dict(connect={"from": f"{text_input}#0.TextOutput", "to": f"{component}#{index}.InputText"}),
                  dict(connect={"from": f"{text_input}#1.TextOutput", "to": f"{component}#{index}.Key"}),
                  dict(connect={"from": f"{component}#{index}.OutputText", "to": f"{output}#{index}.Input"})]
    return dict(
        out=f"{CLASSIC}/{file}", base="Tools/RandomNumberGenerators", edits=edits, plugin=plugin,
        en=dict(title=title, summary=f"<i>{title}</i>", description=description[0], memo=memo[0]),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description[1], memo=memo[1]),
        keywords=keywords,
    )


SQ1 = "EXTRAKLMPOHWZQDGVUSIFCBYN"
SPEC = []
add = SPEC.append


# ---------------------------------------------------------------------------------------------------------------- periodic ciphers
add(reuse("VariantCipher", VIGENERE, "ATTACK AT DAWN", ["APPLE"], (dict(Mode="0", Action="1"), dict(Mode="0", Action="0")), "Variant Cipher", "Variant-Chiffre",
          ("The Variant cipher is the Vigenère cipher with the subtraction C = P - K instead of the addition: encrypting with the Variant cipher is decrypting with the Vigenère cipher, and the other way round.",
           "Die Variant-Chiffre ist die Vigenère-Chiffre mit der Subtraktion C = P - K statt der Addition: Mit der Variant-Chiffre zu verschlüsseln heißt, mit der Vigenère-Chiffre zu entschlüsseln, und umgekehrt."),
          ("The first {NAME} component is set to {\\b Decrypt}: it subtracts the key letters of APPLE from the plaintext letters (A = 0, B = 1, ...), which is the Variant encipherment. The second component, set to {\\b Encrypt}, adds them again.",
           "Die erste Komponente {NAME} ist auf {\\b Entschlüsseln} gestellt: Sie zieht die Schlüsselbuchstaben von APPLE von den Klartextbuchstaben ab (A = 0, B = 1, ...), das ist die Verschlüsselung der Variant-Chiffre. Die zweite Komponente, auf {\\b Verschlüsseln} gestellt, addiert sie wieder."),
          "Variant, Vigenère, polyalphabetic, ACA"))
QUAG_EN = ("A Quagmire is a periodic cipher with keyed alphabets. The indicator key gives the period and the setting of the cipher alphabet against the plain alphabet; every letter of the indicator selects one alternative alphabet.",
           "Ein Quagmire ist eine periodische Chiffre mit gemischten Alphabeten. Der Indikator bestimmt die Periode und die Stellung des Geheimtextalphabets gegen das Klartextalphabet; jeder Buchstabe des Indikators wählt ein anderes Alphabet.")
add(aca("Quagmire1Cipher", 1, "THE QUAG ONE IS A PERIODIC CIPHER WITH A KEYED PLAIN ALPHABET", ["SPRINGFEVER", "FLOWER"], "Quagmire I Cipher", "Quagmire-I-Chiffre", QUAG_EN,
        ("Quagmire I uses a keyed plain alphabet (here from the key word SPRINGFEVER) against a straight cipher alphabet. The indicator key FLOWER gives the period 6: its letter stands under the plain letter A (see the setting {\\b Indicator under}).",
         "Quagmire I verwendet ein gemischtes Klartextalphabet (hier aus dem Schlüsselwort SPRINGFEVER) gegen ein gerades Geheimtextalphabet. Der Indikator FLOWER ergibt die Periode 6: Sein Buchstabe steht unter dem Klartextbuchstaben A (siehe die Einstellung {\\b Indikator unter})."),
        "Quagmire, keyed alphabet, indicator, ACA"))
add(aca("Quagmire2Cipher", 2, "IN THE QUAG TWO A STRAIGHT PLAIN ALPHABET IS RUN AGAINST A KEYED CIPHER ALPHABET", ["SPRINGFEVER", "FLOWER"], "Quagmire II Cipher", "Quagmire-II-Chiffre", QUAG_EN,
        ("Quagmire II runs a straight plain alphabet against a keyed cipher alphabet (from the key word SPRINGFEVER). The indicator key FLOWER has 6 letters, so the period is 6.",
         "Quagmire II lässt ein gerades Klartextalphabet gegen ein gemischtes Geheimtextalphabet (aus dem Schlüsselwort SPRINGFEVER) laufen. Der Indikator FLOWER hat 6 Buchstaben, die Periode ist also 6."),
        "Quagmire, keyed alphabet, indicator, ACA"))
add(aca("Quagmire3Cipher", 3, "THE SAME KEYED ALPHABET IS USED FOR PLAIN AND CIPHER ALPHABETS", ["AUTOMOBILE", "HIGHWAY"], "Quagmire III Cipher", "Quagmire-III-Chiffre", QUAG_EN,
        ("Quagmire III uses the same keyed alphabet (key word AUTOMOBILE) as plain and cipher alphabet. The indicator HIGHWAY has 7 letters, so the period is 7.",
         "Quagmire III verwendet dasselbe gemischte Alphabet (Schlüsselwort AUTOMOBILE) als Klartext- und Geheimtextalphabet. Der Indikator HIGHWAY hat 7 Buchstaben, die Periode ist also 7."),
        "Quagmire, keyed alphabet, indicator, ACA"))
add(aca("Quagmire4Cipher", 4, "THIS ONE EMPLOYS THREE KEYWORDS", ["SENSORY", "EXTRA", "PERCEPTION"], "Quagmire IV Cipher", "Quagmire-IV-Chiffre", QUAG_EN,
        ("Quagmire IV has three keys: the key word of the plain alphabet (SENSORY), the indicator (EXTRA) and the key word of the cipher alphabet (PERCEPTION). In this example the indicator stands under the plain letter S, the setting {\\b Indicator under} of the components.",
         "Quagmire IV hat drei Schlüssel: das Schlüsselwort des Klartextalphabets (SENSORY), den Indikator (EXTRA) und das Schlüsselwort des Geheimtextalphabets (PERCEPTION). In diesem Beispiel steht der Indikator unter dem Klartextbuchstaben S, der Einstellung {\\b Indikator unter} der Komponenten."),
        "Quagmire, keyed alphabet, indicator, ACA", dict(Under="S")))
add(aca("PortaxCipher", 5, "THE EARLY BIRD GETS THE WORM", ["EASY"], "Portax Cipher", "Portax-Chiffre",
        ("Portax enciphers vertical pairs of letters with a slide: the two letters are opposite corners of a rectangle in the slide, the other two corners are the cipher letters. The key letter sets the slide.",
         "Portax verschlüsselt senkrechte Buchstabenpaare mit einem Schieber: Die beiden Buchstaben sind gegenüberliegende Ecken eines Rechtecks im Schieber, die anderen beiden Ecken sind die Geheimtextbuchstaben. Der Schlüsselbuchstabe stellt den Schieber ein."),
        ("The text is written in rows under the key word EASY, filled up with X, and each column is enciphered in pairs of two rows with the slide set by its key letter. The cipher is reciprocal: the second component, in decryption mode, performs the same operation.",
         "Der Text wird in Zeilen unter das Schlüsselwort EASY geschrieben, mit X aufgefüllt, und jede Spalte wird in Paaren aus zwei Zeilen mit dem Schieber verschlüsselt, den ihr Schlüsselbuchstabe einstellt. Die Chiffre ist reziprok: Die zweite Komponente führt im Entschlüsselungsmodus dieselbe Operation aus."),
        "Portax, slide, digraph, ACA"))
add(aca("SlidefairCipher", 6, "THE SLIDEFAIR CAN BE USED WITH VIGENERE VARIANT OR BEAUFORT", ["DIGRAPH"], "Slidefair Cipher", "Slidefair-Chiffre",
        ("Slidefair enciphers pairs of letters: the first one is looked up in the top alphabet, the second one in the row of a Vigenère, Variant or Beaufort table that the key letter selects. The other corners of the rectangle are the cipher letters.",
         "Slidefair verschlüsselt Buchstabenpaare: Der erste wird im oberen Alphabet gesucht, der zweite in der Zeile einer Vigenère-, Variant- oder Beaufort-Tabelle, die der Schlüsselbuchstabe wählt. Die anderen Ecken des Rechtecks sind die Geheimtextbuchstaben."),
        ("The key word DIGRAPH gives the period 7: the pairs use the key letters D, I, G, R, A, P, H one after the other. Choose another table (Variant or Beaufort) in the settings of the {NAME} components.",
         "Das Schlüsselwort DIGRAPH ergibt die Periode 7: Die Paare benutzen nacheinander die Schlüsselbuchstaben D, I, G, R, A, P, H. Wählen Sie in den Einstellungen der Komponenten {NAME} eine andere Tabelle (Variant oder Beaufort)."),
        "Slidefair, digraph, Vigenère, ACA", dict(Table="0")))
add(aca("ProgressiveKeyCipher", 7, "THIS CIPHER CAN BE USED WITH ANY OF THE PERIODICS", ["GRAPEFRUIT"], "Progressive Key Cipher", "Chiffre mit fortschreitendem Schlüssel",
        ("The text is enciphered with the key word, then a second time with a key letter that moves on after every group of one period: A, B, C, ... for the progression index 1.",
         "Der Text wird mit dem Schlüsselwort verschlüsselt, dann ein zweites Mal mit einem Schlüsselbuchstaben, der nach jeder Gruppe einer Periode weiterrückt: A, B, C, ... beim Fortschreitungsindex 1."),
        ("The key word GRAPEFRUIT has 10 letters, so the second key letter is A for the first 10 letters, B for the next 10 and so on. The progression index can be changed in the settings of the {NAME} components.",
         "Das Schlüsselwort GRAPEFRUIT hat 10 Buchstaben; der zweite Schlüsselbuchstabe ist also A für die ersten 10 Buchstaben, B für die nächsten 10 und so weiter. Der Fortschreitungsindex lässt sich in den Einstellungen der Komponenten {NAME} ändern."),
        "progressive key, Vigenère, polyalphabetic, ACA"))
add(aca("InterruptedKeyCipher", 8, "THIS CIPHER CAN BE USED WITH ANY OF THE PERIODICS", ["ORANGE", "4 6 2 3 4 3 1 1 5 1 2 3 5"], "Interrupted Key Cipher", "Chiffre mit unterbrochenem Schlüssel",
        ("A Vigenère cipher whose key word is interrupted: after a number of letters it starts again from its first letter.",
         "Eine Vigenère-Chiffre, deren Schlüsselwort unterbrochen wird: Nach einer Anzahl von Buchstaben beginnt es wieder beim ersten Buchstaben."),
        ("The key word is ORANGE; the second key gives the lengths of the pieces that are used: 4 letters (ORAN), then 6 (ORANGE), then 2 (OR) and so on, as in the example of the ACA. The entire key word has to be used at least once.",
         "Das Schlüsselwort ist ORANGE; der zweite Schlüssel gibt die Längen der benutzten Stücke an: 4 Buchstaben (ORAN), dann 6 (ORANGE), dann 2 (OR) und so weiter, wie im Beispiel der ACA. Das ganze Schlüsselwort muss mindestens einmal benutzt werden."),
        "interrupted key, Vigenère, polyalphabetic, ACA"))
add(reuse("RunningKeyCipher", VIGENERE, "ITHANYOFTHEPERIODICS", ["THISCIPHERCANBEUSEDW"], (dict(Mode="0", Action="0"), dict(Mode="0", Action="1")), "Running Key Cipher", "Chiffre mit laufendem Schlüssel",
          ("The key is a long text that is as long as the message, for example a passage of a book; it is added letter by letter as in the Vigenère cipher.",
           "Der Schlüssel ist ein langer Text, der so lang ist wie die Nachricht, zum Beispiel eine Buchstabenfolge aus einem Buch; er wird Buchstabe für Buchstabe addiert wie bei der Vigenère-Chiffre."),
          ("A running key cipher is a Vigenère cipher whose key is as long as the text, so the {NAME} component does it. In the example of the ACA the text is divided in half: the first half (THISCIPHERCANBEUSEDW) is the key of the second half.",
           "Eine Chiffre mit laufendem Schlüssel ist eine Vigenère-Chiffre, deren Schlüssel so lang ist wie der Text, die Komponente {NAME} erledigt das also. Im Beispiel der ACA wird der Text in zwei Hälften geteilt: Die erste Hälfte (THISCIPHERCANBEUSEDW) ist der Schlüssel der zweiten."),
          "running key, book cipher, Vigenère, ACA"))
add(aca("GromarkCipher", 10, "THERE ARE UP TO TEN SUBSTITUTES PER LETTER", ["ENIGMA", "23452"], "Gromark Cipher", "Gromark-Chiffre",
        ("Gromark is a Gronsfeld cipher with a mixed alphabet and a running key: the digits of the key are made from a primer by adding pairs of digits (mod 10).",
         "Gromark ist eine Gronsfeld-Chiffre mit gemischtem Alphabet und laufendem Schlüssel: Die Ziffern des Schlüssels entstehen aus einem Primer durch Addition von Ziffernpaaren (mod 10)."),
        ("The cipher alphabet comes from the key word ENIGMA (the columns of its transposition block, taken in alphabetical order). The primer 23452 is extended to 2 3 4 5 2 5 7 9 7 7 ...; each digit says how far to the right of the plain letter the cipher letter stands.",
         "Das Geheimtextalphabet kommt aus dem Schlüsselwort ENIGMA (den Spalten seines Transpositionsblocks in alphabetischer Reihenfolge). Der Primer 23452 wird zu 2 3 4 5 2 5 7 9 7 7 ... verlängert; jede Ziffer sagt, wie weit rechts vom Klartextbuchstaben der Geheimtextbuchstabe steht."),
        "Gromark, Gronsfeld, running key, ACA"))
add(aca("PeriodicGromarkCipher", 11, "WINTRY SHOWERS WILL CONTINUE FOR THE NEXT FEW DAYS ACCORDING TO THE FORECAST", ["ENIGMA"], "Periodic Gromark Cipher", "Periodische Gromark-Chiffre",
        ("A Gromark cipher in which the numbering of the key word is the primer and every group of one period starts at another place of the cipher alphabet.",
         "Eine Gromark-Chiffre, bei der die Nummerierung des Schlüsselworts der Primer ist und jede Gruppe einer Periode an einer anderen Stelle des Geheimtextalphabets beginnt."),
        ("The key word ENIGMA gives the primer 2 6 4 3 5 1 and the cipher alphabet; the key letters E, N, I, G, M, A (one for every group of 6 letters) set where the alphabet starts.",
         "Das Schlüsselwort ENIGMA ergibt den Primer 2 6 4 3 5 1 und das Geheimtextalphabet; die Schlüsselbuchstaben E, N, I, G, M, A (einer je Gruppe von 6 Buchstaben) legen fest, wo das Alphabet beginnt."),
        "periodic Gromark, running key, ACA"))
add(aca("CondiCipher", 12, "OURS IS A VERY GREEN PASTIME. THE WIDE VARIETY OF CIPHERS WE USE CAN ALL BE SOLVED WITH PENCIL AND PAPER.", ["STRANGE"], "Condi Cipher", "Condi-Chiffre",
        ("The Condi cipher uses a keyed alphabet as a table; the step from a plaintext letter to its substitute is the position of the plaintext letter before it in the alphabet.",
         "Die Condi-Chiffre benutzt ein gemischtes Alphabet als Tabelle; die Schrittweite vom Klartextbuchstaben zu seinem Ersatz ist die Position des vorhergehenden Klartextbuchstabens im Alphabet."),
        ("The key word is STRANGE, the alphabet is moved by 5 places and the first letter is substituted with the start value 25 (settings {\\b Alphabet shift} and {\\b Offset} of the {NAME} components). Word divisions and punctuation are kept.",
         "Das Schlüsselwort ist STRANGE, das Alphabet ist um 5 Stellen verschoben, und der erste Buchstabe wird mit dem Startwert 25 ersetzt (Einstellungen {\\b Alphabetverschiebung} und {\\b Versatz} der Komponenten {NAME}). Wortgrenzen und Satzzeichen bleiben erhalten."),
        "Condi, keyed alphabet, autokey, ACA", dict(Shift="5", Offset="25")))
add(aca("RagbabyCipher", 13, "WORD DIVISIONS ARE KEPT", ["GROSBEAK"], "Ragbaby Cipher", "Ragbaby-Chiffre",
        ("Ragbaby shifts every letter in a 24-letter keyed alphabet by the number of its place: 1 for the first letter of the first word, 2 for the second letter, and so on; every new word starts with the number of the word.",
         "Ragbaby verschiebt jeden Buchstaben in einem gemischten Alphabet aus 24 Buchstaben um die Zahl seiner Stelle: 1 für den ersten Buchstaben des ersten Wortes, 2 für den zweiten Buchstaben und so weiter; jedes neue Wort beginnt mit der Nummer des Wortes."),
        ("The alphabet comes from the key word GROSBEAK (I/J and W/X share a place). The numbers are 1 2 3 4 for WORD, 2 3 4 5 ... for DIVISIONS, 3 4 5 for ARE and 4 5 6 7 for KEPT.",
         "Das Alphabet kommt aus dem Schlüsselwort GROSBEAK (I/J und W/X teilen sich eine Stelle). Die Zahlen sind 1 2 3 4 für WORD, 2 3 4 5 ... für DIVISIONS, 3 4 5 für ARE und 4 5 6 7 für KEPT."),
        "Ragbaby, keyed alphabet, ACA"))
PHILLIPS_TEXT = "SQUARES ONE AND FIVE ARE ACTUALLY THE SAME AS ARE SQUARES TWO AND EIGHT"
add(aca("PhillipsCipher", 14, PHILLIPS_TEXT, ["DIAGONALS"], "Phillips Cipher", "Phillips-Chiffre",
        ("The Phillips cipher uses eight 5x5 Polybius squares in turn, five letters each; every letter is replaced by the one diagonally down to the right of it.",
         "Die Phillips-Chiffre benutzt nacheinander acht 5x5-Polybius-Quadrate, je fünf Buchstaben; jeder Buchstabe wird durch den schräg rechts unter ihm ersetzt."),
        ("The first square is made from the key word DIAGONALS (written in alternating directions). Squares 2 to 5 move its first row down one row at a time, squares 6 to 8 do this with the second row; the period is therefore 40 letters.",
         "Das erste Quadrat entsteht aus dem Schlüsselwort DIAGONALS (in wechselnder Richtung geschrieben). Die Quadrate 2 bis 5 verschieben seine erste Zeile jeweils um eine Zeile nach unten, die Quadrate 6 bis 8 tun dasselbe mit der zweiten Zeile; die Periode beträgt daher 40 Buchstaben."),
        "Phillips, Polybius, square, ACA"))
add(aca("PhillipsRCCipher", 15, PHILLIPS_TEXT, ["DIAGONALS"], "Phillips-RC Cipher", "Phillips-RC-Chiffre",
        ("Phillips-RC is the Phillips cipher in which both the rows and the columns are moved for each new square.",
         "Phillips-RC ist die Phillips-Chiffre, bei der für jedes neue Quadrat sowohl die Zeilen als auch die Spalten verschoben werden."),
        ("Like in the Phillips cipher the eight squares are used in turn with five letters each, but from square to square a row and a column are moved. The key word is DIAGONALS.",
         "Wie bei der Phillips-Chiffre werden die acht Quadrate nacheinander mit je fünf Buchstaben benutzt, aber von Quadrat zu Quadrat werden eine Zeile und eine Spalte verschoben. Das Schlüsselwort ist DIAGONALS."),
        "Phillips, Polybius, square, ACA"))
add(reuse("KeyPhraseCipher", SUBSTITUTION, "A CIPHER TEXT LETTER MAY STAND FOR MORE THAN ONE PLAIN TEXT LETTER", ["ABCDEFGHIJKLMNOPQRSTUVWXYZ", "GIVEMELIBERTYORGIVEMEDEATH"], (dict(Action="0"), dict(Action="1")), "Key Phrase Cipher", "Schlüsselsatz-Chiffre",
          ("The cipher alphabet is a phrase of exactly 26 letters under the straight plain alphabet. Letters of the phrase occur several times, so a cipher letter can stand for more than one plain letter.",
           "Das Geheimtextalphabet ist ein Satz aus genau 26 Buchstaben unter dem geraden Klartextalphabet. Buchstaben des Satzes kommen mehrfach vor, ein Geheimtextbuchstabe kann also für mehrere Klartextbuchstaben stehen."),
          ("This is a simple substitution, so the {NAME} component does it: the source alphabet is A to Z, the destination alphabet is the phrase GIVE ME LIBERTY OR GIVE ME DEATH (26 letters). Decryption is not unique because letters of the phrase repeat.",
           "Das ist eine einfache Substitution, die Komponente {NAME} erledigt sie also: Das Quellalphabet ist A bis Z, das Zielalphabet der Satz GIVE ME LIBERTY OR GIVE ME DEATH (26 Buchstaben). Das Entschlüsseln ist nicht eindeutig, weil sich Buchstaben des Satzes wiederholen."),
          "key phrase, substitution, ambiguous, ACA"))
add(aca("NumberedKeyCipher", 17, "THE ROAD TO SUCCESS IS ALWAYS UNDER CONSTRUCTION", ["I LIKE CIPHERS"], "Numbered Key Cipher", "Chiffre mit nummeriertem Schlüssel",
        ("The key phrase is extended by the missing letters of the alphabet and numbered; every plaintext letter is replaced by the number of one of its places.",
         "Der Schlüsselsatz wird um die fehlenden Buchstaben des Alphabets erweitert und nummeriert; jeder Klartextbuchstabe wird durch die Nummer einer seiner Stellen ersetzt."),
        ("The extended key of \"I LIKE CIPHERS\" is ILIKECIPHERSABDFGJMNOQTUVWXYZ; the numbering starts at its 18th place (setting {\\b Offset}). The letter I occurs three times and has the numbers 11, 13 and 17, which are used in turn.",
         "Der erweiterte Schlüssel von \"I LIKE CIPHERS\" ist ILIKECIPHERSABDFGJMNOQTUVWXYZ; die Nummerierung beginnt an seiner 18. Stelle (Einstellung {\\b Versatz}). Der Buchstabe I kommt dreimal vor und hat die Nummern 11, 13 und 17, die der Reihe nach benutzt werden."),
        "numbered key, substitution, homophonic, ACA", dict(Offset="18")))
add(aca("HomophonicCipher", 18, "WORD DIVISIONS MAY BE KEPT", ["GOLF"], "Homophonic Cipher (ACA)", "Homophone Chiffre (ACA)",
        ("Every letter has four two-digit numbers (01 to 00 in four rows of 25); the four letters of the key word say where the numbers of the rows start.",
         "Jeder Buchstabe hat vier zweistellige Zahlen (01 bis 00 in vier Zeilen zu 25); die vier Buchstaben des Schlüsselworts sagen, wo die Zahlen der Zeilen beginnen."),
        ("The key word GOLF gives G the number 01, O the number 26, L the number 51 and F the number 76. The component takes the four rows in turn, so repeated letters get different numbers.",
         "Das Schlüsselwort GOLF gibt G die Nummer 01, O die Nummer 26, L die Nummer 51 und F die Nummer 76. Die Komponente nimmt die vier Zeilen der Reihe nach, wiederholte Buchstaben erhalten also verschiedene Nummern."),
        "homophonic, substitution, numbers, ACA"))
add(aca("NullCipher", 19, "HELP", ["THE GREAT OLD PUMPERS SOLD THE SOFA"], "Null Cipher", "Nullen-Chiffre",
        ("A null cipher hides the message in a harmless text: here the middle letter of each word is a letter of the message.",
         "Eine Nullen-Chiffre versteckt die Nachricht in einem harmlosen Text: Hier ist der mittlere Buchstabe jedes Wortes ein Buchstabe der Nachricht."),
        ("The first component takes for every letter of HELP the next word of the cover text with this letter in the middle: THE, GREAT, OLD, PUMPERS. The second one reads the middle letters again. The place of the letter (first, middle, last) is a setting of the {NAME} components.",
         "Die erste Komponente nimmt für jeden Buchstaben von HELP das nächste Wort des Deckungstextes, das diesen Buchstaben in der Mitte hat: THE, GREAT, OLD, PUMPERS. Die zweite liest die mittleren Buchstaben wieder aus. Die Stelle des Buchstabens (erster, mittlerer, letzter) ist eine Einstellung der Komponenten {NAME}."),
        "null cipher, steganography, concealment, ACA", dict(NullPosition="1")))

# ---------------------------------------------------------------------------------------------------------------- transposition ciphers
COLUMNAR_TEXT = "WE ARE DISCOVERED FLEE AT ONCE"
add(aca("MyszkowskiTransposition", 22, "INCOMPLETE COLUMNAR WITH PATTERN WORD KEY AND LETTERS UNDER SAME NUMBER TAKEN OFF BY ROW", ["BANANA"], "Myszkowski Transposition", "Myszkowski-Transposition",
        ("A columnar transposition with a key word that has repeated letters: columns with the same letter get the same number and are read together, row by row.",
         "Eine Spaltentransposition mit einem Schlüsselwort mit wiederholten Buchstaben: Spalten mit demselben Buchstaben erhalten dieselbe Nummer und werden zusammen, Zeile für Zeile, gelesen."),
        ("In BANANA the three A get the number 1, B the number 2 and the two N the number 3. The first piece of the ciphertext is therefore made of the 2nd, 4th and 6th columns, row by row.",
         "In BANANA erhalten die drei A die Nummer 1, B die Nummer 2 und die beiden N die Nummer 3. Das erste Stück des Geheimtextes besteht daher aus der 2., 4. und 6. Spalte, Zeile für Zeile."),
        "Myszkowski, columnar transposition, ACA"))
add(aca("AmscoCipher", 23, "INCOMPLETE COLUMNAR WITH ALTERNATING SINGLE LETTERS AND DIGRAPHS", ["41325"], "AMSCO Cipher", "AMSCO-Chiffre",
        ("AMSCO is a columnar transposition in which the places of the block hold one letter or two letters alternately.",
         "AMSCO ist eine Spaltentransposition, bei der die Plätze des Blocks abwechselnd einen oder zwei Buchstaben enthalten."),
        ("The key 41325 gives the order of the columns. The first place holds two letters (setting {\\b Start with a digraph}); the pattern alternates along the rows and down the columns, so the block is a checkerboard of digraphs and single letters.",
         "Der Schlüssel 41325 gibt die Reihenfolge der Spalten an. Der erste Platz enthält zwei Buchstaben (Einstellung {\\b Mit einem Digraph beginnen}); das Muster wechselt entlang der Zeilen und die Spalten hinunter, der Block ist also ein Schachbrett aus Digraphen und einzelnen Buchstaben."),
        "AMSCO, columnar transposition, digraph, ACA", dict(StartWithDigraph="True")))
CADENUS_TEXT = ("ASEVERELIMITATIONONTHEUSEFULNESSOFTHECADENUSISTHATEVERYMESSAGEMUSTBEAMULTIPLEOFTWENTYFIVELETTERSLONG")
add(aca("CadenusCipher", 24, CADENUS_TEXT, ["EASY"], "Cadenus Cipher", "Cadenus-Chiffre",
        ("Cadenus is a columnar transposition of a block of 25 rows in which every column also starts at another row: a letter of the alphabet is attached to each row and the key letter chooses the row where its column begins.",
         "Cadenus ist eine Spaltentransposition eines Blocks aus 25 Zeilen, bei der jede Spalte außerdem in einer anderen Zeile beginnt: An jeder Zeile steht ein Buchstabe des Alphabets, und der Schlüsselbuchstabe wählt die Zeile, in der seine Spalte beginnt."),
        ("The key word EASY has 4 letters, so the text needs 100 letters. The first plaintext column starts at the 22nd row (the one with E) and goes on around the block. The text of this template is the example of the ACA.",
         "Das Schlüsselwort EASY hat 4 Buchstaben, der Text braucht also 100 Buchstaben. Die erste Klartextspalte beginnt in der 22. Zeile (der mit E) und geht rund um den Block weiter. Der Text dieser Vorlage ist das Beispiel der ACA."),
        "Cadenus, columnar transposition, ACA"))
add(aca("NicodemusCipher", 25, "THE EARLY BIRD GETS THE WORM BUT THE SECOND MOUSE GETS THE CHEESE", ["CAT"], "Nicodemus Cipher", "Nicodemus-Chiffre",
        ("Nicodemus combines three steps: a columnar transposition, a Vigenère encipherment of the columns with the same key, and reading the columns off five letters at a time.",
         "Nicodemus verbindet drei Schritte: eine Spaltentransposition, eine Vigenère-Verschlüsselung der Spalten mit demselben Schlüssel und das Ablesen der Spalten jeweils fünf Buchstaben auf einmal."),
        ("With the key CAT the columns are put in the order A, C, T and enciphered with the key letters A, C, T; then the first five letters of every column are taken, then the next five, and so on.",
         "Mit dem Schlüssel CAT werden die Spalten in die Reihenfolge A, C, T gebracht und mit den Schlüsselbuchstaben A, C, T verschlüsselt; danach werden die ersten fünf Buchstaben jeder Spalte genommen, dann die nächsten fünf und so weiter."),
        "Nicodemus, columnar transposition, Vigenère, ACA"))
add(aca("NihilistTransposition", 26, "SQUARE NEEDED HERE", ["2134"], "Nihilist Transposition", "Nihilisten-Transposition",
        ("The text fills a square; its columns and then its rows are moved by the same key, and the result is read by rows or by columns.",
         "Der Text füllt ein Quadrat; seine Spalten und dann seine Zeilen werden mit demselben Schlüssel verschoben, und das Ergebnis wird zeilen- oder spaltenweise gelesen."),
        ("The key 2134 says that the 1st column goes to the 2nd place, the 2nd to the 1st and so on, and the same for the rows. In this template the square is read by rows; the setting {\\b Read by columns} of the {NAME} components reads it by columns.",
         "Der Schlüssel 2134 sagt, dass die 1. Spalte an die 2. Stelle kommt, die 2. an die 1. und so weiter, und dasselbe gilt für die Zeilen. In dieser Vorlage wird das Quadrat zeilenweise gelesen; die Einstellung {\\b Spaltenweise lesen} der Komponenten {NAME} liest es spaltenweise."),
        "Nihilist, transposition, square, ACA", dict(ByColumns="False")))
add(aca("RouteTransposition", 27, "THERE ARE MANY ROUTES", [], "Route Transposition", "Routentransposition",
        ("The text is written into a block along one route and read from it along another, for example along diagonals and spirals.",
         "Der Text wird entlang einer Route in einen Block geschrieben und entlang einer anderen aus ihm gelesen, zum Beispiel entlang von Diagonalen und Spiralen."),
        ("The block has 3 columns. The text is written in by alternating diagonals and taken out by a clockwise spiral (as in the example of the ACA). The settings {\\b Write route} and {\\b Read route} of the {NAME} components offer ten routes.",
         "Der Block hat 3 Spalten. Der Text wird auf abwechselnden Diagonalen eingeschrieben und auf einer Spirale im Uhrzeigersinn herausgenommen (wie im Beispiel der ACA). Die Einstellungen {\\b Schreibroute} und {\\b Leseroute} der Komponenten {NAME} bieten zehn Routen."),
        "route transposition, spiral, diagonal, ACA", dict(Columns="3", WriteRoute="5", ReadRoute="6")))
add(aca("SequenceTransposition", 28, "THE EARLY BIRD GETS THE WORM", ["GUMMYBEARS", "69315"], "Sequence Transposition", "Sequenz-Transposition",
        ("A numeric sequence made from a primer decides into which column every plaintext letter goes; the columns are numbered by a key word.",
         "Eine aus einem Primer gebildete Zahlenfolge entscheidet, in welche Spalte jeder Klartextbuchstabe kommt; die Spalten werden durch ein Schlüsselwort nummeriert."),
        ("The primer 69315 is extended to the length of the text by adding pairs of digits (6 9 3 1 5 5 2 4 ...). The key word GUMMYBEARS numbers its columns 4 9 5 6 0 2 3 1 7 8. A letter with the digit 4 goes into the first column and so on.",
         "Der Primer 69315 wird durch Addition von Ziffernpaaren auf die Länge des Textes verlängert (6 9 3 1 5 5 2 4 ...). Das Schlüsselwort GUMMYBEARS nummeriert seine Spalten mit 4 9 5 6 0 2 3 1 7 8. Ein Buchstabe mit der Ziffer 4 kommt in die erste Spalte und so weiter."),
        "sequence transposition, primer, ACA"))
add(reuse("GrilleCipher", FLEISSNER, "THETURNINGGRILLE", [],
          (dict(ActionMode="0", ActionRotate="1", StencilString="1000000101010000"), dict(ActionMode="1", ActionRotate="1", StencilString="1000000101010000")), "Turning Grille", "Drehschablone (Grille)",
          ("A square card with holes is laid on the block; a quarter of the text is written through the holes, then the card is turned by 90 degrees, and so on until the block is full.",
           "Eine quadratische Karte mit Löchern wird auf den Block gelegt; ein Viertel des Textes wird durch die Löcher geschrieben, dann wird die Karte um 90 Grad gedreht, und so weiter, bis der Block voll ist."),
          ("The {NAME} component does this (it is an experimental component). The 4x4 grille of the ACA has the holes 1, 8, 10 and 12 (counted by rows); written as the stencil it is 1000000101010000 (1 = hole), which is entered in the settings of the component. Each of the four turns uses four places, and together they cover all 16 places once. The text has exactly 16 letters; a shorter text would be filled up with random letters. The ciphertext is read by rows; the second component, set to decrypt, undoes it.",
           "Die Komponente {NAME} erledigt das (es ist eine experimentelle Komponente). Die 4x4-Schablone der ACA hat die Löcher 1, 8, 10 und 12 (zeilenweise gezählt); als Stencil geschrieben ist das 1000000101010000 (1 = Loch), der in den Einstellungen der Komponente eingetragen wird. Jede der vier Drehungen benutzt vier Plätze, und zusammen decken sie alle 16 Plätze genau einmal ab. Der Text hat genau 16 Buchstaben; ein kürzerer Text würde mit Zufallsbuchstaben aufgefüllt. Der Geheimtext wird zeilenweise gelesen; die zweite Komponente, auf Entschlüsseln gestellt, macht es rückgängig."),
          "grille, turning grille, Cardan, Fleißner, transposition, ACA"))
add(aca("SwagmanCipher", 30, "DONT BE AFRAID TO TAKE A BIG LEAP", ["32145 15324 24531 53412 41253"], "Swagman Cipher", "Swagman-Chiffre",
        ("Swagman is a transposition with a Latin square as key: in every column of the text the letters are moved to the rows that the numbers of the key square give.",
         "Swagman ist eine Transposition mit einem lateinischen Quadrat als Schlüssel: In jeder Spalte des Textes werden die Buchstaben in die Zeilen verschoben, die die Zahlen des Schlüsselquadrats angeben."),
        ("The text is written into 5 rows; the key square (5x5, no number repeats in a row or a column) is repeated along the columns. The first column D E N C S goes to the rows 3 1 2 5 4 and becomes E N D S C. The ciphertext is read by columns.",
         "Der Text wird in 5 Zeilen geschrieben; das Schlüsselquadrat (5x5, keine Zahl wiederholt sich in einer Zeile oder Spalte) wird entlang der Spalten wiederholt. Die erste Spalte D E N C S geht in die Zeilen 3 1 2 5 4 und wird zu E N D S C. Der Geheimtext wird spaltenweise gelesen."),
        "Swagman, Latin square, transposition, ACA"))

# ---------------------------------------------------------------------------------------------------------------- fractionation and digraph ciphers
add(aca("CMBifidCipher", 31, "ODD PERIODS ARE POPULAR", [SQ1, "NOVELTY"], "CM Bifid Cipher", "CM-Bifid-Chiffre",
        ("The conjugated matrix bifid cipher is the Bifid cipher with two squares: the plaintext letters are looked up in the first, the numbers are mixed over a period and read as letters of the second.",
         "Die Conjugated-Matrix-Bifid-Chiffre ist die Bifid-Chiffre mit zwei Quadraten: Die Klartextbuchstaben werden im ersten gesucht, die Zahlen über eine Periode gemischt und als Buchstaben des zweiten gelesen."),
        ("The first square is given by the 25 letters of the first key, the second by the key word NOVELTY written in alternating verticals. The period is 7 (setting {\\b Period}): the row numbers and column numbers of 7 letters are written one after the other and taken in pairs.",
         "Das erste Quadrat ist durch die 25 Buchstaben des ersten Schlüssels gegeben, das zweite durch das Schlüsselwort NOVELTY, in abwechselnden Senkrechten geschrieben. Die Periode ist 7 (Einstellung {\\b Periode}): Die Zeilen- und Spaltennummern von 7 Buchstaben werden hintereinander geschrieben und paarweise genommen."),
        "CM Bifid, conjugated matrix, fractionation, ACA", dict(Period="7")))
add(aca("DigrafidCipher", 32, "THIS IS THE FOREST PRIMEVAL", ["KEYWORD", "VERTICAL"], "Digrafid Cipher", "Digrafid-Chiffre",
        ("Digrafid gives every pair of letters a three-digit number from a table with two mixed alphabets, fractionates the digits over a group of pairs and converts them back to pairs.",
         "Digrafid gibt jedem Buchstabenpaar eine dreistellige Zahl aus einer Tabelle mit zwei gemischten Alphabeten, mischt die Ziffern über eine Gruppe von Paaren und wandelt sie zurück in Paare."),
        ("The horizontal alphabet comes from the key word KEYWORD, the vertical one from VERTICAL. With the period 3 (setting {\\b Period}) the digits of 3 pairs are written under each other and read by lines. # is the 27th symbol; an odd text gets one.",
         "Das waagerechte Alphabet kommt aus dem Schlüsselwort KEYWORD, das senkrechte aus VERTICAL. Bei der Periode 3 (Einstellung {\\b Periode}) werden die Ziffern von 3 Paaren untereinander geschrieben und zeilenweise gelesen. # ist das 27. Zeichen; ein ungerader Text erhält eines."),
        "Digrafid, fractionation, digraph, ACA", dict(Period="3")))
add(aca("FractionatedMorseCipher", 33, "COME AT ONCE", ["ROUNDTABLE"], "Fractionated Morse Cipher", "Fraktioniertes Morse",
        ("The text is written in Morse code (x between letters, xx between words), the dots, dashes and x are taken in groups of three, and every group is a letter of a keyed alphabet.",
         "Der Text wird in Morsecode geschrieben (x zwischen Buchstaben, xx zwischen Wörtern), die Punkte, Striche und x werden in Dreiergruppen genommen, und jede Gruppe ist ein Buchstabe eines gemischten Alphabets."),
        ("There are 26 groups of three (all but xxx), one for each letter of the keyed alphabet ROUNDTABLE... The first group \"-.-\" becomes C. The second component decrypts to the Morse code and from there back to the text.",
         "Es gibt 26 Dreiergruppen (alle außer xxx), je eine für jeden Buchstaben des gemischten Alphabets ROUNDTABLE... Die erste Gruppe \"-.-\" wird zu C. Die zweite Komponente entschlüsselt zum Morsecode und von dort zurück zum Text."),
        "fractionated Morse, Morse, fractionation, ACA"))
add(aca("MorbitCipher", 34, "ONCE UPON A TIME", ["WISECRACK"], "Morbit Cipher", "Morbit-Chiffre",
        ("Morbit writes the text in Morse code and replaces every pair of symbols (dot, dash, x) by a digit from 1 to 9.",
         "Morbit schreibt den Text in Morsecode und ersetzt jedes Paar von Zeichen (Punkt, Strich, x) durch eine Ziffer von 1 bis 9."),
        ("The key word WISECRACK of 9 letters numbers the nine possible pairs 9 5 8 4 2 7 1 3 6 (letters in alphabetical order, equal letters from left to right). The first pair \"--\" becomes 2.",
         "Das Schlüsselwort WISECRACK aus 9 Buchstaben nummeriert die neun möglichen Paare mit 9 5 8 4 2 7 1 3 6 (Buchstaben in alphabetischer Reihenfolge, gleiche Buchstaben von links nach rechts). Das erste Paar \"--\" wird zu 2."),
        "Morbit, Morse, fractionation, ACA"))
add(aca("PolluxCipher", 35, "LUCK HELPS", ["x-..x.--x."], "Pollux Cipher", "Pollux-Chiffre",
        ("Pollux writes the text in Morse code; every dot, dash and separator is replaced by one of several digits.",
         "Pollux schreibt den Text in Morsecode; jeder Punkt, Strich und Trenner wird durch eine von mehreren Ziffern ersetzt."),
        ("The key says what the digits 1 2 3 4 5 6 7 8 9 0 stand for: x - . . x . - - x . (x is the separator). Every symbol uses its digits in turn, so the same letter gets different digits.",
         "Der Schlüssel sagt, wofür die Ziffern 1 2 3 4 5 6 7 8 9 0 stehen: x - . . x . - - x . (x ist der Trenner). Jedes Zeichen benutzt der Reihe nach seine Ziffern, derselbe Buchstabe erhält also verschiedene Ziffern."),
        "Pollux, Morse, digits, ACA"))
add(aca("TridigitalCipher", 36, "THE IDES OF MARCH", ["DRAGONFLY", "NOVELCRAFT"], "Tridigital Cipher", "Tridigital-Chiffre",
        ("A keyed alphabet fills a block of ten columns; every letter is replaced by the digit above its column, so every digit stands for three letters.",
         "Ein gemischtes Alphabet füllt einen Block mit zehn Spalten; jeder Buchstabe wird durch die Ziffer über seiner Spalte ersetzt, jede Ziffer steht also für drei Buchstaben."),
        ("The alphabet comes from DRAGONFLY, the digits above the columns from NOVELCRAFT (6 7 0 3 5 2 8 1 4 9). The last column is empty; its digit 9 separates the words. Decryption is not unique: the second component lists the three possible letters of each digit in brackets.",
         "Das Alphabet kommt aus DRAGONFLY, die Ziffern über den Spalten aus NOVELCRAFT (6 7 0 3 5 2 8 1 4 9). Die letzte Spalte ist leer; ihre Ziffer 9 trennt die Wörter. Das Entschlüsseln ist nicht eindeutig: Die zweite Komponente listet die drei möglichen Buchstaben jeder Ziffer in Klammern auf."),
        "Tridigital, substitution, digits, ACA"))
add(aca("TriSquareCipher", 37, "THREE KEY SQUARES USED X", ["NSFMUOAGPWVBHQXECIRYLDKTZ", "READINGBCFHKLMOPQSTUVWXYZ", "PASTINOQRMLYZUEKXWVBHGFDC"], "Tri-Square Cipher", "Tri-Square-Chiffre",
        ("Tri-Square enciphers a pair of letters with three squares and gives a group of three cipher letters; the first and the last may be chosen freely within a column or a row.",
         "Tri-Square verschlüsselt ein Buchstabenpaar mit drei Quadraten und liefert eine Gruppe von drei Geheimtextbuchstaben; der erste und der letzte dürfen innerhalb einer Spalte oder Zeile frei gewählt werden."),
        ("The first plaintext letter is looked up in square 1, the second in square 2. The first cipher letter is a letter of the column of the first one in square 1, the second one is the letter of square 3 where row and column meet, the third is a letter of the row of the second one in square 2. The component takes the free letters in turn.",
         "Der erste Klartextbuchstabe wird in Quadrat 1 gesucht, der zweite in Quadrat 2. Der erste Geheimtextbuchstabe ist ein Buchstabe der Spalte des ersten in Quadrat 1, der zweite der Buchstabe von Quadrat 3, an dem sich Zeile und Spalte treffen, der dritte ein Buchstabe der Zeile des zweiten in Quadrat 2. Die Komponente nimmt die freien Buchstaben der Reihe nach."),
        "Tri-Square, Polybius, trigraph, ACA"))
add(aca("SeriatedPlayfair", 38, "COME QUICKLY WE NEED HELP IMMEDIATELY TOM", ["LOGARITHM"], "Seriated Playfair", "Seriated Playfair",
        ("The text is written in groups of two lines; the vertical pairs are enciphered with the Playfair rules.",
         "Der Text wird in Gruppen aus zwei Zeilen geschrieben; die senkrechten Paare werden nach den Playfair-Regeln verschlüsselt."),
        ("With the period 6 the first group is COMEQU over ICKLYW (setting {\\b Period}). If a vertical pair would be a double letter, an X is put into the lower line. The ciphertext is the two lines of every group one after the other.",
         "Bei der Periode 6 ist die erste Gruppe COMEQU über ICKLYW (Einstellung {\\b Periode}). Wäre ein senkrechtes Paar ein Doppelbuchstabe, wird ein X in die untere Zeile gesetzt. Der Geheimtext besteht aus den beiden Zeilen jeder Gruppe nacheinander."),
        "Seriated Playfair, Playfair, digraph, ACA", dict(Period="6")))
add(aca("CheckerboardCipher", 39, "NUMBERS CAN ALSO BE USED AS COORDINATES", ["KNIGHPQRSTOYZUAMXWVBLFEDC", "BLACK", "WHITE"], "Checkerboard Cipher", "Checkerboard-Chiffre",
        ("A Polybius square with a key word at its left and one above it: a letter is replaced by the letter of its row and the letter of its column.",
         "Ein Polybius-Quadrat mit einem Schlüsselwort links und einem darüber: Ein Buchstabe wird durch den Buchstaben seiner Zeile und den Buchstaben seiner Spalte ersetzt."),
        ("The rows are labelled BLACK, the columns WHITE. The letter N is in the first row and the second column and becomes BH. The cipher uses only the ten letters of the two words.",
         "Die Zeilen sind mit BLACK beschriftet, die Spalten mit WHITE. Der Buchstabe N steht in der ersten Zeile und der zweiten Spalte und wird zu BH. Die Chiffre benutzt nur die zehn Buchstaben der beiden Wörter."),
        "Checkerboard, Polybius, square, ACA"))
add(aca("MonomeDinomeCipher", 40, "HIGH FREQUENCY KEYS SHORT ENCIPHER TEXT", ["NOTARIES", "RMASTERTON"], "Monome-Dinome Cipher", "Monome-Dinome-Chiffre",
        ("The 24 letters are in a box of 3 rows and 8 columns; the letters of the top row are replaced by one digit, the others by two.",
         "Die 24 Buchstaben stehen in einem Kasten aus 3 Zeilen und 8 Spalten; die Buchstaben der obersten Zeile werden durch eine Ziffer ersetzt, die anderen durch zwei."),
        ("The box comes from NOTARIES. The second key RMASTERTON gives the ten digits 6 3 1 8 9 2 7 0 5 4: the first two are the row digits of the second and third rows, the other eight are the column digits. Frequent letters in the top row make the ciphertext short.",
         "Der Kasten kommt aus NOTARIES. Der zweite Schlüssel RMASTERTON gibt die zehn Ziffern 6 3 1 8 9 2 7 0 5 4: Die ersten beiden sind die Zeilenziffern der zweiten und dritten Zeile, die anderen acht die Spaltenziffern. Häufige Buchstaben in der obersten Zeile machen den Geheimtext kurz."),
        "Monome-Dinome, digits, box, ACA"))
add(aca("CompressocratCipher", 41, "THE KEY WORD MAY BE SHIFTED TO AVOID CODING THE SAME DIGIT", ["FRACTION"], "Compressocrat Cipher", "Compressocrat-Chiffre",
        ("Compressocrat compresses the plaintext with a Huffman-like code of digits (1, 2, 3), takes the digits in groups of three and replaces every group by a letter of a keyed alphabet.",
         "Compressocrat komprimiert den Klartext mit einem Huffman-ähnlichen Code aus Ziffern (1, 2, 3), nimmt die Ziffern in Dreiergruppen und ersetzt jede Gruppe durch einen Buchstaben eines gemischten Alphabets."),
        ("Frequent letters have short codes (E = 31, T = 12), rare ones long codes (Z = 321113). The alphabet comes from FRACTION and is moved by 2 places (setting {\\b Alphabet shift}). The ciphertext is shorter than the plaintext.",
         "Häufige Buchstaben haben kurze Codes (E = 31, T = 12), seltene lange (Z = 321113). Das Alphabet kommt aus FRACTION und ist um 2 Stellen verschoben (Einstellung {\\b Alphabetverschiebung}). Der Geheimtext ist kürzer als der Klartext."),
        "Compressocrat, Huffman, fractionation, ACA", dict(Shift="2")))

add(aca("HeadlinesCipher", 42, "*Bush Signs Intelligence Overhaul Legislation\n*Bin *Laden Urges Fighters to Strike Oil Facilities\n*Pfizer: Painkiller may pose increased cardiovascular risk\n*Carrey masters disguises in '*Lemony *Snicket'\n*Martinez blasts ex-teammate *Schilling",
        ["APOTHECARY", "CHEMIST", "DRUGS"], "Headlines Cipher", "Headlines-Chiffre",
        ("Five headlines are enciphered by simple substitution with the same mixed alphabet at five different settings; proper nouns are marked with an asterisk. Three related keywords make the alphabet and the settings.",
         "Fünf Schlagzeilen werden durch einfache Substitution mit demselben gemischten Alphabet in fünf verschiedenen Stellungen verschlüsselt; Eigennamen sind mit einem Stern markiert. Drei verwandte Schlüsselwörter bilden das Alphabet und die Stellungen."),
        ("The key block is made of the key word CHEMIST (its alphabet is written in rows of the width of the hat word APOTHECARY, and the columns are taken in the order of the letters of the hat), which gives the mixed alphabet CFUAPTOSNZILYEJWHGVBQMKXDR. Every line uses the next letter of the setting word DRUGS: the letter that stands under the first letter of the alphabet (as in Quagmire III). Asterisks, spaces and punctuation are kept.",
         "Der Schlüsselblock besteht aus dem Schlüsselwort CHEMIST (sein Alphabet wird in Zeilen von der Breite des Hutwortes APOTHECARY geschrieben, und die Spalten werden in der Reihenfolge der Buchstaben des Hutes genommen), das ergibt das gemischte Alphabet CFUAPTOSNZILYEJWHGVBQMKXDR. Jede Zeile benutzt den nächsten Buchstaben des Einstellungswortes DRUGS: den Buchstaben, der unter dem ersten Buchstaben des Alphabets steht (wie bei Quagmire III). Sterne, Leerzeichen und Satzzeichen bleiben erhalten."),
        "Headlines, mixed alphabet, settings, ACA"))

add(aca("PolybiosCipher", 43, "HALLO", [""], "Polybius Cipher", "Polybios-Chiffre",
        ("The Polybius cipher replaces every letter by the number of its row and of its column in a 5x5 square; I and J share a cell.",
         "Die Polybios-Chiffre ersetzt jeden Buchstaben durch die Nummer seiner Zeile und seiner Spalte in einem 5x5-Quadrat; I und J teilen sich ein Feld."),
        ("With an empty key the square holds the plain alphabet: H is in row 2, column 3 and becomes 23, A becomes 11. A key word gives a keyed square, which is better against frequency analysis. The pairs are written with a space between them.",
         "Bei leerem Schlüssel enthält das Quadrat das einfache Alphabet: H steht in Zeile 2, Spalte 3 und wird zu 23, A wird zu 11. Ein Schlüsselwort ergibt ein gemischtes Quadrat, das gegen die Häufigkeitsanalyse besser schützt. Die Paare werden mit einem Leerzeichen dazwischen geschrieben."),
        "Polybius, Polybios, square, coordinates"))
add(aca("TapCode", 44, "WASSER", [], "Tap Code (Klopfcode)", "Klopfcode",
        ("The tap code of prisoners is a Polybius square (C and K share a cell): a letter is knocked as the number of its row, a pause, and the number of its column.",
         "Der Klopfcode von Gefangenen ist ein Polybios-Quadrat (C und K teilen sich ein Feld): Ein Buchstabe wird als Zahl der Zeile, eine Pause und Zahl der Spalte geklopft."),
        ("W is in row 5, column 2: five knocks, a pause, two knocks. The output shows the numbers as digits; a K is written as C.",
         "W steht in Zeile 5, Spalte 2: fünf Klopfer, eine Pause, zwei Klopfer. Die Ausgabe zeigt die Zahlen als Ziffern; ein K wird als C geschrieben."),
        "tap code, Klopfcode, prisoners, Polybius"))
add(aca("ABCCipher", 45, "ATTACK AT DAWN", ["ZEBRAS"], "ABC Cipher", "ABC-Chiffre",
        ("The ABC cipher has two stages: a Vigenère substitution with the key ABC (the letters of a group of three move on by 0, 1 and 2) and a columnar transposition.",
         "Die ABC-Chiffre hat zwei Stufen: eine Vigenère-Substitution mit dem Schlüssel ABC (die Buchstaben einer Dreiergruppe rücken um 0, 1 und 2 weiter) und eine Spaltentransposition."),
        ("The first stage turns ATT into ABV; the second writes the result in rows under the key word ZEBRAS and reads the columns in the alphabetical order of its letters. The second component undoes both stages.",
         "Die erste Stufe macht aus ATT die Buchstaben ABV; die zweite schreibt das Ergebnis zeilenweise unter das Schlüsselwort ZEBRAS und liest die Spalten in der alphabetischen Reihenfolge seiner Buchstaben. Die zweite Komponente macht beide Stufen rückgängig."),
        "ABC cipher, Vigenère, columnar transposition"))
add(aca("Rasterschluessel44Cipher", 46, "feind greift seit elf x vier fuenf uhr bei aa igqalfis igqalfis ee mit aqt panz naq suedwest an",
        ["##..###..#..#.##.#.##.###\n.##.#.#..#.##.##.###.##.#\n..#.#..##.##.###.#..#####\n####..##...#..###..###.##\n.###.#..#.#.##.###.##..##\n#####..##.#..#.##.#.###..\n###.###.##.#.#.##.#..#..#\n####.##.###..#.###..#.#..\n##.####.###..###.#...#..#\n#####.##.##.#.##..#.#.#..\n#.#...##.#.#.##.#.##.####\n#####.#.#.#...#..#.###.##\n###..#..##...###..###.###\n.#...#.###.#.#.##.#.#####", "3 20 8 13 5 19 1 14 9 21 18 15 24 2 12 7 10 22 4 16 25 11 23 17 6"],
        "Rasterschlüssel 44 (RS 44)", "Rasterschlüssel 44 (RS 44)",
        ("The Rasterschlüssel 44 of the German army (from March 1944) is a pure transposition on a printed form: a grid of 24 rows and 25 columns in which every row has 10 white cells. The text is written row by row into the white cells and read off by columns.",
         "Der Rasterschlüssel 44 des deutschen Heeres (ab März 1944) ist eine reine Transposition auf einem gedruckten Formular: ein Raster aus 24 Zeilen und 25 Spalten, in dem jede Zeile 10 weiße Felder hat. Der Text wird zeilenweise in die weißen Felder geschrieben und spaltenweise herausgelesen."),
        ("This is the example of the article on kryptografie.de (the first 14 rows of the form are used; the form was changed every day). Writing starts at row 5, column 14 (the cell bbae of the message header, the first white cell from there); reading starts at the column with the number 14 and goes on with 15, 16, ... 25, 1, ... 13. The ciphertext has exactly as many letters as the plaintext. The message header (time, number of letters, start cell and reading column, each coded with the letter swap table) is not made here. Text before encryption: digits, umlauts and punctuation are spelled out, ch and ck are written as q.",
         "Das ist das Beispiel aus dem Artikel auf kryptografie.de (benutzt werden die ersten 14 Zeilen des Formulars; das Formular wurde jeden Tag gewechselt). Das Schreiben beginnt in Zeile 5, Spalte 14 (die Zelle bbae des Spruchkopfs, ab dort das erste weiße Feld); das Herauslesen beginnt bei der Spalte mit der Nummer 14 und geht mit 15, 16, ... 25, 1, ... 13 weiter. Der Geheimtext hat genau so viele Buchstaben wie der Klartext. Der Spruchkopf (Uhrzeit, Buchstabenzahl, Startzelle und Herauslesespalte, jeweils mit der Buchstabentauschtafel verschlüsselt) wird hier nicht gebildet. Text vor der Verschlüsselung: Ziffern, Umlaute und Satzzeichen werden ausgeschrieben, ch und ck als q geschrieben."),
        "RS 44, Rasterschlüssel, transposition, grid, Wehrmacht",
        dict(StartRow="5", StartColumn="14", ReadNumber="14")))
add(aca("WerftschluesselCipher", 47, "KEINE BESONDEREN VORKOMMNISSE",
        ["KV=TC EO=XS IR=EK NK=HD BM=CT EM=KK SN=IP OI=LN NS=YM DS=FE EE=ZC RX=KM EX=VA NX=RB", "1 3 5 2 4 6"],
        "Werftschlüssel (Reservehandverfahren)", "Werftschlüssel (Reservehandverfahren)",
        ("The Werftschlüssel of the German navy is a hand cipher with two steps: the groups of five letters are put into the rows of a box in a secret order, then two rows at a time are replaced pair by pair (letters above each other) with pairs from a table of all 676 double letters.",
         "Der Werftschlüssel der deutschen Kriegsmarine ist ein Handverfahren mit zwei Schritten: Die Fünfergruppen werden in einer geheimen Reihenfolge in die Zeilen eines Kastens gesetzt, dann werden je zwei Zeilen Paar für Paar (übereinanderstehende Buchstaben) durch Paare aus einer Tafel aller 676 Doppelbuchstaben ersetzt."),
        ("This is the example of the article on kryptografie.de. The plaintext KEINE BESONDEREN VORKOMMNISSE gives the groups KEINE BESON DEREN VORKO MMNIS SEXXX; the row order 1 3 5 2 4 6 puts them into the rows 1, 3, 5, 2, 4, 6. The pairs of rows 1 and 2 are K/V, E/O, I/R, N/K, E/O and are replaced by TC, XS, EK, HD, XS (table C of the key booklet): the first letters TXEHX and the second letters CSKDS are the first two groups of the ciphertext. The table is involutory (KV = TC and TC = KV), so the second component decrypts with the same table. Only the 14 pairs of the example are in this table; the whole table (676 pairs, 20 or 30 of them in a key booklet) can be pasted into the input.",
         "Das ist das Beispiel aus dem Artikel auf kryptografie.de. Der Klartext KEINE BESONDEREN VORKOMMNISSE ergibt die Gruppen KEINE BESON DEREN VORKO MMNIS SEXXX; die Zeilenreihenfolge 1 3 5 2 4 6 setzt sie in die Zeilen 1, 3, 5, 2, 4, 6. Die Paare der Zeilen 1 und 2 sind K/V, E/O, I/R, N/K, E/O und werden durch TC, XS, EK, HD, XS ersetzt (Tafel C des Schlüsselhefts): Die ersten Buchstaben TXEHX und die zweiten CSKDS sind die ersten beiden Gruppen des Geheimtextes. Die Tafel ist involutorisch (KV = TC und TC = KV), die zweite Komponente entschlüsselt also mit derselben Tafel. In dieser Tafel stehen nur die 14 Paare des Beispiels; die ganze Tafel (676 Paare, 20 oder 30 davon in einem Schlüsselheft) kann in den Eingang eingefügt werden."),
        "Werftschlüssel, Reservehandverfahren, Doppelbuchstabentauschtafel, Kriegsmarine, bigram"))
add(aca("LeSphinxCipher", 48, "TOPSECRETS", ["1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20"],
        "Le Sphinx", "Le Sphinx",
        ("Le Sphinx is a French pocket device (about 1930) with ten lanes of sliding alphabet bars: ten letters are enciphered at once, every letter by the bar of its lane.",
         "Le Sphinx ist ein französisches Taschengerät (um 1930) mit zehn Bahnen aus verschiebbaren Alphabetschienen: Zehn Buchstaben werden auf einmal verschlüsselt, jeder durch die Schiene seiner Bahn."),
        ("In every lane an upper bar (plaintext) and a lower bar (ciphertext) are moved together. The key lists the numbers of the 20 bars; with the arrangement 1 to 20 the example of Crypto Museum TOPSECRETS gives IRUVPYQHWB: T is at place 19 of the first bar, and the first lower bar (11) has I there. A text longer than 10 letters starts again with the first lane.",
         "In jeder Bahn werden eine obere Schiene (Klartext) und eine untere (Geheimtext) zusammen verschoben. Der Schlüssel nennt die Nummern der 20 Schienen; mit der Anordnung 1 bis 20 ergibt das Beispiel des Crypto Museum TOPSECRETS die Buchstaben IRUVPYQHWB: T steht an Stelle 19 der ersten Schiene, und die erste untere Schiene (11) hat dort I. Ein Text von mehr als 10 Buchstaben beginnt wieder mit der ersten Bahn."),
        "Le Sphinx, Lugagne, strip cipher, sliding alphabets"))
add(twin("TwinBifidCipher", "BifidCipher", "Bifid", 5, "THE SAME PHRASE OF THE PLAINTEXT APPEARS IN BOTH MESSAGES WITH DIFFERENT PERIODS", "SECRETKEY", (5, 7),
         "Twin Bifid", "Twin Bifid",
         ("Two Bifid messages that are enciphered with the same square but with different periods have a phrase of the plaintext in common; this is the Twin Bifid exercise of the ACA.",
          "Zwei Bifid-Nachrichten, die mit demselben Quadrat, aber mit verschiedenen Perioden verschlüsselt wurden, haben eine Phrase des Klartextes gemeinsam; das ist die Twin-Bifid-Aufgabe der ACA."),
         ("The two {\\b Bifid Cipher} components encipher the same text with the same key and the periods 5 and 7. For the ACA the periods should not both be even. Because the same phrase is mixed differently, the two ciphertexts give an attacker a lot of information about the square.",
          "Die beiden Komponenten {\\b Bifid-Chiffre} verschlüsseln denselben Text mit demselben Schlüssel und den Perioden 5 und 7. Bei der ACA sollen die Perioden nicht beide gerade sein. Weil dieselbe Phrase verschieden gemischt wird, verraten die beiden Geheimtexte einem Angreifer viel über das Quadrat."),
         "Twin Bifid, Bifid, period, ACA"))
add(twin("TwinTrifidCipher", "TrifidCipher", "Trifid", 3, "THE SAME PHRASE OF THE PLAINTEXT APPEARS IN BOTH MESSAGES WITH DIFFERENT PERIODS", "SECRETKEY", (5, 7),
         "Twin Trifid", "Twin Trifid",
         ("Two Trifid messages that are enciphered with the same key but with different periods have a phrase of the plaintext in common; this is the Twin Trifid exercise of the ACA.",
          "Zwei Trifid-Nachrichten, die mit demselben Schlüssel, aber mit verschiedenen Perioden verschlüsselt wurden, haben eine Phrase des Klartextes gemeinsam; das ist die Twin-Trifid-Aufgabe der ACA."),
         ("The two {\\b Trifid Cipher} components encipher the same text with the same key and the periods 5 and 7. For the ACA the periods should not both be divisible by three.",
          "Die beiden Komponenten {\\b Trifid-Chiffre} verschlüsseln denselben Text mit demselben Schlüssel und den Perioden 5 und 7. Bei der ACA sollen die Perioden nicht beide durch drei teilbar sein."),
         "Twin Trifid, Trifid, period, ACA"))

# Autokey: the Vigenère component in its autokey mode (derived from the Vigenère template)
SPEC.append(dict(
    out=f"{CLASSIC}/AutokeyCipher", base=f"{CLASSIC}/Vigenere",
    edits=[dict(match="Vigenere.Vigenere", set=dict(Mode="1")),
           dict(match="TextInput", where=dict(Text="SECRETKEY"), set=dict(Text="PRIMER"))],
    plugin="Vigenere",
    en=dict(title="Autokey Cipher", summary="Encryption and decryption with the <i>autokey cipher</i>",
            description="The autokey cipher is a Vigenère cipher whose key is a short primer followed by the plaintext itself, so that the key is as long as the message and never repeats.",
            memo="The {\\b Vigenère} component is set to the mode \"Vigenère Autokey\": the primer PRIMER is the key for the first six letters, after that every plaintext letter is the key letter of the letter six places later. The receiver needs the primer; decryption recovers the plaintext letter by letter. The mode \"Beaufort Autokey\" does the same with the Beaufort table."),
    de=dict(title="Autokey-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Autokey-Chiffre</i>",
            description="Die Autokey-Chiffre ist eine Vigenère-Chiffre, deren Schlüssel aus einem kurzen Primer und danach dem Klartext selbst besteht, sodass der Schlüssel so lang ist wie die Nachricht und sich nie wiederholt.",
            memo="Die Komponente {\\b Vigenère} ist auf die Betriebsart \"Vigenère Autokey\" gestellt: Der Primer PRIMER ist der Schlüssel für die ersten sechs Buchstaben, danach ist jeder Klartextbuchstabe der Schlüsselbuchstabe des Buchstabens sechs Stellen später. Der Empfänger braucht den Primer; die Entschlüsselung gewinnt den Klartext Buchstabe für Buchstabe zurück. Die Betriebsart \"Beaufort Autokey\" macht dasselbe mit der Beaufort-Tabelle."),
    keywords="autokey, Vigenère, primer, polyalphabetic, ACA"))

if __name__ == "__main__":
    derived.TEMPLATES = SPEC
    derived.main()

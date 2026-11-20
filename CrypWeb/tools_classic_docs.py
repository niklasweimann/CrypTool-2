"""The long documentation of the classic cipher components (CrypPlugins/<Cipher>/DetailedDescription/doc.xml): for each cipher number of
tools_classic_specs.PLUGINS the background and the procedure, English and German. tools-gen-classic-plugins.py makes the doc.xml from it, the
specification (inputs, settings) and the example of the template. Lines of a text are separated by \\n; steps are written as "1. ...".
"""

DOCS = {
    1: dict(en="A Quagmire is a periodic cipher with keyed alphabets from the American Cryptogram Association; the number says which of the two alphabets are keyed. In Quagmire I the plain alphabet is keyed (from the key word) and the cipher alphabet is the straight alphabet.\n"
               "1. Make the keyed plain alphabet: the letters of the key word without repeats, then the remaining letters of the alphabet.\n"
               "2. Write the straight alphabet as the cipher alphabet. The indicator key gives the period: it has one letter for every alternative alphabet. The first letter of the indicator is placed under the plain letter given in the setting (A), the cipher alphabet is shifted accordingly.\n"
               "3. For every plaintext letter take the next letter of the indicator (it repeats), find the plaintext letter in the keyed plain alphabet and read the cipher letter in the same column of the row of the indicator letter.\n"
               "Decryption goes the other way: find the cipher letter in the row and read the plain letter above it.",
            de="Ein Quagmire ist eine periodische Chiffre mit gemischten Alphabeten aus der American Cryptogram Association; die Zahl sagt, welche der beiden Alphabete gemischt sind. Bei Quagmire I ist das Klartextalphabet gemischt (aus dem Schlüsselwort) und das Geheimtextalphabet das gerade Alphabet.\n"
               "1. Bilden Sie das gemischte Klartextalphabet: die Buchstaben des Schlüsselworts ohne Wiederholungen, danach die übrigen Buchstaben des Alphabets.\n"
               "2. Schreiben Sie das gerade Alphabet als Geheimtextalphabet. Der Indikator bestimmt die Periode: Er hat einen Buchstaben für jedes andere Alphabet. Der erste Buchstabe des Indikators steht unter dem in der Einstellung gewählten Klartextbuchstaben (A), das Geheimtextalphabet wird entsprechend verschoben.\n"
               "3. Nehmen Sie für jeden Klartextbuchstaben den nächsten Buchstaben des Indikators (er wiederholt sich), suchen Sie den Klartextbuchstaben im gemischten Klartextalphabet und lesen Sie den Geheimtextbuchstaben in derselben Spalte der Zeile des Indikatorbuchstabens ab.\n"
               "Das Entschlüsseln geht umgekehrt: Suchen Sie den Geheimtextbuchstaben in der Zeile und lesen Sie darüber den Klartextbuchstaben."),
    2: dict(en="Quagmire II runs the straight plain alphabet against a keyed cipher alphabet (made from the key word).\n"
               "1. Make the keyed cipher alphabet from the key word. The plain alphabet is A to Z.\n"
               "2. Every letter of the indicator key selects a row: the keyed alphabet moved so that the indicator letter stands under the plain letter A (setting).\n"
               "3. For every plaintext letter take the next indicator letter and read the cipher letter in the column of the plaintext letter. The period is the length of the indicator.",
            de="Quagmire II lässt das gerade Klartextalphabet gegen ein gemischtes Geheimtextalphabet (aus dem Schlüsselwort) laufen.\n"
               "1. Bilden Sie das gemischte Geheimtextalphabet aus dem Schlüsselwort. Das Klartextalphabet ist A bis Z.\n"
               "2. Jeder Buchstabe des Indikators wählt eine Zeile: das gemischte Alphabet so verschoben, dass der Indikatorbuchstabe unter dem Klartextbuchstaben A steht (Einstellung).\n"
               "3. Nehmen Sie für jeden Klartextbuchstaben den nächsten Indikatorbuchstaben und lesen Sie den Geheimtextbuchstaben in der Spalte des Klartextbuchstabens ab. Die Periode ist die Länge des Indikators."),
    3: dict(en="Quagmire III uses the same keyed alphabet (from one key word) as plain and as cipher alphabet.\n"
               "1. Make the keyed alphabet from the key word; it is the plain alphabet and the cipher alphabet.\n"
               "2. Every letter of the indicator selects a row: the keyed alphabet moved so that the indicator letter stands under the first plain letter (setting).\n"
               "3. For every plaintext letter take the next indicator letter; the cipher letter is in the row of the indicator letter, in the column of the plaintext letter (found in the keyed plain alphabet).",
            de="Quagmire III benutzt dasselbe gemischte Alphabet (aus einem Schlüsselwort) als Klartext- und als Geheimtextalphabet.\n"
               "1. Bilden Sie das gemischte Alphabet aus dem Schlüsselwort; es ist Klartext- und Geheimtextalphabet.\n"
               "2. Jeder Buchstabe des Indikators wählt eine Zeile: das gemischte Alphabet so verschoben, dass der Indikatorbuchstabe unter dem ersten Klartextbuchstaben steht (Einstellung).\n"
               "3. Nehmen Sie für jeden Klartextbuchstaben den nächsten Indikatorbuchstaben; der Geheimtextbuchstabe steht in der Zeile des Indikatorbuchstabens, in der Spalte des Klartextbuchstabens (im gemischten Klartextalphabet gesucht)."),
    4: dict(en="Quagmire IV has three keys: a keyed plain alphabet, a keyed cipher alphabet with another key word, and the indicator.\n"
               "1. Make the keyed plain alphabet from the first key word and the keyed cipher alphabet from the second.\n"
               "2. Place the first indicator letter under the plain letter chosen in the setting (in the ACA example the first letter of the plain alphabet); every indicator letter gives one shifted cipher alphabet.\n"
               "3. For every plaintext letter use the next indicator letter and read the cipher letter in the column of the plaintext letter.",
            de="Quagmire IV hat drei Schlüssel: ein gemischtes Klartextalphabet, ein gemischtes Geheimtextalphabet mit einem anderen Schlüsselwort und den Indikator.\n"
               "1. Bilden Sie das gemischte Klartextalphabet aus dem ersten Schlüsselwort und das gemischte Geheimtextalphabet aus dem zweiten.\n"
               "2. Setzen Sie den ersten Indikatorbuchstaben unter den in der Einstellung gewählten Klartextbuchstaben (im ACA-Beispiel der erste Buchstabe des Klartextalphabets); jeder Indikatorbuchstabe liefert ein verschobenes Geheimtextalphabet.\n"
               "3. Benutzen Sie für jeden Klartextbuchstaben den nächsten Indikatorbuchstaben und lesen Sie den Geheimtextbuchstaben in der Spalte des Klartextbuchstabens ab."),
    5: dict(en="Portax is a digraphic periodic cipher of the ACA that works with a slide of two alphabets.\n"
               "1. Write the text in rows under the key word and fill the last row up with X; take two rows at a time, so that the letters above each other form pairs.\n"
               "2. The slide has a fixed upper alphabet A to M, a sliding row N to Z (repeated) and, below it, the alphabet in pairs of letters (AB, CD, ...). The key letter of a column sets the slide: its pair of letters moves under the first letter of the fixed row.\n"
               "3. The first letter of a pair is looked up in the upper alphabets, the second in the lower one. They are opposite corners of a rectangle; the other two corners are the cipher letters, the one from the upper alphabet first. If both letters are in the same column, the other two letters of that column are used.\n"
               "The operation is its own inverse, so decrypting is the same as encrypting.",
            de="Portax ist eine digraphische periodische Chiffre der ACA, die mit einem Schieber aus zwei Alphabeten arbeitet.\n"
               "1. Schreiben Sie den Text zeilenweise unter das Schlüsselwort und füllen Sie die letzte Zeile mit X auf; nehmen Sie je zwei Zeilen, sodass die übereinanderstehenden Buchstaben Paare bilden.\n"
               "2. Der Schieber hat ein festes oberes Alphabet A bis M, eine verschiebbare Reihe N bis Z (wiederholt) und darunter das Alphabet in Buchstabenpaaren (AB, CD, ...). Der Schlüsselbuchstabe einer Spalte stellt den Schieber ein: sein Buchstabenpaar rückt unter den ersten Buchstaben der festen Reihe.\n"
               "3. Der erste Buchstabe eines Paares wird in den oberen Alphabeten gesucht, der zweite im unteren. Sie sind gegenüberliegende Ecken eines Rechtecks; die beiden anderen Ecken sind die Geheimtextbuchstaben, zuerst der aus dem oberen Alphabet. Stehen beide Buchstaben in derselben Spalte, werden die beiden anderen Buchstaben dieser Spalte genommen.\n"
               "Die Operation ist ihre eigene Umkehrung, Entschlüsseln ist also dasselbe wie Verschlüsseln."),
    6: dict(en="Slidefair is a digraphic cipher of the ACA that uses a Vigenère, Variant or Beaufort table.\n"
               "1. Cut the text into pairs (an odd text gets an X). The key word gives the period: the pairs use its letters one after the other.\n"
               "2. The first letter of a pair is looked up in the top alphabet; the second in the row of the table that the key letter of the pair selects.\n"
               "3. The two letters are opposite corners of a rectangle; the cipher letters are the other two corners, the one in the top alphabet first. If the letters form a vertical pair, the pair to its right replaces them.\n"
               "The table is chosen in the settings: Vigenère (the row is the alphabet shifted by the key letter), Variant (shifted back) or Beaufort (reversed).",
            de="Slidefair ist eine digraphische Chiffre der ACA mit einer Vigenère-, Variant- oder Beaufort-Tabelle.\n"
               "1. Zerlegen Sie den Text in Paare (ein ungerader Text erhält ein X). Das Schlüsselwort bestimmt die Periode: Die Paare benutzen nacheinander seine Buchstaben.\n"
               "2. Der erste Buchstabe eines Paares wird im oberen Alphabet gesucht, der zweite in der Zeile der Tabelle, die der Schlüsselbuchstabe des Paares wählt.\n"
               "3. Die beiden Buchstaben sind gegenüberliegende Ecken eines Rechtecks; die Geheimtextbuchstaben sind die anderen beiden Ecken, zuerst der im oberen Alphabet. Bilden die Buchstaben ein senkrechtes Paar, ersetzt sie das Paar rechts davon.\n"
               "Die Tabelle wird in den Einstellungen gewählt: Vigenère (die Zeile ist das um den Schlüsselbuchstaben verschobene Alphabet), Variant (zurückverschoben) oder Beaufort (umgekehrt)."),
    7: dict(en="The progressive key cipher enciphers the text twice with a periodic cipher.\n"
               "1. Encipher the text with the key word (Vigenère, Variant or Beaufort, chosen in the settings): this is the primary ciphertext.\n"
               "2. Cut the text into groups of one period (the length of the key word). Encipher the primary ciphertext a second time with a key letter that moves on after every group: A for the first group, then A plus the progression index, and so on (A, B, C, ... for index 1; A, C, E, ... for index 2).\n"
               "Decryption undoes the second step first, then the first.",
            de="Die Chiffre mit fortschreitendem Schlüssel verschlüsselt den Text zweimal mit einer periodischen Chiffre.\n"
               "1. Verschlüsseln Sie den Text mit dem Schlüsselwort (Vigenère, Variant oder Beaufort, in den Einstellungen gewählt): Das ist der primäre Geheimtext.\n"
               "2. Zerlegen Sie den Text in Gruppen einer Periode (der Länge des Schlüsselworts). Verschlüsseln Sie den primären Geheimtext ein zweites Mal mit einem Schlüsselbuchstaben, der nach jeder Gruppe weiterrückt: A für die erste Gruppe, dann A plus der Fortschreitungsindex und so weiter (A, B, C, ... bei Index 1; A, C, E, ... bei Index 2).\n"
               "Das Entschlüsseln macht erst den zweiten, dann den ersten Schritt rückgängig."),
    8: dict(en="The interrupted key cipher is a periodic cipher (Vigenère, Variant or Beaufort) whose key word is interrupted: after some letters it starts again from its first letter.\n"
               "1. The second key lists the lengths of the pieces of the key word that are used, for example 4 6 2 3 (the list repeats when the text is longer).\n"
               "2. Build the key text: the first 4 letters of the key word, then its first 6 letters, then the first 2, and so on.\n"
               "3. Encipher the letters of the text with this key text. The ACA asks that the whole key word is used at least once.",
            de="Die Chiffre mit unterbrochenem Schlüssel ist eine periodische Chiffre (Vigenère, Variant oder Beaufort), deren Schlüsselwort unterbrochen wird: Nach einigen Buchstaben beginnt es wieder beim ersten Buchstaben.\n"
               "1. Der zweite Schlüssel nennt die Längen der benutzten Stücke des Schlüsselworts, zum Beispiel 4 6 2 3 (die Liste wiederholt sich, wenn der Text länger ist).\n"
               "2. Bilden Sie den Schlüsseltext: die ersten 4 Buchstaben des Schlüsselworts, dann seine ersten 6 Buchstaben, dann die ersten 2 und so weiter.\n"
               "3. Verschlüsseln Sie die Buchstaben des Textes mit diesem Schlüsseltext. Die ACA verlangt, dass das ganze Schlüsselwort mindestens einmal benutzt wird."),
    10: dict(en="Gromark (GROnsfeld with Mixed Alphabet and Running Key) is a Gronsfeld cipher with a mixed cipher alphabet and a running key of digits.\n"
                "1. Make the cipher alphabet: write the key word (without repeats) and then the rest of the alphabet in rows as wide as the key word; take the columns in the alphabetical order of the key letters.\n"
                "2. The primer (5 digits) starts the running key; every further digit is the sum of two digits (mod 10): the first plus the second gives the sixth, the second plus the third the seventh, and so on.\n"
                "3. Every plaintext letter moves to the right in the cipher alphabet by the digit at its position; the letter found there is the cipher letter.\n"
                "Decryption moves to the left. The ACA writes the primer in front of the ciphertext.",
             de="Gromark (GROnsfeld with Mixed Alphabet and Running Key) ist eine Gronsfeld-Chiffre mit gemischtem Geheimtextalphabet und laufendem Ziffernschlüssel.\n"
                "1. Bilden Sie das Geheimtextalphabet: Schreiben Sie das Schlüsselwort (ohne Wiederholungen) und dann den Rest des Alphabets in Zeilen so breit wie das Schlüsselwort; nehmen Sie die Spalten in der alphabetischen Reihenfolge der Schlüsselbuchstaben.\n"
                "2. Der Primer (5 Ziffern) beginnt den laufenden Schlüssel; jede weitere Ziffer ist die Summe zweier Ziffern (mod 10): die erste plus die zweite ergibt die sechste, die zweite plus die dritte die siebte und so weiter.\n"
                "3. Jeder Klartextbuchstabe rückt im Geheimtextalphabet um die Ziffer seiner Stelle nach rechts; der dort gefundene Buchstabe ist der Geheimtextbuchstabe.\n"
                "Das Entschlüsseln geht nach links. Die ACA schreibt den Primer vor den Geheimtext."),
    11: dict(en="The periodic Gromark cipher is a Gromark cipher in which the numbering of the key word is the primer and every group of one period starts at another place of the cipher alphabet.\n"
                "1. Make the cipher alphabet from the key word as for Gromark. The primer is the numbering of the key letters in alphabetical order (ENIGMA gives 2 6 4 3 5 1).\n"
                "2. Extend the primer to the length of the text by adding pairs of digits (mod 10).\n"
                "3. Cut the text into groups of one period. The k-th group starts at the place of the k-th key letter (repeating) in the cipher alphabet; every letter moves on by the digit at its position.",
             de="Die periodische Gromark-Chiffre ist eine Gromark-Chiffre, bei der die Nummerierung des Schlüsselworts der Primer ist und jede Gruppe einer Periode an einer anderen Stelle des Geheimtextalphabets beginnt.\n"
                "1. Bilden Sie das Geheimtextalphabet aus dem Schlüsselwort wie bei Gromark. Der Primer ist die Nummerierung der Schlüsselbuchstaben in alphabetischer Reihenfolge (ENIGMA ergibt 2 6 4 3 5 1).\n"
                "2. Verlängern Sie den Primer durch Addition von Ziffernpaaren (mod 10) auf die Länge des Textes.\n"
                "3. Zerlegen Sie den Text in Gruppen einer Periode. Die k-te Gruppe beginnt an der Stelle des k-ten Schlüsselbuchstabens (wiederholt) im Geheimtextalphabet; jeder Buchstabe rückt um die Ziffer seiner Stelle weiter."),
    12: dict(en="The Condi cipher is an ACA cipher that keeps word divisions and punctuation. A keyed alphabet is used as a table, and the step depends on the plaintext.\n"
                "1. Make the keyed alphabet from the key word and move it to the right by the alphabet shift (setting). Number its places 1 to 26.\n"
                "2. The first plaintext letter is replaced by the letter that stands as many places further as the start value (setting).\n"
                "3. Every following letter is replaced by the letter that many places further as the position of the plaintext letter before it.\n"
                "Decryption subtracts the same steps.",
             de="Die Condi-Chiffre ist eine ACA-Chiffre, die Wortgrenzen und Satzzeichen beibehält. Ein gemischtes Alphabet dient als Tabelle, und die Schrittweite hängt vom Klartext ab.\n"
                "1. Bilden Sie das gemischte Alphabet aus dem Schlüsselwort und verschieben Sie es um die Alphabetverschiebung (Einstellung) nach rechts. Nummerieren Sie seine Stellen von 1 bis 26.\n"
                "2. Der erste Klartextbuchstabe wird durch den Buchstaben ersetzt, der so viele Stellen weiter steht wie der Startwert (Einstellung).\n"
                "3. Jeder folgende Buchstabe wird durch den Buchstaben ersetzt, der so viele Stellen weiter steht wie die Position des Klartextbuchstabens davor.\n"
                "Das Entschlüsseln zieht dieselben Schritte ab."),
    13: dict(en="Ragbaby historically uses a 24-letter keyed alphabet (I/J and W/X share a place) and keeps word divisions.\n"
                "1. Make the keyed alphabet from the key word, then the remaining letters.\n"
                "2. Number the letters of the plaintext: the first letter of the first word gets 1, its second letter 2, and so on; the first letter of the second word gets 2, of the third word 3, and so on. The numbers go up to 24 and then start again.\n"
                "3. Every letter moves to the right in the alphabet by its number; the letter found there is the substitute.\n"
                "Decryption moves to the left by the same numbers.",
             de="Ragbaby benutzt historisch ein gemischtes Alphabet aus 24 Buchstaben (I/J und W/X teilen sich eine Stelle) und behält die Wortgrenzen bei.\n"
                "1. Bilden Sie das gemischte Alphabet aus dem Schlüsselwort, danach die übrigen Buchstaben.\n"
                "2. Nummerieren Sie die Buchstaben des Klartextes: Der erste Buchstabe des ersten Wortes erhält 1, sein zweiter 2 und so weiter; der erste Buchstabe des zweiten Wortes erhält 2, der des dritten Wortes 3 und so weiter. Die Nummern gehen bis 24 und beginnen dann wieder.\n"
                "3. Jeder Buchstabe rückt im Alphabet um seine Nummer nach rechts; der dort gefundene Buchstabe ist der Ersatz.\n"
                "Das Entschlüsseln rückt um dieselben Nummern nach links."),
    14: dict(en="The Phillips cipher uses eight 5x5 Polybius squares in turn, five letters with each, so its period is 40 letters.\n"
                "1. Write the key word into a 5x5 square in alternating directions (row 1 left to right, row 2 right to left, ...), then the remaining letters (I and J together): this is square 1.\n"
                "2. Squares 2 to 5 move the first row down one row at a time; squares 6 to 8 do this with the second row.\n"
                "3. Every plaintext letter is replaced by the letter diagonally down to the right of it in the square of its block of five letters; a letter in the last column or row continues at the first column or row of the ring.\n"
                "Decryption takes the letter diagonally up to the left.",
             de="Die Phillips-Chiffre benutzt nacheinander acht 5x5-Polybios-Quadrate, mit jedem fünf Buchstaben; ihre Periode ist also 40 Buchstaben.\n"
                "1. Schreiben Sie das Schlüsselwort in wechselnder Richtung in ein 5x5-Quadrat (Zeile 1 von links nach rechts, Zeile 2 von rechts nach links, ...), danach die übrigen Buchstaben (I und J zusammen): Das ist Quadrat 1.\n"
                "2. Die Quadrate 2 bis 5 verschieben die erste Zeile jeweils um eine Zeile nach unten; die Quadrate 6 bis 8 tun dasselbe mit der zweiten Zeile.\n"
                "3. Jeder Klartextbuchstabe wird durch den Buchstaben schräg rechts darunter im Quadrat seines Fünferblocks ersetzt; ein Buchstabe in der letzten Spalte oder Zeile geht in der ersten Spalte oder Zeile des Rings weiter.\n"
                "Das Entschlüsseln nimmt den Buchstaben schräg links darüber."),
    15: dict(en="Phillips-RC is the Phillips cipher in which both the rows and the columns of the squares are moved.\n"
                "1. Make square 1 from the key word as for Phillips.\n"
                "2. Squares 2 to 5 move the first row and the first column to the second, third, fourth and fifth place; squares 6 to 8 do this with the second row and column.\n"
                "3. Use the eight squares in turn for five letters each and replace every letter by the letter diagonally down to the right.",
             de="Phillips-RC ist die Phillips-Chiffre, bei der sowohl die Zeilen als auch die Spalten der Quadrate verschoben werden.\n"
                "1. Bilden Sie Quadrat 1 aus dem Schlüsselwort wie bei Phillips.\n"
                "2. Die Quadrate 2 bis 5 verschieben die erste Zeile und die erste Spalte an die zweite, dritte, vierte und fünfte Stelle; die Quadrate 6 bis 8 tun dasselbe mit der zweiten Zeile und Spalte.\n"
                "3. Benutzen Sie die acht Quadrate nacheinander für je fünf Buchstaben und ersetzen Sie jeden Buchstaben durch den schräg rechts darunter."),
    17: dict(en="The numbered key cipher replaces letters by numbers; every letter can have several numbers.\n"
                "1. Extend the key phrase by the letters of the alphabet that are missing (in alphabetical order). Letters of the phrase that occur several times stay.\n"
                "2. Number the places of this extended key 0, 1, 2, ... starting at the place given by the offset and going around.\n"
                "3. Replace every plaintext letter by the number of one of its places; this component uses the numbers of a letter in turn, so repeated letters get different numbers. The numbers are written with two digits.\n"
                "Decryption looks up the letter of every number.",
             de="Die Chiffre mit nummeriertem Schlüssel ersetzt Buchstaben durch Zahlen; jeder Buchstabe kann mehrere Zahlen haben.\n"
                "1. Erweitern Sie den Schlüsselsatz um die fehlenden Buchstaben des Alphabets (in alphabetischer Reihenfolge). Buchstaben, die im Satz mehrfach vorkommen, bleiben.\n"
                "2. Nummerieren Sie die Stellen dieses erweiterten Schlüssels mit 0, 1, 2, ... beginnend an der Stelle des Versatzes und im Kreis weiter.\n"
                "3. Ersetzen Sie jeden Klartextbuchstaben durch die Nummer einer seiner Stellen; diese Komponente benutzt die Nummern eines Buchstabens der Reihe nach, wiederholte Buchstaben erhalten also verschiedene Nummern. Die Nummern werden zweistellig geschrieben.\n"
                "Das Entschlüsseln sucht zu jeder Nummer den Buchstaben."),
    18: dict(en="The ACA homophonic cipher replaces every letter by one of four two-digit numbers.\n"
                "1. The 25 letters (I and J together) have the numbers 01 to 25 in the first row, 26 to 50 in the second, 51 to 75 in the third and 76 to 00 in the fourth.\n"
                "2. The four letters of the key word say where each row starts: the key letter of a row has the first number of the row (G with 01, O with 26, ... for the key GOLF).\n"
                "3. Every plaintext letter is replaced by its number in one of the four rows; this component takes the rows in turn.\n"
                "Decryption finds the letter of every number.",
             de="Die homophone Chiffre der ACA ersetzt jeden Buchstaben durch eine von vier zweistelligen Zahlen.\n"
                "1. Die 25 Buchstaben (I und J zusammen) haben in der ersten Zeile die Zahlen 01 bis 25, in der zweiten 26 bis 50, in der dritten 51 bis 75 und in der vierten 76 bis 00.\n"
                "2. Die vier Buchstaben des Schlüsselworts sagen, wo jede Zeile beginnt: Der Schlüsselbuchstabe einer Zeile hat die erste Zahl der Zeile (G mit 01, O mit 26, ... beim Schlüssel GOLF).\n"
                "3. Jeder Klartextbuchstabe wird durch seine Zahl in einer der vier Zeilen ersetzt; diese Komponente nimmt die Zeilen der Reihe nach.\n"
                "Das Entschlüsseln findet zu jeder Zahl den Buchstaben."),
    19: dict(en="A null cipher hides the message in an innocent text; it is a concealment (steganography), not an encryption.\n"
                "1. For every letter of the message look for a word of the cover text that has this letter at the chosen place: the first, the middle or the last letter (for an even length the left of the two middle letters).\n"
                "2. Write the words one after the other. The component searches the cover text from the word after the last one used and starts again at the beginning if needed.\n"
                "3. To read the message take the chosen letter of every word of the text.",
             de="Eine Nullen-Chiffre versteckt die Nachricht in einem harmlosen Text; sie ist eine Verdeckung (Steganografie), keine Verschlüsselung.\n"
                "1. Suchen Sie für jeden Buchstaben der Nachricht ein Wort des Deckungstextes, das diesen Buchstaben an der gewählten Stelle hat: dem ersten, dem mittleren oder dem letzten Buchstaben (bei gerader Länge der linke der beiden mittleren).\n"
                "2. Schreiben Sie die Wörter hintereinander. Die Komponente sucht im Deckungstext ab dem Wort nach dem zuletzt benutzten und beginnt bei Bedarf wieder am Anfang.\n"
                "3. Zum Lesen der Nachricht nehmen Sie den gewählten Buchstaben jedes Wortes des Textes."),
    22: dict(en="The Myszkowski transposition is a columnar transposition whose key word has repeated letters.\n"
                "1. Number the letters of the key word in alphabetical order; equal letters get the same number (BANANA: A is 1, B is 2, N is 3).\n"
                "2. Write the plaintext in rows under the key word (the last row may be incomplete).\n"
                "3. Read the columns with number 1, then those with number 2, and so on. Columns with the same number are read together, row by row from left to right.\n"
                "Decryption fills the columns again in the same order.",
             de="Die Myszkowski-Transposition ist eine Spaltentransposition, deren Schlüsselwort wiederholte Buchstaben hat.\n"
                "1. Nummerieren Sie die Buchstaben des Schlüsselworts in alphabetischer Reihenfolge; gleiche Buchstaben erhalten dieselbe Nummer (BANANA: A ist 1, B ist 2, N ist 3).\n"
                "2. Schreiben Sie den Klartext zeilenweise unter das Schlüsselwort (die letzte Zeile darf unvollständig sein).\n"
                "3. Lesen Sie die Spalten mit der Nummer 1, dann die mit der Nummer 2 und so weiter. Spalten mit derselben Nummer werden zusammen gelesen, Zeile für Zeile von links nach rechts.\n"
                "Das Entschlüsseln füllt die Spalten in derselben Reihenfolge wieder auf."),
    23: dict(en="AMSCO is a columnar transposition in which the places of the block hold one letter or two letters alternately.\n"
                "1. The key (word or number) gives the order of the columns. The block is filled row by row; the places alternate between a digraph and a single letter along the rows and down the columns (a checkerboard), starting with a digraph or a single letter (setting).\n"
                "2. The text ends where it ends: the last place may hold only one letter of a digraph; no null is needed.\n"
                "3. Read the columns in the order of the key, each from top to bottom.",
             de="AMSCO ist eine Spaltentransposition, bei der die Plätze des Blocks abwechselnd einen oder zwei Buchstaben enthalten.\n"
                "1. Der Schlüssel (Wort oder Zahl) gibt die Reihenfolge der Spalten an. Der Block wird zeilenweise gefüllt; die Plätze wechseln entlang der Zeilen und die Spalten hinunter zwischen Digraph und einzelnem Buchstaben (ein Schachbrett), beginnend mit einem Digraph oder einem einzelnen Buchstaben (Einstellung).\n"
                "2. Der Text endet, wo er endet: Der letzte Platz kann nur einen Buchstaben eines Digraphs enthalten; eine Null ist nicht nötig.\n"
                "3. Lesen Sie die Spalten in der Reihenfolge des Schlüssels, jede von oben nach unten."),
    24: dict(en="Cadenus is a columnar transposition of the ACA with a shift of every column.\n"
                "1. The text needs 25 rows: 25 letters for every letter of the key word. Write it in rows under the key word.\n"
                "2. A letter of the 25-letter alphabet (V and W together) is attached to every row, going down from A: A, Z, Y, X, V/W, U, ...\n"
                "3. Every column of the plaintext block goes into the column of the cipher block that the alphabetical order of the key letters gives, but it starts at the row whose attached letter is the key letter of the column, and goes on around the block.\n"
                "4. The ciphertext is read from the cipher block by rows.",
             de="Cadenus ist eine Spaltentransposition der ACA mit einer Verschiebung jeder Spalte.\n"
                "1. Der Text braucht 25 Zeilen: 25 Buchstaben für jeden Buchstaben des Schlüsselworts. Schreiben Sie ihn zeilenweise unter das Schlüsselwort.\n"
                "2. An jeder Zeile steht ein Buchstabe des 25-Buchstaben-Alphabets (V und W zusammen), von A abwärts: A, Z, Y, X, V/W, U, ...\n"
                "3. Jede Spalte des Klartextblocks kommt in die Spalte des Geheimtextblocks, die die alphabetische Reihenfolge der Schlüsselbuchstaben ergibt, beginnt aber in der Zeile, deren angehängter Buchstabe der Schlüsselbuchstabe der Spalte ist, und geht rund um den Block weiter.\n"
                "4. Der Geheimtext wird zeilenweise aus dem Geheimtextblock gelesen."),
    25: dict(en="The Nicodemus cipher combines three steps with one key word.\n"
                "1. Columnar transposition: write the text in rows under the key word and take the columns in the alphabetical order of its letters; the columns now stand in that order.\n"
                "2. Vigenère: add to every letter of a column the key letter that stands over it (the key word sorted alphabetically).\n"
                "3. Read the letters off five at a time from every column in turn (from top to bottom); what is left of a column at the end is taken as it is.",
             de="Die Nicodemus-Chiffre verbindet drei Schritte mit einem Schlüsselwort.\n"
                "1. Spaltentransposition: Schreiben Sie den Text zeilenweise unter das Schlüsselwort und nehmen Sie die Spalten in der alphabetischen Reihenfolge seiner Buchstaben; die Spalten stehen nun in dieser Reihenfolge.\n"
                "2. Vigenère: Addieren Sie zu jedem Buchstaben einer Spalte den Schlüsselbuchstaben, der darüber steht (das alphabetisch sortierte Schlüsselwort).\n"
                "3. Lesen Sie die Buchstaben jeweils fünf auf einmal aus jeder Spalte der Reihe nach (von oben nach unten); was am Ende von einer Spalte übrig ist, wird so genommen, wie es ist."),
    26: dict(en="The Nihilist transposition moves the columns and the rows of a square with the same key.\n"
                "1. The key (word or number) gives the size n of the square; the text is written into n x n squares (filled up with X).\n"
                "2. The key says where every column goes: the column in the place k of the key goes to the place with the rank of the key digit (writing-in key); the same holds for the rows.\n"
                "3. The square is read by rows or by columns (setting).",
             de="Die Nihilisten-Transposition verschiebt die Spalten und Zeilen eines Quadrats mit demselben Schlüssel.\n"
                "1. Der Schlüssel (Wort oder Zahl) gibt die Größe n des Quadrats an; der Text wird in n x n große Quadrate geschrieben (mit X aufgefüllt).\n"
                "2. Der Schlüssel sagt, wohin jede Spalte kommt: Die Spalte an der Stelle k des Schlüssels kommt an die Stelle mit dem Rang der Schlüsselziffer (Einschreibeschlüssel); für die Zeilen gilt dasselbe.\n"
                "3. Das Quadrat wird zeilenweise oder spaltenweise gelesen (Einstellung)."),
    27: dict(en="In a route transposition the text is written into a block along one route and read from it along another.\n"
                "1. The block has the width given in the settings; the text is filled up with X to a full block.\n"
                "2. The routes (all start at the top left): rows, columns, alternating rows or columns, diagonals, alternating diagonals (zigzag), and spirals inwards or outwards, clockwise or counterclockwise.\n"
                "3. Write the text along the write route, read it along the read route. Decryption writes along the read route and reads along the write route.",
             de="Bei einer Routentransposition wird der Text entlang einer Route in einen Block geschrieben und entlang einer anderen daraus gelesen.\n"
                "1. Der Block hat die in den Einstellungen gewählte Breite; der Text wird mit X zu einem vollen Block aufgefüllt.\n"
                "2. Die Routen (alle beginnen oben links): Zeilen, Spalten, Zeilen oder Spalten im Wechsel, Diagonalen, Diagonalen im Wechsel (Zickzack) und Spiralen nach innen oder außen, im oder gegen den Uhrzeigersinn.\n"
                "3. Schreiben Sie den Text entlang der Schreibroute und lesen Sie ihn entlang der Leseroute. Das Entschlüsseln schreibt entlang der Leseroute und liest entlang der Schreibroute."),
    28: dict(en="The sequence transposition uses a number sequence to decide which column every letter goes into.\n"
                "1. The key word (10 letters) numbers its columns 1 2 ... 9 0 in alphabetical order (equal letters from left to right).\n"
                "2. The primer (digits) is extended to the length of the text by adding pairs of digits (mod 10): the first plus the second gives the next digit, and so on.\n"
                "3. The plaintext letter at a position goes into the column whose number is the digit of that position.\n"
                "4. The columns are written one after the other from left to right.",
             de="Die Sequenz-Transposition benutzt eine Zahlenfolge, um zu entscheiden, in welche Spalte jeder Buchstabe kommt.\n"
                "1. Das Schlüsselwort (10 Buchstaben) nummeriert seine Spalten in alphabetischer Reihenfolge mit 1 2 ... 9 0 (gleiche Buchstaben von links nach rechts).\n"
                "2. Der Primer (Ziffern) wird durch Addition von Ziffernpaaren (mod 10) auf die Länge des Textes verlängert: die erste plus die zweite ergibt die nächste Ziffer und so weiter.\n"
                "3. Der Klartextbuchstabe an einer Stelle kommt in die Spalte, deren Nummer die Ziffer dieser Stelle ist.\n"
                "4. Die Spalten werden von links nach rechts hintereinander geschrieben."),
    30: dict(en="Swagman is a transposition with a Latin square as key.\n"
                "1. The key square has n rows and n columns with the numbers 1 to n, no number repeating in a row or a column (rows separated by spaces).\n"
                "2. The text is written row by row into a rectangle of n rows (filled up with X); the key square is repeated along the columns.\n"
                "3. Within every column the letter of row r moves to the row that the key square gives for row r in that column.\n"
                "4. The ciphertext is read by columns.",
             de="Swagman ist eine Transposition mit einem lateinischen Quadrat als Schlüssel.\n"
                "1. Das Schlüsselquadrat hat n Zeilen und n Spalten mit den Zahlen 1 bis n, keine Zahl wiederholt sich in einer Zeile oder Spalte (Zeilen durch Leerzeichen getrennt).\n"
                "2. Der Text wird zeilenweise in ein Rechteck aus n Zeilen geschrieben (mit X aufgefüllt); das Schlüsselquadrat wiederholt sich entlang der Spalten.\n"
                "3. In jeder Spalte wandert der Buchstabe der Zeile r in die Zeile, die das Schlüsselquadrat für die Zeile r in dieser Spalte angibt.\n"
                "4. Der Geheimtext wird spaltenweise gelesen."),
    31: dict(en="The conjugated matrix Bifid (CM Bifid) is the Bifid cipher with two squares.\n"
                "1. Make two 5x5 squares (I and J together): the plain square from the first key word and the cipher square from the second (written in alternating verticals).\n"
                "2. Cut the text into groups of the period (0 = the whole text). Look up every letter in the plain square and write the row numbers of the group in one line and the column numbers in a second line.\n"
                "3. Read the two lines one after the other as a string of numbers and take two numbers at a time as row and column in the cipher square: this gives the cipher letters.\n"
                "Decryption looks up the cipher letters in the cipher square, writes the numbers again and splits them into rows and columns.",
             de="Das Conjugated Matrix Bifid (CM Bifid) ist die Bifid-Chiffre mit zwei Quadraten.\n"
                "1. Bilden Sie zwei 5x5-Quadrate (I und J zusammen): das Klartextquadrat aus dem ersten Schlüsselwort und das Geheimtextquadrat aus dem zweiten (in abwechselnden Senkrechten geschrieben).\n"
                "2. Zerlegen Sie den Text in Gruppen der Periode (0 = der ganze Text). Suchen Sie jeden Buchstaben im Klartextquadrat und schreiben Sie die Zeilennummern der Gruppe in eine Zeile und die Spaltennummern in eine zweite.\n"
                "3. Lesen Sie die beiden Zeilen hintereinander als Zahlenfolge und nehmen Sie je zwei Zahlen als Zeile und Spalte im Geheimtextquadrat: Das ergibt die Geheimtextbuchstaben.\n"
                "Das Entschlüsseln sucht die Geheimtextbuchstaben im Geheimtextquadrat, schreibt die Zahlen wieder hin und teilt sie in Zeilen und Spalten."),
    32: dict(en="Digrafid is a fractionating digraphic cipher of the ACA with two mixed alphabets of 27 symbols (the letters and #).\n"
                "1. The first key word fills a table of 3 rows and 9 columns row by row (horizontal alphabet); the second fills a table of 9 rows and 3 columns column by column (vertical alphabet).\n"
                "2. Cut the text into pairs (an odd text gets a #). A pair gives three digits: the column of the first letter in the horizontal alphabet, then a digit from its row (3 rows) and the column of the second letter in the vertical alphabet, and the row of the second letter.\n"
                "3. The digits of a group of pairs (the period) are written under each other and read by lines; every three digits give a new pair from the two alphabets.",
             de="Digrafid ist eine fraktionierende digraphische Chiffre der ACA mit zwei gemischten Alphabeten aus 27 Zeichen (den Buchstaben und #).\n"
                "1. Das erste Schlüsselwort füllt eine Tabelle aus 3 Zeilen und 9 Spalten zeilenweise (waagerechtes Alphabet); das zweite füllt eine Tabelle aus 9 Zeilen und 3 Spalten spaltenweise (senkrechtes Alphabet).\n"
                "2. Zerlegen Sie den Text in Paare (ein ungerader Text erhält ein #). Ein Paar ergibt drei Ziffern: die Spalte des ersten Buchstabens im waagerechten Alphabet, dann eine Ziffer aus seiner Zeile (3 Zeilen) und der Spalte des zweiten Buchstabens im senkrechten Alphabet, und die Zeile des zweiten Buchstabens.\n"
                "3. Die Ziffern einer Gruppe von Paaren (der Periode) werden untereinander geschrieben und zeilenweise gelesen; je drei Ziffern ergeben ein neues Paar aus den beiden Alphabeten."),
    33: dict(en="Fractionated Morse converts the text to Morse code and then to letters in groups of three symbols.\n"
                "1. Write the text in Morse code with x between the letters and xx between the words.\n"
                "2. Take the symbols in groups of three (dot, dash, x; the last group is filled up with x). There are 26 possible groups (all but xxx).\n"
                "3. The groups are the letters of a keyed alphabet, in the order of the symbols (. - x): the first group (...) is the first letter of the keyed alphabet, the last (xx-) is the last.\n"
                "Decryption turns the letters into groups, joins them and reads the Morse code.",
             de="Fraktioniertes Morse wandelt den Text in Morsecode und diesen in Buchstaben aus Dreiergruppen von Zeichen.\n"
                "1. Schreiben Sie den Text in Morsecode mit x zwischen den Buchstaben und xx zwischen den Wörtern.\n"
                "2. Nehmen Sie die Zeichen in Dreiergruppen (Punkt, Strich, x; die letzte Gruppe wird mit x aufgefüllt). Es gibt 26 mögliche Gruppen (alle außer xxx).\n"
                "3. Die Gruppen sind die Buchstaben eines gemischten Alphabets, in der Reihenfolge der Zeichen (. - x): Die erste Gruppe (...) ist der erste Buchstabe des gemischten Alphabets, die letzte (xx-) der letzte.\n"
                "Das Entschlüsseln macht aus den Buchstaben wieder Gruppen, fügt sie zusammen und liest den Morsecode."),
    34: dict(en="Morbit converts the text to Morse code and then to digits in groups of two symbols.\n"
                "1. Write the text in Morse code with x between the letters and xx between the words.\n"
                "2. Take the symbols in pairs (a last single symbol gets an x). There are nine pairs: .. .- .x -. -- -x x. x- xx.\n"
                "3. The key word of 9 letters gives the digits 1 to 9 to these pairs: the letters in alphabetical order, equal letters from left to right. Every pair is replaced by its digit.\n"
                "Decryption turns the digits into pairs and reads the Morse code.",
             de="Morbit wandelt den Text in Morsecode und diesen in Ziffern aus Zweiergruppen von Zeichen.\n"
                "1. Schreiben Sie den Text in Morsecode mit x zwischen den Buchstaben und xx zwischen den Wörtern.\n"
                "2. Nehmen Sie die Zeichen paarweise (ein letztes einzelnes Zeichen erhält ein x). Es gibt neun Paare: .. .- .x -. -- -x x. x- xx.\n"
                "3. Das Schlüsselwort aus 9 Buchstaben gibt diesen Paaren die Ziffern 1 bis 9: die Buchstaben in alphabetischer Reihenfolge, gleiche Buchstaben von links nach rechts. Jedes Paar wird durch seine Ziffer ersetzt.\n"
                "Das Entschlüsseln macht aus den Ziffern wieder Paare und liest den Morsecode."),
    35: dict(en="Pollux writes the text in Morse code and replaces every symbol by one of several digits.\n"
                "1. Write the text in Morse code with x between the letters and xx between the words.\n"
                "2. The key says what the digits 1 2 3 4 5 6 7 8 9 0 stand for: a dot, a dash or x (usually four dots and three each of the others).\n"
                "3. Every symbol is replaced by one of the digits that stand for it; this component takes them in turn.\n"
                "Decryption replaces every digit by its symbol and reads the Morse code.",
             de="Pollux schreibt den Text in Morsecode und ersetzt jedes Zeichen durch eine von mehreren Ziffern.\n"
                "1. Schreiben Sie den Text in Morsecode mit x zwischen den Buchstaben und xx zwischen den Wörtern.\n"
                "2. Der Schlüssel sagt, wofür die Ziffern 1 2 3 4 5 6 7 8 9 0 stehen: ein Punkt, ein Strich oder x (üblicherweise vier Punkte und je drei der anderen).\n"
                "3. Jedes Zeichen wird durch eine der Ziffern ersetzt, die dafür stehen; diese Komponente nimmt sie der Reihe nach.\n"
                "Das Entschlüsseln ersetzt jede Ziffer durch ihr Zeichen und liest den Morsecode."),
    36: dict(en="Tridigital replaces letters by digits; every digit stands for three letters.\n"
                "1. The keyed alphabet fills a block of ten columns row by row; the last column stays empty.\n"
                "2. The key word of 10 letters gives the digits 1 to 9 and 0 to the columns (alphabetical order).\n"
                "3. A letter is replaced by the digit above its column; a space by the digit above the empty column.\n"
                "Decryption cannot be unique: it lists the three possible letters of every digit.",
             de="Tridigital ersetzt Buchstaben durch Ziffern; jede Ziffer steht für drei Buchstaben.\n"
                "1. Das gemischte Alphabet füllt einen Block mit zehn Spalten zeilenweise; die letzte Spalte bleibt leer.\n"
                "2. Das Schlüsselwort aus 10 Buchstaben gibt den Spalten die Ziffern 1 bis 9 und 0 (alphabetische Reihenfolge).\n"
                "3. Ein Buchstabe wird durch die Ziffer über seiner Spalte ersetzt; ein Leerzeichen durch die Ziffer über der leeren Spalte.\n"
                "Das Entschlüsseln kann nicht eindeutig sein: Es listet die drei möglichen Buchstaben jeder Ziffer auf."),
    37: dict(en="Tri-Square encipherment of a pair of letters with three squares gives three cipher letters.\n"
                "1. Make three 5x5 squares from three key words (I and J together).\n"
                "2. Cut the text into pairs. Find the first letter in square 1 and the second in square 2.\n"
                "3. First cipher letter: any letter of the column of the first plaintext letter in square 1 (this component takes the rows in turn). Second: the letter of square 3 in the row of the first and the column of the second plaintext letter. Third: any letter of the row of the second plaintext letter in square 2.\n"
                "Decryption needs the column of the first, the row and column in square 3 and the row of the third letter to find the pair.",
             de="Die Tri-Square-Verschlüsselung eines Buchstabenpaares mit drei Quadraten ergibt drei Geheimtextbuchstaben.\n"
                "1. Bilden Sie drei 5x5-Quadrate aus drei Schlüsselwörtern (I und J zusammen).\n"
                "2. Zerlegen Sie den Text in Paare. Suchen Sie den ersten Buchstaben in Quadrat 1 und den zweiten in Quadrat 2.\n"
                "3. Erster Geheimtextbuchstabe: ein beliebiger Buchstabe der Spalte des ersten Klartextbuchstabens in Quadrat 1 (diese Komponente nimmt die Zeilen der Reihe nach). Zweiter: der Buchstabe von Quadrat 3 in der Zeile des ersten und der Spalte des zweiten Klartextbuchstabens. Dritter: ein beliebiger Buchstabe der Zeile des zweiten Klartextbuchstabens in Quadrat 2.\n"
                "Das Entschlüsseln braucht die Spalte des ersten, Zeile und Spalte in Quadrat 3 und die Zeile des dritten Buchstabens, um das Paar zu finden."),
    38: dict(en="Seriated Playfair writes the text in groups of two lines and enciphers the vertical pairs with the Playfair rules.\n"
                "1. Make the 5x5 square from the key word (I and J together).\n"
                "2. Write the text in groups: a line of the period length, then a second line of the same length under it. If a vertical pair would be a double letter, an X is put into the lower line.\n"
                "3. Encipher every vertical pair: same row, take the letters to the right; same column, take the letters below; otherwise the other corners of the rectangle.\n"
                "4. The ciphertext is the two lines of every group one after the other.",
             de="Seriated Playfair schreibt den Text in Gruppen aus zwei Zeilen und verschlüsselt die senkrechten Paare nach den Playfair-Regeln.\n"
                "1. Bilden Sie das 5x5-Quadrat aus dem Schlüsselwort (I und J zusammen).\n"
                "2. Schreiben Sie den Text in Gruppen: eine Zeile von der Länge der Periode, darunter eine zweite Zeile derselben Länge. Wäre ein senkrechtes Paar ein Doppelbuchstabe, wird ein X in die untere Zeile gesetzt.\n"
                "3. Verschlüsseln Sie jedes senkrechte Paar: gleiche Zeile, die Buchstaben rechts davon; gleiche Spalte, die Buchstaben darunter; sonst die anderen Ecken des Rechtecks.\n"
                "4. Der Geheimtext besteht aus den beiden Zeilen jeder Gruppe nacheinander."),
    39: dict(en="The checkerboard is a Polybius square with letters as coordinates.\n"
                "1. Make the 5x5 square from the key word (I and J together). Put a word of five different letters at its left (rows) and one above it (columns). In the complex form there are two words at each side; this component uses them in turn.\n"
                "2. A plaintext letter is replaced by the letter of its row and the letter of its column.\n"
                "Decryption takes two letters at a time and looks up the cell.",
             de="Das Checkerboard ist ein Polybios-Quadrat mit Buchstaben als Koordinaten.\n"
                "1. Bilden Sie das 5x5-Quadrat aus dem Schlüsselwort (I und J zusammen). Setzen Sie ein Wort aus fünf verschiedenen Buchstaben links davon (Zeilen) und eines darüber (Spalten). In der komplexen Form gibt es zwei Wörter an jeder Seite; diese Komponente benutzt sie der Reihe nach.\n"
                "2. Ein Klartextbuchstabe wird durch den Buchstaben seiner Zeile und den Buchstaben seiner Spalte ersetzt.\n"
                "Das Entschlüsseln nimmt je zwei Buchstaben und sucht das Feld."),
    40: dict(en="Monome-Dinome uses a box of 3 rows and 8 columns of the 24 letters (I/J and Y/Z together).\n"
                "1. The key word fills the box row by row, then the remaining letters.\n"
                "2. A second key word of ten letters gives ten digits (alphabetical order, 1 to 9 and 0): the first two are the row digits of the second and third rows, the other eight are the column digits.\n"
                "3. A letter of the top row is replaced by the digit of its column (one digit: monome); a letter of the other rows by the digit of its row and then of its column (two digits: dinome).\n"
                "Decryption reads a row digit as the start of a two-digit group.",
             de="Monome-Dinome benutzt einen Kasten aus 3 Zeilen und 8 Spalten mit den 24 Buchstaben (I/J und Y/Z zusammen).\n"
                "1. Das Schlüsselwort füllt den Kasten zeilenweise, danach die übrigen Buchstaben.\n"
                "2. Ein zweites Schlüsselwort aus zehn Buchstaben ergibt zehn Ziffern (alphabetische Reihenfolge, 1 bis 9 und 0): Die ersten beiden sind die Zeilenziffern der zweiten und dritten Zeile, die anderen acht die Spaltenziffern.\n"
                "3. Ein Buchstabe der obersten Zeile wird durch die Ziffer seiner Spalte ersetzt (eine Ziffer: Monom); ein Buchstabe der anderen Zeilen durch die Ziffer seiner Zeile und dann seiner Spalte (zwei Ziffern: Dinom).\n"
                "Das Entschlüsseln liest eine Zeilenziffer als Beginn einer zweistelligen Gruppe."),
    41: dict(en="Compressocrat compresses the text with a Huffman-like code and then replaces groups of three digits by letters.\n"
                "1. Every letter has a code of the digits 1, 2 and 3 of different length: frequent letters are short (E 31, T 12, A 13), rare ones long (Z 321113).\n"
                "2. Join the codes of the text and cut the digits into groups of three (the digit 1 is added once or twice to complete the last group).\n"
                "3. The 26 groups 111 to 332 are the letters of a keyed alphabet moved to the right by the alphabet shift (setting). Every group is replaced by its letter.\n"
                "The ciphertext is shorter than the plaintext. Decryption turns letters into groups and reads the codes (the code is prefix-free).",
             de="Compressocrat komprimiert den Text mit einem Huffman-ähnlichen Code und ersetzt dann Dreiergruppen von Ziffern durch Buchstaben.\n"
                "1. Jeder Buchstabe hat einen Code aus den Ziffern 1, 2 und 3 unterschiedlicher Länge: häufige Buchstaben sind kurz (E 31, T 12, A 13), seltene lang (Z 321113).\n"
                "2. Fügen Sie die Codes des Textes aneinander und zerlegen Sie die Ziffern in Dreiergruppen (die Ziffer 1 wird ein- oder zweimal angehängt, um die letzte Gruppe zu vervollständigen).\n"
                "3. Die 26 Gruppen 111 bis 332 sind die Buchstaben eines gemischten Alphabets, das um die Alphabetverschiebung (Einstellung) nach rechts verschoben ist. Jede Gruppe wird durch ihren Buchstaben ersetzt.\n"
                "Der Geheimtext ist kürzer als der Klartext. Das Entschlüsseln macht aus den Buchstaben Gruppen und liest die Codes (der Code ist präfixfrei)."),
    42: dict(en="Headlines is an ACA exercise: five headlines are enciphered by simple substitution with the same mixed alphabet at five different settings.\n"
                "1. The hat word, the key word and the setting word are related by their meaning.\n"
                "2. Write the alphabet of the key word in a block as wide as the hat word; number the columns by the alphabetical order of the hat letters and take them in that order: this is the mixed alphabet.\n"
                "3. Headline k uses the k-th letter of the setting word: the cipher alphabet is the mixed alphabet starting with that letter.\n"
                "4. Replace every letter of the headline; spaces, punctuation and the asterisks that mark proper nouns stay.",
             de="Headlines ist eine ACA-Aufgabe: Fünf Schlagzeilen werden durch einfache Substitution mit demselben gemischten Alphabet in fünf verschiedenen Stellungen verschlüsselt.\n"
                "1. Hutwort, Schlüsselwort und Einstellungswort hängen inhaltlich zusammen.\n"
                "2. Schreiben Sie das Alphabet des Schlüsselworts in einen Block so breit wie das Hutwort; nummerieren Sie die Spalten nach der alphabetischen Reihenfolge der Hutbuchstaben und nehmen Sie sie in dieser Reihenfolge: Das ist das gemischte Alphabet.\n"
                "3. Die Schlagzeile k benutzt den k-ten Buchstaben des Einstellungswortes: Das Geheimtextalphabet ist das gemischte Alphabet, beginnend mit diesem Buchstaben.\n"
                "4. Ersetzen Sie jeden Buchstaben der Schlagzeile; Leerzeichen, Satzzeichen und die Sterne, die Eigennamen markieren, bleiben."),
    43: dict(en="The Polybius cipher (named after the Greek historian Polybius, who used the square for signalling with torches) replaces every letter by two numbers.\n"
                "1. Write the alphabet (I and J together) into a 5x5 square, row by row; with a key word the keyed alphabet.\n"
                "2. Replace every letter by the number of its row and the number of its column. The pairs are written with a space between them.\n"
                "Decryption reads two digits at a time as row and column.",
             de="Die Polybios-Chiffre (nach dem griechischen Historiker Polybios benannt, der das Quadrat zum Signalisieren mit Fackeln benutzte) ersetzt jeden Buchstaben durch zwei Zahlen.\n"
                "1. Schreiben Sie das Alphabet (I und J zusammen) zeilenweise in ein 5x5-Quadrat; mit einem Schlüsselwort das gemischte Alphabet.\n"
                "2. Ersetzen Sie jeden Buchstaben durch die Nummer seiner Zeile und die Nummer seiner Spalte. Die Paare werden mit einem Leerzeichen dazwischen geschrieben.\n"
                "Das Entschlüsseln liest je zwei Ziffern als Zeile und Spalte."),
    44: dict(en="The tap code is the Polybius square of prisoners who tap messages on walls and pipes.\n"
                "1. The square holds the alphabet without K (K is written as C), row by row.\n"
                "2. A letter is tapped as the number of its row, a pause, and the number of its column: W is in row 5, column 2, five knocks, a pause, two knocks. The pause between two letters is longer.\n"
                "This component writes the pairs of numbers; decryption reads two digits at a time.",
             de="Der Klopfcode ist das Polybios-Quadrat von Gefangenen, die Nachrichten an Wände und Rohre klopfen.\n"
                "1. Das Quadrat enthält das Alphabet ohne K (K wird als C geschrieben), zeilenweise.\n"
                "2. Ein Buchstabe wird als Zahl seiner Zeile, eine Pause und Zahl seiner Spalte geklopft: W steht in Zeile 5, Spalte 2, fünf Klopfer, eine Pause, zwei Klopfer. Die Pause zwischen zwei Buchstaben ist länger.\n"
                "Diese Komponente schreibt die Zahlenpaare; das Entschlüsseln liest je zwei Ziffern."),
    45: dict(en="The ABC cipher has two stages.\n"
                "1. Substitution: the text is cut into groups of three letters; the first letter stays, the second moves one letter on in the alphabet, the third two letters (a Vigenère cipher with the key ABC).\n"
                "2. Transposition: the result is written in rows under the key word and the columns are read in the alphabetical order of its letters (an incomplete columnar transposition).\n"
                "Decryption undoes the transposition first and then the substitution.",
             de="Die ABC-Chiffre hat zwei Stufen.\n"
                "1. Substitution: Der Text wird in Dreiergruppen zerlegt; der erste Buchstabe bleibt, der zweite rückt im Alphabet um einen Buchstaben weiter, der dritte um zwei (eine Vigenère-Chiffre mit dem Schlüssel ABC).\n"
                "2. Transposition: Das Ergebnis wird zeilenweise unter das Schlüsselwort geschrieben, und die Spalten werden in der alphabetischen Reihenfolge seiner Buchstaben gelesen (eine unvollständige Spaltentransposition).\n"
                "Das Entschlüsseln macht erst die Transposition, dann die Substitution rückgängig."),
    46: dict(en="The Rasterschlüssel 44 (RS 44) was used by the German army from March 1944 as the successor of the Doppelkastenschlüssel. It is a pure transposition on a printed form of 24 rows and 25 columns; every row has 10 white cells, the others are black. The grid was changed every day, messages were limited to about 200 letters.\n"
                "1. Choose a start cell and write the plaintext row by row into the white cells; black cells are skipped; at the end of a row writing continues at the left of the next row.\n"
                "2. The columns have numbers (1 to 25, printed above them). Read the columns one after the other in the order of the numbers, starting at the reading column and going on from number 25 to 1, each from top to bottom.\n"
                "3. The ciphertext has as many letters as the plaintext; it is written in groups of five. The message header (time, number of letters, start cell, reading column) is coded with the letter swap table; this component does not make it.\n"
                "Decryption fills the cells that the message covers column by column in the same order and reads them row by row.",
             de="Der Rasterschlüssel 44 (RS 44) wurde ab März 1944 vom deutschen Heer als Nachfolger des Doppelkastenschlüssels benutzt. Er ist eine reine Transposition auf einem gedruckten Formular aus 24 Zeilen und 25 Spalten; jede Zeile hat 10 weiße Felder, die anderen sind schwarz. Das Raster wurde täglich gewechselt, die Sprüche waren auf etwa 200 Buchstaben begrenzt.\n"
                "1. Wählen Sie eine Startzelle und schreiben Sie den Klartext zeilenweise in die weißen Felder; schwarze Felder werden übersprungen; am Ende einer Zeile geht das Schreiben links in der nächsten Zeile weiter.\n"
                "2. Die Spalten haben Nummern (1 bis 25, über ihnen gedruckt). Lesen Sie die Spalten nacheinander in der Reihenfolge der Nummern, beginnend bei der Herauslesespalte und von Nummer 25 weiter mit 1, jede von oben nach unten.\n"
                "3. Der Geheimtext hat so viele Buchstaben wie der Klartext; er wird in Fünfergruppen geschrieben. Der Spruchkopf (Uhrzeit, Buchstabenzahl, Startzelle, Herauslesespalte) wird mit der Buchstabentauschtafel verschlüsselt; diese Komponente bildet ihn nicht.\n"
                "Das Entschlüsseln füllt die Felder, die der Spruch belegt, spaltenweise in derselben Reihenfolge und liest sie zeilenweise."),
    47: dict(en="The Werftschlüssel is a hand cipher of the German navy (the Reservehandverfahren was used from May 1940 to the end of the war). It replaced the Enigma where none was available. It is a transposition followed by a bigram substitution with double-letter tables.\n"
                "1. Cut the plaintext into groups of five letters (the last group is filled up with X, to an even number of groups) and number them.\n"
                "2. Put the groups into the rows of a box with five columns in the order given by the row order: the k-th group goes into the row with the k-th number.\n"
                "3. Take two rows at a time. The letters above each other in a column form a pair; replace it by the pair that the table gives. The first letters of the new pairs form one group of the ciphertext, the second letters the next one.\n"
                "4. The tables are involutory: if KV is replaced by TC, then TC is replaced by KV, so the same table decrypts. A replacement never shares a letter with the pair it replaces. Many tables were issued in key booklets (20, later 30).\n"
                "In this component the table is an input: write the pairs like KV=TC, separated by spaces or lines.",
             de="Der Werftschlüssel ist ein Handverfahren der deutschen Kriegsmarine (das Reservehandverfahren wurde von Mai 1940 bis Kriegsende benutzt). Er ersetzte die Enigma, wo keine zur Verfügung stand. Er ist eine Transposition, gefolgt von einer Bigramm-Substitution mit Doppelbuchstabentafeln.\n"
                "1. Zerlegen Sie den Klartext in Fünfergruppen (die letzte wird mit X aufgefüllt, auf eine gerade Zahl von Gruppen) und nummerieren Sie sie.\n"
                "2. Setzen Sie die Gruppen in der Reihenfolge der Zeilenreihenfolge in die Zeilen eines Kastens mit fünf Spalten: Die k-te Gruppe kommt in die Zeile mit der k-ten Nummer.\n"
                "3. Nehmen Sie je zwei Zeilen. Die übereinanderstehenden Buchstaben einer Spalte bilden ein Paar; ersetzen Sie es durch das Paar, das die Tafel angibt. Die ersten Buchstaben der neuen Paare bilden eine Gruppe des Geheimtextes, die zweiten die nächste.\n"
                "4. Die Tafeln sind involutorisch: Wird KV durch TC ersetzt, wird TC durch KV ersetzt, dieselbe Tafel entschlüsselt also. Ein Ersatz hat nie einen Buchstaben mit dem ersetzten Paar gemeinsam. In Schlüsselheften wurden viele Tafeln ausgegeben (20, später 30).\n"
                "In dieser Komponente ist die Tafel ein Eingang: Schreiben Sie die Paare wie KV=TC, durch Leerzeichen oder Zeilen getrennt."),
    48: dict(en="Le Sphinx is a French pocket device made by Georges Lugagne's company in Paris (about 1930; the earlier Transpositeur is from 1912). It has ten lanes with two sliding alphabet bars each.\n"
                "1. In every lane an upper bar (plaintext) and a lower bar (ciphertext) move together; a bar is one of 20 scrambled alphabets numbered 1 to 20. Bars 1 to 10 are ascending alphabets, bars 11 to 20 descending ones.\n"
                "2. The key is the arrangement of the bars: the numbers of the 10 upper bars, then of the 10 lower bars (1 2 ... 10 and 11 12 ... 20 without a key).\n"
                "3. Ten letters are enciphered at once, one in every lane: the letter is set in the upper window of its lane, the cipher letter stands at the same place in the lower bar. The next group of ten starts again with the first lane.\n"
                "Because the arrangement of the bars does not change during a message, the cipher is not very secure.",
             de="Le Sphinx ist ein französisches Taschengerät der Firma von Georges Lugagne in Paris (um 1930; der frühere Transpositeur stammt von 1912). Es hat zehn Bahnen mit je zwei verschiebbaren Alphabetschienen.\n"
                "1. In jeder Bahn bewegen sich eine obere Schiene (Klartext) und eine untere (Geheimtext) zusammen; eine Schiene ist eines von 20 gemischten Alphabeten mit den Nummern 1 bis 20. Die Schienen 1 bis 10 sind aufsteigende Alphabete, die Schienen 11 bis 20 absteigende.\n"
                "2. Der Schlüssel ist die Anordnung der Schienen: die Nummern der 10 oberen Schienen, dann der 10 unteren (ohne Schlüssel 1 2 ... 10 und 11 12 ... 20).\n"
                "3. Zehn Buchstaben werden auf einmal verschlüsselt, einer in jeder Bahn: Der Buchstabe wird im oberen Fenster seiner Bahn eingestellt, der Geheimtextbuchstabe steht an derselben Stelle der unteren Schiene. Die nächste Zehnergruppe beginnt wieder mit der ersten Bahn.\n"
                "Weil sich die Anordnung der Schienen während eines Spruchs nicht ändert, ist die Chiffre nicht sehr sicher."),
}

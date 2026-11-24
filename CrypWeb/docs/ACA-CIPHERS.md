# The classic ciphers in CrypWeb: ACA list, Wikipedia category, hand ciphers

Sources checked: the list of <https://www.cryptogram.org/resource-area/cipher-types/> (62 cipher types; the 63rd entry there is a chart download), the German
Wikipedia category "Klassische Kryptologie" (with "Manuelle Verschlüsselungsmethode"), and <https://kryptografie.de/kryptografie/chiffre/index-gesamt.htm> for the
hand ciphers of the German forces. The rules and the examples (they are the test vectors) come from the descriptions of the ACA
(`https://www.cryptogram.org/downloads/aca.info/ciphers/<Name>.pdf`), the Wikipedia articles and kryptografie.de.

## Structure

Every cipher that no component did before has **a component of its own** in `CrypPlugins/<Name>` (43 components, usable in the desktop version too; their names are in the
tables below). The components are generated from one specification (`tools/gen/classic_specs.py`) by `tools/gen/classic_plugins.py`: component, settings, resources (English,
German), documentation, project file, and the entries in `CrypTool 2.sln`. The algorithms are in the library `LibSource/ClassicCipherLib` (no plugin code in it);
`tools/gen/aca_templates.py` makes one template for each cipher; `CrypWeb.Tests kat` runs the examples through the components.

**No cipher is implemented twice.** Where a component existed already, the template uses it (the "Component" column names it):

| Cipher | Component that does it |
|---|---|
| Variant | Vigenère, action Decrypt (C = P - K is the Vigenère decryption) |
| Running key | Vigenère with a key as long as the text |
| Key phrase | Substitution (source alphabet A-Z, destination alphabet the phrase of 26 letters) |
| Complete / incomplete columnar transposition | Transposition (template Transposition) |
| Autokey, Beaufort, Gronsfeld, Trithemius | Vigenère (modes) |
| Twin Bifid, Twin Trifid | Bifid / Trifid with two periods |
| Tabula recta | Vigenère (template Trithemius) |
| Grille (turning grille) | Fleißner Grille, an experimental component (its stencil is the grille as a string of 0 and 1; it reproduces the example of the ACA) |

## All ciphers and their templates (`Templates/Cryptography/Classic`)

| Cipher | Component | Template |
|---|---|---|
| AMSCO | AMSCO Cipher | AmscoCipher |
| Autokey | Vigenère (mode Vigenère Autokey) | AutokeyCipher |
| Baconian | Baconian Cipher | BaconCipher |
| Bazeries | Bazeries Cipher | BazeriesCipher |
| Beaufort | Vigenère (mode Beaufort Classic) | BeaufortCipher |
| Bifid | Bifid Cipher | BifidCipher |
| Cadenus | Cadenus Cipher | CadenusCipher |
| Checkerboard | Checkerboard Cipher | CheckerboardCipher |
| CM Bifid | CM Bifid Cipher | CMBifidCipher |
| Compressocrat | Compressocrat Cipher | CompressocratCipher |
| Condi | Condi Cipher | CondiCipher |
| Digrafid | Digrafid Cipher | DigrafidCipher |
| Four-square | Four-square Cipher | FourSquareCipher |
| Fractionated Morse | Fractionated Morse Cipher | FractionatedMorseCipher |
| Grandpré | Grandpré Cipher | GrandpreCipher |
| Grille | Fleißner Grille (experimental) | GrilleCipher |
| Gromark | Gromark Cipher | GromarkCipher |
| Gronsfeld | Vigenère | GronsfeldCipher |
| Headlines | Headlines Cipher | HeadlinesCipher |
| Homophonic | Homophonic Cipher (ACA) | HomophonicCipher |
| Interrupted key | Interrupted Key Cipher | InterruptedKeyCipher |
| Key phrase | Substitution | KeyPhraseCipher |
| Monome-Dinome | Monome-Dinome Cipher | MonomeDinomeCipher |
| Morbit | Morbit Cipher | MorbitCipher |
| Myszkowski | Myszkowski Transposition | MyszkowskiTransposition |
| Nicodemus | Nicodemus Cipher | NicodemusCipher |
| Nihilist substitution | Nihilist Cipher | Nihilist |
| Nihilist transposition | Nihilist Transposition | NihilistTransposition |
| Null | Null Cipher | NullCipher |
| Numbered key | Numbered Key Cipher | NumberedKeyCipher |
| Periodic Gromark | Periodic Gromark Cipher | PeriodicGromarkCipher |
| Phillips | Phillips Cipher | PhillipsCipher |
| Phillips-RC | Phillips-RC Cipher | PhillipsRCCipher |
| Playfair | Playfair | Playfair |
| Pollux | Pollux Cipher | PolluxCipher |
| Porta | Classic Variants | PortaCipher |
| Portax | Portax Cipher | PortaxCipher |
| Progressive key | Progressive Key Cipher | ProgressiveKeyCipher |
| Quagmire I | Quagmire I Cipher | Quagmire1Cipher |
| Quagmire II | Quagmire II Cipher | Quagmire2Cipher |
| Quagmire III | Quagmire III Cipher | Quagmire3Cipher |
| Quagmire IV | Quagmire IV Cipher | Quagmire4Cipher |
| Ragbaby | Ragbaby Cipher | RagbabyCipher |
| Rail fence | Classic Variants | RailFenceCipher |
| Redefence | Classic Variants | RedefenceCipher |
| Route transposition | Route Transposition | RouteTransposition |
| Running key | Vigenère | RunningKeyCipher |
| Sequence transposition | Sequence Transposition | SequenceTransposition |
| Seriated Playfair | Seriated Playfair | SeriatedPlayfair |
| Slidefair | Slidefair Cipher | SlidefairCipher |
| Swagman | Swagman Cipher | SwagmanCipher |
| Syllabary | Syllabary Cipher | SyllabaryCipher |
| Tridigital | Tridigital Cipher | TridigitalCipher |
| Trifid | Trifid Cipher | TrifidCipher |
| Tri-Square | Tri-Square Cipher | TriSquareCipher |
| Twin Bifid | Bifid Cipher (two components) | TwinBifidCipher |
| Twin Trifid | Trifid Cipher (two components) | TwinTrifidCipher |
| Two-square | Classic Variants | TwoSquareCipher |
| Variant | Vigenère | VariantCipher |
| Vigenère | Vigenère | Vigenere |
| Complete columnar transposition | Transposition | Transposition |
| Incomplete columnar transposition | Transposition | Transposition |
| Polybios-Chiffre | Polybius Cipher | PolybiosCipher |
| Klopfcode | Tap Code | TapCode |
| ABC-Chiffre | ABC Cipher | ABCCipher |
| Werftschlüssel | Werftschlüssel (Reservehandverfahren) | WerftschluesselCipher |
| Rasterschlüssel 44 | Rasterschlüssel 44 (RS 44) | Rasterschluessel44Cipher |
| Le Sphinx | Le Sphinx | LeSphinxCipher |

Quagmire I to IV are four components (one for each type). Twin Bifid and Twin Trifid have no component of their own.

## Conventions and deviations

- The components work on the letters A to Z and write upper case. Where the ACA keeps word divisions and punctuation (Condi, Ragbaby, Key phrase)
  the component does as well; all other ciphers drop everything but letters (and digits, where the cipher has them).
- Where the ACA lets the encipherer choose (Homophonic, Numbered key, Pollux, Tri-Square, Checkerboard with two words at each side), the component takes the
  alternatives in turn, so the ciphertext of an example differs from the printed one while the printed one decrypts correctly.
- Key phrase (Substitution) and Tridigital cannot be decrypted uniquely.
- The joined ciphertext of the ACA's Phillips-RC example differs in two letters from the same example written in lines (N/W, Y/O); the component agrees with the lines.
- Cadenus needs 25 letters for every key letter, as the cipher is defined.

# What is not implemented, and why

Everything that Wikipedia, the ACA and kryptografie.de describe sufficiently is implemented. These are not, because the key material or the procedure is not available
as text (the tables are images, scans or secret):

| Cipher | Reason |
|---|---|
| Heftschlüssel | the hole pattern and the column numbers are only in an image; kryptografie.de has no article |
| Reservehandverfahren, Schlüssel Henno | the tables of the key booklets (Tauschtafeln, Zahlenreihen) are images or scans; the Werftschlüssel, which works like it, is implemented with the table as an input |
| Syko | the code cards are random tables that are not published as text; no worked example was found |
| Batco | the use of the monogram and bigram columns and the cards are not described in the sources that were read |
| Slidex | the procedure is described, but the cards (204 words each) are images |
| Alphabetum Kaldeorum | the symbol table is an image |
| Streifenschieber, Handschlüssel, Überschlüsselung | general articles (the strip principle is that of the M-138, Spanish Strip and Le Sphinx components) |

Werftschlüssel and Rasterschlüssel 44 take their key tables as inputs: the templates contain the 14 pairs of the table and the first 14 rows of the grid of the examples
(the rest of the tables is on printed forms that are not available as text; the whole table can be pasted into the input). The grid of the RS 44 example was read from the
picture on kryptografie.de; with one cell corrected it reproduces the ciphertext of the example exactly.

#!/usr/bin/env python3
"""Templates that are variants of existing ones (another mode, other key values): written next to the originals in
../Templates, as .cwm (settings changed by `CrypWeb.Tests derive`) and .xml (texts, in English and German).

  python3 tools/gen/derived_templates.py

Each entry of TEMPLATES below says which template it is derived from and what changes. The function list entry the
template covers is recorded in docs/reference/function-aliases.json ("templates"). Needs the built CrypWeb.Tests.
"""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parents[2]
TEMPLATES_DIR = HERE.parent / "Templates"
CLASSIC = "Cryptography/Classic"
ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"

TEMPLATES = [
    dict(
        out=f"{CLASSIC}/BeaufortCipher", base=f"{CLASSIC}/Vigenere",
        edits=[dict(match="Vigenere.Vigenere", set=dict(Mode="2"))],
        en=dict(title="Beaufort Cipher", summary="Encryption and decryption with the <i>Beaufort cipher</i>",
                description="The Beaufort cipher is a variant of the Vigenère cipher: instead of adding the key letter to the plaintext letter, the plaintext letter is subtracted from the key letter. Encryption and decryption are the same operation (the cipher is reciprocal).",
                memo="The {\\b Beaufort cipher} is a polyalphabetic substitution cipher. Where Vigenère computes C = P + K, Beaufort computes C = K - P (mod 26). Therefore the same operation both encrypts and decrypts. The mode of the Vigenère component is set to \"Beaufort Classic\"."),
        de=dict(title="Beaufort-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Beaufort-Chiffre</i>",
                description="Die Beaufort-Chiffre ist eine Variante der Vigenère-Chiffre: Statt den Schlüsselbuchstaben zum Klartextbuchstaben zu addieren, wird der Klartextbuchstabe vom Schlüsselbuchstaben subtrahiert. Ver- und Entschlüsselung sind dieselbe Operation (die Chiffre ist reziprok).",
                memo="Die {\\b Beaufort-Chiffre} ist eine polyalphabetische Substitutions-Chiffre. Während Vigenère C = P + K berechnet, berechnet Beaufort C = K - P (mod 26). Dieselbe Operation ver- und entschlüsselt daher. Die Betriebsart der Vigenère-Komponente ist auf \"Beaufort Classic\" gestellt."),
        keywords="Beaufort, Vigenère, polyalphabetic",
    ),
    dict(
        out=f"{CLASSIC}/GronsfeldCipher", base=f"{CLASSIC}/Vigenere",
        edits=[dict(match="Vigenere.Vigenere", set=dict(ShiftChar="DBEBF", ShiftValue="3,1,4,1,5")),
               dict(match="TextInput", where=dict(Text="SECRETKEY"), set=dict(Text="DBEBF"))],
        en=dict(title="Gronsfeld Cipher", summary="Encryption and decryption with the <i>Gronsfeld cipher</i>",
                description="The Gronsfeld cipher is a Vigenère cipher whose key is a number: each digit is the shift of one letter, so only the shifts 0 to 9 occur.",
                memo="The {\\b Gronsfeld cipher} is a Vigenère cipher with a numeric key. Every digit d is a shift by d letters. The key 3 1 4 1 5 is entered as the letters D B E B F (A = 0, B = 1, ...), because the Vigenère component takes its key as letters."),
        de=dict(title="Gronsfeld-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Gronsfeld-Chiffre</i>",
                description="Die Gronsfeld-Chiffre ist eine Vigenère-Chiffre mit einer Zahl als Schlüssel: Jede Ziffer ist die Verschiebung eines Buchstabens, es kommen also nur die Verschiebungen 0 bis 9 vor.",
                memo="Die {\\b Gronsfeld-Chiffre} ist eine Vigenère-Chiffre mit Zahlenschlüssel. Jede Ziffer d verschiebt um d Buchstaben. Der Schlüssel 3 1 4 1 5 wird als die Buchstaben D B E B F eingegeben (A = 0, B = 1, ...), weil die Vigenère-Komponente den Schlüssel als Buchstaben erwartet."),
        keywords="Gronsfeld, Vigenère, polyalphabetic",
    ),
    dict(
        out=f"{CLASSIC}/TrithemiusCipher", base=f"{CLASSIC}/Vigenere",
        edits=[dict(match="Vigenere.Vigenere", set=dict(ShiftChar=ALPHABET, ShiftValue=",".join(map(str, range(26))))),
               dict(match="TextInput", where=dict(Text="SECRETKEY"), set=dict(Text=ALPHABET))],
        en=dict(title="Trithemius Cipher", summary="Encryption and decryption with the <i>Trithemius cipher</i>",
                description="The Trithemius cipher (tabula recta) shifts the first letter by 0, the second by 1, the third by 2 and so on. It is the Vigenère cipher with the alphabet as its key.",
                memo="The {\\b Trithemius cipher} is the Vigenère cipher with the key A B C D ... Z: every letter is shifted by one more than the letter before. It has no secret key, its strength rests on the method being unknown."),
        de=dict(title="Trithemius-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Trithemius-Chiffre</i>",
                description="Die Trithemius-Chiffre (Tabula recta) verschiebt den ersten Buchstaben um 0, den zweiten um 1, den dritten um 2 und so weiter. Sie ist die Vigenère-Chiffre mit dem Alphabet als Schlüssel.",
                memo="Die {\\b Trithemius-Chiffre} ist die Vigenère-Chiffre mit dem Schlüssel A B C D ... Z: Jeder Buchstabe wird um eins mehr verschoben als der davor. Sie hat keinen geheimen Schlüssel, ihre Stärke liegt darin, dass das Verfahren unbekannt ist."),
        keywords="Trithemius, tabula recta, Vigenère",
    ),
    dict(
        out=f"{CLASSIC}/AtbashCipher", base=f"{CLASSIC}/AffineCipher",
        edits=[dict(match="AffineCipher.AffineCipher", set=dict(A="25", B="25", CaseSensitive="False")),
               dict(match="TextInput", where=dict(Text="ABCDEFGHIJKLMNOPQRSTUVWXYZabc"), set=dict(Text=ALPHABET))],
        en=dict(title="Atbash Cipher", summary="Encryption and decryption with the <i>Atbash cipher</i>",
                description="The Atbash cipher replaces every letter by the letter at the opposite end of the alphabet (A by Z, B by Y, ...). It is the affine cipher with a = 25 and b = 25 and has no key.",
                memo="The {\\b Atbash cipher} maps the alphabet onto its reverse. It is the affine cipher E(x) = (25x + 25) mod 26. Since the cipher has no key, applying it twice gives the plaintext again."),
        de=dict(title="Atbash-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Atbash-Chiffre</i>",
                description="Die Atbash-Chiffre ersetzt jeden Buchstaben durch den am anderen Ende des Alphabets (A durch Z, B durch Y, ...). Sie ist die affine Chiffre mit a = 25 und b = 25 und hat keinen Schlüssel.",
                memo="Die {\\b Atbash-Chiffre} bildet das Alphabet auf seine Umkehrung ab. Sie ist die affine Chiffre E(x) = (25x + 25) mod 26. Da es keinen Schlüssel gibt, ergibt zweimaliges Anwenden wieder den Klartext."),
        keywords="Atbash, affine, substitution",
    ),
    dict(
        out=f"{CLASSIC}/MultiplicationCipher", base=f"{CLASSIC}/AffineCipher",
        edits=[dict(match="AffineCipher.AffineCipher", set=dict(A="7", B="0", CaseSensitive="False")),
               dict(match="TextInput", where=dict(Text="ABCDEFGHIJKLMNOPQRSTUVWXYZabc"), set=dict(Text=ALPHABET))],
        en=dict(title="Multiplication Cipher", summary="Encryption and decryption with the <i>multiplication cipher</i>",
                description="The multiplication cipher multiplies the position of every letter by a key a that is coprime to the alphabet length. It is the affine cipher with b = 0.",
                memo="The {\\b multiplication cipher} computes E(x) = a * x mod 26. a must be coprime to 26 (1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25). It is the affine cipher with b = 0; change a in the settings of the \"Affine Cipher\" components."),
        de=dict(title="Multiplikations-Chiffre", summary="Ver- und Entschlüsselung mit der <i>Multiplikations-Chiffre</i>",
                description="Die Multiplikations-Chiffre multipliziert die Position jedes Buchstabens mit einem Schlüssel a, der teilerfremd zur Alphabetlänge ist. Sie ist die affine Chiffre mit b = 0.",
                memo="Die {\\b Multiplikations-Chiffre} berechnet E(x) = a * x mod 26. a muss teilerfremd zu 26 sein (1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25). Sie ist die affine Chiffre mit b = 0; a wird in den Einstellungen der \"Affine Cipher\"-Komponenten geändert."),
        keywords="multiplication, affine, substitution",
    ),
    dict(
        out="Hashfunctions/HMAC_SHA256", base="Hashfunctions/HMAC",
        edits=[dict(match="HMAC", set=dict(SelectedHashFunction="3"))],
        en=dict(title="HMAC with SHA-256 (SHA MAC)", summary="Message authentication code with <i>HMAC-SHA-256</i>",
                description="A message authentication code (MAC) protects the integrity and the origin of a message. This template computes it as HMAC with the hash function SHA-256.",
                memo="The {\\b HMAC} component computes a MAC from a message and a secret key. Here the hash function is SHA-256; other hash functions can be chosen in the settings."),
        de=dict(title="HMAC mit SHA-256 (SHA-MAC)", summary="Nachrichten-Authentifizierungscode mit <i>HMAC-SHA-256</i>",
                description="Ein Message Authentication Code (MAC) schützt Integrität und Herkunft einer Nachricht. Diese Vorlage berechnet ihn als HMAC mit der Hashfunktion SHA-256.",
                memo="Die {\\b HMAC}-Komponente berechnet aus einer Nachricht und einem geheimen Schlüssel einen MAC. Hier ist die Hashfunktion SHA-256; andere Hashfunktionen können in den Einstellungen gewählt werden."),
        keywords="HMAC, SHA, MAC",
    ),
    dict(
        out="Hashfunctions/HMAC_RIPEMD160", base="Hashfunctions/HMAC",
        edits=[dict(match="HMAC", set=dict(SelectedHashFunction="1"))],
        en=dict(title="HMAC with RIPEMD-160 (RIPEMD MAC)", summary="Message authentication code with <i>HMAC-RIPEMD-160</i>",
                description="A message authentication code (MAC) protects the integrity and the origin of a message. This template computes it as HMAC with the hash function RIPEMD-160.",
                memo="The {\\b HMAC} component computes a MAC from a message and a secret key. Here the hash function is RIPEMD-160; other hash functions can be chosen in the settings."),
        de=dict(title="HMAC mit RIPEMD-160 (RIPEMD-MAC)", summary="Nachrichten-Authentifizierungscode mit <i>HMAC-RIPEMD-160</i>",
                description="Ein Message Authentication Code (MAC) schützt Integrität und Herkunft einer Nachricht. Diese Vorlage berechnet ihn als HMAC mit der Hashfunktion RIPEMD-160.",
                memo="Die {\\b HMAC}-Komponente berechnet aus einer Nachricht und einem geheimen Schlüssel einen MAC. Hier ist die Hashfunktion RIPEMD-160; andere Hashfunktionen können in den Einstellungen gewählt werden."),
        keywords="HMAC, RIPEMD, MAC",
    ),
]

def block_cipher(name, algorithm, summary_en, summary_de, description_en, description_de, keywords):
    """A template of the BlockCiphers component: the Camellia template with the component replaced."""
    memo_en = ("With this template you can use the {\\b " + name + "} component to encrypt an arbitrary text entered in the {\\b Plaintext} component on the left side. "
               "The key is entered as hexadecimal digits in the {\\b Key} component. The cipher, the chaining mode and the padding are chosen in the settings of the component.")
    memo_de = ("Mit dieser Vorlage können Sie einen beliebigen Text, der in die Komponente {\\b Klartext} auf der linken Seite eingegeben wird, mit der Komponente {\\b " + name + "} verschlüsseln. "
               "Der Schlüssel wird als Hexadezimalziffern in der Komponente {\\b Schlüssel} eingegeben. Die Chiffre, der Verkettungsmodus und das Padding werden in den Einstellungen der Komponente gewählt.")
    return dict(
        out=f"Cryptography/Modern/Symmetric/{name}", base="Cryptography/Modern/Symmetric/Camellia",
        edits=[dict(match="Camellia.Camellia", retype=dict(type="BlockCiphers.BlockCiphers", assembly="BlockCiphers", name="Block Ciphers",
                                                         settings=dict(Algorithm=str(algorithm), Action="0", Mode="0", Padding="1"),
                                                         typeLike=dict(Algorithm="Mode")))],
        plugin="BlockCiphers",
        en=dict(title=f"{name} Cipher", summary=summary_en, description=description_en, memo=memo_en),
        de=dict(title=f"{name}-Chiffre", summary=summary_de, description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    block_cipher("IDEA", 0, "Encryption with the <i>IDEA</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>IDEA</i>",
                 "IDEA (International Data Encryption Algorithm) is a block cipher by Xuejia Lai and James Massey from 1991. It works on 64-bit blocks with a 128-bit key and mixes three incompatible group operations: XOR, addition modulo 2^16 and multiplication modulo 2^16 + 1.",
                 "IDEA (International Data Encryption Algorithm) ist eine Blockchiffre von Xuejia Lai und James Massey aus dem Jahr 1991. Sie arbeitet mit 64-Bit-Blöcken und einem 128-Bit-Schlüssel und mischt drei miteinander unverträgliche Gruppenoperationen: XOR, Addition modulo 2^16 und Multiplikation modulo 2^16 + 1.",
                 "IDEA, block cipher, Lai, Massey, symmetric"),
    block_cipher("RC5", 1, "Encryption with the <i>RC5</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>RC5</i>",
                 "RC5 is a block cipher by Ron Rivest from 1994 whose main feature are data-dependent rotations. This template uses 32-bit words (64-bit blocks) and 12 rounds with a 128-bit key.",
                 "RC5 ist eine Blockchiffre von Ron Rivest aus dem Jahr 1994, deren Hauptmerkmal datenabhängige Rotationen sind. Diese Vorlage verwendet 32-Bit-Wörter (64-Bit-Blöcke) und 12 Runden mit einem 128-Bit-Schlüssel.",
                 "RC5, block cipher, Rivest, symmetric"),
    block_cipher("RC6", 2, "Encryption with the <i>RC6</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>RC6</i>",
                 "RC6 is a block cipher by Rivest, Robshaw, Sidney and Yin from 1998, an extension of RC5 and finalist of the AES competition. It works on 128-bit blocks with 20 rounds.",
                 "RC6 ist eine Blockchiffre von Rivest, Robshaw, Sidney und Yin aus dem Jahr 1998, eine Erweiterung von RC5 und Finalist des AES-Wettbewerbs. Sie arbeitet mit 128-Bit-Blöcken und 20 Runden.",
                 "RC6, block cipher, Rivest, AES finalist, symmetric"),
    block_cipher("Serpent", 3, "Encryption with the <i>Serpent</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>Serpent</i>",
                 "Serpent is a block cipher by Ross Anderson, Eli Biham and Lars Knudsen from 1998 and finalist of the AES competition. It works on 128-bit blocks in 32 rounds of substitution-permutation steps.",
                 "Serpent ist eine Blockchiffre von Ross Anderson, Eli Biham und Lars Knudsen aus dem Jahr 1998 und Finalist des AES-Wettbewerbs. Sie arbeitet mit 128-Bit-Blöcken in 32 Runden aus Substitutions-Permutations-Schritten.",
                 "Serpent, block cipher, AES finalist, symmetric"),
]

def generator(out, algorithm, settings, title_en, title_de, summary_en, summary_de, description_en, description_de, memo_en, memo_de, keywords):
    """A pseudo-random number generator template: the RandomNumberGenerators template with another algorithm."""
    return dict(
        out=out, base="Tools/RandomNumberGenerators",
        edits=[dict(match="RandomNumberGenerator.RandomNumberGenerator", set=dict(AlgorithmType=str(algorithm), **settings))],
        en=dict(title=title_en, summary=summary_en, description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=summary_de, description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    generator("Mathematics/X2modN_Generator", 2, dict(Seed="1234567", Modulus="998244361984199177"),
              "x² mod N Generator", "x² mod N-Generator",
              "Pseudo-random numbers with the <i>x² mod N</i> generator", "Pseudozufallszahlen mit dem <i>x² mod N</i>-Generator",
              "The x² mod N generator squares its state modulo N = p * q in every step: x(i+1) = x(i)^2 mod N. It is the basis of the Blum Blum Shub generator.",
              "Der x² mod N-Generator quadriert in jedem Schritt seinen Zustand modulo N = p * q: x(i+1) = x(i)^2 mod N. Er ist die Grundlage des Blum-Blum-Shub-Generators.",
              "The {\\b Random Number Generator} component is set to the algorithm \"X^2 mod N\". Seed and modulus can be changed in its settings. Without special primes the sequence can have short cycles.",
              "Die Komponente {\\b Zufallszahlengenerator} ist auf den Algorithmus \"X^2 mod N\" gestellt. Startwert und Modul können in den Einstellungen geändert werden. Ohne spezielle Primzahlen kann die Folge kurze Zyklen haben.",
              "x^2 mod N, pseudo-random, generator"),
    generator("Mathematics/BlumBlumShub", 2, dict(Seed="1234567", Modulus="1000000094000000609"),
              "Blum Blum Shub Generator", "Blum-Blum-Shub-Generator",
              "Pseudo-random numbers with the <i>Blum Blum Shub</i> generator", "Pseudozufallszahlen mit dem <i>Blum-Blum-Shub</i>-Generator",
              "The Blum Blum Shub generator (1986) is the x² mod N generator with N = p * q, where p and q are primes congruent to 3 modulo 4 (a Blum integer). Its security rests on the difficulty of factoring N.",
              "Der Blum-Blum-Shub-Generator (1986) ist der x² mod N-Generator mit N = p * q, wobei p und q Primzahlen sind, die kongruent zu 3 modulo 4 sind (eine Blum-Zahl). Seine Sicherheit beruht auf der Schwierigkeit, N zu faktorisieren.",
              "The modulus N = 1000000007 * 1000000087 is a Blum integer (both primes are congruent to 3 mod 4). The seed must be coprime to N. For real use N would have to be much larger.",
              "Der Modul N = 1000000007 * 1000000087 ist eine Blum-Zahl (beide Primzahlen sind kongruent zu 3 mod 4). Der Startwert muss teilerfremd zu N sein. Für den echten Einsatz müsste N viel größer sein.",
              "Blum Blum Shub, BBS, pseudo-random, generator"),
    generator("Mathematics/InverseCongruentialGenerator", 4, dict(Seed="5", Modulus="1000000007", a="7", b="3"),
              "Inverse Congruential Generator (ICG)", "Inverser Kongruenzgenerator (IKG)",
              "Pseudo-random numbers with the <i>inverse congruential generator</i>", "Pseudozufallszahlen mit dem <i>inversen Kongruenzgenerator</i>",
              "The inverse congruential generator computes x(i+1) = a * x(i)^-1 + b mod p for a prime p, where x^-1 is the modular inverse. In contrast to the linear congruential generator its output has no lattice structure.",
              "Der inverse Kongruenzgenerator berechnet x(i+1) = a * x(i)^-1 + b mod p für eine Primzahl p, wobei x^-1 das modulare Inverse ist. Anders als beim linearen Kongruenzgenerator hat seine Ausgabe keine Gitterstruktur.",
              "The modulus p = 1000000007 is prime, so every non-zero value has an inverse. a, b, the seed and the modulus can be changed in the settings of the {\\b Random Number Generator} component.",
              "Der Modul p = 1000000007 ist eine Primzahl, daher hat jeder Wert außer 0 ein Inverses. a, b, Startwert und Modul können in den Einstellungen der Komponente {\\b Zufallszahlengenerator} geändert werden.",
              "ICG, inverse congruential, pseudo-random, generator"),
]

def message_digest(name, algorithm, description_en, description_de, keywords):
    """A template of the MessageDigests component: the MD5 template with the component replaced."""
    return dict(
        out=f"Hashfunctions/{name}", base="Hashfunctions/MD5",
        edits=[dict(match="MD5.MD5", retype=dict(type="MessageDigests.MessageDigests", assembly="MessageDigests", name="MD2 / MD4",
                                                settings=dict(Algorithm=str(algorithm)), typeLike=dict(Algorithm="Mode")))],
        plugin="MessageDigests",
        en=dict(title=name, summary=f"Usage of the <i>{name}</i> hash function", description=description_en, memo=""),
        de=dict(title=name, summary=f"Benutzung der <i>{name}</i>-Hash-Funktion", description=description_de, memo=""),
        keywords=keywords,
    )


TEMPLATES += [
    message_digest("MD2", 0, "This template shows the usage of the <i>MD2</i> hash function (Rivest, 1989, RFC 1319) with a 128-bit hash value.",
                   "Diese Vorlage zeigt die Benutzung der Hash-Funktion <i>MD2</i> (Rivest, 1989, RFC 1319) mit einem 128-Bit-Hashwert.",
                   "message, digest, Hash, Rivest, MD2"),
    message_digest("MD4", 1, "This template shows the usage of the <i>MD4</i> hash function (Rivest, 1990, RFC 1320). MD4 is broken and must not be used for security purposes.",
                   "Diese Vorlage zeigt die Benutzung der Hash-Funktion <i>MD4</i> (Rivest, 1990, RFC 1320). MD4 ist gebrochen und darf nicht für Sicherheitszwecke verwendet werden.",
                   "message, digest, Hash, Rivest, MD4"),
]

def randomness_test(out, test, length, settings, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A randomness test template: the RandomNumberGenerators template with the Randomness Tests component added between generator and output."""
    rng = "RandomNumberGenerator.RandomNumberGenerator"
    return dict(
        out=out, base="Tools/RandomNumberGenerators",
        edits=[dict(match=rng, set=dict(AlgorithmType="1", OutputType="0", OutputLength=str(length))),
               dict(move=dict(match="TextOutput", dx=300)),
               dict(add=dict(type="RandomnessTests.RandomnessTests", at=dict(match=rng, dx=250, dy=0), w=150, h=100,
                             settings=dict(Test=str(test), **settings))),
               dict(disconnect=dict(**{"from": rng + ".Output", "to": "TextOutput.Input"})),
               dict(connect=dict(**{"from": rng + ".Output", "to": "RandomnessTests.InputData"})),
               dict(connect=dict(**{"from": "RandomnessTests.Report", "to": "TextOutput.Input"}))],
        plugin="RandomnessTests",
        en=dict(title=title_en, summary=f"Testing random numbers with the <i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"Zufallszahlen prüfen mit dem <i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


_NOTE_EN = " A good generator fails such a test by chance about once in a hundred runs (significance level 0.01), so a single failure proves nothing."
_NOTE_DE = " Ein guter Generator fällt bei einem solchen Test zufällig etwa einmal in hundert Durchläufen durch (Signifikanzniveau 0,01), ein einzelnes Durchfallen beweist also nichts."

TEMPLATES += [
    randomness_test("Tools/FIPS140-1", 0, 2500, {}, "FIPS 140-1 Test", "FIPS-140-1-Test",
                    "The four statistical tests of FIPS 140-1 (monobit, poker, runs and long run) on 20000 bits from a random number generator.",
                    "Die vier statistischen Tests aus FIPS 140-1 (Monobit, Poker, Runs und Long Run) über 20000 Bits eines Zufallszahlengenerators.",
                    "The {\\b Random Number Generator} produces 2500 bytes (20000 bits). The {\\b Randomness Tests} component applies the four tests of FIPS 140-1 and reports the result of each." + _NOTE_EN,
                    "Der {\\b Zufallszahlengenerator} erzeugt 2500 Bytes (20000 Bits). Die Komponente {\\b Zufallstests} wendet die vier Tests aus FIPS 140-1 an und meldet das Ergebnis jedes Tests." + _NOTE_DE,
                    "FIPS 140-1, randomness, monobit, poker, runs, long run"),
    randomness_test("Tools/PokerTest", 1, 12500, dict(PokerBlockLength="4"), "Poker Test", "Poker-Test",
                    "The poker test cuts a bit sequence into blocks of m bits and compares the frequency of the 2^m values with the uniform distribution (chi-square test).",
                    "Der Poker-Test zerlegt eine Bitfolge in Blöcke zu m Bit und vergleicht die Häufigkeit der 2^m Werte mit der Gleichverteilung (Chi-Quadrat-Test).",
                    "The block length m is set in the settings of the {\\b Randomness Tests} component (here 4 bits, 25000 blocks)." + _NOTE_EN,
                    "Die Blocklänge m wird in den Einstellungen der Komponente {\\b Zufallstests} festgelegt (hier 4 Bit, 25000 Blöcke)." + _NOTE_DE,
                    "poker test, randomness, chi-square"),
    randomness_test("Tools/SerialTest", 2, 12500, dict(SerialBlockLength="2"), "Serial Test", "Serial-Test",
                    "The serial test checks whether all overlapping blocks of m bits occur equally often (chi-square test, NIST SP 800-22).",
                    "Der Serial-Test prüft, ob alle überlappenden Blöcke zu m Bit gleich häufig vorkommen (Chi-Quadrat-Test, NIST SP 800-22).",
                    "The block length m is set in the settings of the {\\b Randomness Tests} component (here 2 bits)." + _NOTE_EN,
                    "Die Blocklänge m wird in den Einstellungen der Komponente {\\b Zufallstests} festgelegt (hier 2 Bit)." + _NOTE_DE,
                    "serial test, randomness, chi-square"),
    randomness_test("Tools/RunsTest", 3, 12500, {}, "Runs Test", "Runs-Test",
                    "The runs test compares the number of runs of equal bits with the number expected for the proportion of ones (NIST SP 800-22).",
                    "Der Runs-Test vergleicht die Anzahl der Läufe gleicher Bits mit der Anzahl, die bei dem Anteil von Einsen zu erwarten ist (NIST SP 800-22).",
                    "The {\\b Randomness Tests} component counts the runs in the generated sequence and reports the p-value." + _NOTE_EN,
                    "Die Komponente {\\b Zufallstests} zählt die Läufe in der erzeugten Folge und meldet den p-Wert." + _NOTE_DE,
                    "runs test, randomness"),
]

def signature(out, algorithm, key_size, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """Key generation, signing and verification with the DigitalSignatures component (built on the layout of an empty base template)."""
    sig = "DigitalSignatures.DigitalSignatures"
    text, boolean, output = "CrypTool.TextInput.TextInput", "CrypTool.Plugins.BooleanOperators.BooleanOutput", "TextOutput.TextOutput"
    settings = lambda action: dict(Action=str(action), Algorithm=str(algorithm), Hash="1", KeySize=str(key_size), Curve="0")
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text, x=40, y=60, w=200, h=120, settings=dict(Text="This message is signed and then verified."))),
             dict(add=dict(type=sig, x=40, y=260, w=170, h=100, name="Generate keys", settings=settings(0))),
             dict(add=dict(type=sig, x=360, y=120, w=170, h=100, name="Sign", settings=settings(1))),
             dict(add=dict(type=sig, x=680, y=120, w=170, h=100, name="Verify", settings=settings(2))),
             dict(add=dict(type=output, x=360, y=300, w=300, h=100)),
             dict(add=dict(type=boolean, x=960, y=140, w=120, h=80)),
             dict(connect={"from": text + ".TextOutput", "to": sig + "#1.InputMessage"}),
             dict(connect={"from": text + ".TextOutput", "to": sig + "#2.InputMessage"}),
             dict(connect={"from": sig + "#0.OutputPrivateKey", "to": sig + "#1.InputPrivateKey"}),
             dict(connect={"from": sig + "#0.OutputPublicKey", "to": sig + "#2.InputPublicKey"}),
             dict(connect={"from": sig + "#1.OutputSignature", "to": sig + "#2.InputSignature"}),
             dict(connect={"from": sig + "#1.OutputSignature", "to": output + ".Input"}),
             dict(connect={"from": sig + "#2.Valid", "to": boolean + ".Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="DigitalSignatures",
        en=dict(title=title_en, summary=f"Key generation, signing and verification with <i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"Schlüsselerzeugung, Signieren und Prüfen mit <i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


_SIG_EN = " The first {\\b Digital Signatures} component generates a key pair, the second signs the message with the private key, the third verifies the signature with the public key. Change the message in the text input and the algorithm, hash function and key size in the settings of the components."
_SIG_DE = " Die erste Komponente {\\b Digitale Signaturen} erzeugt ein Schlüsselpaar, die zweite signiert die Nachricht mit dem privaten Schlüssel, die dritte prüft die Signatur mit dem öffentlichen Schlüssel. Nachricht, Verfahren, Hashfunktion und Schlüssellänge lassen sich in der Texteingabe und in den Einstellungen der Komponenten ändern."

TEMPLATES += [
    signature("Cryptography/Modern/Asymmetric/Signature_RSA", 0, 1, "RSA Signature", "RSA-Signatur",
              "Digital signature with RSA (PKCS#1 v1.5): the hash value of the message is signed with the private key and checked with the public key.",
              "Digitale Signatur mit RSA (PKCS#1 v1.5): Der Hashwert der Nachricht wird mit dem privaten Schlüssel signiert und mit dem öffentlichen Schlüssel geprüft.",
              "RSA signatures are deterministic: the same message and key always give the same signature." + _SIG_EN,
              "RSA-Signaturen sind deterministisch: Dieselbe Nachricht und derselbe Schlüssel ergeben immer dieselbe Signatur." + _SIG_DE,
              "RSA, signature, PKCS#1, digital signature"),
    signature("Cryptography/Modern/Asymmetric/Signature_RSA_PSS", 1, 1, "RSA-PSS Signature", "RSA-PSS-Signatur",
              "Digital signature with RSA and the probabilistic signature scheme PSS (PKCS#1 v2.1).",
              "Digitale Signatur mit RSA und dem probabilistischen Signaturverfahren PSS (PKCS#1 v2.1).",
              "PSS adds a random salt, so signing the same message twice gives different signatures that are both valid." + _SIG_EN,
              "PSS fügt ein Zufallssalz hinzu, dieselbe Nachricht ergibt beim zweimaligen Signieren also verschiedene Signaturen, die beide gültig sind." + _SIG_DE,
              "RSA, PSS, signature, probabilistic"),
    signature("Cryptography/Modern/Asymmetric/Signature_DSA", 2, 0, "DSA Signature", "DSA-Signatur",
              "Digital signature with the Digital Signature Algorithm (DSA, FIPS 186) based on the discrete logarithm.",
              "Digitale Signatur mit dem Digital Signature Algorithm (DSA, FIPS 186), das auf dem diskreten Logarithmus beruht.",
              "The template uses a 1024-bit prime (with a 160-bit subgroup) so that the parameters are generated quickly; use 2048 bits or more in practice. DSA is randomized, so signatures differ from run to run." + _SIG_EN,
              "Die Vorlage verwendet eine 1024-Bit-Primzahl (mit 160-Bit-Untergruppe), damit die Parameter schnell erzeugt werden; in der Praxis sind 2048 Bit oder mehr zu verwenden. DSA ist randomisiert, die Signaturen unterscheiden sich daher von Durchlauf zu Durchlauf." + _SIG_DE,
              "DSA, signature, discrete logarithm, FIPS 186"),
    signature("Cryptography/Modern/Asymmetric/Signature_ECDSA", 3, 1, "ECDSA Signature", "ECDSA-Signatur",
              "Digital signature with the elliptic curve variant of DSA (ECDSA) on the curve secp256r1.",
              "Digitale Signatur mit der Variante von DSA auf elliptischen Kurven (ECDSA) auf der Kurve secp256r1.",
              "The curve is chosen in the settings of the key generating component." + _SIG_EN,
              "Die Kurve wird in den Einstellungen der Komponente gewählt, die das Schlüsselpaar erzeugt." + _SIG_DE,
              "ECDSA, elliptic curve, signature"),
    signature("Cryptography/Modern/Asymmetric/Signature_ECNR", 4, 1, "ECNR Signature", "ECNR-Signatur",
              "Digital signature with the Nyberg-Rueppel scheme on elliptic curves (ECNR), a signature with message recovery.",
              "Digitale Signatur mit dem Nyberg-Rueppel-Verfahren auf elliptischen Kurven (ECNR), einer Signatur mit Nachrichten-Wiederherstellung.",
              "The curve is chosen in the settings of the key generating component." + _SIG_EN,
              "Die Kurve wird in den Einstellungen der Komponente gewählt, die das Schlüsselpaar erzeugt." + _SIG_DE,
              "ECNR, Nyberg-Rueppel, elliptic curve, signature"),
]

def public_key_encryption(out, algorithm, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """Key generation, encryption and decryption with the PublicKeyEncryption component (layout built on an empty base template)."""
    pke = "PublicKeyEncryption.PublicKeyEncryption"
    text, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    settings = lambda action: dict(Action=str(action), Algorithm=str(algorithm), KeySize="0")
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text, x=40, y=60, w=240, h=120, settings=dict(Text="Public-key encryption: only the owner of the private key can read this."))),
             dict(add=dict(type=pke, x=40, y=280, w=170, h=100, name="Generate keys", settings=settings(0))),
             dict(add=dict(type=pke, x=360, y=120, w=170, h=100, name="Encrypt", settings=settings(1))),
             dict(add=dict(type=pke, x=680, y=120, w=170, h=100, name="Decrypt", settings=settings(2))),
             dict(add=dict(type=output, x=360, y=300, w=300, h=100)),
             dict(add=dict(type=output, x=960, y=120, w=300, h=100)),
             dict(connect={"from": text + ".TextOutput", "to": pke + "#1.InputData"}),
             dict(connect={"from": pke + "#0.OutputPublicKey", "to": pke + "#1.InputPublicKey"}),
             dict(connect={"from": pke + "#0.OutputPrivateKey", "to": pke + "#2.InputPrivateKey"}),
             dict(connect={"from": pke + "#1.OutputData", "to": pke + "#2.InputData"}),
             dict(connect={"from": pke + "#1.OutputData", "to": output + "#0.Input"}),
             dict(connect={"from": pke + "#2.OutputData", "to": output + "#1.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="PublicKeyEncryption",
        en=dict(title=title_en, summary=f"Key generation, encryption and decryption with <i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"Schlüsselerzeugung, Ver- und Entschlüsselung mit <i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


_PKE_EN = " The first {\\b ElGamal / Rabin} component generates a key pair, the second encrypts the text with the public key, the third decrypts it with the private key. The text in the middle is the ciphertext."
_PKE_DE = " Die erste Komponente {\\b ElGamal / Rabin} erzeugt ein Schlüsselpaar, die zweite verschlüsselt den Text mit dem öffentlichen Schlüssel, die dritte entschlüsselt ihn mit dem privaten Schlüssel. Der Text in der Mitte ist der Geheimtext."

TEMPLATES += [
    public_key_encryption("Cryptography/Modern/Asymmetric/ElGamal", 0, "ElGamal Encryption", "ElGamal-Verschlüsselung",
                          "ElGamal (1985) is a public-key encryption scheme based on the discrete logarithm problem: the ciphertext is the pair (g^k, m * y^k) with a random k.",
                          "ElGamal (1985) ist ein Public-Key-Verschlüsselungsverfahren, das auf dem Problem des diskreten Logarithmus beruht: Der Geheimtext ist das Paar (g^k, m * y^k) mit zufälligem k.",
                          "The group is the standardized 1024-bit safe prime of RFC 2409 with generator 2. Encrypting the same text twice gives different ciphertexts because k is chosen at random." + _PKE_EN,
                          "Die Gruppe ist die standardisierte 1024-Bit-Primzahl aus RFC 2409 mit dem Erzeuger 2. Derselbe Text ergibt beim zweimaligen Verschlüsseln verschiedene Geheimtexte, weil k zufällig gewählt wird." + _PKE_DE,
                          "ElGamal, public key, discrete logarithm, encryption"),
    public_key_encryption("Cryptography/Modern/Asymmetric/Rabin", 1, "Rabin Cryptosystem", "Rabin-Kryptosystem",
                          "The Rabin cryptosystem (1979) encrypts by squaring modulo n = p * q; decrypting means computing square roots, which is as hard as factoring n.",
                          "Das Rabin-Kryptosystem (1979) verschlüsselt durch Quadrieren modulo n = p * q; Entschlüsseln bedeutet, Quadratwurzeln zu ziehen, was so schwer ist wie das Faktorisieren von n.",
                          "A ciphertext has four square roots; the plaintext carries a marker byte and four check bytes, by which the receiver finds the right root. The 1024-bit key is made of two 512-bit primes congruent to 3 modulo 4." + _PKE_EN,
                          "Ein Geheimtext hat vier Quadratwurzeln; der Klartext trägt ein Markierungsbyte und vier Prüfbytes, an denen der Empfänger die richtige Wurzel erkennt. Der 1024-Bit-Schlüssel besteht aus zwei 512-Bit-Primzahlen, die kongruent zu 3 modulo 4 sind." + _PKE_DE,
                          "Rabin, public key, factoring, square roots"),
]

def cipher_mac(name, cipher, key_text, block_bits):
    """A MAC template of the CipherMAC component: the HMAC template with the component replaced."""
    return dict(
        out=f"Hashfunctions/MAC_{name}", base="Hashfunctions/HMAC",
        edits=[dict(match="HMAC.HMAC", retype=dict(type="CipherMAC.CipherMAC", assembly="CipherMAC", name="Cipher MAC",
                                                  settings=dict(Cipher=str(cipher), Mode="0"), typeLike=dict(Cipher="SelectedHashFunction", Mode="SelectedHashFunction"))),
               dict(match="TextInput", where=dict(Text="A test HMAC key"), set=dict(Text=key_text)),
               dict(match="TextInput", where=dict(Text="The message / input"), set=dict(Text="The message that is authenticated."))],
        plugin="CipherMAC",
        en=dict(title=f"{name} MAC", summary=f"Message authentication code with <i>{name}</i> (CMAC)",
                description=f"A message authentication code (MAC) protects the integrity and the origin of a message. This template builds it from the block cipher {name} with the CMAC construction (NIST SP 800-38B); the MAC is {block_bits // 8} bytes long.",
                memo=f"The {{\\b Cipher MAC}} component computes a MAC from a message and a secret key with the block cipher {name}. The key in this template is only an example; its size must fit the cipher. The block cipher and the construction (CMAC or CBC-MAC) are chosen in the settings."),
        de=dict(title=f"{name}-MAC", summary=f"Nachrichten-Authentifizierungscode mit <i>{name}</i> (CMAC)",
                description=f"Ein Message Authentication Code (MAC) schützt Integrität und Herkunft einer Nachricht. Diese Vorlage baut ihn aus der Blockchiffre {name} mit der CMAC-Konstruktion (NIST SP 800-38B); der MAC ist {block_bits // 8} Bytes lang.",
                memo=f"Die Komponente {{\\b Chiffren-MAC}} berechnet aus einer Nachricht und einem geheimen Schlüssel mit der Blockchiffre {name} einen MAC. Der Schlüssel in dieser Vorlage ist nur ein Beispiel; seine Länge muss zur Chiffre passen. Die Blockchiffre und die Konstruktion (CMAC oder CBC-MAC) werden in den Einstellungen gewählt."),
        keywords=f"MAC, CMAC, {name}, message authentication",
    )


TEMPLATES += [cipher_mac(name, index, "12345678" if name == "DES" else "0123456789abcdef", 64 if name in ("DES", "IDEA", "RC2", "RC5") else 128)
              for index, name in enumerate(["AES", "DES", "Camellia", "IDEA", "RC2", "RC5", "RC6", "Serpent", "Twofish"])]

def number_theory(out, operation, numbers, texts, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A Number Theory template (layout built on an empty base template): number inputs (Input 1, Input 2, Modulus) or text inputs
    (CRT: residues, moduli) on the left, the component, the result and the steps on the right."""
    nt = "NumberTheory.NumberTheory"
    number_input, text, output = "CrypTool.Plugins.Numbers.NumberInput", "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=nt, x=400, y=120, w=180, h=120, settings=dict(Operation=str(operation))))]
    for index, (port, value) in enumerate(numbers):
        edits += [dict(add=dict(type=number_input, x=40, y=40 + 110 * index, w=200, h=90, settings=dict(Number=value))),
                  dict(connect={"from": f"{number_input}#{index}.NumberOutput", "to": f"{nt}.{port}"})]
    for index, (port, value) in enumerate(texts):
        edits += [dict(add=dict(type=text, x=40, y=40 + 110 * index, w=200, h=90, settings=dict(Text=value))),
                  dict(connect={"from": f"{text}#{index}.TextOutput", "to": f"{nt}.{port}"})]
    edits += [dict(add=dict(type=output, x=700, y=40, w=240, h=90)), dict(add=dict(type=output, x=700, y=170, w=420, h=260)),
              dict(connect={"from": f"{nt}.Output", "to": f"{output}#0.Input"}),
              dict(connect={"from": f"{nt}.Steps", "to": f"{output}#1.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="NumberTheory",
        en=dict(title=title_en, summary=f"<i>{title_en}</i> with its steps", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i> mit allen Schritten", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    number_theory("Mathematics/SquareAndMultiply", 0, [("Input1", "4"), ("Input2", "13"), ("Mod", "497")], [],
                  "Square and Multiply", "Square and Multiply",
                  "Modular exponentiation by repeated squaring: x^e mod n with about log2(e) squarings and multiplications.",
                  "Modulare Exponentiation durch wiederholtes Quadrieren: x^e mod n mit etwa log2(e) Quadrierungen und Multiplikationen.",
                  "The inputs are the base x, the exponent e and the modulus n. The upper output is the result, the lower one lists for every bit of the exponent what was done. RSA and Diffie-Hellman depend on this algorithm.",
                  "Die Eingaben sind die Basis x, der Exponent e und der Modul n. Die obere Ausgabe ist das Ergebnis, die untere listet für jedes Bit des Exponenten auf, was getan wurde. RSA und Diffie-Hellman beruhen auf diesem Algorithmus.",
                  "square and multiply, binary exponentiation, modular exponentiation"),
    number_theory("Mathematics/BabyStepGiantStep", 1, [("Input1", "5"), ("Input2", "8"), ("Mod", "23")], [],
                  "Baby-Step Giant-Step", "Baby-Step Giant-Step",
                  "Shanks' algorithm for the discrete logarithm: finds e with g^e = h (mod p) in about the square root of p steps.",
                  "Der Algorithmus von Shanks für den diskreten Logarithmus: findet e mit g^e = h (mod p) in etwa der Quadratwurzel aus p Schritten.",
                  "The inputs are the base g, the value h and the prime p. The upper output is the smallest e with g^e = h (mod p), the lower one lists the baby steps and giant steps. For primes of a few hundred bits the method is far too slow, which is why the discrete logarithm protects Diffie-Hellman.",
                  "Die Eingaben sind die Basis g, der Wert h und die Primzahl p. Die obere Ausgabe ist das kleinste e mit g^e = h (mod p), die untere listet die Baby Steps und Giant Steps. Bei Primzahlen von einigen hundert Bit ist das Verfahren viel zu langsam, daher schützt der diskrete Logarithmus Diffie-Hellman.",
                  "baby-step giant-step, Shanks, discrete logarithm"),
    number_theory("Mathematics/ChineseRemainderTheorem", 2, [], [("Residues", "2,3,2"), ("Moduli", "3,5,7")],
                  "Chinese Remainder Theorem", "Chinesischer Restsatz",
                  "The Chinese remainder theorem solves a system of congruences x = a_i (mod m_i) with pairwise coprime moduli.",
                  "Der Chinesische Restsatz löst ein System von Kongruenzen x = a_i (mod m_i) mit paarweise teilerfremden Moduln.",
                  "The inputs are the list of residues and the list of moduli. The upper output is the solution modulo the product of the moduli, the lower one shows the computation (M, M_i and the inverses y_i). RSA decryption is sped up with it.",
                  "Die Eingaben sind die Liste der Reste und die Liste der Moduln. Die obere Ausgabe ist die Lösung modulo dem Produkt der Moduln, die untere zeigt die Rechnung (M, M_i und die Inversen y_i). Die RSA-Entschlüsselung wird damit beschleunigt.",
                  "Chinese remainder theorem, CRT, congruences"),
]

def secret_sharing(out, verifiable, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """Secret sharing templates (layout built on an empty base template): split a secret, then reconstruct it (or verify the shares)."""
    ss = "SecretSharing.SecretSharing"
    number_input, output = "CrypTool.Plugins.Numbers.NumberInput", "TextOutput.TextOutput"
    boolean = "CrypTool.Plugins.BooleanOperators.BooleanOutput"
    settings = lambda action: dict(Action=str(action), Verifiable="True" if verifiable else "False")
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=number_input, x=40, y=20, w=200, h=90, settings=dict(Number="123456789012345678901234567890"))),
             dict(add=dict(type=number_input, x=40, y=130, w=200, h=90, settings=dict(Number="3"))),
             dict(add=dict(type=number_input, x=40, y=240, w=200, h=90, settings=dict(Number="5"))),
             dict(add=dict(type=ss, x=340, y=100, w=180, h=120, settings=settings(0))),
             dict(add=dict(type=output, x=640, y=20, w=300, h=200)),
             dict(connect={"from": number_input + "#0.NumberOutput", "to": ss + "#0.Secret"}),
             dict(connect={"from": number_input + "#1.NumberOutput", "to": ss + "#0.Threshold"}),
             dict(connect={"from": number_input + "#2.NumberOutput", "to": ss + "#0.Number"}),
             dict(connect={"from": ss + "#0.OutputShares", "to": output + "#0.Input"}),
             dict(add=dict(type=ss, x=340, y=380, w=180, h=120, settings=settings(2 if verifiable else 1))),
             dict(connect={"from": ss + "#0.OutputShares", "to": ss + "#1.InputShares"})]
    if verifiable:
        edits += [dict(add=dict(type=output, x=640, y=250, w=300, h=160)), dict(add=dict(type=boolean, x=640, y=440, w=120, h=80)),
                  dict(connect={"from": ss + "#0.OutputCommitments", "to": output + "#1.Input"}),
                  dict(connect={"from": ss + "#0.OutputCommitments", "to": ss + "#1.InputCommitments"}),
                  dict(connect={"from": ss + "#1.Valid", "to": boolean + ".Input"})]
    else:
        edits += [dict(add=dict(type=output, x=640, y=400, w=300, h=100)),
                  dict(connect={"from": ss + "#1.OutputSecret", "to": output + "#1.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="SecretSharing",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    secret_sharing("Protocols/ShamirSecretSharing", False, "Shamir's Secret Sharing", "Secret Sharing nach Shamir",
                   "Shamir's threshold scheme splits a secret into n shares of which any k reconstruct it, while k-1 reveal nothing.",
                   "Das Schwellwertverfahren von Shamir zerlegt ein Geheimnis in n Anteile, von denen je k es rekonstruieren, während k-1 nichts verraten.",
                   "The first component splits the secret (a number) with threshold 3 into 5 shares; the second reconstructs the secret from the shares it is given (here all of them; any 3 of the lines are enough). Change the numbers on the left and delete lines in the shares to see that fewer than 3 shares give a wrong number.",
                   "Die erste Komponente zerlegt das Geheimnis (eine Zahl) mit Schwellenwert 3 in 5 Anteile; die zweite rekonstruiert das Geheimnis aus den Anteilen, die sie bekommt (hier allen; je 3 der Zeilen genügen). Ändern Sie die Zahlen links und streichen Sie Zeilen aus den Anteilen, um zu sehen, dass weniger als 3 Anteile eine falsche Zahl ergeben.",
                   "Shamir, secret sharing, threshold, polynomial"),
    secret_sharing("Protocols/VerifiableSecretSharing", True, "Verifiable Secret Sharing (Feldman)", "Überprüfbares Secret Sharing (Feldman)",
                   "Feldman's verifiable secret sharing: the dealer publishes commitments to the polynomial, with which every holder can check their share.",
                   "Überprüfbares Secret Sharing nach Feldman: Der Verteiler veröffentlicht Verpflichtungen auf das Polynom, mit denen jeder Inhaber seinen Anteil prüfen kann.",
                   "The first component splits the secret and publishes the commitments g^a_j; the second checks every share against the commitments (the Boolean output shows the result). Change one digit of a share or of a commitment to see the check fail.",
                   "Die erste Komponente zerlegt das Geheimnis und veröffentlicht die Verpflichtungen g^a_j; die zweite prüft jeden Anteil gegen die Verpflichtungen (die Boolesche Ausgabe zeigt das Ergebnis). Ändern Sie eine Ziffer eines Anteils oder einer Verpflichtung, um zu sehen, dass die Prüfung fehlschlägt.",
                   "Feldman, verifiable secret sharing, VSS, commitments"),
]

def chain(out, plugin, component, in_port, out_port, steps, text, key, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A text goes through one or two steps of the same component (layout built on an empty base template); the result of every step is shown.
    steps: the settings of each step; key: an optional text for the Key input of every step."""
    text_input, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=240, h=130, settings=dict(Text=text)))]
    if key:
        edits.append(dict(add=dict(type=text_input, x=40, y=220, w=240, h=90, settings=dict(Text=key))))
    for index, settings in enumerate(steps):
        x = 380 + 340 * index
        edits += [dict(add=dict(type=component, x=x, y=40, w=180, h=110, settings=settings)),
                  dict(add=dict(type=output, x=x, y=220, w=300, h=160)),
                  dict(connect={"from": (f"{component}#{index - 1}.{out_port}" if index else f"{text_input}#0.TextOutput"), "to": f"{component}#{index}.{in_port}"}),
                  dict(connect={"from": f"{component}#{index}.{out_port}", "to": f"{output}#{index}.Input"})]
        if key:
            edits.append(dict(connect={"from": f"{text_input}#1.TextOutput", "to": f"{component}#{index}.Key"}))
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin=plugin,
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


DT = "DataTools.DataTools"
TEMPLATES += [
    chain("Tools/UUEncoding", "DataTools", DT, "InputData", "OutputData", [dict(Operation="0", Amount="1"), dict(Operation="1", Amount="1")],
          "Binary data can be written as text with uuencode.", None, "UU Encoding", "UU-Codierung",
          "UU encoding (uuencode) writes data as printable text; the second component decodes it again.",
          "Die UU-Codierung (uuencode) schreibt Daten als druckbaren Text; die zweite Komponente decodiert ihn wieder.",
          "The first {\\b Data Tools} component encodes the text with uuencode (header line, lines with a length character, trailer), the second decodes it and gives the text back.",
          "Die erste Komponente {\\b Datenwerkzeuge} codiert den Text mit uuencode (Kopfzeile, Zeilen mit Längenzeichen, Schlusszeile), die zweite decodiert ihn und liefert den Text zurück.",
          "uuencode, UU, encoding"),
    chain("Tools/ZipUnzip", "DataTools", DT, "InputData", "OutputData", [dict(Operation="2", Amount="1"), dict(Operation="3", Amount="1")],
          "Compress me. " * 20, None, "Zip and Unzip", "Zip und Unzip",
          "The data is packed into a ZIP archive (deflate) and unpacked again.",
          "Die Daten werden in ein ZIP-Archiv (Deflate) gepackt und wieder entpackt.",
          "The first {\\b Data Tools} component creates a ZIP archive from the text, the second reads the first entry of the archive. The text repeats itself, so it compresses well.",
          "Die erste Komponente {\\b Datenwerkzeuge} erzeugt aus dem Text ein ZIP-Archiv, die zweite liest den ersten Eintrag des Archivs. Der Text wiederholt sich und lässt sich daher gut komprimieren.",
          "zip, unzip, compression, deflate"),
    chain("Tools/ByteWiseAddition", "DataTools", DT, "InputData", "OutputData", [dict(Operation="4", Amount="1"), dict(Operation="5", Amount="1")],
          "HAL", "1", "Byte-wise Addition", "Byteweise Addition",
          "The key bytes are added to the data bytes modulo 256, and subtracted again.",
          "Die Schlüsselbytes werden modulo 256 zu den Datenbytes addiert und wieder subtrahiert.",
          "The key (here the character 1, which is the byte 0x31) is added to every byte of the text modulo 256: H (0x48) becomes y (0x79). The second component subtracts the key again and gives HAL back. A longer key is repeated.",
          "Der Schlüssel (hier das Zeichen 1, also das Byte 0x31) wird modulo 256 zu jedem Byte des Textes addiert: aus H (0x48) wird y (0x79). Die zweite Komponente subtrahiert den Schlüssel wieder und liefert HAL zurück. Ein längerer Schlüssel wird wiederholt.",
          "byte-wise addition, modulo 256, Vigenere"),
    chain("Tools/BitShift", "DataTools", DT, "InputData", "OutputData", [dict(Operation="6", Amount="1"), dict(Operation="7", Amount="1")],
          "Bits move: \u00e4", None, "Bit Shift and Rotation", "Bit-Verschiebung und -Rotation",
          "Every byte is shifted by one bit to the left and back to the right: the bit that is pushed out at the high end is lost; a rotation keeps all bits.",
          "Jedes Byte wird um ein Bit nach links und wieder nach rechts geschoben: Das Bit, das am oberen Ende herausgeschoben wird, geht verloren; eine Rotation behält alle Bits.",
          "The first {\\b Data Tools} component shifts every byte of the text by one bit to the left, the second rotates the result by one bit to the right. Because the shift lost the high bit, the text does not come back; change the first operation to a rotation and it does. The settings show the number of bits.",
          "Die erste Komponente {\\b Datenwerkzeuge} schiebt jedes Byte des Textes um ein Bit nach links, die zweite rotiert das Ergebnis um ein Bit nach rechts. Weil das Schieben das höchste Bit verloren hat, kommt der Text nicht zurück; stellen Sie die erste Operation auf Rotation, dann schon. Die Einstellungen zeigen die Anzahl der Bits.",
          "bit shift, rotation, bits"),
    chain("Tools/Entropy", "ShannonEntropy", "ShannonEntropy.ShannonEntropy", "InputData", "Report", [dict(Symbol="0")],
          "The entropy of a text is lower than that of random data, because the letters of a language do not occur equally often and depend on each other.", None,
          "Entropy", "Entropie",
          "The Shannon entropy in bits per byte measures how unpredictable data is.",
          "Die Shannon-Entropie in Bit pro Byte misst, wie unvorhersagbar Daten sind.",
          "The {\\b Entropy} component counts the bytes of the text and computes H = -sum p(x) * log2 p(x). English text has about 4 to 4.5 bits per byte (at most 8); replace the text by the ciphertext of a good cipher and the entropy rises towards 8.",
          "Die Komponente {\\b Entropie} zählt die Bytes des Textes und berechnet H = -Summe p(x) * log2 p(x). Englischer Text hat etwa 4 bis 4,5 Bit je Byte (höchstens 8); ersetzen Sie den Text durch den Geheimtext einer guten Chiffre, und die Entropie steigt gegen 8.",
          "entropy, Shannon, information"),
]

CV = "ClassicVariants.ClassicVariants"


def classic(out, cipher, text, key, rails, offset, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A classic cipher template: encryption and, in the second component, decryption of a text."""
    steps = [dict(Cipher=str(cipher), Action=str(action), Rails=str(rails), Offset=str(offset)) for action in (0, 1)]
    return chain(out, "ClassicVariants", CV, "InputText", "OutputText", steps, text, key, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords)


TEMPLATES += [
    classic("Cryptography/Classic/PortaCipher", 0, "ATTACK AT DAWN", "SECRET", 3, 0, "Porta Cipher", "Porta-Chiffre",
            "Porta (1563) is a polyalphabetic cipher with 13 reciprocal alphabets selected by pairs of key letters.",
            "Porta (1563) ist eine polyalphabetische Chiffre mit 13 reziproken Alphabeten, die durch Paare von Schlüsselbuchstaben gewählt werden.",
            "The first {\\\\b Classic Variants} component encrypts the text with the key word, the second decrypts: because the cipher is reciprocal, both do the same thing. Spaces are kept.",
            "Die erste Komponente {\\\\b Klassische Varianten} verschlüsselt den Text mit dem Schlüsselwort, die zweite entschlüsselt: Weil die Chiffre reziprok ist, tun beide dasselbe. Leerzeichen bleiben erhalten.",
            "Porta, polyalphabetic, reciprocal"),
    classic("Cryptography/Classic/RailFenceCipher", 1, "WEAREDISCOVEREDFLEEATONCE", None, 3, 0, "Rail Fence Cipher", "Rail-Fence-Chiffre",
            "The rail fence cipher writes the text on a zigzag over several rails and reads it rail by rail.",
            "Die Rail-Fence-Chiffre schreibt den Text im Zickzack über mehrere Schienen und liest ihn Schiene für Schiene.",
            "The number of rails (here 3) and the offset are set in the settings of the {\\\\b Classic Variants} components. The example is the one of the Wikipedia article: WEAREDISCOVEREDFLEEATONCE becomes WECRLTEERDSOEEFEAOCAIVDEN.",
            "Die Anzahl der Schienen (hier 3) und der Versatz werden in den Einstellungen der Komponenten {\\\\b Klassische Varianten} festgelegt. Das Beispiel ist das aus dem Wikipedia-Artikel: aus WEAREDISCOVEREDFLEEATONCE wird WECRLTEERDSOEEFEAOCAIVDEN.",
            "rail fence, zigzag, transposition"),
    classic("Cryptography/Classic/RedefenceCipher", 1, "WEAREDISCOVEREDFLEEATONCE", "CAB", 3, 1, "Redefence Cipher", "Redefence-Chiffre",
            "Redefence is the rail fence cipher with an offset and a key that decides in which order the rails are read.",
            "Redefence ist die Rail-Fence-Chiffre mit einem Versatz und einem Schlüssel, der bestimmt, in welcher Reihenfolge die Schienen gelesen werden.",
            "The key has one letter per rail; the rails are read in the alphabetical order of the letters (here the second rail first, then the third, then the first). The offset 1 starts the zigzag on the second step.",
            "Der Schlüssel hat einen Buchstaben je Schiene; die Schienen werden in der alphabetischen Reihenfolge der Buchstaben gelesen (hier erst die zweite, dann die dritte, dann die erste). Der Versatz 1 beginnt den Zickzack beim zweiten Schritt.",
            "redefence, rail fence, transposition"),
    classic("Cryptography/Classic/KamasutraCipher", 2, "MEET ME AT NOON", "BADCFEHGJILKNMPORQTSVUXWZY", 3, 0, "Kamasutra Cipher", "Kamasutra-Chiffre",
            "The Kamasutra cipher is a reciprocal substitution: the key cuts the alphabet into pairs of letters, each letter is replaced by its partner.",
            "Die Kamasutra-Chiffre ist eine reziproke Substitution: Der Schlüssel zerlegt das Alphabet in Buchstabenpaare, jeder Buchstabe wird durch seinen Partner ersetzt.",
            "In this template the key BADCFEHG... pairs A with B, C with D, E with F and so on. Both components do the same thing, because the cipher is reciprocal.",
            "In dieser Vorlage verbindet der Schlüssel BADCFEHG... A mit B, C mit D, E mit F und so weiter. Beide Komponenten tun dasselbe, weil die Chiffre reziprok ist.",
            "Kamasutra, reciprocal, substitution"),
    classic("Cryptography/Classic/TwoSquareCipher", 3, "HELPMEOBIWANKENOBI", "EXAMPLE KEYWORD", 3, 0, "Two-Square Cipher (Doppelkasten)", "Doppelkasten-Chiffre (Two-Square)",
            "The two-square cipher encrypts pairs of letters with two 5x5 squares made of two key words.",
            "Die Doppelkasten-Chiffre (Two-Square) verschlüsselt Buchstabenpaare mit zwei 5x5-Quadraten aus zwei Schlüsselwörtern.",
            "The key consists of two words (here EXAMPLE and KEYWORD). The two letters of a pair, one looked up in each square, form a rectangle; the other two corners are the cipher letters. J is treated as I, an odd text gets an X.",
            "Der Schlüssel besteht aus zwei Wörtern (hier EXAMPLE und KEYWORD). Die beiden Buchstaben eines Paares, je einer in jedem Quadrat gesucht, bilden ein Rechteck; die anderen beiden Ecken sind die Geheimtextbuchstaben. J wird wie I behandelt, ein ungerader Text erhält ein X.",
            "two-square, Doppelkasten, digraph"),
]

TEMPLATES += [
    public_key_encryption("Cryptography/Modern/Asymmetric/MerkleHellman", 2, "Merkle-Hellman Knapsack", "Merkle-Hellman-Rucksack",
                          "The Merkle-Hellman cryptosystem (1978) is based on the knapsack problem: the public key is a disguised easy knapsack, the ciphertext the sum of the selected elements.",
                          "Das Kryptosystem von Merkle und Hellman (1978) beruht auf dem Rucksackproblem: Der öffentliche Schlüssel ist ein getarnter leichter Rucksack, der Geheimtext die Summe der ausgewählten Elemente.",
                          "The public key has 64 numbers b_i = r * w_i mod q made from a superincreasing sequence w_i, which is the private key. A block of 64 bits is encrypted as the sum of the b_i where the bit is 1. Shamir broke the scheme in 1982, it is only of historical interest." + _PKE_EN,
                          "Der öffentliche Schlüssel besteht aus 64 Zahlen b_i = r * w_i mod q, die aus einer superwachsenden Folge w_i gebildet werden, dem privaten Schlüssel. Ein Block von 64 Bit wird als Summe der b_i verschlüsselt, bei denen das Bit 1 ist. Shamir hat das Verfahren 1982 gebrochen, es ist nur von historischem Interesse." + _PKE_DE,
                          "Merkle-Hellman, knapsack, subset sum, public key"),
]

def hash_signature(out, scheme, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """Like the signature templates, with the HashBasedSignatures component (same connectors): LMS (scheme 0) or SPHINCS+ (scheme 1)."""
    spec = signature(out, 0, 0, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords)
    old, new = "DigitalSignatures.DigitalSignatures", "HashBasedSignatures.HashBasedSignatures"
    for edit in spec["edits"]:
        if "add" in edit and edit["add"]["type"] == old:
            action = edit["add"]["settings"]["Action"]
            edit["add"]["type"] = new
            edit["add"]["settings"] = dict(Action=action, Scheme=str(scheme), Height="0", Winternitz="2", Variant="0")
        for key in ("connect", "disconnect"):
            if key in edit:
                edit[key] = {k: v.replace(old, new) for k, v in edit[key].items()}
    spec["plugin"] = "HashBasedSignatures"
    return spec


_HS_EN = " The first {\\\\b Hash-Based Signatures} component generates a key pair, the second signs the message and the third verifies the signature. After signing, the LMS private key changes (a one-time key is used up); the number of signatures that are left is an output of the signing component."
_HS_DE = " Die erste Komponente {\\\\b Hashbasierte Signaturen} erzeugt ein Schlüsselpaar, die zweite signiert die Nachricht und die dritte prüft die Signatur. Nach dem Signieren ändert sich der private LMS-Schlüssel (ein Einmalschlüssel ist verbraucht); die Zahl der verbleibenden Signaturen ist ein Ausgang der signierenden Komponente."

TEMPLATES += [
    hash_signature("Cryptography/Modern/Asymmetric/Signature_LMS", 0, "LMS Signature (Merkle Tree, Winternitz)", "LMS-Signatur (Merkle-Baum, Winternitz)",
                   "LMS (RFC 8554) makes many signatures out of Winternitz one-time signatures with a Merkle tree; its security rests on a hash function only.",
                   "LMS (RFC 8554) macht mit einem Merkle-Baum aus Winternitz-Einmalsignaturen viele Signaturen; seine Sicherheit beruht nur auf einer Hashfunktion.",
                   "The tree has 32 leaves (height 5), the one-time signatures use w = 4 bits per hash chain. The public key is the root of the tree." + _HS_EN,
                   "Der Baum hat 32 Blätter (Höhe 5), die Einmalsignaturen verwenden w = 4 Bit je Hashkette. Der öffentliche Schlüssel ist die Wurzel des Baums." + _HS_DE,
                   "LMS, Merkle, Winternitz, hash-based signature, post-quantum"),
    hash_signature("Cryptography/Modern/Asymmetric/Signature_SPHINCSplus", 1, "SPHINCS+ Signature", "SPHINCS+-Signatur",
                   "SPHINCS+ is a stateless hash-based signature scheme that resists quantum computers.",
                   "SPHINCS+ ist ein zustandsloses hashbasiertes Signaturverfahren, das Quantencomputern widersteht.",
                   "The parameter set is sha2-128f-simple (signature about 17 KB)." + _HS_EN,
                   "Der Parametersatz ist sha2-128f-simple (Signatur etwa 17 KB)." + _HS_DE,
                   "SPHINCS+, hash-based signature, post-quantum"),
]

TEMPLATES += [
    public_key_encryption("Cryptography/Modern/Asymmetric/ECIES", 3, "ECIES (Elliptic Curve Integrated Encryption)", "ECIES (Elliptic Curve Integrated Encryption)",
                          "ECIES is a hybrid encryption with elliptic curves: Diffie-Hellman with a fresh key gives the keys for AES and a MAC.",
                          "ECIES ist eine hybride Verschlüsselung mit elliptischen Kurven: Diffie-Hellman mit einem frischen Schlüssel liefert die Schlüssel für AES und einen MAC.",
                          "The key pair is on the curve secp256r1. The ciphertext starts with the sender's ephemeral public point (65 bytes), followed by the AES-CTR encrypted text and a 32-byte HMAC-SHA-256 tag; change a byte of the middle text and the decryption is refused." + _PKE_EN,
                          "Das Schlüsselpaar liegt auf der Kurve secp256r1. Der Geheimtext beginnt mit dem flüchtigen öffentlichen Punkt des Senders (65 Bytes), danach folgen der mit AES-CTR verschlüsselte Text und ein 32-Byte-HMAC-SHA-256-Tag; ändern Sie ein Byte des mittleren Textes, und die Entschlüsselung wird abgelehnt." + _PKE_DE,
                          "ECIES, elliptic curve, hybrid encryption, ECDH"),
]

def mceliece(out, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """Classic McEliece: key generation, encapsulation of a secret with the public key, decapsulation with the private key; both secrets are shown."""
    mce, output = "ClassicMcEliece.ClassicMcEliece", "TextOutput.TextOutput"
    settings = lambda action: dict(Action=str(action), Parameters="0")
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=mce, x=40, y=120, w=180, h=100, name="Generate keys", settings=settings(0))),
             dict(add=dict(type=mce, x=360, y=40, w=180, h=100, name="Encapsulate", settings=settings(1))),
             dict(add=dict(type=mce, x=680, y=120, w=180, h=100, name="Decapsulate", settings=settings(2))),
             dict(add=dict(type=output, x=360, y=240, w=300, h=100)),
             dict(add=dict(type=output, x=680, y=300, w=300, h=100)),
             dict(add=dict(type=output, x=360, y=380, w=300, h=100)),
             dict(connect={"from": mce + "#0.OutputPublicKey", "to": mce + "#1.InputPublicKey"}),
             dict(connect={"from": mce + "#0.OutputPrivateKey", "to": mce + "#2.InputPrivateKey"}),
             dict(connect={"from": mce + "#1.OutputEncapsulation", "to": mce + "#2.InputEncapsulation"}),
             dict(connect={"from": mce + "#1.OutputSecret", "to": output + "#0.Input"}),
             dict(connect={"from": mce + "#1.OutputEncapsulation", "to": output + "#2.Input"}),
             dict(connect={"from": mce + "#2.OutputSecret", "to": output + "#1.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="ClassicMcEliece",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    mceliece("Cryptography/Modern/Asymmetric/ClassicMcEliece", "Classic McEliece (McEliece / Niederreiter)", "Classic McEliece (McEliece / Niederreiter)",
             "Classic McEliece is a code-based key encapsulation: the secret is hidden in an error vector that only the owner of the Goppa code can find.",
             "Classic McEliece ist eine codebasierte Schlüsselkapselung: Das Geheimnis steckt in einem Fehlervektor, den nur der Besitzer des Goppa-Codes finden kann.",
             "The first component generates the key pair (the public key has 261 KB and is not shown). The second encapsulates a fresh shared secret with the public key (the upper output); the encapsulation is the 96-byte syndrome in the lower output. The third decapsulates it with the private key (the right output). The two secrets are equal. This is Niederreiter's form of McEliece's cryptosystem.",
             "Die erste Komponente erzeugt das Schlüsselpaar (der öffentliche Schlüssel hat 261 KB und wird nicht angezeigt). Die zweite kapselt mit dem öffentlichen Schlüssel ein frisches gemeinsames Geheimnis (obere Ausgabe); die Kapsel ist das 96 Byte lange Syndrom in der unteren Ausgabe. Die dritte entkapselt sie mit dem privaten Schlüssel (rechte Ausgabe). Die beiden Geheimnisse sind gleich. Das ist die Form von Niederreiter des Kryptosystems von McEliece.",
             "McEliece, Niederreiter, Goppa code, code-based, post-quantum"),
]

def with_key(spec, key_hex):
    """The same template with another key in the hexadecimal key input."""
    spec["edits"].append(dict(match="TextInput", where=dict(Text="0123456789abcdef"), set=dict(Text=key_hex)))
    return spec


def cbc_mac(spec, name):
    """The MAC template as CBC-MAC (for ciphers whose block size does not allow CMAC)."""
    spec["edits"][0]["retype"]["settings"]["Mode"] = "1"
    for language in ("en", "de"):
        for key, text in spec[language].items():
            spec[language][key] = text.replace("(CMAC)", "(CBC-MAC)").replace("CMAC construction (NIST SP 800-38B)", "classic CBC-MAC construction (FIPS 113)").replace("mit der CMAC-Konstruktion (NIST SP 800-38B)", "mit der klassischen CBC-MAC-Konstruktion (FIPS 113)")
    spec["keywords"] = spec["keywords"].replace("CMAC", "CBC-MAC")
    return spec


TEMPLATES += [
    block_cipher("Shacal-2", 6, "Encryption with the <i>Shacal-2</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>Shacal-2</i>",
                 "Shacal-2 (Handschuh and Naccache, 2000) is the compression function of SHA-256 used as a block cipher: 256-bit blocks, keys of 128 to 512 bits. The component also offers Shacal-1 (SHA-1, 160-bit blocks).",
                 "Shacal-2 (Handschuh und Naccache, 2000) ist die Kompressionsfunktion von SHA-256, als Blockchiffre verwendet: 256-Bit-Blöcke, Schlüssel von 128 bis 512 Bit. Die Komponente bietet auch Shacal-1 (SHA-1, 160-Bit-Blöcke).",
                 "shacal, SHA-256, block cipher, Handschuh, Naccache"),
    with_key(block_cipher("DESX", 4, "Encryption with the <i>DESX</i> block cipher", "Verschlüsselung mit der Blockchiffre <i>DESX</i>",
                          "DESX (Rivest) is DES with key whitening: two more 64-bit keys are added by XOR before and after DES. The key has 24 bytes (DES key, K1, K2), here 48 hexadecimal digits.",
                          "DESX (Rivest) ist DES mit Schlüssel-Whitening: Zwei weitere 64-Bit-Schlüssel werden vor und nach DES per XOR hinzugefügt. Der Schlüssel hat 24 Bytes (DES-Schlüssel, K1, K2), hier 48 Hexadezimalziffern.",
                          "DESX, DES, whitening, block cipher"), "0123456789abcdeffedcba98765432100f1e2d3c4b5a6978"),
    cbc_mac(cipher_mac("Shacal-2", 10, "0123456789abcdef", 256), "Shacal-2"),
]

TEMPLATES += [
    dict(out="Hashfunctions/FileHash", base="Hashfunctions/SHA256",
         edits=[dict(remove="TextInput"),
                dict(add=dict(type="FileInput.FileInputClass", x=40, y=60, w=260, h=120,
                              settings=dict(OpenFilename="Templates\\Cryptography\\Modern\\Symmetric\\Alice im Wunderland.txt"))),
                dict(connect={"from": "FileInput.FileInputClass.StreamOutput", "to": "SHA.SHA.InputData"})],
         plugin="SHA",
         en=dict(title="Hash Value of a File", summary="The <i>SHA-256</i> hash value of a file",
                 description="The hash value of a file proves that the file is unchanged: a single changed bit gives a completely different value.",
                 memo="The {\\\\b File Input} component reads a file (here a sample text of the virtual file system; choose your own in the component) and the {\\\\b SHA} component computes its SHA-256 hash value. Compare the value with one published by the source of the file to check the download."),
         de=dict(title="Hashwert einer Datei", summary="Der <i>SHA-256</i>-Hashwert einer Datei",
                 description="Der Hashwert einer Datei belegt, dass die Datei unverändert ist: Ein einziges geändertes Bit ergibt einen völlig anderen Wert.",
                 memo="Die Komponente {\\\\b Dateieingabe} liest eine Datei (hier ein Beispieltext des virtuellen Dateisystems; wählen Sie in der Komponente eine eigene) und die Komponente {\\\\b SHA} berechnet ihren SHA-256-Hashwert. Vergleichen Sie den Wert mit dem, den die Quelle der Datei veröffentlicht, um den Download zu prüfen."),
         keywords="hash, file, SHA-256, checksum"),
]

TEMPLATES += [
    dict(out="Hashfunctions/HMAC_Tiger", base="Hashfunctions/HMAC",
         edits=[dict(match="HMAC.HMAC", retype=dict(type="HmacHashes.HmacHashes", assembly="HmacHashes", name="HMAC",
                                                   settings=dict(Hash="13"), typeLike=dict(Hash="SelectedHashFunction")))],
         plugin="HmacHashes",
         en=dict(title="HMAC with Tiger (Tiger MAC)", summary="Message authentication code with <i>HMAC-Tiger</i>",
                 description="A message authentication code (MAC) protects the integrity and the origin of a message. This template computes it as HMAC with the hash function Tiger (192-bit hash value).",
                 memo="The {\\\\b HMAC (many hash functions)} component computes a MAC from a message and a secret key. Here the hash function is Tiger; the component offers MD2, MD4, MD5, SHA-1, SHA-2, SHA-3, RIPEMD, Whirlpool and SM3 as well."),
         de=dict(title="HMAC mit Tiger (Tiger-MAC)", summary="Nachrichten-Authentifizierungscode mit <i>HMAC-Tiger</i>",
                 description="Ein Message Authentication Code (MAC) schützt Integrität und Herkunft einer Nachricht. Diese Vorlage berechnet ihn als HMAC mit der Hashfunktion Tiger (192-Bit-Hashwert).",
                 memo="Die Komponente {\\\\b HMAC (viele Hashfunktionen)} berechnet aus einer Nachricht und einem geheimen Schlüssel einen MAC. Hier ist die Hashfunktion Tiger; die Komponente bietet auch MD2, MD4, MD5, SHA-1, SHA-2, SHA-3, RIPEMD, Whirlpool und SM3 an."),
         keywords="HMAC, Tiger, MAC"),
]

CVZ = "CipherVisualization.CipherVisualization"


def visualization(out, cipher, text, key, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A template whose component is shown with its presentation (view state 1): text and key on the left, the picture of the calculation in the middle."""
    text_input, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=260, h=150, settings=dict(Text=text))),
             dict(add=dict(type=text_input, x=40, y=240, w=260, h=100, settings=dict(Text=key))),
             dict(add=dict(type=CVZ, x=360, y=40, w=700, h=560, viewState=1, settings=dict(Cipher=str(cipher), Action="0"))),
             dict(add=dict(type=output, x=360, y=640, w=700, h=100)),
             dict(connect={"from": f"{text_input}#0.TextOutput", "to": f"{CVZ}.InputText"}),
             dict(connect={"from": f"{text_input}#1.TextOutput", "to": f"{CVZ}.Key"}),
             dict(connect={"from": f"{CVZ}.OutputText", "to": f"{output}.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="CipherVisualization",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    visualization("Cryptography/Classic/CaesarVisualization", 0, "HELLO WORLD", "3", "Caesar Cipher Visualization", "Caesar-Chiffre: Visualisierung",
                  "The alphabet shifted by the key, with the letters of the text marked.",
                  "Das um den Schlüssel verschobene Alphabet, mit den Buchstaben des Textes markiert.",
                  "The {\\\\b Cipher Visualization} component encrypts the text with the Caesar cipher and shows the plain alphabet above the shifted one. Change the text and the key (a number or a letter) on the left, and switch the component to decryption in its settings.",
                  "Die Komponente {\\\\b Chiffren-Visualisierung} verschlüsselt den Text mit der Caesar-Chiffre und zeigt das Klaralphabet über dem verschobenen. Ändern Sie links Text und Schlüssel (eine Zahl oder einen Buchstaben) und stellen Sie die Komponente in ihren Einstellungen auf Entschlüsseln.",
                  "Caesar, visualization, shift, alphabet"),
    visualization("Cryptography/Classic/VigenereVisualization", 1, "ATTACK AT DAWN", "LEMON", "Vigenère Cipher Visualization", "Vigenère-Chiffre: Visualisierung",
                  "The Vigenère square with the cells that the text uses.",
                  "Das Vigenère-Quadrat mit den Zellen, die der Text benutzt.",
                  "The {\\\\b Cipher Visualization} component encrypts the text with the Vigenère cipher. In the square the row is the key letter and the column the plaintext letter; the cell where they meet is the ciphertext letter. The cells used are green, below them the first steps are listed.",
                  "Die Komponente {\\\\b Chiffren-Visualisierung} verschlüsselt den Text mit der Vigenère-Chiffre. Im Quadrat ist die Zeile der Schlüsselbuchstabe und die Spalte der Klartextbuchstabe; die Zelle, in der sie sich treffen, ist der Geheimtextbuchstabe. Die benutzten Zellen sind grün, darunter sind die ersten Schritte aufgelistet.",
                  "Vigenere, visualization, tableau"),
    visualization("Cryptography/Classic/NihilistVisualization", 2, "DYNAMITE WINTER PALACE", "ZEBRAS RUSSIAN", "Nihilist Cipher Visualization", "Nihilist-Chiffre: Visualisierung",
                  "The Polybius square and the additions of the Nihilist cipher.",
                  "Das Polybius-Quadrat und die Additionen der Nihilist-Chiffre.",
                  "The key has two words: ZEBRAS makes the Polybius square, RUSSIAN is the additive key. Each letter of the text becomes the number of its row and column, the number of the key letter is added: D = 23, R = 14, 23 + 14 = 37. The example is the one of the Wikipedia article.",
                  "Der Schlüssel hat zwei Wörter: ZEBRAS bildet das Polybius-Quadrat, RUSSIAN ist der additive Schlüssel. Jeder Buchstabe des Textes wird zur Zahl aus Zeile und Spalte, die Zahl des Schlüsselbuchstabens wird addiert: D = 23, R = 14, 23 + 14 = 37. Das Beispiel ist das aus dem Wikipedia-Artikel.",
                  "Nihilist, Polybius, visualization"),
]

MVZ = "MathVisualization.MathVisualization"


def math_visualization(out, mode, parameters, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A template whose component is shown with its presentation: the parameters on the left, the picture in the middle."""
    text_input, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=260, h=120, settings=dict(Text=parameters))),
             dict(add=dict(type=MVZ, x=360, y=40, w=680, h=620, viewState=1, settings=dict(Visualization=str(mode)))),
             dict(add=dict(type=output, x=360, y=700, w=680, h=90)),
             dict(connect={"from": f"{text_input}#0.TextOutput", "to": f"{MVZ}.Parameters"}),
             dict(connect={"from": f"{MVZ}.Result", "to": f"{output}.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="MathVisualization",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    math_visualization("Cryptography/Modern/Asymmetric/RSAVisualization", 0, "61 53 17 65", "RSA Visualization", "RSA-Visualisierung",
                       "All numbers of an RSA example with small primes, step by step.",
                       "Alle Zahlen eines RSA-Beispiels mit kleinen Primzahlen, Schritt für Schritt.",
                       "The parameters are p, q, e and the message m. The picture lists n, phi(n), d, the keys, the ciphertext c = m^e mod n and the decryption. The example is the one of the Wikipedia article (p = 61, q = 53, e = 17, m = 65 gives c = 2790). Try other primes; the message must be smaller than n.",
                       "Die Parameter sind p, q, e und die Nachricht m. Das Bild listet n, phi(n), d, die Schlüssel, den Geheimtext c = m^e mod n und die Entschlüsselung auf. Das Beispiel ist das aus dem Wikipedia-Artikel (p = 61, q = 53, e = 17, m = 65 ergibt c = 2790). Probieren Sie andere Primzahlen; die Nachricht muss kleiner als n sein.",
                       "RSA, visualization, key generation"),
    math_visualization("Protocols/ShamirVisualization", 1, "23 5 11 3 7", "Shamir's Secret Sharing Visualization", "Secret-Sharing nach Shamir: Visualisierung",
                       "The polynomial of Shamir's scheme, its shares and the secret as points over a small prime field.",
                       "Das Polynom des Verfahrens von Shamir, seine Anteile und das Geheimnis als Punkte über einem kleinen Primkörper.",
                       "The parameters are the prime p = 23, the number of shares 5 and the coefficients 11, 3, 7 (the secret is 11). The picture shows all points of f(x) = 11 + 3x + 7x^2 modulo 23, the five shares in orange and the secret f(0) in green; the secret is rebuilt from three shares.",
                       "Die Parameter sind die Primzahl p = 23, die Anzahl der Anteile 5 und die Koeffizienten 11, 3, 7 (das Geheimnis ist 11). Das Bild zeigt alle Punkte von f(x) = 11 + 3x + 7x^2 modulo 23, die fünf Anteile orange und das Geheimnis f(0) grün; aus drei Anteilen wird das Geheimnis wiederhergestellt.",
                       "Shamir, secret sharing, polynomial, visualization"),
    math_visualization("Cryptography/Modern/Asymmetric/EllipticCurveVisualization", 2, "17 2 2 5 1 6 3", "Elliptic Curve Visualization", "Elliptische Kurve: Visualisierung",
                       "The points of an elliptic curve over a small prime field and the addition of two points.",
                       "Die Punkte einer elliptischen Kurve über einem kleinen Primkörper und die Addition zweier Punkte.",
                       "The parameters are p = 17, a = 2, b = 2 (the curve y^2 = x^3 + 2x + 2) and the points P = (5, 1) and Q = (6, 3). The picture shows the 18 points of the curve and P, Q and P + Q = (10, 6) with the formulas of the addition. Change the points to add others; without points only the curve is drawn.",
                       "Die Parameter sind p = 17, a = 2, b = 2 (die Kurve y^2 = x^3 + 2x + 2) und die Punkte P = (5, 1) und Q = (6, 3). Das Bild zeigt die 18 Punkte der Kurve und P, Q und P + Q = (10, 6) mit den Formeln der Addition. Ändern Sie die Punkte, um andere zu addieren; ohne Punkte wird nur die Kurve gezeichnet.",
                       "elliptic curve, point addition, visualization"),
]

PVZ = "ProtocolVisualization.ProtocolVisualization"


def protocol_visualization(out, mode, parameters, message, received, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A template whose component is shown with its presentation: parameters (and the messages of the signature) on the left."""
    text_input, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=260, h=100, settings=dict(Text=parameters))),
             dict(add=dict(type=PVZ, x=360, y=40, w=860, h=640, viewState=1, settings=dict(Protocol=str(mode)))),
             dict(add=dict(type=output, x=360, y=720, w=860, h=90)),
             dict(connect={"from": f"{text_input}#0.TextOutput", "to": f"{PVZ}.Parameters"}),
             dict(connect={"from": f"{PVZ}.Result", "to": f"{output}.Input"})]
    if message is not None:
        edits += [dict(add=dict(type=text_input, x=40, y=190, w=260, h=100, settings=dict(Text=message))),
                  dict(connect={"from": f"{text_input}#1.TextOutput", "to": f"{PVZ}.Message"})]
        edits += [dict(add=dict(type=text_input, x=40, y=340, w=260, h=100, settings=dict(Text=received))),
                  dict(connect={"from": f"{text_input}#2.TextOutput", "to": f"{PVZ}.Received"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="ProtocolVisualization",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    protocol_visualization("Protocols/DiffieHellmanForwardSecrecy", 0, "23 5 6 15 4 9", None, None,
                           "Diffie-Hellman Key Exchange with Perfect Forward Secrecy: Visualization", "Diffie-Hellman-Schlüsselaustausch mit Perfect Forward Secrecy: Visualisierung",
                           "The key exchange between Alice and Bob with every number, in two sessions with new exponents.",
                           "Der Schlüsselaustausch zwischen Alice und Bob mit allen Zahlen, in zwei Sitzungen mit neuen Exponenten.",
                           "The parameters are p = 23, g = 5 and the secret exponents of Alice and Bob for each session (6 and 15, then 4 and 9). Each session gives another key (2 and 13). Eve finds the exponent by trial because the prime is tiny. Add more exponents or change the prime to try others.",
                           "Die Parameter sind p = 23, g = 5 und die geheimen Exponenten von Alice und Bob je Sitzung (6 und 15, dann 4 und 9). Jede Sitzung ergibt einen anderen Schlüssel (2 und 13). Eve findet den Exponenten durch Probieren, weil die Primzahl winzig ist. Fügen Sie weitere Exponenten hinzu oder ändern Sie die Primzahl, um andere auszuprobieren.",
                           "Diffie-Hellman, forward secrecy, key exchange, visualization"),
    protocol_visualization("Cryptography/Modern/Asymmetric/DigitalSignatureVisualization", 1, "1009 1013 17", "Pay Bob 10 euros", "Pay Bob 10 euros",
                           "Digital Signature: Visualization", "Digitale Signatur: Visualisierung",
                           "Alice signs the hash value of a message, Bob checks the signature of the message he received.",
                           "Alice signiert den Hashwert einer Nachricht, Bob prüft die Signatur der Nachricht, die er empfangen hat.",
                           "The parameters are two primes and the public exponent of an RSA key. The upper text is signed, the lower text is what Bob receives: make them differ (\"Pay Bob 1000 euros\") and the signature is no longer valid. The key is small so that the numbers can be followed; real keys have 2048 bits or more.",
                           "Die Parameter sind zwei Primzahlen und der öffentliche Exponent eines RSA-Schlüssels. Der obere Text wird signiert, der untere ist das, was Bob empfängt: Machen Sie sie verschieden (\"Pay Bob 1000 euros\"), und die Signatur ist nicht mehr gültig. Der Schlüssel ist klein, damit sich die Zahlen verfolgen lassen; echte Schlüssel haben 2048 Bit oder mehr.",
                           "digital signature, RSA, SHA-256, visualization"),
]

RVZ = "RandomVisualization.RandomVisualization"


def random_visualization(out, settings, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """The random number generator template with the Random Visualization component, shown with its presentation, instead of the text output."""
    rng = "RandomNumberGenerator.RandomNumberGenerator"
    output = "TextOutput.TextOutput"
    edits = [dict(match=rng, set=dict(OutputType="0", OutputLength="20000", **settings)),
             dict(remove="TextOutput"),
             dict(add=dict(type=RVZ, x=400, y=40, w=520, h=700, viewState=1)),
             dict(add=dict(type=output, x=400, y=780, w=520, h=90)),
             dict(connect={"from": rng + ".Output", "to": RVZ + ".InputData"}),
             dict(connect={"from": RVZ + ".Summary", "to": output + ".Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="RandomVisualization",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    random_visualization("Tools/RandomVisualization", dict(AlgorithmType="1"), "Random Numbers: Visualization", "Zufall: Visualisierung",
                         "20000 bytes of a cryptographic random number generator as bits, histogram and plot of consecutive bytes.",
                         "20000 Bytes eines kryptografischen Zufallszahlengenerators als Bits, Histogramm und Diagramm aufeinanderfolgender Bytes.",
                         "The generator is the cryptographic one of the system. The bits look like noise, the histogram is flat and the points fill the square evenly. Change the algorithm in the settings of the {\\\\b Random Number Generator} component to see how weaker generators look.",
                         "Der Generator ist der kryptografische des Systems. Die Bits sehen aus wie Rauschen, das Histogramm ist flach und die Punkte füllen das Quadrat gleichmäßig. Ändern Sie den Algorithmus in den Einstellungen der Komponente {\\\\b Zufallszahlengenerator}, um zu sehen, wie schwächere Generatoren aussehen.",
                         "random numbers, visualization, histogram, pairs"),
]

TEMPLATES += [
    protocol_visualization("Protocols/AuthenticationVisualization", 2, "secret 8271 9034", None, None,
                           "Authentication in a Network: Visualization", "Authentifizierung im Netz: Visualisierung",
                           "Three ways to prove that one knows a password, and what an eavesdropper can do with each.",
                           "Drei Wege zu beweisen, dass man ein Passwort kennt, und was ein Lauscher mit jedem anfangen kann.",
                           "The parameters are the password and two challenges of the server (three words). Sending the password lets Eve read it; sending its hash value lets her replay it; with challenge and response the old response is useless for a new challenge. Give the same challenge twice and the replay succeeds.",
                           "Die Parameter sind das Passwort und zwei Aufforderungen des Servers (drei Wörter). Wird das Passwort gesendet, liest Eve es mit; wird sein Hashwert gesendet, kann sie ihn wiederholen; bei Aufforderung und Antwort ist die alte Antwort für eine neue Aufforderung nutzlos. Geben Sie dieselbe Aufforderung zweimal an, und der Replay gelingt.",
                           "authentication, challenge response, replay, password, visualization"),
    protocol_visualization("Protocols/SmimeVisualization", 3, "1009 1013 17 1019 1021 11", "Meet me at noon", None,
                           "S/MIME: Visualization", "S/MIME: Visualisierung",
                           "A signed and encrypted e-mail step by step: signature, session key, symmetric encryption and the session key encrypted for the recipient.",
                           "Eine signierte und verschlüsselte E-Mail Schritt für Schritt: Signatur, Sitzungsschlüssel, symmetrische Verschlüsselung und der für den Empfänger verschlüsselte Sitzungsschlüssel.",
                           "The parameters are the RSA keys of Alice (signature) and Bob (encryption) as two primes and an exponent each. The keys are tiny so that the numbers can be followed; real keys have 2048 bits or more.",
                           "Die Parameter sind die RSA-Schlüssel von Alice (Signatur) und Bob (Verschlüsselung) als je zwei Primzahlen und ein Exponent. Die Schlüssel sind winzig, damit sich die Zahlen verfolgen lassen; echte Schlüssel haben 2048 Bit oder mehr.",
                           "S/MIME, e-mail, hybrid encryption, signature, visualization"),
    protocol_visualization("Protocols/TlsHandshakeVisualization", 4, "23 5 6 15", None, None,
                           "TLS 1.3 Handshake: Visualization", "TLS-1.3-Handshake: Visualisierung",
                           "The handshake between client and server with key shares, Finished messages and the keys of the key schedule of RFC 8446.",
                           "Der Handshake zwischen Client und Server mit Schlüsselanteilen, Finished-Nachrichten und den Schlüsseln des Schlüsselplans aus RFC 8446.",
                           "The parameters are p = 23, g = 5 and the secret exponents of client and server (6 and 15). A small prime lets Eve find the exponent by trial; with a large prime (try 1000000007) she does not try. The keys are computed with HKDF and SHA-256 as in TLS 1.3.",
                           "Die Parameter sind p = 23, g = 5 und die geheimen Exponenten von Client und Server (6 und 15). Bei einer kleinen Primzahl findet Eve den Exponenten durch Probieren; bei einer großen (probieren Sie 1000000007) versucht sie es nicht. Die Schlüssel werden wie bei TLS 1.3 mit HKDF und SHA-256 berechnet.",
                           "TLS, SSL, handshake, key exchange, HKDF, visualization"),
    protocol_visualization("Protocols/ZeroKnowledgeVisualization", 5, "11 13 5 7 1 9 0 2 1", None, None,
                           "Zero-Knowledge Proof (Fiat-Shamir): Visualization", "Zero-Knowledge-Beweis (Fiat-Shamir): Visualisierung",
                           "Peggy proves that she knows a secret in three rounds; a cheater who does not know it is caught.",
                           "Peggy beweist in drei Runden, dass sie ein Geheimnis kennt; ein Betrüger, der es nicht kennt, fliegt auf.",
                           "The parameters are the primes 11 and 13, the secret 5 and for every round the random number of Peggy and the challenge of Victor (7 and 1, 9 and 0, 2 and 1). Change the challenges to 0 to see that a cheater then gets through.",
                           "Die Parameter sind die Primzahlen 11 und 13, das Geheimnis 5 und je Runde die Zufallszahl von Peggy und die Aufforderung von Victor (7 und 1, 9 und 0, 2 und 1). Setzen Sie die Aufforderungen auf 0, um zu sehen, dass ein Betrüger dann durchkommt.",
                           "zero-knowledge, Fiat-Shamir, identification, proof, visualization"),
]


def attr(text):
    return text.replace("&", "&amp;").replace('"', "&quot;")


def rewrite_xml(base_xml, spec, out_name):
    """The base template's .xml with title, summary, description, keywords and memo replaced (per language)."""
    xml = re.sub(r"<!--File name: .*?-->", f"<!--File name: {out_name}.xml-->", base_xml)
    for lang in ("en", "de"):
        t = spec[lang]
        for tag, key in (("title", "title"), ("summary", "summary"), ("description", "description")):
            xml = re.sub(rf'(<{tag} lang="{lang}">).*?(</{tag}>)', lambda m: m.group(1) + t[key] + m.group(2), xml, flags=re.S)
        xml = re.sub(rf'(<replacements lang="{lang}">.*?<replacement key="\$memo\$" value=")(.*?)("\s*/>)',
                     lambda m: m.group(1) + attr(t["memo"]) + m.group(3), xml, count=1, flags=re.S)
    xml = re.sub(r'<keywords( lang="\w+")?>.*?</keywords>', f'<keywords lang="en">{spec["keywords"]}</keywords>', xml, flags=re.S)
    if "plugin" in spec:
        xml = re.sub(r'<plugin name="[^"]*" />', f'<plugin name="{spec["plugin"]}" />', xml)
    xml = re.sub(r'<icon file="[^"]*" />', f'<icon file="{out_name}.png" />', xml)
    return xml


def game(out, plugin_type, settings, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A learning game: one component, shown with its presentation (the game is played in it)."""
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=plugin_type, x=40, y=40, w=760, h=700, viewState=1, settings=settings))]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin=plugin_type.split(".")[0],
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    game("Games/NumberShark", "NumberShark.NumberShark", dict(Size="24"), "Number Shark", "Zahlenhai",
         "A game with the divisors of the numbers 1 to 24 against the shark.", "Ein Spiel mit den Teilern der Zahlen von 1 bis 24 gegen den Hai.",
         "Take a number; the shark takes its divisors. Change the size in the settings of the {\\\\b Number Shark} component.",
         "Nehmen Sie eine Zahl; der Hai nimmt ihre Teiler. Ändern Sie die Größe in den Einstellungen der Komponente {\\\\b Zahlenhai}.",
         "number shark, taxman, divisors, game, primes"),
    game("Games/DivisorGame", "DivisorGame.DivisorGame", dict(Number="60"), "Divisor Game", "Teilerspiel",
         "Take divisors of 60 against the computer; whoever has to take 1 loses.", "Nehmen Sie Teiler von 60 gegen den Computer; wer die 1 nehmen muss, verliert.",
         "The computer searches all positions. The first player can always win: try to find the winning first move for other numbers in the settings of the {\\\\b Divisor Game} component.",
         "Der Computer durchsucht alle Stellungen. Der erste Spieler kann immer gewinnen: Versuchen Sie, in den Einstellungen der Komponente {\\\\b Teilerspiel} für andere Zahlen den gewinnenden ersten Zug zu finden.",
         "divisor game, chomp, divisors, game"),
    game("Games/Sudoku", "Sudoku.Sudoku", dict(Puzzle="1", Difficulty="1"), "Zudo-Ku (Sudoku)", "Zudo-Ku (Sudoku)",
         "A Sudoku puzzle with a unique solution.", "Ein Sudoku-Rätsel mit eindeutiger Lösung.",
         "Click a cell, then a digit. The same puzzle number always gives the same puzzle; change number and difficulty in the settings of the {\\\\b Sudoku} component.",
         "Klicken Sie eine Zelle, dann eine Ziffer. Dieselbe Rätselnummer ergibt immer dasselbe Rätsel; ändern Sie Nummer und Schwierigkeit in den Einstellungen der Komponente {\\\\b Sudoku}.",
         "sudoku, zudo-ku, latin square, game"),
    game("Games/AntColony", "AntColony.AntColony", dict(Cities="12", Ants="20", Seed="1"), "Ant Colony Optimization", "Ameisenalgorithmus",
         "Ants search the shortest round trip through 12 cities.", "Ameisen suchen die kürzeste Rundreise durch 12 Städte.",
         "Press the buttons to let the ants walk. Change the number of cities, ants, the weights and the evaporation in the settings of the {\\\\b Ant Colony} component.",
         "Drücken Sie die Schaltflächen, um die Ameisen laufen zu lassen. Ändern Sie Städte, Ameisen, Gewichte und Verdunstung in den Einstellungen der Komponente {\\\\b Ameisenalgorithmus}.",
         "ant colony, ACO, travelling salesman, pheromone, heuristic"),
    game("Games/PatternLock", "PatternLock.PatternLock", dict(ShowNumbers="True"), "Android Unlock Pattern", "Android-Entsperrmuster",
         "Draw a pattern, see how few patterns exist and find it from its hash value.", "Zeichnen Sie ein Muster, sehen Sie, wie wenige Muster es gibt, und finden Sie es aus seinem Hashwert.",
         "Click the points in the order of the pattern. The stored value is an unsalted SHA-1 hash, so trying all 389112 patterns with the {\\\\b Find it from the hash} button finds it at once.",
         "Klicken Sie die Punkte in der Reihenfolge des Musters an. Der gespeicherte Wert ist ein SHA-1-Hash ohne Salt; mit der Schaltfläche {\\\\b Aus dem Hash finden} werden alle 389112 Muster probiert und das Muster sofort gefunden.",
         "android, unlock pattern, SHA-1, brute force, hash"),
    game("Games/Kleptography", "Kleptography.Kleptography", dict(Method="1", Seed="1"), "Kleptography: SETUP Attack on RSA (upper bits)", "Kleptographie: SETUP-Angriff auf RSA (obere Bits)",
         "A changed RSA key generator hides the prime p in the key; the attacker reads it.", "Ein veränderter RSA-Schlüsselgenerator versteckt die Primzahl p im Schlüssel; der Angreifer liest sie aus.",
         "The presentation shows the three steps with all numbers. Change the run in the settings of the {\\\\b Kleptography} component.",
         "Die Präsentation zeigt die drei Schritte mit allen Zahlen. Ändern Sie den Ablauf in den Einstellungen der Komponente {\\\\b Kleptographie}.",
         "kleptography, SETUP, backdoor, RSA, key generation"),
    game("Games/KleptographyLowerBits", "Kleptography.Kleptography", dict(Method="2", Seed="1"), "Kleptography: SETUP Attack on RSA (lower bits)", "Kleptographie: SETUP-Angriff auf RSA (untere Bits)",
         "The hidden value is in the lower bits of the modulus.", "Der versteckte Wert steckt in den unteren Bits des Moduls.",
         "Compare with the other methods in the settings of the {\\\\b Kleptography} component.",
         "Vergleichen Sie mit den anderen Methoden in den Einstellungen der Komponente {\\\\b Kleptographie}.",
         "kleptography, SETUP, backdoor, RSA, key generation"),
    game("Games/KleptographySharedPad", "Kleptography.Kleptography", dict(Method="3", Seed="1"), "Kleptography: Backdoor with a Shared Secret", "Kleptographie: Hintertür mit gemeinsamem Geheimnis",
         "The hidden value is combined with a pad that attacker and generator share.", "Der versteckte Wert wird mit einem Pad verknüpft, das Angreifer und Generator teilen.",
         "Compare with the other methods in the settings of the {\\\\b Kleptography} component.",
         "Vergleichen Sie mit den anderen Methoden in den Einstellungen der Komponente {\\\\b Kleptographie}.",
         "kleptography, SETUP, backdoor, RSA, key generation"),
    game("Games/KleptographyHonest", "Kleptography.Kleptography", dict(Method="0", Seed="1"), "Kleptography: Honest Key Generation", "Kleptographie: Ehrliche Schlüsselerzeugung",
         "Nothing is hidden; the attack finds nothing.", "Nichts ist versteckt; der Angriff findet nichts.",
         "Compare with the other methods in the settings of the {\\\\b Kleptography} component.",
         "Vergleichen Sie mit den anderen Methoden in den Einstellungen der Komponente {\\\\b Kleptographie}.",
         "kleptography, SETUP, backdoor, RSA, key generation"),
]


def huffman_errors(out, protect, position, text, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords):
    """A text goes through the Huffman code, a bit error and (optionally) the Hamming code; shown with the presentation of the component."""
    text_input, output, hz = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput", "HuffmanErrors.HuffmanErrors"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=text_input, x=40, y=40, w=300, h=120, settings=dict(Text=text))),
             dict(add=dict(type=hz, x=400, y=40, w=820, h=560, viewState=1, settings=dict(Protect=str(protect), ErrorPosition=str(position)))),
             dict(add=dict(type=output, x=400, y=640, w=820, h=100)),
             dict(connect={"from": f"{text_input}.TextOutput", "to": f"{hz}.InputText"}),
             dict(connect={"from": f"{hz}.OutputText", "to": f"{output}.Input"})]
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin="HuffmanErrors",
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


TEMPLATES += [
    game("Hashfunctions/RainbowTable", "RainbowTable.RainbowTable", dict(Length="3", Chains="800", ChainLength="40", Seed="1", Target="abc"), "Rainbow Table", "Regenbogentabelle",
         "Chains of hash and reduction steps for passwords of three lowercase letters, and the lookup of a hash value.", "Ketten aus Hash- und Reduktionsschritten für Passwörter aus drei Kleinbuchstaben und die Suche nach einem Hashwert.",
         "Change the length, the number and length of the chains and the password to find in the settings of the {\\\\b Rainbow Table} component and see how the coverage and the success rate change.",
         "Ändern Sie Länge, Anzahl und Länge der Ketten und das gesuchte Passwort in den Einstellungen der Komponente {\\\\b Regenbogentabelle} und beobachten Sie, wie sich Abdeckung und Erfolgsquote ändern.",
         "rainbow table, hash, password, time-memory trade-off, reduction"),
    game("Cryptanalysis/SideChannelSPA", "SideChannel.SideChannel", dict(Mode="0", Seed="1", Noise="100"), "Side-Channel Attack: SPA", "Seitenkanalangriff: SPA",
         "The secret exponent of square-and-multiply, read from a single simulated power trace.", "Der geheime Exponent von Square-and-Multiply, gelesen aus einer einzigen simulierten Strommessung.",
         "Increase the noise in the settings of the {\\\\b Side-Channel Attacks} component until the trace can no longer be read.",
         "Erhöhen Sie in den Einstellungen der Komponente {\\\\b Seitenkanalangriffe} das Rauschen, bis sich die Messung nicht mehr ablesen lässt.",
         "side channel, SPA, power analysis, square and multiply, RSA"),
    game("Cryptanalysis/SideChannelDPA", "SideChannel.SideChannel", dict(Mode="1", Seed="1", Traces="200", Noise="100"), "Side-Channel Attack: DPA", "Seitenkanalangriff: DPA",
         "A key byte of AES, found by correlating predicted and measured power over many traces.", "Ein Schlüsselbyte von AES, gefunden durch Korrelation von vorhergesagtem und gemessenem Stromverbrauch über viele Messungen.",
         "Reduce the number of traces or increase the noise in the settings of the {\\\\b Side-Channel Attacks} component to see when the attack fails.",
         "Verringern Sie in den Einstellungen der Komponente {\\\\b Seitenkanalangriffe} die Zahl der Messungen oder erhöhen Sie das Rauschen, um zu sehen, wann der Angriff scheitert.",
         "side channel, DPA, differential power analysis, AES, correlation"),
    huffman_errors("Codes/HuffmanBitError", 0, 5, "hello world, this is a test", "Huffman Coding: Effect of a Bit Error", "Huffman-Codierung: Wirkung eines Bitfehlers",
                   "A text in a Huffman code with one flipped bit: the receiver loses its place.", "Ein Text im Huffman-Code mit einem gekippten Bit: Der Empfänger verliert die Stelle.",
                   "Change the position of the bit error in the settings of the {\\\\b Huffman Coding with Error Correction} component.",
                   "Ändern Sie die Position des Bitfehlers in den Einstellungen der Komponente {\\\\b Huffman-Codierung mit Fehlerkorrektur}.",
                   "Huffman, bit error, compression, error propagation"),
    huffman_errors("Codes/HuffmanErrorCorrection", 1, 5, "hello world, this is a test", "Huffman Coding with Hamming Code: Error Correction", "Huffman-Codierung mit Hamming-Code: Fehlerkorrektur",
                   "The same text and the same bit error, now protected by a Hamming(7,4) code: the error is corrected.", "Derselbe Text und derselbe Bitfehler, jetzt durch einen Hamming(7,4)-Code geschützt: Der Fehler wird korrigiert.",
                   "The Hamming code adds 3 check bits to every 4 bits. Change the position of the bit error: every single error is corrected.",
                   "Der Hamming-Code fügt je 4 Bits 3 Prüfbits hinzu. Ändern Sie die Position des Bitfehlers: Jeder einzelne Fehler wird korrigiert.",
                   "Huffman, Hamming code, error correction, compression"),
]


def tool(out, plugin_type, inputs, outputs, settings, title_en, title_de, description_en, description_de, memo_en, memo_de, keywords, input_height=100):
    """A tool: text inputs on the left, the component, text outputs below it. inputs: (port, text); outputs: port names."""
    text_input, output = "CrypTool.TextInput.TextInput", "TextOutput.TextOutput"
    edits = [dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
             dict(add=dict(type=plugin_type, x=420, y=40, w=300, h=160, settings=settings))]
    for k, (port, text) in enumerate(inputs):
        edits.append(dict(add=dict(type=text_input, x=40, y=40 + k * (input_height + 40), w=320, h=input_height, settings=dict(Text=text))))
        edits.append(dict(connect={"from": f"{text_input}#{k}.TextOutput", "to": f"{plugin_type}.{port}"}))
    for k, port in enumerate(outputs):
        edits.append(dict(add=dict(type=output, x=420 + (k % 2) * 330, y=260 + (k // 2) * 160, w=320, h=120)))
        edits.append(dict(connect={"from": f"{plugin_type}.{port}", "to": f"{output}#{k}.Input"}))
    return dict(
        out=out, base="Tools/RandomNumberGenerators", edits=edits, plugin=plugin_type.split(".")[0],
        en=dict(title=title_en, summary=f"<i>{title_en}</i>", description=description_en, memo=memo_en),
        de=dict(title=title_de, summary=f"<i>{title_de}</i>", description=description_de, memo=memo_de),
        keywords=keywords,
    )


# data of the key search templates: cipher index -> (name, key, plaintext, ciphertext), made by the command kdemo of CrypWeb.Tests
KEY_SEARCH = {
    0: ("IDEA", "3B76B1EC27629DD8134E89C4FF3A75B0", "5468652073656372", "CADD2E5A5B25B72E"),
    1: ("RC2", "3C77B2ED28639ED9144F8AC5003B76B1", "5468652073656372", "FE7332D7D6A2A73F"),
    2: ("RC4", "3D78B3EE29649FDA15508BC6013C77B2", "546865207365637265742069732073616665", "23AC39C228725B55C3020BAEE9EB925441C1"),
    3: ("RC6", "3E79B4EF2A65A0DB16518CC7023D78B3", "54686520736563726574206973207361", "A92DBCBD57CE17DCF347DC2D1ABE4BF9"),
    4: ("Serpent", "3F7AB5F02B66A1DC17528DC8033E79B4", "54686520736563726574206973207361", "F9778C980A2C5E8BD0F5B8A0F4840DD9"),
    5: ("Twofish", "407BB6F12C67A2DD18538EC9043F7AB5", "54686520736563726574206973207361", "9863B217DB47883A40E8013CE6E99228"),
    6: ("DESX", "417CB7F22D68A3DE19548FCA05407BB6F12C67A2DD18538E", "5468652073656372", "80BD30D92295E805"),
    9: ("DESL", "447FBAF5306BA6E1", "5468652073656372", "3946FCE2850243B8"),
    11: ("MARS", "4681BCF7326DA8E31E5994CF0A4580BB", "54686520736563726574206973207361", "3AED867ED35CB2A151072A9E1AAD97B8"),
    12: ("MISTY1", "4782BDF8336EA9E41F5A95D00B4681BC", "5468652073656372", "B8E64C67DAA1544B"),
    13: ("SAFERPlus", "4883BEF9346FAAE5205B96D10C4782BD", "54686520736563726574206973207361", "88A7EA1B72848E060226323B934F0776"),
    10: ("DESXL", "4580BBF6316CA7E21D5893CE09447FBAF5306BA6E11C5792", "5468652073656372", "9624E0F9F6CB111F"),
}
for _cipher, (_name, _key, _plain, _cipher_text) in KEY_SEARCH.items():
    TEMPLATES.append(tool(f"Cryptanalysis/Modern/KeySearch_{_name}", "KeySearch.KeySearch",
                          [("Plaintext", _plain), ("Ciphertext", _cipher_text), ("KnownKey", _key[:-4] + "0000")], ["FoundKey", "Tries", "Report"],
                          dict(Cipher=str(_cipher), UnknownBits="16"),
                          f"Key Search for {_name} (last 16 key bits unknown)", f"Schlüsselsuche für {_name} (letzte 16 Schlüsselbits unbekannt)",
                          f"Plaintext and ciphertext are known, the key is known except for its last 16 bits: all 65536 values are tried.",
                          f"Klartext und Geheimtext sind bekannt, der Schlüssel ist bis auf seine letzten 16 Bits bekannt: Alle 65536 Werte werden probiert.",
                          f"The key {_key} is given with the last 16 bits set to zero. Each extra unknown bit doubles the work: change the number in the settings of the {{\\\\b Key Search}} component (up to 24 bits).",
                          f"Der Schlüssel {_key} ist mit den letzten 16 Bits auf null angegeben. Jedes weitere unbekannte Bit verdoppelt die Arbeit: Ändern Sie die Zahl in den Einstellungen der Komponente {{\\\\b Schlüsselsuche}} (bis 24 Bit).",
                          f"key search, attack, {_name}, brute force, known plaintext"))


TEXT_SAMPLE_EN = "It was the best of times, it was the worst of times, it was the age of wisdom, it was the age of foolishness, it was the epoch of belief, it was the epoch of incredulity."
TEXT_SAMPLE_2 = "It was the best of times, it was the worst of times, it was the age of wisdom, it was the age of foolishness."
TEXT_SAMPLE_3 = "The quick brown fox jumps over the lazy dog while seven zebras quietly watch the river flow past the hills."

TEMPLATES += [
    tool("Cryptanalysis/Classic/NGramAnalysis", "TextAnalysis.TextAnalysis", [("InputA", TEXT_SAMPLE_EN)], ["Report"], dict(Mode="0", N="2", Top="12"),
         "N-gram Analysis", "N-Gramm-Analyse", "The most frequent bigrams of a text.", "Die häufigsten Bigramme eines Textes.",
         "Change the length n of the n-grams in the settings of the {\\\\b Text Analysis} component: 1 gives letters, 3 gives trigrams.",
         "Ändern Sie die Länge n der N-Gramme in den Einstellungen der Komponente {\\\\b Textanalyse}: 1 ergibt Buchstaben, 3 ergibt Trigramme.", "n-gram, bigram, trigram, frequency, analysis"),
    tool("Cryptanalysis/Classic/VitanyiDistance", "TextAnalysis.TextAnalysis", [("InputA", TEXT_SAMPLE_EN), ("InputB", TEXT_SAMPLE_2)], ["Result", "Report"], dict(Mode="1"),
         "Normalized Compression Distance (Vitányi)", "Normalisierter Kompressionsabstand (Vitányi)", "How similar two texts are, measured by compression.", "Wie ähnlich zwei Texte sind, gemessen durch Kompression.",
         f"The two texts share a long beginning, so their distance is small. Replace the second text by this one to see a large distance: {TEXT_SAMPLE_3}",
         f"Die beiden Texte haben einen langen gemeinsamen Anfang, ihr Abstand ist daher klein. Ersetzen Sie den zweiten Text durch diesen, um einen großen Abstand zu sehen: {TEXT_SAMPLE_3}", "Vitanyi, compression distance, Kolmogorov complexity, similarity", input_height=140),
    tool("Cryptanalysis/Classic/ByteAdditionAnalysis", "TextAnalysis.TextAnalysis", [("InputA", "9AB2B2C16DBAB26DAEC16DC1B5B26DBCB9B16DAFBFB6B1B4B26DAEC16DBAB6B1BBB6B4B5C16DAEBBB16DAFBFB6BBB46DC1B5B26DB1BCB0C2BAB2BBC1C07B")], ["Result", "Report"], dict(Mode="2"),
         "Analysis of a Byte-wise Addition", "Analyse einer byteweisen Addition", "A constant was added to every byte of the text: all 256 constants are tried.", "Zu jedem Byte des Textes wurde eine Konstante addiert: Alle 256 Konstanten werden probiert.",
         "The ciphertext is given as hexadecimal digits. The report lists the three most text-like results.",
         "Der Geheimtext ist als Hexadezimalziffern angegeben. Der Bericht listet die drei am meisten textähnlichen Ergebnisse.", "byte addition, caesar, analysis, constant", input_height=160),
    tool("Cryptanalysis/Classic/HillKnownPlaintext", "ClassicAnalysis.ClassicAnalysis", [("Plaintext", "HELLOWORLDFROMTHEHILLCIPHERXYZ"), ("Ciphertext", "TFJIPIJSGZDKBSPPRBLASLSLZNVLXV")], ["Key", "Report"], dict(Mode="0", Size="3"),
         "Hill Cipher: Known-Plaintext Attack", "Hill-Chiffre: Known-Plaintext-Angriff", "The 3 x 3 key matrix is calculated from known plaintext and ciphertext.", "Die 3 x 3-Schlüsselmatrix wird aus bekanntem Klartext und Geheimtext berechnet.",
         "The key is GYBNQKURP, the matrix (6 24 1; 13 16 10; 20 17 15). With the size 2 and a 2 x 2 cipher the attack works the same way.",
         "Der Schlüssel ist GYBNQKURP, die Matrix (6 24 1; 13 16 10; 20 17 15). Mit der Größe 2 und einer 2 x 2-Chiffre funktioniert der Angriff genauso.", "Hill cipher, known plaintext, matrix, analysis"),
    tool("Cryptanalysis/Classic/FleissnerAnalysis", "ClassicAnalysis.ClassicAnalysis", [("Ciphertext", "TLLENEGHWEBMETIIHBRAETOELLIDDTHDGANMIIGEHADTTDNTHNDVHEAETNSETRTEHXXSNSXEXMXIXIOX")], ["Key", "Report"], dict(Mode="1", Size="4"),
         "Fleissner Grille: Analysis", "Fleißner-Schablone: Analyse", "All 4 x 4 grilles are tried; the result with the most common bigrams and trigrams is the text.", "Alle 4 x 4-Schablonen werden probiert; das Ergebnis mit den meisten häufigen Bi- und Trigrammen ist der Text.",
         "Set the size 6 for a 6 x 6 grille (262144 grilles; the ciphertext length must be a multiple of 36).",
         "Stellen Sie die Größe 6 für eine 6 x 6-Schablone ein (262144 Schablonen; die Länge des Geheimtextes muss ein Vielfaches von 36 sein).", "Fleissner, grille, transposition, analysis", input_height=160),
]


TEMPLATES += [
    tool("Tools/NumberLength", "NumberTools.NumberTools", [("InputA", "1267650600228229401496703205376")], ["Result", "Report"], dict(Mode="0", Base="10"),
         "Length of a Number", "Länge einer Zahl", "The number of digits, bits and bytes of the number 2^100.", "Die Anzahl der Stellen, Bits und Bytes der Zahl 2^100.",
         "Enter any number. The base of the digits can be changed in the settings of the {\\\\b Number Tools} component.", "Geben Sie eine beliebige Zahl ein. Die Basis der Stellen lässt sich in den Einstellungen der Komponente {\\\\b Zahlenwerkzeuge} ändern.",
         "length of a number, digits, bits, size"),
    tool("Mathematics/ReciprocalSubtraction", "NumberTools.NumberTools", [("InputA", "252"), ("InputB", "105")], ["Result", "Report"], dict(Mode="1"),
         "Reciprocal Subtraction", "Reziproke Subtraktion", "The greatest common divisor of two numbers by repeated subtraction.", "Der größte gemeinsame Teiler zweier Zahlen durch wiederholte Subtraktion.",
         "Each line shows how often the smaller number is subtracted from the larger one and what remains; the last remainder before zero is the greatest common divisor.",
         "Jede Zeile zeigt, wie oft die kleinere Zahl von der größeren abgezogen wird und was übrig bleibt; der letzte Rest vor null ist der größte gemeinsame Teiler.", "reciprocal subtraction, euclid, gcd, anthyphairesis", input_height=80),
    tool("Codes/ViterbiDecoder", "ConvolutionalCode.ConvolutionalCode", [("InputBits", "1011010011101"), ("ErrorPositions", "3 11")], ["Encoded", "Received", "Decoded", "Report"], dict(),
         "Convolutional Code and Viterbi Decoder", "Faltungscode und Viterbi-Decoder", "A message is coded with a convolutional code, two bits are flipped and the Viterbi decoder corrects them.", "Eine Nachricht wird mit einem Faltungscode codiert, zwei Bits werden gekippt, und der Viterbi-Decoder korrigiert sie.",
         "Change the positions of the errors: any two errors are corrected, because the free distance of the code is 5. Add a third close to them to see the decoder fail.",
         "Ändern Sie die Positionen der Fehler: Beliebige zwei Fehler werden korrigiert, weil die freie Distanz des Codes 5 ist. Fügen Sie einen dritten in ihrer Nähe hinzu, um den Decoder scheitern zu sehen.", "Viterbi, convolutional code, error correction, trellis", input_height=80),
    tool("Protocols/TwoFactorTotp", "OneTimePassword.OneTimePassword", [("Secret", "12345678901234567890"), ("CounterOrTime", "1111111109")], ["Code"], dict(Mode="1", Algorithm="0", Digits="8", Period="30", SecretFormat="0"),
         "Two-Factor Authentication: TOTP", "Zwei-Faktor-Authentisierung: TOTP", "The time-based one-time password of RFC 6238 for a time and a shared secret.", "Das zeitbasierte Einmalpasswort nach RFC 6238 für eine Zeit und ein gemeinsames Geheimnis.",
         "The values are the test case of RFC 6238: the time 1111111109 gives the code 07081804. Choose HOTP in the settings of the {\\\\b One-Time Password} component to use a counter.",
         "Die Werte sind der Testfall aus RFC 6238: Die Zeit 1111111109 ergibt den Code 07081804. Wählen Sie in den Einstellungen der Komponente {\\\\b Einmalpasswort} HOTP, um einen Zähler zu benutzen.", "two-factor, TOTP, HOTP, one-time password, authenticator", input_height=80),
    tool("Protocols/ExposureNotification", "ExposureNotification.ExposureNotification", [("TemporaryExposureKey", "03142536475869708192A3B4C5D6E7F8"), ("IntervalNumber", "2670000"), ("Metadata", "40E20000")], ["RollingProximityIdentifier", "EncryptedMetadata", "Report"], dict(),
         "Exposure Notification (Corona-Warn-App)", "Exposure Notification (Corona-Warn-App)", "The Rolling Proximity Identifier that a phone sends in one 10-minute interval, derived from its daily key.", "Der Rolling Proximity Identifier, den ein Telefon in einem 10-Minuten-Intervall sendet, abgeleitet aus seinem Tagesschlüssel.",
         "Change the interval number: every interval gives another identifier that cannot be linked to the others without the daily key.",
         "Ändern Sie die Intervallnummer: Jedes Intervall ergibt einen anderen Identifier, der ohne den Tagesschlüssel nicht mit den anderen verknüpft werden kann.", "Corona, exposure notification, HKDF, AES, privacy", input_height=80),
]


# sample data made with the OpenSSL command line (a 1024-bit RSA key for the demonstration, never to be used for anything else)
SAMPLE_RSA_PRIVATE = """-----BEGIN PRIVATE KEY-----
MIICdgIBADANBgkqhkiG9w0BAQEFAASCAmAwggJcAgEAAoGBALZLugKH7cI6gg4G
2EkkIzMHMo4fzz9Ll8yKHjEnQbNHDBN9PgAagsxWqr2WstOJPhi9khwgkB+neihC
Jk9ryKVQaCmXEo9K58GKRr5NsN4j+5A2JCdt87UAKMmbZQqKp3gR6rUbsYUxucyB
WaU6xUNjyxbLAJ/H/ad8ILpev/uRAgMBAAECgYB6JamgkoX8dAm67lk0QROY2ITG
eIf8qGExe4aZdu1t+/gagvvtQ9ZKpvPHD1gJp9dE6pkhYaxQmX6fxRs4ckdBYn2t
+F8znKSirofMfKc1UyP60zAd191M7lseQDRHp6FpYjukPNXVvEUJI+PsQeyrV3Pp
JuzfUhuyVoEEPH5sAQJBAPJz0QxXrd1Fx0Jo6cA0G83dwXfuOlx9ShB4bug4Xxsi
XF7HhR/kXVSJfl/TkXu5akwldx+oBisigHYGvI7Uj8ECQQDAe2TrB6tqumP9aBxi
O5DMoHaVzia2ZVZ+VfMQM68Cf4jwXGRBfUMWkOataEBhIU49WiAdwoNEfry/QEJZ
Xl/RAkAiEJO2oDsVc9BjTEpLbwbZDec46ofYpuHZHpK2nfT+4v0HdBEIQhW64vaW
kCtIrux4dmseT4VOWjKiYp/rkYIBAkBsNHH1FiitJP8U87QRHP83qXVAndVxb+KB
vvuXPhbCRsPKq4XIswxF4UtsbFDUyY5cOxAW6sQ0ezLMKOmjp8QhAkEAorBjhFT/
MhDsYycmnBRng7A+C9+3ZyxcjIOb3fMWezmKVVMX6q6SUcIYv9m3XrPbO2G4WmeQ
wlK15IuL4kJcNQ==
-----END PRIVATE KEY-----"""
SAMPLE_RSA_PUBLIC = """-----BEGIN PUBLIC KEY-----
MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC2S7oCh+3COoIOBthJJCMzBzKO
H88/S5fMih4xJ0GzRwwTfT4AGoLMVqq9lrLTiT4YvZIcIJAfp3ooQiZPa8ilUGgp
lxKPSufBika+TbDeI/uQNiQnbfO1ACjJm2UKiqd4Eeq1G7GFMbnMgVmlOsVDY8sW
ywCfx/2nfCC6Xr/7kQIDAQAB
-----END PUBLIC KEY-----"""
SAMPLE_CMS = "MIIECQYJKoZIhvcNAQcCoIID+jCCA/YCAQExDTALBglghkgBZQMEAgEwCwYJKoZIhvcNAQcBoIICBTCCAgEwggFqoAMCAQICFANzx3I+Yc/ydCUfpdkosstIih2dMA0GCSqGSIb3DQEBCwUAMCMxDjAMBgNVBAMMBUFsaWNlMREwDwYDVQQKDAhDcnlwVG9vbDAeFw0yNjExMTkxNTEwMTBaFw0zNjExMTYxNTEwMTBaMCMxDjAMBgNVBAMMBUFsaWNlMREwDwYDVQQKDAhDcnlwVG9vbDCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAtku6AoftwjqCDgbYSSQjMwcyjh/PP0uXzIoeMSdBs0cME30+ABqCzFaqvZay04k+GL2SHCCQH6d6KEImT2vIpVBoKZcSj0rnwYpGvk2w3iP7kDYkJ23ztQAoyZtlCoqneBHqtRuxhTG5zIFZpTrFQ2PLFssAn8f9p3wgul6/+5ECAwEAAaMyMDAwHQYDVR0OBBYEFNIio1Vo8keCnHKrl6PvDQAHr+BTMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADgYEAckLntzHRjb3NK9dWlNw6v6ohj3AeI59wHtlBEX9MuaZU9mWU9VwG3ccc1Kg3NbSyv4wf53/60bYg4/wv1jal52WObblZ2fbQhJ6T7d9L8n6oMvqVSdiXeaDaPs8kMtefalwmHF4f9br7ic+7R466t+SmS+sXiE5PfydNvME1iWIxggHKMIIBxgIBATA7MCMxDjAMBgNVBAMMBUFsaWNlMREwDwYDVQQKDAhDcnlwVG9vbAIUA3PHcj5hz/J0JR+l2Siyy0iKHZ0wCwYJYIZIAWUDBAIBoIHkMBgGCSqGSIb3DQEJAzELBgkqhkiG9w0BBwEwHAYJKoZIhvcNAQkFMQ8XDTI2MTExOTE1MTAxMFowLwYJKoZIhvcNAQkEMSIEIMyk1GTuJp2cOezyJ1CPQEmmfRkgtAkJuz+dG3oEnYmdMHkGCSqGSIb3DQEJDzFsMGowCwYJYIZIAWUDBAEqMAsGCWCGSAFlAwQBFjALBglghkgBZQMEAQIwCgYIKoZIhvcNAwcwDgYIKoZIhvcNAwICAgCAMA0GCCqGSIb3DQMCAgFAMAcGBSsOAwIHMA0GCCqGSIb3DQMCAgEoMA0GCSqGSIb3DQEBAQUABIGAcL9DH92AMnGOouaxFUy/8AduKKJbS/RcEej8p2vObnN6FT1CZj/Wuws7gTCdvqJQUwiOTTyfuyMYKQqTaHJDmc6ryXWS4TAtFR4UZ85c4Noq4hQuueZjRZJG2SVLcV0yLnniWxZ+QQeJcmUqsGEKPU9tq0qfhwX6XG8vSoc51sc="

CERTIFICATE_CHAIN = dict(
    base="Tools/RandomNumberGenerators", plugin="CertificateTool",
    edits=[dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
           dict(add=dict(type="CrypTool.TextInput.TextInput", x=40, y=40, w=320, h=80, settings=dict(Text="www.example.org"))),
           dict(add=dict(type="CertificateTool.CertificateTool", x=420, y=40, w=300, h=140, settings=dict(Mode="0"))),
           dict(add=dict(type="CertificateTool.CertificateTool", x=780, y=40, w=300, h=140, settings=dict(Mode="1"))),
           dict(add=dict(type="TextOutput.TextOutput", x=420, y=260, w=320, h=300)),
           dict(add=dict(type="TextOutput.TextOutput", x=780, y=260, w=320, h=300)),
           dict(connect={"from": "CrypTool.TextInput.TextInput.TextOutput", "to": "CertificateTool.CertificateTool#0.Input"}),
           dict(connect={"from": "CertificateTool.CertificateTool#0.Chain", "to": "CertificateTool.CertificateTool#1.Input"}),
           dict(connect={"from": "CertificateTool.CertificateTool#0.Report", "to": "TextOutput.TextOutput#0.Input"}),
           dict(connect={"from": "CertificateTool.CertificateTool#1.Report", "to": "TextOutput.TextOutput#1.Input"})])

TEMPLATES += [
    tool("Tools/ASN1Viewer", "Asn1Viewer.Asn1Viewer", [("Input", SAMPLE_RSA_PUBLIC)], ["Output"], dict(Mode="0"),
         "ASN.1 Viewer", "ASN.1-Betrachter", "The structure of an RSA public key: the algorithm and the numbers n and e.", "Der Aufbau eines öffentlichen RSA-Schlüssels: der Algorithmus und die Zahlen n und e.",
         "The input is a PEM block; hexadecimal digits and Base64 work as well. Paste a certificate to see its fields.", "Die Eingabe ist ein PEM-Block; Hexadezimalziffern und Base64 funktionieren ebenfalls. Fügen Sie ein Zertifikat ein, um seine Felder zu sehen.", "ASN.1, DER, PEM, structure, key, certificate", input_height=200),
    tool("Tools/ExtractSignature", "Asn1Viewer.Asn1Viewer", [("Input", SAMPLE_CMS)], ["Output"], dict(Mode="1"),
         "Extract the Signature of a Signed Message", "Signatur einer signierten Nachricht extrahieren", "The signer, the algorithms, the signature value and the content of a CMS message.", "Der Unterzeichner, die Algorithmen, der Signaturwert und der Inhalt einer CMS-Nachricht.",
         "The message was signed with OpenSSL (cms -sign) with an RSA key and SHA-256.", "Die Nachricht wurde mit OpenSSL (cms -sign) mit einem RSA-Schlüssel und SHA-256 signiert.", "signature, extract, CMS, PKCS#7, signed message", input_height=200),
    tool("Cryptography/Modern/Asymmetric/KeyGeneratorRSA", "KeyTool.KeyTool", [], ["PrivateKey", "PublicKey", "Report"], dict(Mode="0", KeySize="1024"),
         "Generate an RSA Key Pair (PEM)", "RSA-Schlüsselpaar erzeugen (PEM)", "A new RSA key pair as PEM blocks, with its parameters and fingerprint.", "Ein neues RSA-Schlüsselpaar als PEM-Blöcke, mit seinen Parametern und seinem Fingerabdruck.",
         "The key has 1024 bits to be quick; set 2048 bits or more for real keys in the settings of the {\\\\b Key Tool} component. The PEM blocks can be used with OpenSSL.", "Der Schlüssel hat 1024 Bit, damit es schnell geht; stellen Sie für echte Schlüssel in den Einstellungen der Komponente {\\\\b Schlüsselwerkzeug} 2048 Bit oder mehr ein. Die PEM-Blöcke lassen sich mit OpenSSL benutzen.", "key generation, RSA, PEM, PKCS#8, OpenSSL"),
    tool("Cryptography/Modern/Asymmetric/KeyGeneratorEC", "KeyTool.KeyTool", [], ["PrivateKey", "PublicKey", "Report"], dict(Mode="1", Curve="0"),
         "Generate an EC Key Pair (PEM)", "EC-Schlüsselpaar erzeugen (PEM)", "A new elliptic curve key pair on the curve P-256 as PEM blocks.", "Ein neues Schlüsselpaar auf der elliptischen Kurve P-256 als PEM-Blöcke.",
         "Choose another curve in the settings of the {\\\\b Key Tool} component.", "Wählen Sie in den Einstellungen der Komponente {\\\\b Schlüsselwerkzeug} eine andere Kurve.", "key generation, elliptic curve, EC, PEM, P-256"),
    tool("Cryptography/Modern/Asymmetric/KeyShow", "KeyTool.KeyTool", [("Input", SAMPLE_RSA_PRIVATE)], ["PrivateKey", "PublicKey", "Report"], dict(Mode="2"),
         "Import and Show a Key", "Schlüssel importieren und anzeigen", "A private key in the old PKCS#1 format is read, described and exported in the standard PKCS#8 format.", "Ein privater Schlüssel im alten PKCS#1-Format wird gelesen, beschrieben und im Standardformat PKCS#8 exportiert.",
         "The key was made with OpenSSL. The public key is calculated from the private key.", "Der Schlüssel wurde mit OpenSSL erzeugt. Der öffentliche Schlüssel wird aus dem privaten berechnet.", "key import, key export, PEM, PKCS#1, PKCS#8", input_height=200),
    dict(out="Protocols/CertificateChain", **CERTIFICATE_CHAIN,
         en=dict(title="Public Key Infrastructure: Certificate Chain", summary="<i>Public Key Infrastructure: Certificate Chain</i>", description="A root CA, an intermediate CA and a server certificate are created and the chain is verified.", memo="The first component creates the chain, the second verifies it from the PEM blocks. Change the function of a component in its settings to show certificates."),
         de=dict(title="Public-Key-Infrastruktur: Zertifikatskette", summary="<i>Public-Key-Infrastruktur: Zertifikatskette</i>", description="Eine Wurzelstelle, eine Zwischenstelle und ein Serverzertifikat werden erzeugt, und die Kette wird geprüft.", memo="Die erste Komponente erzeugt die Kette, die zweite prüft sie anhand der PEM-Blöcke. Ändern Sie die Funktion einer Komponente in ihren Einstellungen, um Zertifikate anzuzeigen."),
         keywords="PKI, certificate, chain, X.509, CA, verification"),
]


HYBRID = dict(
    base="Tools/RandomNumberGenerators", plugin="HybridEncryption",
    edits=[dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
           dict(add=dict(type="CrypTool.TextInput.TextInput", x=40, y=40, w=320, h=80, settings=dict(Text="Meet me at the old bridge at midnight."))),
           dict(add=dict(type="CrypTool.TextInput.TextInput", x=40, y=180, w=320, h=200, settings=dict(Text=SAMPLE_RSA_PUBLIC))),
           dict(add=dict(type="CrypTool.TextInput.TextInput", x=40, y=420, w=320, h=240, settings=dict(Text=SAMPLE_RSA_PRIVATE))),
           dict(add=dict(type="HybridEncryption.HybridEncryption", x=420, y=40, w=300, h=140, settings=dict(Mode="0"))),
           dict(add=dict(type="HybridEncryption.HybridEncryption", x=780, y=40, w=300, h=140, settings=dict(Mode="1"))),
           dict(add=dict(type="TextOutput.TextOutput", x=420, y=260, w=320, h=200)),
           dict(add=dict(type="TextOutput.TextOutput", x=780, y=260, w=320, h=200)),
           dict(connect={"from": "CrypTool.TextInput.TextInput#0.TextOutput", "to": "HybridEncryption.HybridEncryption#0.Input"}),
           dict(connect={"from": "CrypTool.TextInput.TextInput#1.TextOutput", "to": "HybridEncryption.HybridEncryption#0.Key"}),
           dict(connect={"from": "HybridEncryption.HybridEncryption#0.Output", "to": "HybridEncryption.HybridEncryption#1.Input"}),
           dict(connect={"from": "CrypTool.TextInput.TextInput#2.TextOutput", "to": "HybridEncryption.HybridEncryption#1.Key"}),
           dict(connect={"from": "HybridEncryption.HybridEncryption#0.Output", "to": "TextOutput.TextOutput#0.Input"}),
           dict(connect={"from": "HybridEncryption.HybridEncryption#1.Output", "to": "TextOutput.TextOutput#1.Input"})])

TEMPLATES += [
    dict(out="Cryptography/Modern/Asymmetric/HybridEncryption_RSA_AES", **HYBRID,
         en=dict(title="Hybrid Encryption (RSA-AES)", summary="<i>Hybrid Encryption (RSA-AES)</i>", description="A message is encrypted with AES-GCM, the AES key with the RSA public key, and decrypted again.", memo="The first component encrypts with the public key, the second decrypts with the private key. The key pair is a 1024-bit demonstration key made with OpenSSL: do not use it for anything else."),
         de=dict(title="Hybride Verschlüsselung (RSA-AES)", summary="<i>Hybride Verschlüsselung (RSA-AES)</i>", description="Eine Nachricht wird mit AES-GCM verschlüsselt, der AES-Schlüssel mit dem öffentlichen RSA-Schlüssel, und wieder entschlüsselt.", memo="Die erste Komponente verschlüsselt mit dem öffentlichen Schlüssel, die zweite entschlüsselt mit dem privaten. Das Schlüsselpaar ist ein 1024-Bit-Demonstrationsschlüssel, der mit OpenSSL erzeugt wurde: Benutzen Sie ihn für nichts anderes."),
         keywords="hybrid encryption, RSA, AES, OAEP, GCM"),
    tool("Cryptography/Modern/Symmetric/PBES2", "PasswordBasedEncryption.PasswordBasedEncryption", [("Input", "Meet me at the old bridge at midnight."), ("Password", "correct horse battery staple")], ["Output", "Report"], dict(Mode="0", Iterations="10000", Hash="1"),
         "Password-Based Encryption (PBES2)", "Passwortbasierte Verschlüsselung (PBES2)", "A text is encrypted with a key derived from a password (PBKDF2, AES-256-CBC).", "Ein Text wird mit einem aus einem Passwort abgeleiteten Schlüssel verschlüsselt (PBKDF2, AES-256-CBC).",
         "The result is the ASN.1 structure of PKCS #5 in Base64; a random salt is chosen every time. Choose Decrypt in the settings of the {\\\\b Password-Based Encryption} component and connect the result to decrypt.",
         "Das Ergebnis ist die ASN.1-Struktur von PKCS #5 in Base64; jedes Mal wird ein zufälliger Salt gewählt. Wählen Sie in den Einstellungen der Komponente {\\\\b Passwortbasierte Verschlüsselung} Entschlüsseln und verbinden Sie das Ergebnis, um zu entschlüsseln.", "PBES2, PBKDF2, password, AES, PKCS5"),
    tool("Protocols/BurmesterDesmedt", "MultipartyKeyExchange.MultipartyKeyExchange", [("Parameters", "1019 2 17 23 41 59")], ["Key", "Report"], dict(),
         "Multi-Party Key Exchange (Burmester-Desmedt)", "Schlüsselaustausch für mehrere Teilnehmer (Burmester-Desmedt)", "Four parties in a ring agree on a common key.", "Vier Teilnehmer in einem Ring einigen sich auf einen gemeinsamen Schlüssel.",
         "The parameters are the prime 1019, the generator 2 and the secret numbers 17, 23, 41 and 59 of the four parties. Add more numbers for more parties.",
         "Die Parameter sind die Primzahl 1019, der Erzeuger 2 und die geheimen Zahlen 17, 23, 41 und 59 der vier Teilnehmer. Fügen Sie weitere Zahlen für weitere Teilnehmer hinzu.", "multi-party, key exchange, Burmester, Desmedt, conference key", input_height=80),
    tool("Tools/EllipticCurveRandomGenerator", "EcRandom.EcRandom", [], ["Output", "Report"], dict(Curve="0", Seed="1", Blocks="5"),
         "Elliptic Curve Random Generator with Backdoor", "Zufallsgenerator auf elliptischen Kurven mit Hintertür", "A Dual_EC_DRBG-style generator and the prediction of its output with the backdoor.", "Ein Generator im Stil von Dual_EC_DRBG und die Vorhersage seiner Ausgabe mit der Hintertür.",
         "The attack needs two blocks and tries 65536 values; it then predicts all further blocks.", "Der Angriff braucht zwei Blöcke und probiert 65536 Werte; danach sagt er alle weiteren Blöcke voraus.", "elliptic curve, random number generator, Dual_EC_DRBG, backdoor"),
    tool("Cryptography/Modern/Asymmetric/HomomorphicPaillier", "HomomorphicEncryption.HomomorphicEncryption", [("InputA", "12345"), ("InputB", "6789")], ["Result", "Report"], dict(Scheme="1", Seed="1"),
         "Homomorphic Encryption: Paillier", "Homomorphe Verschlüsselung: Paillier", "Two numbers are encrypted, added in encrypted form and decrypted: 12345 + 6789.", "Zwei Zahlen werden verschlüsselt, in verschlüsselter Form addiert und entschlüsselt: 12345 + 6789.",
         "Choose RSA or DGHV in the settings of the {\\\\b Homomorphic Encryption} component to see multiplication and the noise of a fully homomorphic scheme.",
         "Wählen Sie in den Einstellungen der Komponente {\\\\b Homomorphe Verschlüsselung} RSA oder DGHV, um die Multiplikation und das Rauschen eines voll homomorphen Verfahrens zu sehen.", "homomorphic encryption, Paillier, RSA, DGHV, Gentry", input_height=80),
    tool("Cryptography/Modern/Asymmetric/HomomorphicDGHV", "HomomorphicEncryption.HomomorphicEncryption", [("InputA", "1"), ("InputB", "1")], ["Result", "Report"], dict(Scheme="2", Seed="1"),
         "Homomorphic Encryption: DGHV (fully homomorphic)", "Homomorphe Verschlüsselung: DGHV (voll homomorph)", "Bits are encrypted, XOR and AND are computed on the ciphertexts, and the noise grows until the decryption fails.", "Bits werden verschlüsselt, XOR und UND werden auf den Geheimtexten berechnet, und das Rauschen wächst, bis die Entschlüsselung scheitert.",
         "Every multiplication makes the noise larger; when it reaches half of the secret number p the decryption is wrong. Gentry's bootstrapping removes the noise.",
         "Jede Multiplikation vergrößert das Rauschen; erreicht es die Hälfte der geheimen Zahl p, ist die Entschlüsselung falsch. Das Bootstrapping von Gentry entfernt das Rauschen.", "homomorphic encryption, DGHV, fully homomorphic, noise, bootstrapping", input_height=80),
]


TEMPLATES += [
    tool("Cryptography/Modern/Asymmetric/Signature_GQ", "SignatureSchemes.SignatureSchemes", [("Message", "Pay Bob 10 euros"), ("Received", "Pay Bob 10 euros")], ["Signature", "Valid", "Report"], dict(Scheme="0", Seed="1"),
         "Guillou-Quisquater Signature", "Guillou-Quisquater-Signatur", "A message is signed with the GQ scheme and the signature is verified.", "Eine Nachricht wird mit dem GQ-Verfahren signiert, und die Signatur wird geprüft.",
         "Change the received message to see the verification fail.", "Ändern Sie die empfangene Nachricht, um die Prüfung scheitern zu sehen.", "Guillou-Quisquater, GQ, signature, identification", input_height=80),
    tool("Cryptography/Modern/Asymmetric/Signature_Redactable", "SignatureSchemes.SignatureSchemes", [("Message", "The meeting is on Friday at the old bridge with Mr Smith"), ("Hide", "7 8 9 10 11")], ["Signature", "Valid", "Report"], dict(Scheme="1", Seed="1"),
         "Redactable Signature", "Redigierbare Signatur", "The place and the name are removed from a signed message, and the signature is still valid.", "Der Ort und der Name werden aus einer signierten Nachricht entfernt, und die Signatur ist weiterhin gültig.",
         "The positions of the hidden words are given as numbers. Hide other words or none.", "Die Positionen der verborgenen Wörter sind als Zahlen angegeben. Verbergen Sie andere Wörter oder keine.", "redactable signature, Merkle tree, redaction, privacy", input_height=80),
    tool("Cryptography/Modern/Asymmetric/Signature_CMSS", "SignatureSchemes.SignatureSchemes", [("Message", "Pay Bob 10 euros")], ["Signature", "Valid", "Report"], dict(Scheme="2", Seed="1", Layers="2", Height="3", Index="5"),
         "Merkle Signature with Two Layers of Trees (CMSS)", "Merkle-Signatur mit zwei Baumschichten (CMSS)", "A hash-based signature with 2 layers of trees of height 3: 64 signatures.", "Eine hashbasierte Signatur mit 2 Baumschichten der Höhe 3: 64 Signaturen.",
         "Change the number of the signature (every one-time key may be used only once) or the height in the settings of the {\\\\b More Signature Schemes} component.", "Ändern Sie die Nummer der Signatur (jeder Einmalschlüssel darf nur einmal benutzt werden) oder die Höhe in den Einstellungen der Komponente {\\\\b Weitere Signaturverfahren}.", "CMSS, Merkle signature, hash-based, post-quantum", input_height=80),
    tool("Cryptography/Modern/Asymmetric/Signature_GMSS", "SignatureSchemes.SignatureSchemes", [("Message", "Pay Bob 10 euros")], ["Signature", "Valid", "Report"], dict(Scheme="2", Seed="1", Layers="3", Height="2", Index="37"),
         "Generalized Merkle Signature with Three Layers of Trees (GMSS)", "Verallgemeinerte Merkle-Signatur mit drei Baumschichten (GMSS)", "A hash-based signature with 3 layers of trees of height 2: 64 signatures.", "Eine hashbasierte Signatur mit 3 Baumschichten der Höhe 2: 64 Signaturen.",
         "More layers make the signature longer but allow very many signatures with small trees.", "Mehr Schichten machen die Signatur länger, erlauben aber sehr viele Signaturen mit kleinen Bäumen.", "GMSS, Merkle signature, hash-based, post-quantum", input_height=80),
    tool("Cryptography/Modern/Asymmetric/Signature_OilAndVinegar", "SignatureSchemes.SignatureSchemes", [("Message", "Pay Bob 10 euros")], ["Signature", "Valid", "Report"], dict(Scheme="3", Seed="1"),
         "Multivariate Signature: Oil and Vinegar", "Multivariate Signatur: Oil and Vinegar", "A signature whose public key is a system of quadratic equations over GF(251).", "Eine Signatur, deren öffentlicher Schlüssel ein System quadratischer Gleichungen über GF(251) ist.",
         "The signer chooses random vinegar values and solves a linear system for the oil values; the verifier only evaluates the public polynomials.", "Der Signierer wählt zufällige Vinegar-Werte und löst ein lineares Gleichungssystem für die Oil-Werte; der Prüfer wertet nur die öffentlichen Polynome aus.", "multivariate cryptography, oil and vinegar, UOV, post-quantum", input_height=80),
]


MLKEM = dict(
    base="Tools/RandomNumberGenerators", plugin="MlKem",
    edits=[dict(remove="RandomNumberGenerator"), dict(remove="TextOutput"),
           dict(add=dict(type="MlKem.MlKem", x=40, y=40, w=280, h=140, settings=dict(Mode="0", ParameterSet="1"))),
           dict(add=dict(type="MlKem.MlKem", x=420, y=40, w=280, h=140, settings=dict(Mode="1", ParameterSet="1"))),
           dict(add=dict(type="MlKem.MlKem", x=820, y=40, w=280, h=140, settings=dict(Mode="2", ParameterSet="1"))),
           dict(add=dict(type="TextOutput.TextOutput", x=420, y=260, w=320, h=100)),
           dict(add=dict(type="TextOutput.TextOutput", x=820, y=260, w=320, h=100)),
           dict(add=dict(type="TextOutput.TextOutput", x=40, y=260, w=320, h=100)),
           dict(connect={"from": "MlKem.MlKem#0.NewEncapsulationKey", "to": "MlKem.MlKem#1.EncapsulationKey"}),
           dict(connect={"from": "MlKem.MlKem#1.NewCiphertext", "to": "MlKem.MlKem#2.Ciphertext"}),
           dict(connect={"from": "MlKem.MlKem#0.NewDecapsulationKey", "to": "MlKem.MlKem#2.DecapsulationKey"}),
           dict(connect={"from": "MlKem.MlKem#0.Report", "to": "TextOutput.TextOutput#2.Input"}),
           dict(connect={"from": "MlKem.MlKem#1.SharedSecret", "to": "TextOutput.TextOutput#0.Input"}),
           dict(connect={"from": "MlKem.MlKem#2.SharedSecret", "to": "TextOutput.TextOutput#1.Input"})])

TEMPLATES += [
    dict(out="Cryptography/Modern/Asymmetric/MLKEM", **MLKEM,
         en=dict(title="ML-KEM (Kyber): Post-Quantum Key Encapsulation", summary="<i>ML-KEM (Kyber): Post-Quantum Key Encapsulation</i>", description="The receiver makes a key pair, the sender encapsulates a secret, and the receiver decapsulates the same secret.", memo="The first component generates the key pair, the second encapsulates with the public key, the third decapsulates with the private key. The two shared secrets are equal. Choose another parameter set (512, 768 or 1024) in the settings of every component. The implementation follows FIPS 203 and agrees with OpenSSL."),
         de=dict(title="ML-KEM (Kyber): Post-Quanten-Schlüsselkapselung", summary="<i>ML-KEM (Kyber): Post-Quanten-Schlüsselkapselung</i>", description="Der Empfänger erzeugt ein Schlüsselpaar, der Sender kapselt ein Geheimnis, und der Empfänger entkapselt dasselbe Geheimnis.", memo="Die erste Komponente erzeugt das Schlüsselpaar, die zweite kapselt mit dem öffentlichen Schlüssel, die dritte entkapselt mit dem privaten. Die beiden gemeinsamen Geheimnisse sind gleich. Wählen Sie in den Einstellungen jeder Komponente einen anderen Parametersatz (512, 768 oder 1024). Die Implementierung folgt FIPS 203 und stimmt mit OpenSSL überein."),
         keywords="ML-KEM, Kyber, post-quantum, key encapsulation, lattice, FIPS 203"),
]


SAMPLE_VIGENERE = "TXUGN EVGHU FRUJR CWMZY JEOYA ZAXSQ RIPHU LXMGV YKXSZ LRUBC ZWESF DMABB QESCB OJAFG FRQAH DXNSV YAMBG ZJMKV QITCJ PZQFY TXFZR VRAKA ELQTR PPUBT DSDJV PAECS DYOVN XEZAN JFQCA SMETV CWFSA EIDWA REZSV RLNCH CLACQ ELUGG CYFVV DWAKR WPRWK PHUBG SIYWA OWATG SIEIE CSGBQ TRSTN XMXWR DXTOG SIUGP ZREWQ PVQRG SIDWT SXRIY AVADR CXKCS DSYSB YIAFB ELQFB QXTSV CHMIT SXQFF XCPSN CQDPR YRQHF LMPVV DPMRL ESTWZ ZRQRN JLMJR JSGVR LVPHU LXZSG SIDTV PPPDN COUGY PXMHY LWFAE MIZBR EVQDY TIPHU LXTSU LHZCG"

TEMPLATES += [
    tool("Tools/ASCIICodes", "AsciiCodes.AsciiCodes", [("Input", "Hello CrypTool")], ["Output"], dict(Mode="0"),
         "ASCII Codes", "ASCII-Codes", "A text as decimal ASCII codes.", "Ein Text als dezimale ASCII-Codes.",
         "Choose hexadecimal or binary codes, the conversion back to text or the ASCII table in the settings of the {\\\\b ASCII Codes} component.",
         "Wählen Sie in den Einstellungen der Komponente {\\\\b ASCII-Codes} Hexadezimal- oder Binärcodes, die Umwandlung zurück in Text oder die ASCII-Tabelle.", "ASCII, codes, text, binary, hexadecimal", input_height=80),
    tool("Codes/Barcode_EAN13", "Barcode.Barcode", [("Input", "400638133393")], ["Output", "Modules", "Report"], dict(Format="0"),
         "Barcode: EAN-13", "Barcode: EAN-13", "The bars of an EAN-13 barcode; the check digit is added.", "Die Striche eines EAN-13-Barcodes; die Prüfziffer wird ergänzt.",
         "Enter 12 digits; the thirteenth, the check digit, is calculated. Choose EAN-8 in the settings of the {\\\\b Barcode} component for the short code.",
         "Geben Sie 12 Ziffern ein; die dreizehnte, die Prüfziffer, wird berechnet. Wählen Sie in den Einstellungen der Komponente {\\\\b Barcode} EAN-8 für den kurzen Code.", "barcode, EAN-13, EAN-8, check digit", input_height=80),
    tool("Cryptanalysis/Classic/CipherIdentification", "CipherIdentification.CipherIdentification", [("Input", SAMPLE_VIGENERE)], ["Guess", "Report"], dict(),
         "Cipher Identification", "Chiffre-Identifizierung", "The kind of a cipher is estimated from the statistics of the ciphertext.", "Die Art einer Chiffre wird aus den Statistiken des Geheimtextes geschätzt.",
         "The sample is a Vigenere cipher with the key LEMON: the index of coincidence is low, but the columns for the key length 5 are monoalphabetic. Paste another ciphertext.",
         "Das Beispiel ist eine Vigenère-Chiffre mit dem Schlüssel LEMON: Der Koinzidenzindex ist niedrig, aber die Spalten für die Schlüssellänge 5 sind monoalphabetisch. Fügen Sie einen anderen Geheimtext ein.", "cipher identification, index of coincidence, entropy, classification", input_height=200),
    tool("Hashfunctions/VerySmoothHash", "VerySmoothHash.VerySmoothHash", [("Input", "abc")], ["Hash", "Report"], dict(ModulusBits="512", Seed="1"),
         "Very Smooth Hash (VSH)", "Very Smooth Hash (VSH)", "A hash function whose collision resistance is related to factoring.", "Eine Hashfunktion, deren Kollisionsresistenz mit dem Faktorisieren zusammenhängt.",
         "Change the size of the modulus in the settings of the {\\\\b Very Smooth Hash} component: a larger modulus gives larger blocks.",
         "Ändern Sie die Größe des Moduls in den Einstellungen der Komponente {\\\\b Very Smooth Hash}: Ein größerer Modul ergibt größere Blöcke.", "VSH, very smooth hash, hash function, factoring", input_height=80),
]


TEMPLATES += [
    tool("Cryptography/Modern/Asymmetric/Signature_McEliece_CFS", "CfsSignature.CfsSignature", [("Message", "Pay Bob 10 euros"), ("Received", "Pay Bob 10 euros")], ["Signature", "Valid", "Report"], dict(Seed="1"),
         "McEliece Signature (CFS)", "McEliece-Signatur (CFS)", "A code-based signature: an error vector whose syndrome is the hash value, found by decoding a Goppa code.", "Eine codebasierte Signatur: ein Fehlervektor, dessen Syndrom der Hashwert ist, gefunden durch Decodieren eines Goppa-Codes.",
         "The report shows how many hash values had to be tried until one could be decoded. Change the received message to see the verification fail. The code is tiny (length 256, t = 4) and gives no security.",
         "Der Bericht zeigt, wie viele Hashwerte probiert werden mussten, bis einer decodiert werden konnte. Ändern Sie die empfangene Nachricht, um die Prüfung scheitern zu sehen. Der Code ist winzig (Länge 256, t = 4) und bietet keine Sicherheit.", "McEliece, CFS, Goppa code, code-based signature, post-quantum", input_height=80),
]


TEMPLATES += [
    tool("Cryptography/Modern/Symmetric/DESL", "DesVariants.DesVariants", [("Input", "5468652073656372"), ("Key", "447FBAF5306BA6E1")], ["Output"], dict(Variant="1", Mode="0"),
         "DESL: DES with One S-Box", "DESL: DES mit einer S-Box", "The lightweight DES variant DESL encrypts one block.", "Die leichtgewichtige DES-Variante DESL verschlüsselt einen Block.",
         "DESL replaces the eight S-boxes of DES by one that is used eight times. Choose DES to compare, or Decrypt in the settings of the {\\\\b DES Variants} component.",
         "DESL ersetzt die acht S-Boxen von DES durch eine, die achtmal benutzt wird. Wählen Sie DES zum Vergleich oder Entschlüsseln in den Einstellungen der Komponente {\\\\b DES-Varianten}.", "DESL, DES, lightweight, RFID, S-box", input_height=80),
    tool("Cryptography/Modern/Symmetric/DESXL", "DesVariants.DesVariants", [("Input", "5468652073656372"), ("Key", "4580BBF6316CA7E21D5893CE09447FBAF5306BA6E11C5792")], ["Output"], dict(Variant="3", Mode="0"),
         "DESXL: DESL with Whitening", "DESXL: DESL mit Weißung", "DESL with a whitening key before and after the encryption.", "DESL mit einem Weißungsschlüssel vor und nach der Verschlüsselung.",
         "The key has 24 bytes: the DES key, the pre-whitening key and the post-whitening key.", "Der Schlüssel hat 24 Bytes: den DES-Schlüssel, den Vor- und den Nachweißungsschlüssel.", "DESXL, DESL, whitening, key search", input_height=80),
]


TEMPLATES += [
    tool("Cryptography/Modern/Symmetric/MARS", "ExtraBlockCiphers.ExtraBlockCiphers", [("Input", "54686520736563726574206973207361"), ("Key", "000102030405060708090A0B0C0D0E0F")], ["Output"], dict(Cipher="0", Mode="0"),
         "MARS (AES Finalist)", "MARS (AES-Finalist)", "One block encrypted with the IBM cipher MARS.", "Ein Block, verschlüsselt mit der IBM-Chiffre MARS.",
         "Choose MISTY1 or SAFER+ and the decryption in the settings of the {\\\\b More Block Ciphers} component. MARS accepts keys of 16 to 56 bytes.",
         "Wählen Sie MISTY1 oder SAFER+ und die Entschlüsselung in den Einstellungen der Komponente {\\\\b Weitere Blockchiffren}. MARS akzeptiert Schlüssel von 16 bis 56 Bytes.", "MARS, AES finalist, block cipher, IBM", input_height=80),
    tool("Cryptography/Modern/Symmetric/MISTY1", "ExtraBlockCiphers.ExtraBlockCiphers", [("Input", "0123456789ABCDEF"), ("Key", "00112233445566778899AABBCCDDEEFF")], ["Output"], dict(Cipher="1", Mode="0"),
         "MISTY1", "MISTY1", "The test vector of MISTY1: 0123456789ABCDEF is encrypted to 8B1DA5F56AB3D07C.", "Der Testvektor von MISTY1: 0123456789ABCDEF wird zu 8B1DA5F56AB3D07C verschlüsselt.",
         "MISTY1 has 64-bit blocks and 128-bit keys and is the basis of KASUMI.", "MISTY1 hat 64-Bit-Blöcke und 128-Bit-Schlüssel und ist die Grundlage von KASUMI.", "MISTY1, KASUMI, block cipher, Matsui", input_height=80),
    tool("Cryptography/Modern/Symmetric/SAFERplus", "ExtraBlockCiphers.ExtraBlockCiphers", [("Input", "B3A6DB3C870C3E99245E0D1C06B747DE"), ("Key", "2923BE84E16CD6AE529049F1F1BBE9EB")], ["Output"], dict(Cipher="2", Mode="0"),
         "SAFER+", "SAFER+", "The test vector of SAFER+ with a 128-bit key.", "Der Testvektor von SAFER+ mit einem 128-Bit-Schlüssel.",
         "SAFER+ is used in Bluetooth pairing. Keys of 192 and 256 bits are possible.", "SAFER+ wird beim Bluetooth-Pairing benutzt. Schlüssel von 192 und 256 Bit sind möglich.", "SAFER+, Bluetooth, block cipher, AES candidate", input_height=80),
    tool("Hashfunctions/MAC_MARS", "ExtraBlockCiphers.ExtraBlockCiphers", [("Input", "4D6573736167652074686174206E6565647320612043414D4143"), ("Key", "000102030405060708090A0B0C0D0E0F")], ["Output"], dict(Cipher="0", Mode="2"),
         "CMAC with MARS", "CMAC mit MARS", "A message authentication code made of the block cipher MARS.", "Ein Nachrichtenauthentifizierungscode aus der Blockchiffre MARS.",
         "Choose the classic CBC-MAC in the settings of the {\\\\b More Block Ciphers} component.", "Wählen Sie in den Einstellungen der Komponente {\\\\b Weitere Blockchiffren} den klassischen CBC-MAC.", "MAC, CMAC, MARS, authentication", input_height=80),
    tool("Hashfunctions/MAC_MISTY1", "ExtraBlockCiphers.ExtraBlockCiphers", [("Input", "4D6573736167652074686174206E6565647320612043414D4143"), ("Key", "00112233445566778899AABBCCDDEEFF")], ["Output"], dict(Cipher="1", Mode="2"),
         "CMAC with MISTY1", "CMAC mit MISTY1", "A message authentication code made of the block cipher MISTY1 (64-bit blocks).", "Ein Nachrichtenauthentifizierungscode aus der Blockchiffre MISTY1 (64-Bit-Blöcke).",
         "With 64-bit blocks a MAC loses its security after about 2^32 messages with the same key.", "Bei 64-Bit-Blöcken verliert ein MAC nach etwa 2^32 Nachrichten mit demselben Schlüssel seine Sicherheit.", "MAC, CMAC, MISTY1, authentication", input_height=80),
]


def main():
    for spec in TEMPLATES:
        base, out = TEMPLATES_DIR / spec["base"], TEMPLATES_DIR / spec["out"]
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
            json.dump(spec["edits"], f)
        run = subprocess.run(["dotnet", "run", "--no-build", "--project", str(HERE / "CrypWeb.Tests"), "--", str(TEMPLATES_DIR),
                              "derive", str(base) + ".cwm", str(out) + ".cwm", f.name], capture_output=True, text=True)
        if run.returncode != 0:
            sys.exit(f"{spec['out']}: {run.stdout}{run.stderr}")
        base_xml = Path(str(base) + ".xml").read_text(encoding="utf-8-sig")
        Path(str(out) + ".xml").write_text("﻿" + rewrite_xml(base_xml, spec, out.name), encoding="utf-8")
        if Path(str(base) + ".png").exists():
            shutil.copyfile(str(base) + ".png", str(out) + ".png")
        print("written", spec["out"])


if __name__ == "__main__":
    main()


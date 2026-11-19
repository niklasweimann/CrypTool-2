# Konzept: alle Funktionen der CrypTool-Funktionsliste in CrypWeb

Grundlage: <https://www.cryptool.org/de/functions/> (342 Einträge, Stand 2026-11-18). Die Seite hat vier Spalten: CT1, CT2, JCT (JCrypTool) und CTO (CrypTool-Online).
Verwendet werden nur Name und Fundort einer Funktion; Code aus CT1 oder JCT wird nicht benutzt (siehe Abschnitt 2).

**Stufe 0 ist umgesetzt:**

| Datei | Inhalt |
|---|---|
| [reference/function-list.json](reference/function-list.json) | Momentaufnahme der Seite (Name, Art, Spalten, Fundorte) |
| [reference/function-aliases.json](reference/function-aliases.json) | deutsche Namen der Liste, die nicht zu Klassen-/Ordnernamen passen, und was nicht in den Browser gehört (Tutorials, Wizard) |
| [reference/functions.json](reference/functions.json) | erzeugt: Status je Eintrag (`done`, `partial`, `open`) und gefundene Komponenten/Templates |
| `tools-gen-functions.py` | erzeugt `functions.json` (mit einer gespeicherten Seite als Argument auch die Momentaufnahme) |
| `dotnet run --project CrypWeb.Tests -- ../Templates functions` | prüft, dass jede zugeordnete Komponente in der Registry und jedes Template als Datei existiert |

## 1. Ausgangslage

| Gruppe | Einträge |
|---|---|
| CT2 bietet es an: `done` (alle genannten Komponenten und Templates gefunden) | 185 |
| fehlt, nur CT1 | 53 |
| fehlt, nur JCT | 67 |
| fehlt, nur CTO | 18 |
| fehlt, in mehreren Programmen (CT1+JCT 12, CT1+CTO 5, CT1+JCT+CTO 1, JCT+CTO 1) | 19 |
| **offen insgesamt** | **157** |

Die offenen Einträge sind 123 Funktionen, 15 Visualisierungen, 11 Analysen und 8 Angriffe. Viele sind Varianten („IDEA", „IDEA Angriff"), die echten Bausteine sind weniger.
Vier der 185 erledigten Einträge nennen Primzahl-Tutorials oder den Wizard-Eintrag „Heartbleed Test"; diese Teile sind in der Alias-Datei als „nicht im Browser" geführt (ICrypTutorial bzw. nur im Wizard).

Hinweis: Die erste Fassung dieses Dokuments hatte die CT1-Spalte falsch gelesen (leer) und 54 „nicht markierte" Einträge vermutet. Die Spalten zeigen grüne/rote Icons; mit deren Auswertung ist die CT1-Zuordnung korrekt (111 Einträge in CT1).

## Stand (2026-11-19)

| Stufe | Ergebnis |
|---|---|
| 0 Inventur | fertig: `reference/functions.json`, Test `functions` |
| 1 Templates aus vorhandenen Komponenten | fertig |
| 2 neue Komponenten ohne eigene Oberfläche | **fertig, soweit ohne Tabellen aus den Spezifikationen und ohne Oberfläche möglich** (siehe unten) |
| 3 Visualisierungen, Spiele | **fertig**: alle Visualisierungen und Lernspiele der Funktionsliste sind abgedeckt (Plugins `CipherVisualization`, `MathVisualization`, `ProtocolVisualization`, `RandomVisualization`, `NumberShark`, `DivisorGame`, `Sudoku`, `AntColony`, `PatternLock`, `Kleptography`; Templates unter `Cryptography/`, `Protocols/`, `Tools/` und `Games/`) |

Abdeckung: 280 von 342 Einträgen (62 offen). Neue Plugins in `CrypPlugins/` (alle mit Testvektoren oder Gegenproben im Test `kat`):

| Plugin | Inhalt |
|---|---|
| `BlockCiphers` | IDEA, RC5, RC6, Serpent, DESX, Shacal-1, Shacal-2 (Shacal gegen SHA-1/SHA-256 geprüft) |
| `MessageDigests` | MD2, MD4 |
| `HmacHashes` | HMAC mit MD2 bis Whirlpool und SM3 (16 Hashfunktionen) |
| `CipherMAC` | CMAC und CBC-MAC mit AES, DES, Camellia, IDEA, RC2, RC5, RC6, Serpent, Twofish, Shacal |
| `RandomnessTests` | FIPS 140-1, Poker, Serial, Runs |
| `ShannonEntropy` | Shannon-Entropie |
| `DigitalSignatures` | RSA, RSA-PSS, DSA, ECDSA, ECNR |
| `HashBasedSignatures` | LMS (Merkle-Baum mit Winternitz), SPHINCS+ |
| `PublicKeyEncryption` | ElGamal, Rabin, Merkle-Hellman-Rucksack, ECIES |
| `ClassicMcEliece` | Classic McEliece (McEliece/Niederreiter-KEM) |
| `SecretSharing` | Shamir und Feldman (VSS) |
| `NumberTheory` | Square and Multiply, Baby-Step Giant-Step, Chinesischer Restsatz |
| `DataTools` | UU-Codierung, Zip/Unzip, byteweise Addition, Bit-Verschiebungen |
| `ClassicVariants` | Porta, Rail Fence/Redefence, Kamasutra, Doppelkasten |
| `CipherVisualization` | Caesar, Vigenère, Nihilist mit Präsentation (Stufe 3; Präsentationen sind reiner C#-Code aus WPF-Elementen, ohne XAML) |
| `MathVisualization` | RSA mit allen Zwischenwerten, Shamir-Polynom als Punktdiagramm, Punkte und Addition auf einer elliptischen Kurve über Fp |
| `ProtocolVisualization` | Diffie-Hellman (mehrere Sitzungen, Perfect Forward Secrecy, Lauscher probiert), digitale Signatur (Hash, RSA, Prüfung einer veränderten Nachricht), Authentifizierung im Netz (Passwort-Hash, Challenge-Response mit HMAC, Wiedereinspielung) und S/MIME (RSA-Signatur, AES-Sitzungsschlüssel, Umschlag) als Nachrichtenfolge |
| `NumberShark` | Zahlenhai-Spiel mit getesteter Spiellogik (Punktesumme n(n+1)/2) |
| `DivisorGame` | Teilerspiel (Chomp auf den Teilern); der Computer durchsucht alle Stellungen, der Test prüft, dass der Anziehende für 2 bis 120 gewinnt |
| `Sudoku` | Zudo-Ku: Rätsel mit eindeutiger Lösung (Löser prüft die Eindeutigkeit), reproduzierbar je Rätselnummer |
| `AntColony` | Ameisenalgorithmus (Ant System von Dorigo u. a.) für das Problem des Handlungsreisenden, gegen Brute Force geprüft |
| `PatternLock` | Android-Entsperrmuster: Regeln, die 389112 gültigen Muster (nach Längen geprüft), SHA-1-Wert und Angriff durch Probieren |
| `Kleptography` | SETUP-Angriff auf die RSA-Schlüsselerzeugung (Young/Yung): der Startwert von p, mit dem Schlüssel des Angreifers verschlüsselt, steckt in den oberen Bits von n; der Angreifer liest p und q aus. Vier Methoden einstellbar: ehrliche Erzeugung, SETUP in den oberen Bits, SETUP in den unteren Bits, symmetrische Hintertür mit gemeinsamem Pad; mit kleinen Zahlen, Templates unter `Games/Kleptography*` |
| `RandomVisualization` | Bitbild, Histogramm und Paar-Diagramm aufeinanderfolgender Bytes, Mittelwert, Chi-Quadrat und Korrelation |

Was von den 62 offenen Einträgen bleibt und warum:

| Gruppe | Einträge | Grund |
|---|---|---|
| Analysen und Angriffe | Hill, Vitányi, Periodizität, N-Gramm, Viterbi, byteweise Addition, Fleissner, DESX/DESL/DESXL; Angriffe auf IDEA, RC2, RC4, RC6, Serpent, Twofish, MARS | eigene Analysekomponenten bzw. Anbindung der neuen Chiffren an den KeySearcher (Steuerverbindung) |
| Algorithmen, deren Tabellen oder Spezifikation fehlen | MARS, Misty, SAFER+/++, Dragon, DHA, FORK, TwoTrack, DESL/DESXL, VSH, MD, GMSS/CMSS, GQ, McEliece-Signatur, Multivariate, Redactable Signatures, Homomorphe Verschlüsselung (RSA/Gentry-Halevi), ML-KEM | S-Boxen und Konstanten lassen sich nicht aus dem Gedächtnis zuverlässig nachbauen; ML-KEM fehlt in der BouncyCastle-Version des Repos |
| Infrastruktur, im Browser nicht sinnvoll | OpenSSL, Web-Terminal, TLS-Handshake, PKI, Zertifikate prüfen, Signatur extrahieren, Schlüssel erzeugen/importieren/anzeigen, ASN.1, Zwei-Faktor, RSA-AES, PBES2, CTO-Werkzeuge (Barcode, ASCII, Corona, Chiffre-Identifizierung) | Schlüsselverwaltung/Netz/Dateisystem; teils nur über eigene Komponenten |

Zuordnungen, die eine Näherung sind und geprüft werden sollten: ECC-AES wird durch ECIES gedeckt, McEliece und Niederreiter durch Classic McEliece, Shacal durch Shacal-2, Enigma-Visualisierung durch die Enigma-Komponente mit Präsentation, Hash-Algorithmen-Visualisierung durch die MD5-Präsentation.

## 2. Lizenz

- CrypTool 2 ist Apache-2.0.
- CT1 ist **GPL-2.0**, JCT (`core`, `crypto`) ist **EPL-1.0**.

Entschieden: Die Liste ist nur der Aufhänger. Es wird **kein Code** aus CT1 oder JCT gelesen oder übernommen; die Algorithmen werden aus Standards und Papieren (FIPS, RFC, Originalarbeiten) und mit BouncyCastle in CT2-Stil neu implementiert. Die Algorithmen selbst sind nicht geschützt, nur die jeweilige Implementierung.

## 3. Kernidee: eine Funktionsliste als Quelle der Wahrheit

Nicht pro Funktion einzeln planen, sondern eine Datei `functions.json` pflegen (aus der Seite erzeugt, siehe `reference/function-list-gaps.tsv`):

```
{ "name": "IDEA", "kind": "cipher|analysis|visualization|protocol|tool|game",
  "source": ["CT1","JCT"], "crypweb": { "status": "done|template|component|visual|oos",
  "template": "Cryptography/Modern/IDEA.cwm", "component": "IDEA" } }
```

- Der Test `functions` in `CrypWeb.Tests` prüft: jeder Eintrag ist `done` oder hat Template/Komponente, die es wirklich gibt und die läuft. So ist „Parität" messbar, wie bisher bei den 281 Templates.
- Eine Seite „Funktionen" im Start-Center (reine Liste mit Suche, Filter nach Art, jeder Eintrag öffnet sein Template) deckt die Liste 1:1 ab. Keine Nachbildung der CT1/JCT-Perspektiven: Die Dokument- und die Algorithmen-Perspektive sind nur andere Einstiege in dieselben Funktionen, im Browser reicht das Workspace-Modell.
- Das Command-Palette-Feature findet die Einträge ebenfalls.

## 4. Vorgehen in Stufen (nach Aufwand, nicht nach Alphabet)

**Stufe 0 – Inventur (fertig).** Die Lücke ist 157 Einträge groß. Offen bleibt, ob einzelne davon schon als CT2-Template unter anderem Namen existieren (z. B. „Skytale / Gartenzaun", „Hill-Chiffre", „Vigenère"-Visualisierung); das wird zu Beginn jeder Stufe je Eintrag geprüft und der Eintrag dann in `functions.json`-Alias oder Zuordnung nachgetragen.

**Stufe 1 – nur Templates, kein neuer Code.** Alles, was sich mit vorhandenen Komponenten bauen lässt: Klassik-Varianten aus CTO (Atbash, Beaufort, Porta, Gronsfeld, Trithemius, Rotation, Multiplikation, Railfence/Redefence, Skytale, Bit-Shift, Base64, ASCII, UU), Analyse-Templates für vorhandene Chiffren (DESX-/DESL-Angriffe, Vernam, Hill, Periodizität, Viterbi), Häufigkeit/Histogramm/Hash einer Datei. Hier lohnt eine Prüfung, was die Komponenten schon können (z. B. ob Caesar und Vigenère die Varianten mit Parametern abdecken).

**Stufe 2 – neue Komponenten ohne UI (mittel, hoher Ertrag).** Reine Algorithmen mit BouncyCastle oder Eigenbau, gleiche Plugin-Schnittstelle wie bisher, damit sie auch in CT2 nutzbar sind:
- Blockchiffren: IDEA, MARS, RC5, RC6, SAFER+/++, Serpent, Shacal, Misty, DESX/DESL/DESXL (je mit MAC-Variante über eine gemeinsame CMAC-Komponente statt 15 einzelner).
- Stromchiffren/PRNG: Dragon, LKG, IKG, BBS, x² mod N, EC-PRNG; Zufallstests FIPS-140-1, Poker, Runs, Serial.
- Hashes: MD2, MD4, RIPEMD-160, Tiger, VSH.
- Public Key: ElGamal, DSA, ECDSA, ECNR, ECIES, GQ, Rabin, McEliece, Niederreiter, Winternitz, Merkle, CMSS/GMSS, SPHINCS(+), Multivariate, HE, PBES2, Schlüsselaustausch mehrparteilich.
- Mathematik: EC-Berechnungen, binäre Exponentiation, Shanks BSGS, CRT, reziproke Subtraktion, Länge einer Zahl, Teilerspiel.
- Daten: Entropie, Zip/Unzip, ASN.1, Signatur erzeugen/prüfen/extrahieren, Zertifikate prüfen.
Pro Gruppe ein eigener Commit und ein Test mit bekannten Testvektoren.

**Stufe 3 – Visualisierungen (groß).** Die JCT-Visualisierungen (RSA, DES, ECC/ECDH, Hashing, SSL, Zero-Knowledge, Shamir/VSS, Zufall, Signaturen, Side-Channel DPA/SPA, Kleptographie, Rainbow Tables, ACO, Android-Muster, Huffman-Fehlerkorrektur u. a.) bekommen jeweils eine Komponente plus Präsentation in der bestehenden `CrypWeb.Wpf`-Teilmenge, wie schon die CT2-Visualisierungen. Reihenfolge nach Nutzen für Lehre: RSA, ECC, Hash, DES, Shamir, Zufall, Signatur, danach der Rest. Die Spiele (Zahlenhai, Zudo-Ku, Teilerspiel) ganz ans Ende.

*Stand: fertig.* Zuletzt kamen die Authentifizierung im Netz, S/MIME, die drei Spiele, der Ameisenalgorithmus und das Android-Muster und die Kleptographie hinzu. Offen sind jetzt nur noch Analysen und Angriffe (eigene Komponenten bzw. KeySearcher-Anbindung), Algorithmen ohne verfügbare Tabellen und Infrastruktur, also Einträge, die nicht zur Stufe 3 gehören.

**Stufe 4 – CTO-Werkzeuge.** Klären, ob der Quellcode von CrypTool-Online verfügbar und kompatibel lizenziert ist (nicht geprüft). Wenn ja, ist ein Link/Einbetten aus dem Funktions-Index die billigste Lösung; sonst Stufe 1 oder 2.

**Nicht umsetzbar oder ohne Sinn im Browser (vorschlagen, nicht still weglassen):** OpenSSL, Web-Terminal, S/MIME mit echtem Mailversand, TLS-Handshake gegen echte Server (nur als Simulation), JCTCA/PKI-Server. Das entspricht der bisherigen Linie (Wizard, Hilfe, Cloud sind ausgenommen).

## 5. Reihenfolge und Messung

1. Stufe 0 zuerst, weil sie die Größe der Aufgabe festlegt.
2. Stufe 1 und 2 parallel möglich (verschiedene Dateien), Stufe 3 erst danach.
3. Fortschritt = Anteil `done` in `functions.json`, ausgegeben vom Test, im README unter den bisherigen Paritätszahlen.

## 6. Offene Fragen

- Sollen die neuen Komponenten **nur in CrypWeb** liegen oder in `CrypPlugins/` (dann auch für CT2 nutzbar, was zur Code-Spende passt)? Ich empfehle `CrypPlugins/`.
- Hängen die neuen Einträge unter „View › Experimental plugins" oder sofort sichtbar?

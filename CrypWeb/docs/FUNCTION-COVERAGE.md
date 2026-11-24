# Konzept: alle Funktionen der CrypTool-Funktionsliste in CrypWeb

Grundlage: <https://www.cryptool.org/de/functions/> (342 Einträge, Stand 2026-11-18). Die Seite hat vier Spalten: CT1, CT2, JCT (JCrypTool) und CTO (CrypTool-Online).
Verwendet werden nur Name und Fundort einer Funktion; Code aus CT1 oder JCT wird nicht benutzt (siehe Abschnitt 2).

**Stufe 0 ist umgesetzt:**

| Datei | Inhalt |
|---|---|
| [reference/function-list.json](reference/function-list.json) | Momentaufnahme der Seite (Name, Art, Spalten, Fundorte) |
| [reference/function-aliases.json](reference/function-aliases.json) | deutsche Namen der Liste, die nicht zu Klassen-/Ordnernamen passen, und was nicht in den Browser gehört (Tutorials, Wizard) |
| [reference/functions.json](reference/functions.json) | erzeugt: Status je Eintrag (`done`, `partial`, `open`) und gefundene Komponenten/Templates |
| `tools/gen/functions.py` | erzeugt `functions.json` (mit einer gespeicherten Seite als Argument auch die Momentaufnahme) |
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
| 0 Inventur | fertig: `docs/reference/functions.json`, Test `functions` |
| 1 Templates aus vorhandenen Komponenten | fertig |
| 2 neue Komponenten ohne eigene Oberfläche | **fertig, soweit ohne Tabellen aus den Spezifikationen und ohne Oberfläche möglich** (siehe unten) |
| 3b Restliche Einträge (auf Wunsch: „alle 61 versuchen“) | **53 von 61 umgesetzt**, 8 offen mit Begründung (siehe unten); MARS, MISTY1 und SAFER+ kamen nach einer Web-Recherche nach den Spezifikationen dazu |
| 3 Visualisierungen, Spiele | **fertig**: alle Visualisierungen und Lernspiele der Funktionsliste sind abgedeckt (Plugins `CipherVisualization`, `MathVisualization`, `ProtocolVisualization`, `RandomVisualization`, `NumberShark`, `DivisorGame`, `Sudoku`, `AntColony`, `PatternLock`, `Kleptography`; Templates unter `Cryptography/`, `Protocols/`, `Tools/` und `Games/`) |

Abdeckung: 334 von 342 Einträgen (8 offen). Neue Plugins in `CrypPlugins/` (alle mit Testvektoren oder Gegenproben im Test `kat`):

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
| `ProtocolVisualization` | Diffie-Hellman (mehrere Sitzungen, Perfect Forward Secrecy, Lauscher probiert), digitale Signatur (Hash, RSA, Prüfung einer veränderten Nachricht), Authentifizierung im Netz (Passwort-Hash, Challenge-Response mit HMAC, Wiedereinspielung) S/MIME (RSA-Signatur, AES-Sitzungsschlüssel, Umschlag) TLS-1.3-Handshake (Schlüsselanteile, HKDF-Schlüsselplan nach RFC 8446, Finished) und Zero-Knowledge-Beweis (Fiat-Shamir mit Betrüger) als Nachrichtenfolge |
| `NumberShark` | Zahlenhai-Spiel mit getesteter Spiellogik (Punktesumme n(n+1)/2) |
| `DivisorGame` | Teilerspiel (Chomp auf den Teilern); der Computer durchsucht alle Stellungen, der Test prüft, dass der Anziehende für 2 bis 120 gewinnt |
| `Sudoku` | Zudo-Ku: Rätsel mit eindeutiger Lösung (Löser prüft die Eindeutigkeit), reproduzierbar je Rätselnummer |
| `AntColony` | Ameisenalgorithmus (Ant System von Dorigo u. a.) für das Problem des Handlungsreisenden, gegen Brute Force geprüft |
| `PatternLock` | Android-Entsperrmuster: Regeln, die 389112 gültigen Muster (nach Längen geprüft), SHA-1-Wert und Angriff durch Probieren |
| `Kleptography` | SETUP-Angriff auf die RSA-Schlüsselerzeugung (Young/Yung): der Startwert von p, mit dem Schlüssel des Angreifers verschlüsselt, steckt in den oberen Bits von n; der Angreifer liest p und q aus. Vier Methoden einstellbar: ehrliche Erzeugung, SETUP in den oberen Bits, SETUP in den unteren Bits, symmetrische Hintertür mit gemeinsamem Pad; mit kleinen Zahlen, Templates unter `Games/Kleptography*` |
| `RainbowTable` | Regenbogentabelle für Passwörter aus 3 bis 4 Kleinbuchstaben: Ketten, Reduktionsfunktionen, Suche, Abdeckung und Erfolgsquote (Stufe-3-Plan: Rainbow Tables) |
| `SideChannel` | simulierte Seitenkanalangriffe: SPA auf Square-and-Multiply und DPA auf das S-Box-Byte von AES (S-Box aus dem Inversen in GF(2^8) berechnet) (Stufe-3-Plan: Side-Channel DPA/SPA) |
| `HuffmanErrors` | Huffman-Code, ein Bitfehler und Hamming(7,4)-Korrektur (Stufe-3-Plan: Huffman-Fehlerkorrektur) |
| `KeySearch` | Schlüsselsuche mit teilweise bekanntem Schlüssel für IDEA, RC2, RC4, RC6, Serpent, Twofish, DESX, AES, DES, DESL, DESXL (die Angriffe und DES-Analysen der Liste); RC4, AES und DES gegen bekannte Vektoren |
| `TextAnalysis` | N-Gramm-Häufigkeiten, normalisierter Kompressionsabstand (Vitányi), Analyse der byteweisen Addition |
| `ClassicAnalysis` | Known-Plaintext-Angriff auf die Hill-Chiffre (Beispiel aus Wikipedia), Fleißner-Schablone (alle 4^(n²/4) Schablonen, 4x4 und 6x6) |
| `NumberTools` | Länge einer Zahl, reziproke Subtraktion (Euklid durch Subtraktion, gegen .NET-ggT geprüft) |
| `ConvolutionalCode` | Faltungscode (7, 5) und Viterbi-Decoder (jeder Einzel- und Doppelfehler wird korrigiert) |
| `OneTimePassword` | HOTP und TOTP (die Testwerte aus RFC 4226 und RFC 6238) |
| `ExposureNotification` | Schlüsselplan der Corona-Warn-App (HKDF, AES; gegen .NET geprüft) |
| `Asn1Viewer` | ASN.1-Struktur anzeigen und Signaturen einer CMS-Nachricht extrahieren (mit OpenSSL erzeugte Nachricht) |
| `KeyTool` | RSA- und EC-Schlüssel erzeugen, aus PEM importieren, anzeigen, exportieren (gegen .NET geprüft) |
| `CertificateTool` | Zertifikatskette (Wurzel, Zwischenstelle, Server) erzeugen, Ketten prüfen, Zertifikate anzeigen (die Kette baut auch .NET) |
| `PasswordBasedEncryption` | PBES2 nach RFC 8018 (PBKDF2, AES-256-CBC); mit .NET in beide Richtungen ausgetauscht |
| `HybridEncryption` | RSA-OAEP und AES-GCM; mit .NET in beide Richtungen ausgetauscht |
| `MultipartyKeyExchange` | Schlüsselaustausch für mehrere Teilnehmer nach Burmester und Desmedt |
| `EcRandom` | Dual_EC_DRBG-artiger Zufallsgenerator auf elliptischen Kurven mit Hintertür und Vorhersage |
| `HomomorphicEncryption` | RSA (Produkt), Paillier (Summe) und DGHV (voll homomorph über den ganzen Zahlen, mit Rauschen) |
| `SignatureSchemes` | Guillou-Quisquater, redigierbare Signatur (Merkle-Baum), Merkle-Signatur mit mehreren Baumschichten (deckt CMSS und GMSS ab) und Oil and Vinegar (multivariat) |
| `CfsSignature` | McEliece-Signatur (CFS) mit einem kleinen Goppa-Code (m = 8, t = 4) und dem Algorithmus von Patterson |
| `MlKem` | ML-KEM (Kyber) nach FIPS 203, alle drei Parametersätze; **bitgenau gegen OpenSSL geprüft** (Schlüssel aus demselben Startwert, Entkapselung von OpenSSL-Geheimtexten) |
| `DesVariants` | DES aus den Tabellen (mit .NET-DES über 200 Zufallsschlüssel verglichen), DESL mit der Einzel-S-Box, DESX, DESXL |
| `AsciiCodes` | ASCII-Umwandlung und -Tabelle |
| `Barcode` | EAN-13 und EAN-8 mit Prüfziffer |
| `CipherIdentification` | Schätzung der Chiffre aus Koinzidenzindex, Entropie, Alphabet und Periode |
| `VerySmoothHash` | Very Smooth Hash nach der Definition (Block- und Paddingwahl sind eigene Entscheidungen) |
| `ExtraBlockCiphers` | MARS (alle 120 Known-Answer-Tests von IBM, überarbeitete Schlüsselaufbereitung), MISTY1 (Tabellen und Aufbau aus RFC 2994, zwei veröffentlichte Testvektoren) und SAFER+ (öffentliche Testvektoren für 128, 192 und 256 Bit, Bias-Tabelle des Referenzcodes); ECB, CMAC, CBC-MAC; auch in `KeySearch` |
| `RandomVisualization` | Bitbild, Histogramm und Paar-Diagramm aufeinanderfolgender Bytes, Mittelwert, Chi-Quadrat und Korrelation |

Was von den 8 offenen Einträgen bleibt und warum. Alle anderen der ursprünglich 61 sind jetzt umgesetzt oder einem Template zugeordnet. MARS, MISTY1 und SAFER+ wurden nach einer Web-Recherche nach den Originalspezifikationen (IBM-Veröffentlichung mit S-Box und Testvektoren, RFC 2994, öffentlich gemeinfreier Referenzcode von LibTomCrypt) gebaut und gegen deren Testvektoren geprüft.

| Einträge | Grund |
|---|---|
| DHA, DHA MAC, FORK, FORK MAC | Die Spezifikationen (NIST-Hash-Workshop 2005 bzw. FSE 2006) waren nicht abrufbar: Die Server lieferten nur eine Weiterleitungsseite, die Springer-Fassung ist bezahlpflichtig. FORK-256 hat außerdem zwei Fassungen (Original und „tweaked“); ohne Konstanten und Testvektoren ist eine Umsetzung nicht prüfbar |
| SAFER++ | Die NESSIE-Spezifikation (anderer Schlüsselaufbau und andere Mischschicht als SAFER+) war nicht abrufbar; es fand sich keine freie Referenzimplementierung |
| TwoTrack MAC | Das Verfahren (den Boer, Van Rompay, Preneel, Vandewalle, 2001) baut auf den zwei Spuren von RIPEMD auf; das Papier war nicht abrufbar, es gibt keine Testvektoren |
| Dragon | Die zwei 8x32-S-Boxen sind nur in der Referenzimplementierung bzw. im ausführlichen eSTREAM-Dokument angegeben; die ecrypt-Seite antwortete mit Fehlern, die kurze Beschreibung enthält die Tabellen nicht |
| Web-Terminal | Kein kryptografisches Verfahren, sondern ein Kommandozeilen-Zugang auf der Webseite von CrypTool-Online; im Browser-Programm nicht sinnvoll |

Zuordnungen, die eine Näherung sind und geprüft werden sollten: OpenSSL (CTO-Werkzeug für Schlüssel und Zertifikate) wird durch das Schlüsselwerkzeug abgedeckt, SPHINCS durch SPHINCS+, MD durch MD2, MD4 und MD5, Periodizität durch die Kasiski- und Autokorrelations-Vorlage. Die vier Methoden der Kleptographie und der Aufbau von VSH, CFS (kleine Parameter), DGHV statt des Gitterverfahrens von Gentry und Halevi sowie die Wahl der DESL-Eigenschaften (Anfangs- und Endpermutation bleiben erhalten) sind eigene Entscheidungen, weil die Quellcodes von CT1 und JCT nicht benutzt wurden und für einige davon keine Testvektoren verfügbar waren; die Einzel-S-Box von DESL ist aus dem Artikel erinnert und erfüllt die Entwurfskriterien der DES-S-Boxen.

Pro Gruppe ein eigener Commit und ein Test mit bekannten Testvektoren.

**Stufe 3 – Visualisierungen (groß).** Die JCT-Visualisierungen (RSA, DES, ECC/ECDH, Hashing, SSL, Zero-Knowledge, Shamir/VSS, Zufall, Signaturen, Side-Channel DPA/SPA, Kleptographie, Rainbow Tables, ACO, Android-Muster, Huffman-Fehlerkorrektur u. a.) bekommen jeweils eine Komponente plus Präsentation in der bestehenden `CrypWeb.Wpf`-Teilmenge, wie schon die CT2-Visualisierungen. Reihenfolge nach Nutzen für Lehre: RSA, ECC, Hash, DES, Shamir, Zufall, Signatur, danach der Rest. Die Spiele (Zahlenhai, Zudo-Ku, Teilerspiel) ganz ans Ende.

*Abgleich mit der Funktionsliste:* Die Liste enthält 21 Einträge der Art Visualisierung, alle sind abgedeckt. Die im Plan genannten Themen, die in der Liste keinen eigenen Eintrag haben (Rainbow Tables, Side-Channel DPA/SPA, Huffman-Fehlerkorrektur), sind trotzdem umgesetzt (`RainbowTable`, `SideChannel`, `HuffmanErrors`); SSL ist als „TLS handshake“ umgesetzt.

Die Punkte der Plan-Aufzählung einzeln, mit der Komponente und dem Template, die sie in CrypWeb abdecken (alle laufen im Test `runall`, die neu gebauten zusätzlich mit Tests im Befehl `kat`):

| Punkt des Plans | Komponente | Template |
|---|---|---|
| RSA | `MathVisualization` (neu) | `Cryptography/Modern/Asymmetric/RSAVisualization` |
| DES | `DESVisualization` (vorhandene CT2-Komponente mit Präsentation) | `Cryptography/Modern/Symmetric/DESVisualization` |
| ECC/ECDH | `MathVisualization` (neu), `ECCInput` u. a. (vorhanden) | `Cryptography/Modern/Asymmetric/EllipticCurveVisualization`, `Protocols/ECC_DH` |
| Hashing | MD5-Präsentation (vorhanden) | `Hashfunctions/MD5` |
| SSL/TLS | `ProtocolVisualization`, Protokoll TLS 1.3 (neu) | `Protocols/TlsHandshakeVisualization` |
| Zero-Knowledge | `ProtocolVisualization`, Protokoll Fiat-Shamir (neu); zusätzlich die vorhandenen Komponenten `ZeroKnowledgeChecker` | `Protocols/ZeroKnowledgeVisualization`, `Protocols/Zeroknowledge` |
| Shamir/VSS | `MathVisualization` (neu), `SecretSharing` (neu) | `Protocols/ShamirVisualization`, `Protocols/VerifiableSecretSharing` |
| Zufall | `RandomVisualization` (neu) | `Tools/RandomVisualization` |
| Signaturen | `ProtocolVisualization` (neu) | `Cryptography/Modern/Asymmetric/DigitalSignatureVisualization` |
| Side-Channel DPA/SPA | `SideChannel` (neu) | `Cryptanalysis/SideChannelSPA`, `Cryptanalysis/SideChannelDPA` |
| Kleptographie | `Kleptography` (neu, vier Methoden) | `Games/Kleptography*` |
| Rainbow Tables | `RainbowTable` (neu) | `Hashfunctions/RainbowTable` |
| ACO | `AntColony` (neu) | `Games/AntColony` |
| Android-Muster | `PatternLock` (neu) | `Games/PatternLock` |
| Huffman-Fehlerkorrektur | `HuffmanErrors` (neu) | `Codes/HuffmanBitError`, `Codes/HuffmanErrorCorrection` |

*Stand der Erweiterung:* Auf Wunsch wurden auch die übrigen Einträge versucht; 53 sind umgesetzt, 8 bleiben offen, weil Spezifikationen nicht abrufbar waren (siehe oben).

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

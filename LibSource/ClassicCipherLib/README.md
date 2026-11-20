# ClassicCipherLib

The algorithms of the classic ciphers that have their own components in `CrypPlugins` (Quagmire, Portax, Slidefair, Gromark, Phillips, Ragbaby, AMSCO, Cadenus, Nicodemus,
Myszkowski, Morbit, Pollux, Digrafid, Tri-Square, Checkerboard, Polybius, tap code, ... and the hand ciphers of the German forces). The classes know nothing about
plugins: a method takes the text, the keys and the options and returns the result, or throws an `ArgumentException` with a message for the user.

| File | Contents |
|---|---|
| `CipherText.cs` | alphabets, keyed alphabets and squares, ranks of key words, Morse code, numbers in a text |
| `PolyalphabeticCiphers.cs` | Quagmire, Portax, Slidefair, progressive and interrupted key, Gromark, Condi, Ragbaby, Phillips, Headlines |
| `SubstitutionCiphers.cs` | numbered key, homophonic, null, checkerboard, Monome-Dinome, Tridigital, Compressocrat, Polybius, tap code |
| `TranspositionCiphers.cs` | Myszkowski, AMSCO, Cadenus, Nicodemus, Nihilist transposition, route, sequence, Swagman, ABC |
| `HandCiphers.cs` | Rasterschlüssel 44, Werftschlüssel (hand ciphers of the German forces; the key tables are inputs) |
| `FractionationCiphers.cs` | CM Bifid, Digrafid, fractionated Morse, Morbit, Pollux, Tri-Square, seriated Playfair |

What is not here, because a component exists already: Vigenère, Variant (Vigenère decryption), running key (Vigenère with a long key), key phrase (Substitution
with a 26-letter destination alphabet), turning grille (Fleißner Grille), columnar transposition (Transposition), Bifid, Trifid, Playfair, Four-square, Porta, rail fence.

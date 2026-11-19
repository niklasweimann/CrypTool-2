using CrypTool.PluginBase.IO;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>
/// Known-answer tests of the components that were added for the CrypTool function list (see FUNCTION-COVERAGE.md):
/// each is run through the plugin registry like on the canvas, with settings and inputs, and the output is compared with
/// the published test vector. Run with <c>dotnet run --project CrypWeb.Tests -- ../Templates kat</c>.
/// </summary>
public static class ComponentTests
{
    static int failed;

    /// <summary>Runs every test; returns false when one failed.</summary>
    public static bool Run()
    {
        failed = 0;
        // IDEA: Lai/Massey, test vector from the reference implementation
        Block("IDEA", 0, "00010002000300040005000600070008", "0000000100020003", "11FBED2B01986DE5");
        // RC5-32/12/16: Rivest, "The RC5 Encryption Algorithm", first test vector
        Block("RC5", 1, "00000000000000000000000000000000", "0000000000000000", "21A5DBEE154B8F6D");
        // RC6-32/20/16: RC6 AES submission, first test vector
        Block("RC6", 2, "00000000000000000000000000000000", "00000000000000000000000000000000", "8FC3A53656B1F778C129DF4E9848A41E");
        // Serpent-128: NESSIE set 1, vector 0
        Block("Serpent", 3, "80000000000000000000000000000000", "00000000000000000000000000000000", "264E5481EFF42A4606ABDA06C0BFDA3D");

        // MD2 and MD4: test suites of RFC 1319 and RFC 1320
        Digest("MD2", 0, "", "8350E5A3E24C153DF2275C9F80692773");
        Digest("MD2", 0, "abc", "DA853B0D3F88D99B30283A69E6DED6BB");
        Digest("MD2", 0, "message digest", "AB4F496BFB2A530B219FF33031FE06B0");
        Digest("MD4", 1, "", "31D6CFE0D16AE931B73C59D7E0C089C0");
        Digest("MD4", 1, "abc", "A448017AAF21D8525FC10AE87AA6729D");
        Digest("MD4", 1, "message digest", "D9130A8164549FE818874806E1C7014B");

        Randomness();
        Signatures();
        PublicKeyEncryption();
        CipherMacs();
        NumberTheory();
        SecretSharing();
        DataTools();
        ClassicVariants();
        HashBasedSignatures();
        ClassicMcEliece();
        HmacHashes();
        CipherVisualization();
        MathVisualization();
        ProtocolVisualization();
        RandomVisualization();
        Games();
        AntsAndPatterns();
        Kleptography();
        RainbowSideChannelHuffman();

        // Shacal is the compression function of SHA used as a block cipher: IV + Encrypt(key = message block, IV) is the hash value of that block.
        // The padded block of the message "abc" is 61 62 63 80 00 ... 00 18.
        var abcBlock = new byte[64];
        abcBlock[0] = 0x61; abcBlock[1] = 0x62; abcBlock[2] = 0x63; abcBlock[3] = 0x80; abcBlock[63] = 0x18;
        Shacal("Shacal-2 (SHA-256 of abc)", 6, abcBlock, "6A09E667BB67AE853C6EF372A54FF53A510E527F9B05688C1F83D9AB5BE0CD19", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
        Shacal("Shacal-1 (SHA-1 of abc)", 5, abcBlock, "67452301EFCDAB8998BADCFE10325476C3D2E1F0", "A9993E364706816ABA3E25717850C26C9CD0D89D");

        // DESX against the DES of .NET: C = K2 xor DES_K(P xor K1)
        {
            var key = Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100F1E2D3C4B5A6978");
            var plain = Convert.FromHexString("4E6F772069732074");
            using var des = System.Security.Cryptography.DES.Create();
            des.Key = key[..8];
            var whitened = plain.Select((b, i) => (byte)(b ^ key[8 + i])).ToArray();
            var expected = des.EncryptEcb(whitened, System.Security.Cryptography.PaddingMode.None).Select((b, i) => (byte)(b ^ key[16 + i])).ToArray();
            Block("DESX", 4, Convert.ToHexString(key), Convert.ToHexString(plain), Convert.ToHexString(expected));
        }

        foreach (var (name, algorithm, key) in new[] { ("IDEA", 0, 16), ("RC5", 1, 16), ("RC6", 2, 24), ("Serpent", 3, 32), ("DESX", 4, 24), ("Shacal-1", 5, 16), ("Shacal-2", 6, 64) })
            RoundTrip(name, algorithm, key);

        Console.WriteLine(failed == 0 ? "all known-answer tests passed" : $"{failed} known-answer tests FAILED");
        return failed == 0;
    }

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) failed++;
    }

    static RunComponent Component(string type, params (string Name, string Value)[] settings)
    {
        var component = PluginRegistry.Create(type) ?? throw new InvalidOperationException($"component {type} is not registered");
        component.ApplySettings(settings.ToDictionary(s => s.Name, s => s.Value));
        return component;
    }

    static byte[] Run(RunComponent component, byte[] data, byte[] key, byte[]? iv = null)
    {
        component.PreExecution();   // like the engine: clears the inputs, so it comes before they are set
        var input = new CStreamWriter();
        input.Write(data, 0, data.Length);
        input.Close();
        component.Set("InputStream", input);
        component.Set("InputKey", key);
        if (iv != null) component.Set("InputIV", iv);
        component.Execute();
        var output = ((ICrypToolStream)component.Get("OutputStream")!).CreateReader().ReadFully();
        component.PostExecution();   // clears the outputs
        return output;
    }

    static void Block(string name, int algorithm, string key, string plain, string expected)
    {
        // ECB, no padding: the vector is a single block
        var cipher = Component("BlockCiphers.BlockCiphers", ("Algorithm", algorithm.ToString()), ("Action", "0"), ("Mode", "0"), ("Padding", "0"));
        var actual = Convert.ToHexString(Run(cipher, Convert.FromHexString(plain), Convert.FromHexString(key)));
        Check(actual == expected, $"{name} known answer ({actual})");
    }

    static bool[] Bits(string text) => text.Select(c => c == '1').ToArray();

    static bool Near(double actual, double expected, double tolerance = 1e-6) => Math.Abs(actual - expected) <= tolerance;

    static void Randomness()
    {
        // chi-square distribution: the usual 5 % critical values, erfc from tables
        Check(Near(RandomnessTests.ChiSquare.PValue(3.841459, 1), 0.05, 1e-5) && Near(RandomnessTests.ChiSquare.PValue(24.99579, 15), 0.05, 1e-5)
              && Near(RandomnessTests.ChiSquare.PValue(124.3421, 100), 0.05, 1e-4), "chi-square p-values at the 5 % critical values (1, 15, 100 degrees of freedom)");
        Check(Near(RandomnessTests.ChiSquare.Erfc(0.5), 0.4795001222) && Near(RandomnessTests.ChiSquare.Erfc(1), 0.1572992070), "erfc(0.5) and erfc(1)");

        // the examples of NIST SP 800-22 (serial test 2.11.4, runs test 2.3.4)
        var serial = RandomnessTests.Statistics.Serial(Bits("0011011101"), 3, minimumBits: 0);
        Check(Near(serial.Statistic, 1.6) && Near(serial.PValue, 0.808792, 1e-5), $"serial test, NIST example: statistic {serial.Statistic}, p-value {serial.PValue:F6}");
        var runs = RandomnessTests.Statistics.Runs(Bits("1001101011"), minimumBits: 0);
        Check(Near(runs.Statistic, 7) && Near(runs.PValue, 0.147232, 1e-5), $"runs test, NIST example: {runs.Statistic} runs, p-value {runs.PValue:F6}");

        // poker test: every 4-bit value equally often gives chi-square 0, one value only gives the maximum
        var even = string.Concat(Enumerable.Repeat(string.Concat(Enumerable.Range(0, 16).Select(v => Convert.ToString(v, 2).PadLeft(4, '0'))), 5));
        var poker = RandomnessTests.Statistics.Poker(Bits(even), 4);
        Check(poker.Passed && Near(poker.Statistic, 0), "poker test: all 16 values equally often passes with chi-square 0");
        Check(!RandomnessTests.Statistics.Poker(Bits(new string('0', 400)), 4).Passed, "poker test: only zeros fails");

        // FIPS 140-1: constant and alternating sequences fail, a sequence from a seeded generator passes
        Check(!RandomnessTests.Statistics.Fips1401(Bits(new string('0', 20000))).Passed, "FIPS 140-1: only zeros fails");
        var alternating = RandomnessTests.Statistics.Fips1401(Bits(string.Concat(Enumerable.Repeat("01", 10000))));
        Check(!alternating.Passed && alternating.Report.Contains("Monobit test: 10000 ones") && alternating.Report.Contains("Monobit test: 10000 ones (required: 9654 < n1 < 10346): passed"), "FIPS 140-1: 0101... passes the monobit test but fails the others");
        var generator = new Random(2024);
        var data = new byte[2500];
        generator.NextBytes(data);
        var fips = RandomnessTests.Statistics.Fips1401(RandomnessTests.Statistics.ToBits(data));
        Check(fips.Passed, "FIPS 140-1: 20000 bits from a seeded generator pass" + (fips.Passed ? "" : "\n" + fips.Report));
        var tooShort = RandomnessTests.Statistics.Fips1401(Bits("0101"));
        Check(!tooShort.Passed, "FIPS 140-1: fewer than 20000 bits are rejected");

        // through the component, like on the canvas
        var component = Component("RandomnessTests.RandomnessTests", ("Test", "0"));
        component.PreExecution();
        component.Set("InputData", data);
        component.Execute();
        Check((bool)component.Get("Passed")! && ((string)component.Get("Report")!).Contains("Long run test"), "component: FIPS 140-1 on the generated bytes");
    }

    static CStreamWriter Stream(byte[] data)
    {
        var stream = new CStreamWriter();
        stream.Write(data, 0, data.Length);
        stream.Close();
        return stream;
    }

    /// <summary>Runs the DigitalSignatures component once; returns its outputs by property name.</summary>
    static Dictionary<string, object?> Sign(IEnumerable<(string Name, string Value)> settings, params (string Name, object Value)[] inputs)
    {
        var component = Component("DigitalSignatures.DigitalSignatures", settings.ToArray());
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var outputs = new[] { "OutputPrivateKey", "OutputPublicKey", "OutputSignature", "Valid" }.ToDictionary(n => n, n => component.Get(n));
        component.PostExecution();
        return outputs;
    }

    static (string, string)[] SigSettings(int action, int algorithm, int hash = 1, int keySize = 0, int curve = 0) =>
        new[] { ("Action", action.ToString()), ("Algorithm", algorithm.ToString()), ("Hash", hash.ToString()), ("KeySize", keySize.ToString()), ("Curve", curve.ToString()) };

    static bool Verify(int algorithm, int hash, byte[] message, byte[] publicKey, byte[] signature) =>
        (bool)Sign(SigSettings(2, algorithm, hash), ("InputMessage", Stream(message)), ("InputPublicKey", publicKey), ("InputSignature", signature))["Valid"]!;

    static void Signatures()
    {
        var message = System.Text.Encoding.ASCII.GetBytes("sample");

        // ECDSA P-256 / SHA-256: the signature of RFC 6979, appendix A.2.5, must verify with its public key
        var curve = Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetByName("secp256r1");
        var point = curve.Curve.CreatePoint(
            new Org.BouncyCastle.Math.BigInteger("60FED4BA255A9D31C961EB74C6356D68C049B8923B61FA6CE669622E60F29FB6", 16),
            new Org.BouncyCastle.Math.BigInteger("7903FE1008B8BC99A41AE9E95628BC64F2F1B20C2D7E9F5177A3C294D4462299", 16));
        var publicKey = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
            new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters("ECDSA", point, new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(curve))).GetDerEncoded();
        var rfcSignature = new Org.BouncyCastle.Asn1.DerSequence(
            new Org.BouncyCastle.Asn1.DerInteger(new Org.BouncyCastle.Math.BigInteger("EFD48B2AACB6A8FD1140DD9CD45E81D69D2C877B56AAF991C34D0EA84EAF3716", 16)),
            new Org.BouncyCastle.Asn1.DerInteger(new Org.BouncyCastle.Math.BigInteger("F7CB1C942D657C41D436C7A1B6E29F65F3E900DBB9AFF4064DC4AB2F843ACDA8", 16))).GetDerEncoded();
        Check(Verify(3, 1, message, publicKey, rfcSignature), "ECDSA: the signature of RFC 6979 A.2.5 (P-256, SHA-256, \"sample\") is valid");
        Check(!Verify(3, 1, System.Text.Encoding.ASCII.GetBytes("Sample"), publicKey, rfcSignature), "ECDSA: the same signature is not valid for another message");

        // every scheme: generate keys, sign, verify, and reject a changed message, a changed signature and a wrong key
        var names = new[] { "RSA PKCS#1 v1.5", "RSA-PSS", "DSA", "ECDSA", "ECNR" };
        for (var algorithm = 0; algorithm < names.Length; algorithm++)
        {
            var keys = Sign(SigSettings(0, algorithm, keySize: 0));
            var privateKey = (byte[])keys["OutputPrivateKey"]!;
            var pub = (byte[])keys["OutputPublicKey"]!;
            var signature = (byte[])Sign(SigSettings(1, algorithm), ("InputMessage", Stream(message)), ("InputPrivateKey", privateKey))["OutputSignature"]!;
            var other = (byte[])Sign(SigSettings(0, algorithm, keySize: 0))["OutputPublicKey"]!;
            var broken = (byte[])signature.Clone();
            broken[^1] ^= 1;
            var ok = Verify(algorithm, 1, message, pub, signature)
                     && !Verify(algorithm, 1, System.Text.Encoding.ASCII.GetBytes("sampl3"), pub, signature)
                     && !Verify(algorithm, 1, message, pub, broken)
                     && !Verify(algorithm, 1, message, other, signature);
            Check(ok, $"{names[algorithm]}: key generation, signing, verifying; changed message, signature and wrong key are rejected");
        }

        // RSA, ECDSA and DSA against the signature code of the .NET runtime (an independent implementation)
        var rsaKeys = Sign(SigSettings(0, 0, keySize: 0));
        using (var rsa = System.Security.Cryptography.RSA.Create())
        {
            rsa.ImportPkcs8PrivateKey((byte[])rsaKeys["OutputPrivateKey"]!, out _);
            var ours = (byte[])Sign(SigSettings(1, 0), ("InputMessage", Stream(message)), ("InputPrivateKey", rsaKeys["OutputPrivateKey"]!))["OutputSignature"]!;
            Check(rsa.VerifyData(message, ours, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1), "RSA PKCS#1 v1.5: our signature verifies in .NET");
            Check(ours.SequenceEqual(rsa.SignData(message, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1)), "RSA PKCS#1 v1.5: our signature is identical to the .NET signature (deterministic scheme)");
            var theirs = rsa.SignData(message, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pss);
            Check(Verify(1, 1, message, (byte[])rsaKeys["OutputPublicKey"]!, theirs), "RSA-PSS: a .NET signature verifies in our component");
            var ourPss = (byte[])Sign(SigSettings(1, 1), ("InputMessage", Stream(message)), ("InputPrivateKey", rsaKeys["OutputPrivateKey"]!))["OutputSignature"]!;
            Check(rsa.VerifyData(message, ourPss, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pss), "RSA-PSS: our signature verifies in .NET");
        }

        var ecKeys = Sign(SigSettings(0, 3, curve: 1));
        using (var ecdsa = System.Security.Cryptography.ECDsa.Create())
        {
            ecdsa.ImportPkcs8PrivateKey((byte[])ecKeys["OutputPrivateKey"]!, out _);
            var ours = (byte[])Sign(SigSettings(1, 3, hash: 2), ("InputMessage", Stream(message)), ("InputPrivateKey", ecKeys["OutputPrivateKey"]!))["OutputSignature"]!;
            Check(ecdsa.VerifyData(message, ours, System.Security.Cryptography.HashAlgorithmName.SHA384, System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence), "ECDSA P-384 / SHA-384: our signature verifies in .NET");
            var theirs = ecdsa.SignData(message, System.Security.Cryptography.HashAlgorithmName.SHA384, System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence);
            Check(Verify(3, 2, message, (byte[])ecKeys["OutputPublicKey"]!, theirs), "ECDSA P-384 / SHA-384: a .NET signature verifies in our component");
        }
    }

    /// <summary>Runs the ElGamal / Rabin component once; returns its outputs by property name (null where nothing was produced).</summary>
    static Dictionary<string, object?> Pke(int action, int algorithm, params (string Name, object Value)[] inputs)
    {
        var component = Component("PublicKeyEncryption.PublicKeyEncryption", ("Action", action.ToString()), ("Algorithm", algorithm.ToString()), ("KeySize", "0"));
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var outputs = new[] { "OutputPrivateKey", "OutputPublicKey", "OutputData" }.ToDictionary(n => n, n => component.Get(n));
        component.PostExecution();
        return outputs;
    }

    static byte[]? Bytes(object? stream) => stream == null ? null : ((ICrypToolStream)stream).CreateReader().ReadFully();

    static System.Numerics.BigInteger Number(byte[] bytes, int offset, int count) =>
        new System.Numerics.BigInteger(bytes.AsSpan(offset, count), isUnsigned: true, isBigEndian: true);

    static System.Numerics.BigInteger[] Integers(byte[] der) =>
        ((Org.BouncyCastle.Asn1.Asn1Sequence)Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(der)).OfType<Org.BouncyCastle.Asn1.DerInteger>()
            .Select(i => System.Numerics.BigInteger.Parse(i.Value.ToString())).ToArray();

    static void PublicKeyEncryption()
    {
        var message = new byte[] { 0, 0, 7 }.Concat(System.Text.Encoding.ASCII.GetBytes("leading zeros survive; a text that is longer than one block of the key: " + new string('x', 200))).ToArray();
        foreach (var (name, algorithm) in new[] { ("ElGamal", 0), ("Rabin", 1), ("Merkle-Hellman knapsack", 2), ("ECIES", 3) })
        {
            var keys = Pke(0, algorithm);
            var privateKey = (byte[])keys["OutputPrivateKey"]!;
            var publicKey = (byte[])keys["OutputPublicKey"]!;
            var ciphertext = Bytes(Pke(1, algorithm, ("InputData", Stream(message)), ("InputPublicKey", publicKey))["OutputData"])!;
            var plain = Bytes(Pke(2, algorithm, ("InputData", Stream(ciphertext)), ("InputPrivateKey", privateKey))["OutputData"]);
            Check(plain != null && plain.SequenceEqual(message), $"{name}: encrypt/decrypt round trip of {message.Length} bytes in {ciphertext.Length} bytes of ciphertext");

            var other = (byte[])Pke(0, algorithm)["OutputPrivateKey"]!;
            Check(Bytes(Pke(2, algorithm, ("InputData", Stream(ciphertext)), ("InputPrivateKey", other))["OutputData"]) == null, $"{name}: decryption with a wrong key is rejected");

            var empty = Bytes(Pke(2, algorithm, ("InputData", Stream(Bytes(Pke(1, algorithm, ("InputData", Stream(Array.Empty<byte>())), ("InputPublicKey", publicKey))["OutputData"])!)), ("InputPrivateKey", privateKey))["OutputData"]);
            Check(empty != null && empty.Length == 0, $"{name}: the empty message");
        }

        // ECIES: the ephemeral point, the tag and the shared secret checked with the ECDH of .NET (an independent implementation)
        {
            var ecies = Pke(0, 3);
            var msg = System.Text.Encoding.ASCII.GetBytes("ECIES encrypts with a key pair of an elliptic curve.");
            var wire = Bytes(Pke(1, 3, ("InputData", Stream(msg)), ("InputPublicKey", ecies["OutputPublicKey"]!))["OutputData"])!;
            const int pointLength = 65, tagLength = 32;
            Check(wire.Length == pointLength + msg.Length + tagLength && wire[0] == 4, "ECIES: ephemeral point (65 bytes, uncompressed), encrypted message of the same length and 32-byte tag");
            using var own = System.Security.Cryptography.ECDiffieHellman.Create();
            own.ImportPkcs8PrivateKey((byte[])ecies["OutputPrivateKey"]!, out _);
            using var ephemeral = System.Security.Cryptography.ECDiffieHellman.Create(new System.Security.Cryptography.ECParameters
            {
                Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
                Q = new System.Security.Cryptography.ECPoint { X = wire[1..33], Y = wire[33..65] }
            });
            var z = own.DeriveRawSecretAgreement(ephemeral.PublicKey);
            var keyBytes = System.Security.Cryptography.SHA256.HashData(z.Concat(new byte[] { 0, 0, 0, 1 }).ToArray())
                .Concat(System.Security.Cryptography.SHA256.HashData(z.Concat(new byte[] { 0, 0, 0, 2 }).ToArray())).ToArray();
            var ct = wire[pointLength..^tagLength];
            var tag = System.Security.Cryptography.HMACSHA256.HashData(keyBytes[16..48], wire[..pointLength].Concat(ct).ToArray());
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = keyBytes[..16];
            var plain = new byte[ct.Length];
            for (var i = 0; i < ct.Length; i += 16)
            {
                var counter = new byte[16];
                counter[15] = (byte)(i / 16);
                var stream = aes.EncryptEcb(counter, System.Security.Cryptography.PaddingMode.None);
                for (var j = 0; j < 16 && i + j < ct.Length; j++) plain[i + j] = (byte)(ct[i + j] ^ stream[j]);
            }
            Check(tag.SequenceEqual(wire[^tagLength..]) && plain.SequenceEqual(msg), "ECIES: the ECDH secret computed by .NET gives the same KDF2 keys: the tag is the HMAC-SHA-256 of point and ciphertext, and AES-CTR decrypts the message");
            var changed = (byte[])wire.Clone();
            changed[pointLength + 3] ^= 1;
            var tamperedPoint = (byte[])wire.Clone();
            tamperedPoint[10] ^= 1;
            var refusedCiphertext = Bytes(Pke(2, 3, ("InputData", Stream(changed)), ("InputPrivateKey", ecies["OutputPrivateKey"]!))["OutputData"]) == null;
            var refusedPoint = Bytes(Pke(2, 3, ("InputData", Stream(tamperedPoint)), ("InputPrivateKey", ecies["OutputPrivateKey"]!))["OutputData"]) == null;
            Check(refusedCiphertext && refusedPoint, "ECIES: a changed ciphertext and a changed ephemeral point (no longer on the curve, or another secret) are refused");
            Check(Bytes(Pke(2, 3, ("InputData", Stream(wire[..40])), ("InputPrivateKey", ecies["OutputPrivateKey"]!))["OutputData"]) == null, "ECIES: a ciphertext that is too short is refused");
        }

        // Merkle-Hellman knapsack: structure of the keys checked with .NET arithmetic, ciphertext of a block computed independently
        {
            var knapsack = Pke(0, 2);
            var privateNumbers = Integers((byte[])knapsack["OutputPrivateKey"]!);   // w_1..w_64, q, r
            var w = privateNumbers.Take(64).ToArray();
            var qk = privateNumbers[64];
            var rk = privateNumbers[65];
            var beta = Integers((byte[])knapsack["OutputPublicKey"]!);
            var superincreasing = Enumerable.Range(1, 63).All(i => w[i] > w.Take(i).Aggregate(System.Numerics.BigInteger.Zero, (a, b) => a + b));
            Check(beta.Length == 64 && superincreasing && qk > w.Aggregate(System.Numerics.BigInteger.Zero, (a, b) => a + b) && System.Numerics.BigInteger.GreatestCommonDivisor(rk, qk) == 1
                  && Enumerable.Range(0, 64).All(i => beta[i] == w[i] * rk % qk), "knapsack: w is superincreasing, q exceeds its sum, gcd(r, q) = 1 and b_i = r * w_i mod q");
            var chunk = System.Text.Encoding.ASCII.GetBytes("abc");
            var knapsackBlock = new byte[8];
            knapsackBlock[0] = 3;
            Array.Copy(chunk, 0, knapsackBlock, 1, 3);
            var sum = Enumerable.Range(0, 64).Where(i => (knapsackBlock[i / 8] >> (7 - i % 8) & 1) == 1).Aggregate(System.Numerics.BigInteger.Zero, (a, i) => a + beta[i]);
            var expectedLength = ((int)beta.Max(x => x.GetBitLength()) + 6 + 7) / 8;
            var expectedCipher = new byte[expectedLength];
            var sumBytes = sum.ToByteArray(isUnsigned: true, isBigEndian: true);
            Array.Copy(sumBytes, 0, expectedCipher, expectedLength - sumBytes.Length, sumBytes.Length);
            var actual = Bytes(Pke(1, 2, ("InputData", Stream(chunk)), ("InputPublicKey", knapsack["OutputPublicKey"]!))["OutputData"]);
            Check(actual != null && actual.SequenceEqual(expectedCipher), "knapsack: the ciphertext of \"abc\" is the sum of the public elements selected by the bits of the block");
        }

        // ElGamal checked with .NET arithmetic: m = c2 * c1^(p-1-x) mod p must be 0x01 followed by the message
        var elGamal = Pke(0, 0);
        var key = Integers((byte[])elGamal["OutputPrivateKey"]!);   // p, g, x
        var short_ = System.Text.Encoding.ASCII.GetBytes("ElGamal");
        var c = Bytes(Pke(1, 0, ("InputData", Stream(short_)), ("InputPublicKey", elGamal["OutputPublicKey"]!))["OutputData"])!;
        var length = c.Length / 2;
        var m = System.Numerics.BigInteger.ModPow(Number(c, 0, length), key[0] - 1 - key[2], key[0]) * Number(c, length, length) % key[0];
        var expected = new byte[] { 1 }.Concat(short_).ToArray();
        Check(m == Number(expected, 0, expected.Length), "ElGamal: c2 * c1^(p-1-x) mod p computed with .NET is the message block");
        Check(Integers((byte[])elGamal["OutputPublicKey"]!)[2] == System.Numerics.BigInteger.ModPow(key[1], key[2], key[0]), "ElGamal: the public key is g^x mod p");

        // Rabin: a ciphertext made here from the block layout (0x01, data, four bytes of SHA-256) is decrypted by the component
        var rabin = Pke(0, 1);
        var factors = Integers((byte[])rabin["OutputPrivateKey"]!);   // p, q
        var n = factors[0] * factors[1];
        var data = System.Text.Encoding.ASCII.GetBytes("Rabin");
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        var block = new byte[] { 1 }.Concat(data).Concat(hash.Take(4)).ToArray();
        var ciphertextNumber = System.Numerics.BigInteger.ModPow(Number(block, 0, block.Length), 2, n);
        var aligned = new byte[(int)((n.GetBitLength() + 7) / 8)];   // as many bytes as the modulus
        var raw = ciphertextNumber.ToByteArray(isUnsigned: true, isBigEndian: true);
        Array.Copy(raw, 0, aligned, aligned.Length - raw.Length, raw.Length);
        var decrypted = Bytes(Pke(2, 1, ("InputData", Stream(aligned)), ("InputPrivateKey", rabin["OutputPrivateKey"]!))["OutputData"]);
        Check(decrypted != null && decrypted.SequenceEqual(data), "Rabin: a ciphertext m^2 mod n computed with .NET is decrypted to the data (one of four roots)");
        Check(factors[0] % 4 == 3 && factors[1] % 4 == 3, "Rabin: both primes are congruent to 3 modulo 4");
    }

    /// <summary>Runs the CipherMAC component (cipher index as in its settings, mode 0 = CMAC, 1 = CBC-MAC); null when it refused the input.</summary>
    static byte[]? Mac(int cipher, int mode, byte[] data, byte[] key)
    {
        var component = Component("CipherMAC.CipherMAC", ("Cipher", cipher.ToString()), ("Mode", mode.ToString()));
        component.PreExecution();
        component.Set("InputData", Stream(data));
        component.Set("Key", key);
        component.Execute();
        var mac = (byte[]?)component.Get("OutputData");
        component.PostExecution();
        return mac;
    }

    static void CipherMacs()
    {
        // CMAC-AES-128, the examples of NIST SP 800-38B (appendix D.1)
        var key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
        var message = Convert.FromHexString("6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411");
        foreach (var (length, expected) in new[] { (0, "BB1D6929E95937287FA37D129B756746"), (16, "070A16B46B4D4144F79BDD9DD04A287C"), (40, "DFA66747DE9AE63030CA32611497C827") })
        {
            var actual = Convert.ToHexString(Mac(0, 0, message.Take(length).ToArray(), key)!);
            Check(actual == expected, $"CMAC-AES-128, {length} bytes: {actual}");
        }

        // CBC-MAC: the last block of the CBC encryption with a zero IV, computed by .NET for AES and DES
        var data = Enumerable.Range(0, 48).Select(i => (byte)(i * 5 + 1)).ToArray();
        using (var aes = System.Security.Cryptography.Aes.Create())
        {
            aes.Key = key;
            var cbc = aes.EncryptCbc(data, new byte[16], System.Security.Cryptography.PaddingMode.None);
            Check(Mac(0, 1, data, key)!.SequenceEqual(cbc[^16..]), "CBC-MAC-AES equals the last CBC block computed by .NET");
        }
        var desKey = System.Text.Encoding.ASCII.GetBytes("12345678");
        using (var des = System.Security.Cryptography.DES.Create())
        {
            des.Key = desKey;
            var cbc = des.EncryptCbc(data, new byte[8], System.Security.Cryptography.PaddingMode.None);
            Check(Mac(1, 1, data, desKey)!.SequenceEqual(cbc[^8..]), "CBC-MAC-DES equals the last CBC block computed by .NET");
        }

        // the other ciphers: CBC-MAC equals the last block of the BlockCiphers component in CBC mode with a zero IV (algorithm index there: IDEA 0, RC5 1, RC6 2, Serpent 3)
        foreach (var (name, macCipher, blockCipher, blockSize, keySize) in new[] { ("IDEA", 3, 0, 8, 16), ("RC5", 5, 1, 8, 16), ("RC6", 6, 2, 16, 16), ("Serpent", 7, 3, 16, 32), ("Shacal-1", 9, 5, 20, 16), ("Shacal-2", 10, 6, 32, 32) })
        {
            var k = Enumerable.Range(1, keySize).Select(i => (byte)(i * 11)).ToArray();
            var aligned = Enumerable.Range(0, 160).Select(i => (byte)(i * 5 + 1)).ToArray();   // 160 bytes are whole blocks of 8, 16, 20 and 32 bytes
            var encrypted = Bytes(Run(Component("BlockCiphers.BlockCiphers", ("Algorithm", blockCipher.ToString()), ("Action", "0"), ("Mode", "1"), ("Padding", "0")), aligned, k, new byte[blockSize])
                                  is var o ? (object)Stream(o) : null)!;
            Check(Mac(macCipher, 1, aligned, k)!.SequenceEqual(encrypted[^blockSize..]), $"CBC-MAC-{name} equals the last CBC block of the BlockCiphers component");
        }

        // properties every MAC must have, for every cipher: a fixed length (one block), the key and the data matter, a wrong key size is refused
        var names = new[] { "AES", "DES", "Camellia", "IDEA", "RC2", "RC5", "RC6", "Serpent", "Twofish", "Shacal-1", "Shacal-2" };
        var sizes = new[] { 16, 8, 16, 16, 16, 16, 16, 16, 16, 16, 16 };
        var blocks = new[] { 16, 8, 16, 8, 8, 8, 16, 16, 16, 20, 32 };
        for (var cipher = 0; cipher < names.Length; cipher++)
        {
            var ok = true;
            foreach (var mode in cipher >= 9 ? new[] { 1 } : new[] { 0, 1 })   // CMAC exists only for 64-bit and 128-bit blocks
            {
                var k = Enumerable.Range(1, sizes[cipher]).Select(i => (byte)(i * 13)).ToArray();
                var other = (byte[])k.Clone();
                other[0] ^= 0x80;
                var d = System.Text.Encoding.ASCII.GetBytes("authenticated message of 38 bytes, ok?");
                var mac = Mac(cipher, mode, d, k);
                var changed = (byte[])d.Clone();
                changed[5] ^= 1;
                ok &= mac != null && mac.Length == blocks[cipher]
                      && mac.SequenceEqual(Mac(cipher, mode, d, k)!)
                      && !mac.SequenceEqual(Mac(cipher, mode, changed, k)!)
                      && !mac.SequenceEqual(Mac(cipher, mode, d, other)!)
                      && Mac(cipher, mode, d, cipher is 4 or 5 ? Array.Empty<byte>() : new byte[sizes[cipher] + (cipher >= 9 ? 60 : 3)]) == null;   // RC2 and RC5 take keys of nearly any length: only the empty key is wrong
            }
            if (cipher >= 9) ok &= Mac(cipher, 0, System.Text.Encoding.ASCII.GetBytes("data"), new byte[16]) == null;   // CMAC is refused for the 160-bit and 256-bit blocks
            Check(ok, $"{names[cipher]}: CMAC and CBC-MAC are {blocks[cipher]} bytes long, depend on key and data, and a wrong key size is refused");
        }
    }

    static (System.Numerics.BigInteger? Output, string? Steps) Nt(int operation, params (string Name, object Value)[] inputs)
    {
        var component = Component("NumberTheory.NumberTheory", ("Operation", operation.ToString()));
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var steps = (string?)component.Get("Steps");
        var output = steps == null ? (System.Numerics.BigInteger?)null : (System.Numerics.BigInteger)component.Get("Output")!;
        component.PostExecution();
        return (output, steps);
    }

    static void NumberTheory()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        // square and multiply: the textbook example 4^13 mod 497 = 445, and 100 random cases against BigInteger.ModPow
        Check(Nt(0, ("Input1", new System.Numerics.BigInteger(4)), ("Input2", new System.Numerics.BigInteger(13)), ("Mod", new System.Numerics.BigInteger(497))).Output == 445, "square and multiply: 4^13 mod 497 = 445");
        var ok = true;
        for (var i = 0; i < 100; i++)
        {
            var bytes = new byte[40];
            rng.GetBytes(bytes);
            var x = new System.Numerics.BigInteger(bytes.AsSpan(0, 16), isUnsigned: true);
            var e = new System.Numerics.BigInteger(bytes.AsSpan(16, 16), isUnsigned: true);
            var n = new System.Numerics.BigInteger(bytes.AsSpan(32, 8), isUnsigned: true) + 2;
            ok &= Nt(0, ("Input1", x), ("Input2", e), ("Mod", n)).Output == System.Numerics.BigInteger.ModPow(x, e, n);
        }
        Check(ok, "square and multiply: 100 random cases equal BigInteger.ModPow");
        var steps = Nt(0, ("Input1", new System.Numerics.BigInteger(4)), ("Input2", new System.Numerics.BigInteger(13)), ("Mod", new System.Numerics.BigInteger(497))).Steps!;
        Check(steps.Contains("1101") && steps.Contains("Result: 445"), "square and multiply: the steps show the exponent 13 = 1101 in binary");

        // baby-step giant-step: 5^6 = 8 (mod 23); 2^8 = 3 (mod 11); a case with no solution; random cases checked by g^e = h
        Check(Nt(1, ("Input1", new System.Numerics.BigInteger(5)), ("Input2", new System.Numerics.BigInteger(8)), ("Mod", new System.Numerics.BigInteger(23))).Output == 6, "baby-step giant-step: 5^e = 8 (mod 23) gives e = 6");
        Check(Nt(1, ("Input1", new System.Numerics.BigInteger(2)), ("Input2", new System.Numerics.BigInteger(3)), ("Mod", new System.Numerics.BigInteger(11))).Output == 8, "baby-step giant-step: 2^e = 3 (mod 11) gives e = 8");
        Check(Nt(1, ("Input1", new System.Numerics.BigInteger(3)), ("Input2", new System.Numerics.BigInteger(2)), ("Mod", new System.Numerics.BigInteger(11))).Steps == null, "baby-step giant-step: 3^e = 2 (mod 11) has no solution and is reported");
        ok = true;
        foreach (var p in new[] { 1000003L, 10000019L, 999999937L })
        {
            for (var i = 0; i < 5; i++)
            {
                var bytes = new byte[8];
                rng.GetBytes(bytes);
                var g = 2 + (long)(BitConverter.ToUInt32(bytes, 0) % (uint)(p - 3));
                var secret = 1 + (long)(BitConverter.ToUInt32(bytes, 4) % (uint)(p - 2));
                var h = System.Numerics.BigInteger.ModPow(g, secret, p);
                var found = Nt(1, ("Input1", new System.Numerics.BigInteger(g)), ("Input2", h), ("Mod", new System.Numerics.BigInteger(p))).Output;
                ok &= found != null && System.Numerics.BigInteger.ModPow(g, found.Value, p) == h && found <= secret;
            }
        }
        Check(ok, "baby-step giant-step: 15 random logarithms modulo primes up to 10^9 satisfy g^e = h and e is not larger than the secret exponent");

        // Chinese remainder theorem: x = 2 (3), 3 (5), 2 (7) gives 23; against brute force
        Check(Nt(2, ("Residues", "2,3,2"), ("Moduli", "3,5,7")).Output == 23, "Chinese remainder theorem: 2 mod 3, 3 mod 5, 2 mod 7 gives 23");
        var brute = Enumerable.Range(0, 11 * 13 * 17).First(x => x % 11 == 4 && x % 13 == 9 && x % 17 == 5);
        Check(Nt(2, ("Residues", "4 9 5"), ("Moduli", "11; 13; 17")).Output == brute, $"Chinese remainder theorem: 4 mod 11, 9 mod 13, 5 mod 17 gives {brute} like brute force");
        Check(Nt(2, ("Residues", "1,2"), ("Moduli", "4,6")).Steps == null, "Chinese remainder theorem: moduli that are not coprime are refused");
    }

    static Dictionary<string, object?> Share(int action, bool verifiable, params (string Name, object Value)[] inputs)
    {
        var component = Component("SecretSharing.SecretSharing", ("Action", action.ToString()), ("Verifiable", verifiable ? "True" : "False"));
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var outputs = new[] { "OutputShares", "OutputCommitments", "OutputSecret", "Valid" }.ToDictionary(n => n, n => component.Get(n));
        component.PostExecution();
        return outputs;
    }

    static string[] Lines(object? text) => ((string?)text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    static void SecretSharing()
    {
        var p = SecretSharingP();
        var q = (p - 1) / 2;
        Check(new Org.BouncyCastle.Math.BigInteger(p.ToString()).IsProbablePrime(40) && new Org.BouncyCastle.Math.BigInteger(q.ToString()).IsProbablePrime(40),
              "secret sharing: P and q = (P-1)/2 are prime (safe prime)");
        Check(System.Numerics.BigInteger.ModPow(4, q, p) == 1, "secret sharing: g = 4 has order dividing q modulo P");

        var secret = System.Numerics.BigInteger.Parse("123456789012345678901234567890123456789");
        foreach (var (k, n) in new[] { (2, 3), (3, 5), (5, 8) })
        {
            var split = Share(0, true, ("Secret", secret), ("Threshold", k), ("Number", n));
            var shares = Lines(split["OutputShares"]).Select(l => l.Split(',')).Select(a => (x: System.Numerics.BigInteger.Parse(a[0]), y: System.Numerics.BigInteger.Parse(a[1]))).ToArray();
            var commitments = Lines(split["OutputCommitments"]).Select(System.Numerics.BigInteger.Parse).ToArray();
            var ok = shares.Length == n && commitments.Length == k && commitments[0] == System.Numerics.BigInteger.ModPow(4, secret, p);

            // reconstruct from consecutive groups of k shares: every group gives the secret, k-1 shares do not
            for (var start = 0; start + k <= n; start++)
            {
                var text = string.Join("\n", shares.Skip(start).Take(k).Select(s => $"{s.x},{s.y}"));
                ok &= (System.Numerics.BigInteger)Share(1, false, ("InputShares", text))["OutputSecret"]! == secret;
            }
            var tooFew = string.Join("\n", shares.Take(k - 1).Select(s => $"{s.x},{s.y}"));
            ok &= k == 1 || (System.Numerics.BigInteger)Share(1, false, ("InputShares", tooFew))["OutputSecret"]! != secret;
            Check(ok, $"secret sharing ({k} of {n}): any {k} shares give the secret, {k - 1} do not; commitments: {commitments.Length}");

            // verification: all shares are valid, a changed share and a share of another polynomial are not
            var all = string.Join("\n", shares.Select(s => $"{s.x},{s.y}"));
            var commitmentText = string.Join("\n", commitments);
            var good = (bool)Share(2, true, ("InputShares", all), ("InputCommitments", commitmentText))["Valid"]!;
            var changed = string.Join("\n", shares.Select((s, i) => i == 0 ? $"{s.x},{s.y + 1}" : $"{s.x},{s.y}"));
            var bad = (bool)Share(2, true, ("InputShares", changed), ("InputCommitments", commitmentText))["Valid"]!;
            var otherCommitments = string.Join("\n", Lines(Share(0, true, ("Secret", secret), ("Threshold", k), ("Number", n))["OutputCommitments"]));
            var wrongPolynomial = (bool)Share(2, true, ("InputShares", all), ("InputCommitments", otherCommitments))["Valid"]!;
            Check(good && !bad && !wrongPolynomial, $"verifiable secret sharing ({k} of {n}): shares fit their commitments, a changed share or other commitments are rejected");
        }

        Check(Share(0, false, ("Secret", q + 1), ("Threshold", 2), ("Number", 3))["OutputShares"] == null, "secret sharing: a secret that is not smaller than q is refused");
        Check(Share(1, false, ("InputShares", "1,5\n1,7"))["OutputSecret"] is System.Numerics.BigInteger zero && zero.IsZero, "secret sharing: shares with equal x values are refused");
    }

    static System.Numerics.BigInteger SecretSharingP()
    {
        // the safe prime of RFC 2409 (group 2), read from the component's own constant through its public static field
        return (System.Numerics.BigInteger)typeof(global::SecretSharing.Sharing).GetField("P")!.GetValue(null)!;
    }

    static byte[]? Tool(int operation, byte[] data, byte[]? key = null, int amount = 1)
    {
        var component = Component("DataTools.DataTools", ("Operation", operation.ToString()), ("Amount", amount.ToString()));
        component.PreExecution();
        component.Set("InputData", Stream(data));
        if (key != null) component.Set("Key", key);
        component.Execute();
        var output = Bytes(component.Get("OutputData"));
        component.PostExecution();
        return output;
    }

    static void DataTools()
    {
        var ascii = System.Text.Encoding.ASCII;
        // uuencode: "Cat" is the example of the Wikipedia article ("#0V%T")
        Check(ascii.GetString(Tool(0, ascii.GetBytes("Cat"))!) == "begin 644 data\n#0V%T\n`\nend\n", "UU encode: \"Cat\" gives #0V%T");
        Check(ascii.GetString(Tool(1, ascii.GetBytes("#0V%T"))!) == "Cat", "UU decode: #0V%T gives \"Cat\" (without header and trailer)");
        var ok = true;
        for (var length = 0; length <= 200; length += 7)
        {
            var data = Enumerable.Range(0, length).Select(i => (byte)(i * 37 + length)).ToArray();
            var encoded = ascii.GetString(Tool(0, data)!);
            var lines = encoded.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var dataLines = lines.Skip(1).Take(lines.Length - 3).ToArray();   // without "begin ...", "`" and "end"
            ok &= dataLines.Take(Math.Max(0, dataLines.Length - 1)).All(l => l.Length == 61 && l[0] == 'M')   // full lines: 45 bytes = 60 characters and the length character 'M'
                  && dataLines.Length == (length + 44) / 45 && dataLines.All(l => l.Length <= 61);
            ok &= Tool(1, ascii.GetBytes(encoded))!.SequenceEqual(data);
        }
        Check(ok, "UU encode and decode: round trips of 0 to 200 bytes, lines of at most 61 characters");

        // zip: an archive made here is read by .NET, an archive of .NET is read here
        var text = ascii.GetBytes(string.Concat(Enumerable.Repeat("compress me, compress me. ", 40)));
        var zipped = Tool(2, text)!;
        using (var archive = new System.IO.Compression.ZipArchive(new MemoryStream(zipped)))
        using (var entry = archive.Entries[0].Open())
        {
            var read = new MemoryStream();
            entry.CopyTo(read);
            Check(archive.Entries.Count == 1 && archive.Entries[0].Name == "data" && read.ToArray().SequenceEqual(text) && zipped.Length < text.Length, $"zip: .NET reads our archive ({text.Length} bytes in {zipped.Length})");
        }
        var theirs = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(theirs, System.IO.Compression.ZipArchiveMode.Create, true))
        using (var entry = archive.CreateEntry("x.txt").Open())
            entry.Write(text);
        Check(Tool(3, theirs.ToArray())!.SequenceEqual(text), "unzip: we read an archive made by .NET");
        Check(Tool(3, ascii.GetBytes("this is not a zip archive")) == null, "unzip: data that is not an archive is refused");

        // byte-wise addition: HAL + 1 = IBM; and back
        Check(ascii.GetString(Tool(4, ascii.GetBytes("HAL"), new byte[] { 1 })!) == "IBM" && ascii.GetString(Tool(5, ascii.GetBytes("IBM"), new byte[] { 1 })!) == "HAL", "byte-wise addition and subtraction: HAL + 1 = IBM");
        Check(Tool(4, new byte[] { 250, 5 }, new byte[] { 10, 200 })!.SequenceEqual(new byte[] { 4, 205 }) && Tool(4, new byte[] { 1 }) == null, "byte-wise addition: modulo 256, and the key is needed");

        // bit operations on 0xB1 = 10110001
        var b1 = new byte[] { 0xB1 };
        Check(Tool(6, b1)![0] == 0x62 && Tool(8, b1)![0] == 0x63 && Tool(7, b1, amount: 2)![0] == 0x2C && Tool(9, b1, amount: 2)![0] == 0x6C, "shifts and rotations of 10110001: left 1 = 0x62, rotate left 1 = 0x63, right 2 = 0x2C, rotate right 2 = 0x6C");

        // entropy
        double Entropy(byte[] data, int symbol = 0)
        {
            var component = Component("ShannonEntropy.ShannonEntropy", ("Symbol", symbol.ToString()));
            component.PreExecution();
            component.Set("InputData", data);
            component.Execute();
            var value = component.Get("Value");
            var produced = component.Get("Report") != null;   // a refused input leaves the report empty
            component.PostExecution();
            return !produced || value == null ? double.NaN : (double)value;
        }
        Check(Entropy(ascii.GetBytes("AAAA")) == 0 && Math.Abs(Entropy(ascii.GetBytes("ABAB")) - 1) < 1e-12 && Math.Abs(Entropy(ascii.GetBytes("AABC")) - 1.5) < 1e-12, "entropy: AAAA = 0, ABAB = 1, AABC = 1.5 bits");
        Check(Math.Abs(Entropy(Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray()) - 8) < 1e-12, "entropy: all 256 byte values equally often = 8 bits");
        Check(Math.Abs(Entropy(ascii.GetBytes("AABBAABB"), 1) - 1) < 1e-12 && double.IsNaN(Entropy(new byte[1], 1)), "entropy: pairs of bytes (AA BB AA BB = 1 bit), and data that is too short is refused");
    }

    static string? Classic(int cipher, string text, string? key = null, int action = 0, int rails = 3, int offset = 0)
    {
        var component = Component("ClassicVariants.ClassicVariants", ("Cipher", cipher.ToString()), ("Action", action.ToString()), ("Rails", rails.ToString()), ("Offset", offset.ToString()));
        component.PreExecution();
        component.Set("InputText", text);
        if (key != null) component.Set("Key", key);
        component.Execute();
        var output = (string?)component.Get("OutputText");
        component.PostExecution();
        return output;
    }

    static void ClassicVariants()
    {
        // Porta: key letters A/B select the alphabet that exchanges A-M with N-Z in order, C/D the one shifted by one
        Check(Classic(0, "A", "A") == "N" && Classic(0, "N", "A") == "A" && Classic(0, "A", "C") == "O" && Classic(0, "N", "C") == "M" && Classic(0, "A", "B") == "N", "Porta: key A/B: A <-> N; key C/D: A <-> O and N <-> M");
        var plain = "THE QUICK BROWN FOX, 7 TIMES.";
        var porta = Classic(0, plain, "SECRET")!;
        Check(porta != plain && Classic(0, porta, "SECRET") == plain && porta[3] == ' ' && porta[^1] == '.', "Porta: reciprocal (the same operation decrypts), spaces and punctuation are kept");

        // rail fence: the example of Wikipedia
        var fence = Classic(1, "WEAREDISCOVEREDFLEEATONCE")!;
        Check(fence == "WECRLTEERDSOEEFEAOCAIVDEN" && Classic(1, fence, action: 1) == "WEAREDISCOVEREDFLEEATONCE", "rail fence, 3 rails: WEAREDISCOVEREDFLEEATONCE -> WECRLTEERDSOEEFEAOCAIVDEN, and back");
        var ok = true;
        foreach (var rails in new[] { 2, 3, 5, 8 })
            foreach (var offset in new[] { 0, 1, 4, 9 })
            {
                var text = "Rail fences keep all characters, spaces too!";
                var e = Classic(1, text, rails: rails, offset: offset)!;
                ok &= e.Length == text.Length && Classic(1, e, action: 1, rails: rails, offset: offset) == text && e.OrderBy(c => c).SequenceEqual(text.OrderBy(c => c));
            }
        Check(ok, "rail fence: round trips with 2 to 8 rails and offsets, the ciphertext is a permutation of the text");
        var re = Classic(1, "WEAREDISCOVEREDFLEEATONCE", "ABC")!;
        Check(re == fence, "redefence: the key ABC (natural order of the rails) gives the rail fence cipher");
        var reversed = Classic(1, "WEAREDISCOVEREDFLEEATONCE", "CBA")!;
        Check(reversed != fence && Classic(1, reversed, "CBA", action: 1) == "WEAREDISCOVEREDFLEEATONCE" && Classic(1, "ABCDEFGHIJKLM", "CAB") != Classic(1, "ABCDEFGHIJKLM"), "redefence: a key with another order of the rails gives another ciphertext and decrypts");
        Check(Classic(1, "TEXT", "AB") == null, "redefence: a key with the wrong number of letters is refused");

        // Kamasutra
        Check(Classic(2, "ABCDEF", "BADCFE") == "BADCFE" && Classic(2, "Hello, World", "BADCFEHGJILKNMPORQTSVUXWZY") is { } k && Classic(2, k, "BADCFEHGJILKNMPORQTSVUXWZY") == "HELLO, WORLD", "Kamasutra: A<->B, C<->D, E<->F; reciprocal");
        Check(Classic(2, "ABC", "ABC") == null && Classic(2, "ABC", "AAB") == null, "Kamasutra: a key with an odd number of letters or a repeated letter is refused");

        // two-square: squares of EXAMPLE and KEYWORD; HE -> G, X by hand (rectangle of H in the left, E in the right square)
        Check(ClassicVariantsSquare("EXAMPLE") == "EXAMPLBCDFGHIKNOQRSTUVWYZ" && ClassicVariantsSquare("KEYWORD") == "KEYWORDABCFGHILMNPQSTUVXZ", "two-square: the squares made of EXAMPLE and KEYWORD");
        Check(Classic(3, "HE", "EXAMPLE KEYWORD") == "GX" && Classic(3, "GX", "EXAMPLE KEYWORD", action: 1) == "HE", "two-square: HE -> GX by hand with EXAMPLE and KEYWORD, and back");
        ok = true;
        foreach (var text in new[] { "HELPMEOBIWANKENOBI", "ATTACKATDAWN", "ODDLENGTH" })
        {
            var e = Classic(3, text, "EXAMPLE KEYWORD")!;
            var d = Classic(3, e, "EXAMPLE KEYWORD", action: 1)!;
            ok &= e.Length == d.Length && e.Length % 2 == 0 && d.StartsWith(text.Replace('J', 'I'));
        }
        Check(ok, "two-square: round trips (an odd text gets an X)");
        Check(Classic(3, "TEXT", "ONLYONE") == null, "two-square: a key with only one word is refused");
    }

    static string ClassicVariantsSquare(string keyword) => global::ClassicVariants.Ciphers.Square(keyword);

    static Dictionary<string, object?> HashSig(int action, int scheme, params (string Name, object Value)[] inputs)
    {
        var component = Component("HashBasedSignatures.HashBasedSignatures", ("Action", action.ToString()), ("Scheme", scheme.ToString()), ("Height", "0"), ("Winternitz", "2"), ("Variant", "0"));
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var outputs = new[] { "OutputPrivateKey", "OutputPublicKey", "OutputSignature", "Remaining", "Valid" }.ToDictionary(n => n, n => component.Get(n));
        component.PostExecution();
        return outputs;
    }

    static void HashBasedSignatures()
    {
        var m1 = Stream(System.Text.Encoding.ASCII.GetBytes("first message"));
        var m2 = Stream(System.Text.Encoding.ASCII.GetBytes("second message"));

        // LMS with height 5 and Winternitz w = 4 (RFC 8554 type codes 5 and 3): key format, state, one signature per one-time key
        var keys = HashSig(0, 0);
        var privateKey = (byte[])keys["OutputPrivateKey"]!;
        var publicKey = (byte[])keys["OutputPublicKey"]!;
        Check(publicKey.Length == 56 && publicKey.Take(8).SequenceEqual(new byte[] { 0, 0, 0, 5, 0, 0, 0, 3 }) && (int)keys["Remaining"]! == 32, "LMS: the public key is u32 5, u32 3, I (16 bytes), T1 (32 bytes); 32 signatures are possible");
        var first = HashSig(1, 0, ("InputMessage", m1), ("InputPrivateKey", privateKey));
        var signature1 = (byte[])first["OutputSignature"]!;
        var state1 = (byte[])first["OutputPrivateKey"]!;
        var second = HashSig(1, 0, ("InputMessage", Stream(System.Text.Encoding.ASCII.GetBytes("second message"))), ("InputPrivateKey", state1));
        var signature2 = (byte[])second["OutputSignature"]!;
        Check(signature1.Take(4).SequenceEqual(new byte[] { 0, 0, 0, 0 }) && signature2.Take(4).SequenceEqual(new byte[] { 0, 0, 0, 1 }) && (int)first["Remaining"]! == 31 && (int)second["Remaining"]! == 30,
              "LMS: the first signature uses leaf 0, the second (with the updated key) leaf 1; 31 and 30 signatures left");
        bool ValidLms(byte[] pub, object message, byte[] sig) => (bool)HashSig(2, 0, ("InputMessage", message), ("InputPublicKey", pub), ("InputSignature", sig))["Valid"]!;
        var broken = (byte[])signature1.Clone();
        broken[100] ^= 1;
        Check(ValidLms(publicKey, Stream(System.Text.Encoding.ASCII.GetBytes("first message")), signature1) && ValidLms(publicKey, Stream(System.Text.Encoding.ASCII.GetBytes("second message")), signature2)
              && !ValidLms(publicKey, Stream(System.Text.Encoding.ASCII.GetBytes("second message")), signature1) && !ValidLms(publicKey, Stream(System.Text.Encoding.ASCII.GetBytes("first message")), broken)
              && !ValidLms((byte[])HashSig(0, 0)["OutputPublicKey"]!, Stream(System.Text.Encoding.ASCII.GetBytes("first message")), signature1),
              "LMS: both signatures verify; another message, a changed signature and another public key are rejected");

        // an LMS key signs 32 messages and no more
        var state = privateKey;
        var count = 0;
        while (true)
        {
            var step = HashSig(1, 0, ("InputMessage", Stream(new byte[] { (byte)count })), ("InputPrivateKey", state));
            if (step["OutputSignature"] == null) break;
            state = (byte[])step["OutputPrivateKey"]!;
            count++;
            if (count > 40) break;
        }
        Check(count == 32, $"LMS: a key of height 5 signs exactly 32 messages ({count}); then signing is refused");

        // SPHINCS+ sha2-128f-simple: key and signature sizes of the specification (32, 64 and 17088 bytes), no state
        var sphincs = HashSig(0, 1);
        var sphincsPrivate = (byte[])sphincs["OutputPrivateKey"]!;
        var sphincsPublic = (byte[])sphincs["OutputPublicKey"]!;
        var sphincsSignature = (byte[])HashSig(1, 1, ("InputMessage", Stream(System.Text.Encoding.ASCII.GetBytes("first message"))), ("InputPrivateKey", sphincsPrivate))["OutputSignature"]!;
        var sphincsBroken = (byte[])sphincsSignature.Clone();
        sphincsBroken[5000] ^= 1;
        bool ValidSphincs(byte[] pub, string message, byte[] sig) => (bool)HashSig(2, 1, ("InputMessage", Stream(System.Text.Encoding.ASCII.GetBytes(message))), ("InputPublicKey", pub), ("InputSignature", sig))["Valid"]!;
        Check(sphincsPublic.Length == 32 && sphincsPrivate.Length == 64 && sphincsSignature.Length == 17088 && (int)sphincs["Remaining"]! == -1, "SPHINCS+ sha2-128f-simple: public key 32, private key 64, signature 17088 bytes");
        Check(ValidSphincs(sphincsPublic, "first message", sphincsSignature) && !ValidSphincs(sphincsPublic, "other message", sphincsSignature) && !ValidSphincs(sphincsPublic, "first message", sphincsBroken)
              && !ValidSphincs((byte[])HashSig(0, 1)["OutputPublicKey"]!, "first message", sphincsSignature), "SPHINCS+: the signature verifies; another message, a changed signature and another public key are rejected");
    }

    static Dictionary<string, object?> Mce(int action, params (string Name, object Value)[] inputs)
    {
        var component = Component("ClassicMcEliece.ClassicMcEliece", ("Action", action.ToString()), ("Parameters", "0"));
        component.PreExecution();
        foreach (var (name, value) in inputs) component.Set(name, value);
        component.Execute();
        var outputs = new[] { "OutputPrivateKey", "OutputPublicKey", "OutputEncapsulation", "OutputSecret" }.ToDictionary(n => n, n => component.Get(n));
        component.PostExecution();
        return outputs;
    }

    static void ClassicMcEliece()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var keys = Mce(0);
        var privateKey = (byte[])keys["OutputPrivateKey"]!;
        var publicKey = (byte[])keys["OutputPublicKey"]!;
        Console.WriteLine($"     (mceliece348864 key generation: {watch.Elapsed.TotalSeconds:F1} s)");
        Check(publicKey.Length == 261120 && privateKey.Length == 6492, $"Classic McEliece mceliece348864: public key {publicKey.Length} bytes, private key {privateKey.Length} bytes (specification: 261120 and 6492)");
        var sent = Mce(1, ("InputPublicKey", publicKey));
        var encapsulation = (byte[])sent["OutputEncapsulation"]!;
        var secret = (byte[])sent["OutputSecret"]!;
        var received = (byte[])Mce(2, ("InputPrivateKey", privateKey), ("InputEncapsulation", encapsulation))["OutputSecret"]!;
        // the sizes of BouncyCastle: the syndrome of 96 bytes (no confirmation hash) and a 128-bit secret
        Check(encapsulation.Length == 96 && secret.Length == 16 && received.SequenceEqual(secret), "Classic McEliece: encapsulation of 96 bytes (the syndrome), shared secret of 16 bytes, the receiver gets the same secret");
        var changed = (byte[])encapsulation.Clone();
        changed[7] ^= 1;
        var other = (byte[])Mce(0)["OutputPrivateKey"]!;
        Check(!((byte[])Mce(2, ("InputPrivateKey", privateKey), ("InputEncapsulation", changed))["OutputSecret"]!).SequenceEqual(secret)
              && !((byte[])Mce(2, ("InputPrivateKey", other), ("InputEncapsulation", encapsulation))["OutputSecret"]!).SequenceEqual(secret),
              "Classic McEliece: a changed encapsulation or another private key gives a different secret (implicit rejection)");
        Check(!((byte[])Mce(1, ("InputPublicKey", publicKey))["OutputSecret"]!).SequenceEqual(secret), "Classic McEliece: every encapsulation has a fresh secret");
    }

    static byte[]? Hmac(int hash, byte[] data, byte[] key)
    {
        var component = Component("HmacHashes.HmacHashes", ("Hash", hash.ToString()));
        component.PreExecution();
        component.Set("InputData", Stream(data));
        component.Set("Key", key);
        component.Execute();
        var mac = (byte[]?)component.Get("OutputData");
        component.PostExecution();
        return mac;
    }

    static void HmacHashes()
    {
        var hiThere = System.Text.Encoding.ASCII.GetBytes("Hi There");
        Check(Convert.ToHexString(Hmac(2, hiThere, Enumerable.Repeat((byte)0x0b, 16).ToArray())!) == "9294727A3638BB1C13F48EF8158BFC9D", "HMAC-MD5, RFC 2202 test case 1");
        Check(Convert.ToHexString(Hmac(3, hiThere, Enumerable.Repeat((byte)0x0b, 20).ToArray())!) == "B617318655057264E28BC0B6FB378C8EF146BE00", "HMAC-SHA-1, RFC 2202 test case 1");
        Check(Convert.ToHexString(Hmac(5, hiThere, Enumerable.Repeat((byte)0x0b, 20).ToArray())!) == "B0344C61D8DB38535CA8AFCEAF0BF12B881DC200C9833DA726E9376C2E32CFF7", "HMAC-SHA-256, RFC 4231 test case 1");
        Check(Convert.ToHexString(Hmac(7, hiThere, Enumerable.Repeat((byte)0x0b, 20).ToArray())!) == "87AA7CDEA5EF619D4FF0B4241A1D6CB02379F4E2CE4EC2787AD0B30545E17CDEDAA833B7D6B8A702038B274EAEA3F4E4BE9D914EEB61F1702E696C203A126854", "HMAC-SHA-512, RFC 4231 test case 1");

        // against .NET with keys shorter than, equal to and longer than the block, and with hash functions of different block sizes
        var ok = true;
        foreach (var keyLength in new[] { 1, 16, 64, 100, 129 })
        {
            var key = Enumerable.Range(0, keyLength).Select(i => (byte)(i * 7 + 3)).ToArray();
            var data = Enumerable.Range(0, 200).Select(i => (byte)(i * 3)).ToArray();
            ok &= Hmac(2, data, key)!.SequenceEqual(System.Security.Cryptography.HMACMD5.HashData(key, data))
                  && Hmac(3, data, key)!.SequenceEqual(System.Security.Cryptography.HMACSHA1.HashData(key, data))
                  && Hmac(5, data, key)!.SequenceEqual(System.Security.Cryptography.HMACSHA256.HashData(key, data))
                  && Hmac(6, data, key)!.SequenceEqual(System.Security.Cryptography.HMACSHA384.HashData(key, data))
                  && Hmac(7, data, key)!.SequenceEqual(System.Security.Cryptography.HMACSHA512.HashData(key, data));
        }
        Check(ok, "HMAC-MD5, -SHA-1, -SHA-256, -SHA-384 and -SHA-512 equal .NET for keys of 1 to 129 bytes");

        // Tiger and every other hash function: HMAC written out from RFC 2104 with the hash function itself (block size 64 for Tiger)
        var digestKey = Enumerable.Range(0, 40).Select(i => (byte)(i + 1)).ToArray();
        var message = System.Text.Encoding.ASCII.GetBytes("What do ya want for nothing?");
        var names = new[] { "MD2", "MD4", "MD5", "SHA-1", "SHA-224", "SHA-256", "SHA-384", "SHA-512", "SHA3-256", "RIPEMD-128", "RIPEMD-160", "RIPEMD-256", "RIPEMD-320", "Tiger", "Whirlpool", "SM3" };
        var blockSizes = new[] { 16, 64, 64, 64, 64, 64, 128, 128, 136, 64, 64, 64, 64, 64, 64, 64 };
        for (var hash = 0; hash < names.Length; hash++)
        {
            byte[] H(byte[] input)
            {
                var digest = global::HmacHashes.HmacHashes.CreateDigest(hash);
                digest.BlockUpdate(input, 0, input.Length);
                var result = new byte[digest.GetDigestSize()];
                digest.DoFinal(result, 0);
                return result;
            }
            var k = digestKey.Length > blockSizes[hash] ? H(digestKey) : digestKey;
            var padded = new byte[blockSizes[hash]];
            k.CopyTo(padded, 0);
            var expected = H(padded.Select(b => (byte)(b ^ 0x5c)).Concat(H(padded.Select(b => (byte)(b ^ 0x36)).Concat(message).ToArray())).ToArray());
            var actual = Hmac(hash, message, digestKey);
            Check(actual != null && actual.SequenceEqual(expected) && actual.Length == expected.Length, $"HMAC-{names[hash]} equals the construction of RFC 2104 written out with the hash function ({expected.Length} bytes)");
        }
    }

    static string? Viz(int cipher, string text, string key, int action = 0)
    {
        var component = Component("CipherVisualization.CipherVisualization", ("Cipher", cipher.ToString()), ("Action", action.ToString()));
        component.PreExecution();
        component.Set("InputText", text);
        component.Set("Key", key);
        component.Execute();
        var output = (string?)component.Get("OutputText");
        component.PostExecution();
        return output;
    }

    static void CipherVisualization()
    {
        // Caesar: HELLO by 3 (and by the letter D), ROT13 twice, decryption
        Check(Viz(0, "HELLO", "3") == "KHOOR" && Viz(0, "Hello, World", "D") == "KHOOR, ZRUOG" && Viz(0, Viz(0, "ROT THIRTEEN", "13")!, "13") == "ROT THIRTEEN" && Viz(0, "KHOOR", "3", 1) == "HELLO" && Viz(0, "ABC", "-1") == "ZAB", "Caesar: HELLO by 3 = KHOOR, the key letter D, ROT13 twice, decryption, shift -1");
        // Vigenere: the example of Wikipedia (ATTACKATDAWN with LEMON), decryption, other characters are kept
        Check(Viz(1, "ATTACKATDAWN", "LEMON") == "LXFOPVEFRNHR" && Viz(1, "LXFOPVEFRNHR", "LEMON", 1) == "ATTACKATDAWN", "Vigenère: ATTACKATDAWN with LEMON = LXFOPVEFRNHR, and back");
        Check(Viz(1, "ATTACK AT DAWN!", "LEMON") == "LXFOPV EF RNHR!", "Vigenère: spaces and punctuation are kept and use up no key letter");
        // Nihilist: the example of Wikipedia: square of ZEBRAS, additive key RUSSIAN, DYNAMITE WINTER PALACE begins 37 106 62 36 67 47 86 26 (calculated letter by letter by hand)
        var nihilist = Viz(2, "DYNAMITE WINTER PALACE", "ZEBRAS RUSSIAN")!;
        Check(nihilist.StartsWith("37 106 62 36 67 47 86 26 ") && nihilist.Split(' ').Length == 20 && Viz(2, nihilist, "ZEBRAS RUSSIAN", 1) == "DYNAMITEWINTERPALACE", $"Nihilist: DYNAMITE WINTER PALACE with ZEBRAS / RUSSIAN begins 37 106 62 36 67 47 86 26, and decrypts ({nihilist})");
        Check(Viz(2, "TEXT", "ZEBRAS") == null && Viz(2, "99 99", "ZEBRAS RUSSIAN", 1) == null && Viz(2, "x y", "ZEBRAS RUSSIAN", 1) == null, "Nihilist: a key with one word, numbers that fit no letter and text instead of numbers are refused");
    }

    static string? MathViz(int visualization, string parameters)
    {
        var component = Component("MathVisualization.MathVisualization", ("Visualization", visualization.ToString()));
        component.PreExecution();
        component.Set("Parameters", parameters);
        component.Execute();
        var result = (string?)component.Get("Result");
        component.PostExecution();
        return result;
    }

    static void MathVisualization()
    {
        // RSA: the example of Wikipedia: p = 61, q = 53, n = 3233, phi = 3120, e = 17, d = 2753, m = 65, c = 2790
        var rsa = global::MathVisualization.Calculations.Rsa("61 53 17 65");
        Check(rsa.N == 3233 && rsa.Phi == 3120 && rsa.D == 2753 && rsa.Ciphertext == 2790 && rsa.Decrypted == 65 && MathViz(0, "p=61 q=53 e=17 m=65") == "2790", "RSA visualization: p = 61, q = 53, e = 17, m = 65 gives n = 3233, phi = 3120, d = 2753, c = 2790 and decrypts to 65");
        Check(MathViz(0, "61 53 2 65") == null && MathViz(0, "61 53 17 3233") == null && MathViz(0, "61 61 17 65") == null && MathViz(0, "61 53") == null, "RSA visualization: e not coprime to phi, a message that is not smaller than n, p = q and missing parameters are refused");

        // Shamir: f(x) = 11 + 3x + 7x^2 modulo 23, 5 shares; the points are computed here again
        var shamir = global::MathVisualization.Calculations.Shamir("23 5 11 3 7");
        Check(shamir.Secret == 11 && shamir.Points.Count == 5 && shamir.Points.All(point => point.Value == (11 + 3 * point.Key + 7 * point.Key * point.Key) % 23) && shamir.Curve.Length == 23 && MathViz(1, "23 5 11 3 7") == "11",
              "Shamir visualization: f(x) = 11 + 3x + 7x^2 mod 23, the 5 shares are on the polynomial and the secret 11 is rebuilt from the first 3");
        Check(MathViz(1, "24 5 11 3 7") == null && MathViz(1, "23 2 11 3 7") == null && MathViz(1, "251 5 1 2 3") == null, "Shamir visualization: a modulus that is not prime or larger than 211, and fewer shares than the threshold are refused");

        // elliptic curve y^2 = x^3 + 2x + 2 modulo 17 with G = (5, 1): 19 points, 2G = (6, 3), 3G = (10, 6) (the textbook example)
        var curve = global::MathVisualization.Calculations.Curve("17 2 2 5 1 6 3");
        Check(curve.Points.Count == 18 && curve.Sum.HasValue && curve.Sum.Value.X == 10 && curve.Sum.Value.Y == 6 && MathViz(2, "17 2 2 5 1 6 3") == "10,6", "elliptic curve visualization: 18 points and O on y^2 = x^3 + 2x + 2 mod 17; (5,1) + (6,3) = (10,6)");
        Check(MathViz(2, "17 2 2 5 1 5 1") == "6,3" && MathViz(2, "17 2 2 5 1 5 16") == "O" && MathViz(2, "17 2 2 5 1 10 6") == "3,1", "elliptic curve visualization: doubling (5,1) = (6,3), opposite points give O, (5,1) + (10,6) = 4G = (3,1)");
        Check(MathViz(2, "17 2 2") == "" && MathViz(2, "17 0 0") == null && MathViz(2, "17 2 2 5 2 6 3") == null && MathViz(2, "18 2 2") == null, "elliptic curve visualization: without points only the curve is shown; a singular curve, a point that is not on the curve and a modulus that is not prime are refused");
    }

    static string? Proto(int protocol, string parameters, string? message = null, string? received = null)
    {
        var component = Component("ProtocolVisualization.ProtocolVisualization", ("Protocol", protocol.ToString()));
        component.PreExecution();
        component.Set("Parameters", parameters);
        if (message != null) component.Set("Message", message);
        if (received != null) component.Set("Received", received);
        component.Execute();
        var result = (string?)component.Get("Result");
        component.PostExecution();
        return result;
    }

    static void Games()
    {
        // number shark: with the numbers 1..6 the player takes 6, the shark gets 1+2+3, and 4 and 5 have no free divisor left (the shark takes them)
        var shark = new global::NumberShark.SharkGame(6);
        Check(!shark.IsValid(7) && shark.IsValid(6) && shark.IsValid(5) && !shark.IsValid(1), "number shark: 1 can never be taken (no divisor), a prime can while 1 is free");
        bool moved = shark.Move(6);
        Check(moved && shark.PlayerScore == 6 && shark.SharkScore == 15 && shark.Finished, "number shark: after 6 the shark has 1+2+3 and, as no move is left, 4+5 (15 in all)");
        Check(!shark.Move(4), "number shark: no move after the end");
        var wide = new global::NumberShark.SharkGame(24);
        Check(!wide.Move(1) && wide.Move(24) && wide.SharkScore == 1 + 2 + 3 + 4 + 6 + 8 + 12 && !wide.IsValid(12), "number shark: 24 gives the shark all its divisors; 12 is gone");
        var total = new global::NumberShark.SharkGame(30);
        while (!total.Finished) { total.Move(total.ValidMoves().Last()); }
        Check(total.PlayerScore + total.SharkScore == 30 * 31 / 2, "number shark: every number is scored by one side (the points add up to n(n+1)/2)");

        // divisor game: the player who starts wins for every number above 1 (strategy-stealing argument)
        bool startersWin = true;
        for (int n = 2; n <= 120; n++) { var position = new global::DivisorGame.DivisorPosition(n); startersWin &= position.IsWinning(position.Full); }
        Check(startersWin, "divisor game: the first player wins for every number from 2 to 120");
        var twelve = new global::DivisorGame.DivisorPosition(12);
        long afterTwo = twelve.Take(twelve.Full, 2);
        Check(twelve.Divisors.SequenceEqual(new[] { 1, 2, 3, 4, 6, 12 }) && twelve.Moves(afterTwo).SequenceEqual(new[] { 3 }), "divisor game: taking 2 from 12 removes 2, 4, 6 and 12; only 3 (and the poison 1) is left");
        Check(!twelve.IsWinning(afterTwo) == !twelve.Moves(afterTwo).Any(d => !twelve.IsWinning(twelve.Take(afterTwo, d))), "divisor game: a position is winning exactly when a move leads to a losing one");
        long position6 = twelve.Take(twelve.Full, 12);   // 1 2 3 4 6 left
        Check(twelve.Take(position6, twelve.BestMove(position6)) != position6 && !twelve.IsWinning(twelve.Take(position6, twelve.BestMove(position6))) == twelve.IsWinning(position6), "divisor game: the computer's best move leaves a losing position when it can");

        // sudoku: shuffled valid grid, unique solution, same number gives the same puzzle
        var puzzle = global::Sudoku.SudokuPuzzle.Generate(7, 32);
        bool valid = !global::Sudoku.SudokuPuzzle.Conflicts(puzzle.Solution).Any() && puzzle.Solution.All(d => d >= 1 && d <= 9);
        for (int i = 0; i < 9; i++)
        {
            valid &= Enumerable.Range(0, 9).Select(k => puzzle.Solution[i * 9 + k]).Distinct().Count() == 9 && Enumerable.Range(0, 9).Select(k => puzzle.Solution[k * 9 + i]).Distinct().Count() == 9
                  && Enumerable.Range(0, 9).Select(k => puzzle.Solution[(i / 3 * 3 + k / 3) * 9 + i % 3 * 3 + k % 3]).Distinct().Count() == 9;
        }
        Check(valid, "sudoku: the generated solution has every digit once in each row, column and box");
        int clues = puzzle.Givens.Count(d => d != 0);
        Check(clues >= 32 && clues < 45 && Enumerable.Range(0, 81).All(c => puzzle.Givens[c] == 0 || puzzle.Givens[c] == puzzle.Solution[c]), $"sudoku: {clues} digits are given, all from the solution");
        Check(global::Sudoku.SudokuPuzzle.CountSolutions((int[])puzzle.Givens.Clone(), 2) == 1, "sudoku: the puzzle has exactly one solution");
        Check(global::Sudoku.SudokuPuzzle.Generate(7, 32).Givens.SequenceEqual(puzzle.Givens) && !global::Sudoku.SudokuPuzzle.Generate(8, 32).Givens.SequenceEqual(puzzle.Givens), "sudoku: the same number gives the same puzzle, another number another one");
        var grid = (int[])puzzle.Solution.Clone();
        Check(global::Sudoku.SudokuPuzzle.IsSolved(grid), "sudoku: the solution is recognised as solved");
        int free = Enumerable.Range(0, 81).First(c => puzzle.Givens[c] == 0);
        grid[free] = grid[free] % 9 + 1;
        Check(!global::Sudoku.SudokuPuzzle.IsSolved(grid) && global::Sudoku.SudokuPuzzle.Conflicts(grid).Any(), "sudoku: a wrong digit makes a conflict");
    }

    static void AntsAndPatterns()
    {
        // ant system: cities on a circle (the best tour is the polygon, 8 * 2 * sin(pi / 8)), and random cities against a brute-force search of all tours
        var x = Enumerable.Range(0, 8).Select(i => Math.Cos(2 * Math.PI * i / 8)).ToArray();
        var y = Enumerable.Range(0, 8).Select(i => Math.Sin(2 * Math.PI * i / 8)).ToArray();
        var circle = new global::AntColony.AntSystem(x, y, 20, 1, 3, 0.5, 4);
        for (int i = 0; i < 60; i++) { circle.Iterate(); }
        double polygon = 8 * 2 * Math.Sin(Math.PI / 8);
        Check(Math.Abs(circle.BestLength - polygon) < 1e-9 && circle.BestTour.OrderBy(c => c).SequenceEqual(Enumerable.Range(0, 8)), $"ant system: eight cities on a circle give the polygon as the best tour ({circle.BestLength:F4} = {polygon:F4}), and the tour visits every city once");
        var random = new global::AntColony.AntSystem(7, 20, 1, 3, 0.5, 3);
        double before = random.Pheromone[0, 1];
        random.Iterate();
        Check(random.Pheromone[0, 1] >= before * 0.5 - 1e-12 && random.Iterations == 1, "ant system: the pheromone evaporates by the given share and only grows by what the ants deposit");
        for (int i = 0; i < 60; i++) { random.Iterate(); }
        double optimum = double.MaxValue;
        void Permute(int[] tour, int k) { if (k == tour.Length) { optimum = Math.Min(optimum, random.Length(tour)); return; } for (int i = k; i < tour.Length; i++) { (tour[k], tour[i]) = (tour[i], tour[k]); Permute(tour, k + 1); (tour[k], tour[i]) = (tour[i], tour[k]); } }
        Permute(Enumerable.Range(0, 7).ToArray(), 1);
        Check(random.BestLength >= optimum - 1e-9 && random.BestLength <= optimum * 1.05 && random.BestLength <= random.NearestNeighbourLength + 1e-9, $"ant system: seven random cities: the ants' best tour {random.BestLength:F4} is not below the optimum {optimum:F4} (brute force), within 5 percent of it, and not longer than the nearest-neighbour tour {random.NearestNeighbourLength:F4}");
        var same = new global::AntColony.AntSystem(7, 20, 1, 3, 0.5, 3);
        for (int i = 0; i < 61; i++) { same.Iterate(); }
        Check(same.BestLength == random.BestLength, "ant system: the same seed gives the same run");

        // unlock pattern: the number of valid patterns is known (389112 in all)
        var counts = Enumerable.Range(4, 6).Select(n => global::PatternLock.PatternRules.Count(n)).ToArray();
        Check(counts.SequenceEqual(new long[] { 1624, 7152, 26016, 72912, 140704, 140704 }) && counts.Sum() == 389112, $"unlock pattern: 1624, 7152, 26016, 72912, 140704 and 140704 patterns of length 4 to 9 ({counts.Sum()} in all)");
        var rules = global::PatternLock.PatternRules.Between;
        Check(rules(0, 2) == 1 && rules(0, 8) == 4 && rules(1, 7) == 4 && rules(6, 2) == 4 && rules(0, 5) == -1 && rules(0, 1) == -1 && rules(0, 6) == 3 && rules(4, 4) == -1, "unlock pattern: the point that a line goes over (0-2 over 1, 0-8 over 4, 1-7 over 4, 0-5 over none)");
        var pattern = new[] { 0, 4, 8, 5 };
        var hash = global::PatternLock.PatternRules.Hash(pattern);
        Check(hash.SequenceEqual(System.Security.Cryptography.SHA1.HashData(new byte[] { 0, 4, 8, 5 })), "unlock pattern: the stored value is the SHA-1 hash of the point numbers as bytes");
        var found = global::PatternLock.PatternRules.Crack(hash, out long tries);
        Check(found != null && found.SequenceEqual(pattern) && tries > 0 && tries <= 389112, $"unlock pattern: trying all patterns finds {string.Join(" ", found ?? new int[0])} after {tries} tries");
        Check(global::PatternLock.PatternRules.Crack(new byte[20], out long all) == null && all == 389112, "unlock pattern: a hash value that no pattern has is not found after all 389112 patterns");
    }

    static void Kleptography()
    {
        // four ways to generate the primes: the attacker finds p and q for the three backdoors in several runs, and nothing in an honest key
        var names = new[] { "honest", "SETUP upper bits", "SETUP lower bits", "shared pad" };
        for (int method = 0; method < 4; method++)
        {
            bool recovered = true, none = true, primes = true, sizes = true;
            for (int run = 1; run <= 5; run++)
            {
                var attack = global::Kleptography.SetupAttack.Run(run, method);
                if (method == 0) { none &= !attack.Recovered; }
                else { recovered &= attack.Recovered && attack.RecoveredP == attack.P && attack.RecoveredQ == attack.Q && attack.RecoveredP * attack.RecoveredQ == attack.N; }
                primes &= global::Kleptography.SetupAttack.IsPrime(attack.P) && global::Kleptography.SetupAttack.IsPrime(attack.Q);
                sizes &= attack.N.GetBitLength() >= 310 && attack.N.GetBitLength() <= 321 && attack.P.GetBitLength() == 160;
            }
            Check(method == 0 ? none : recovered, method == 0 ? "kleptography, honest generation: the attacker finds nothing in five runs" : $"kleptography, {names[method]}: from the key the attacker finds p and q in five runs");
            Check(primes && sizes, $"kleptography, {names[method]}: p and q are prime (Miller-Rabin) and n has 310 to 321 bits, like an ordinary modulus");
        }
        var first = global::Kleptography.SetupAttack.Run(1, 1);
        Check(System.Numerics.BigInteger.ModPow(first.Encrypted, first.AttackerD, first.AttackerN) == first.Seed && (first.N >> global::Kleptography.SetupAttack.ShiftBits) == first.Encrypted, "kleptography: the upper bits of n are the seed encrypted with the attacker's key, and his secret exponent decrypts them");
        var lower = global::Kleptography.SetupAttack.Run(1, 2);
        Check(lower.N % (System.Numerics.BigInteger.One << global::Kleptography.SetupAttack.LowBits) == lower.Encrypted, "kleptography: the lower 130 bits of n are exactly the hidden value");
        var pad = global::Kleptography.SetupAttack.Run(1, 3);
        Check((pad.N >> global::Kleptography.SetupAttack.ShiftBits) == (pad.Seed ^ pad.Pad), "kleptography: with a shared pad the upper bits of n are the seed XOR the pad");
        Check(global::Kleptography.SetupAttack.IsPrime(1000003) && !global::Kleptography.SetupAttack.IsPrime(1000001) && global::Kleptography.SetupAttack.IsPrime(System.Numerics.BigInteger.Pow(2, 127) - 1), "kleptography: primality test (1000003 and the Mersenne prime 2^127 - 1 are prime, 1000001 = 101 * 9901 is not)");
    }

    static void RainbowSideChannelHuffman()
    {
        // rainbow table: every password on a chain (apart from the end) is found again from its hash value
        var table = new global::RainbowTable.ChainTable(3, 800, 40, 1);
        bool allFound = true;
        for (int c = 0; c < 20; c++)
        {
            long index = table.Starts[c];
            for (int column = 1; column <= 40; column++)
            {
                string password = table.Password(index);
                string found = table.Lookup(global::RainbowTable.ChainTable.Hash(password), out long work);
                allFound &= found != null && global::RainbowTable.ChainTable.Hash(found) == global::RainbowTable.ChainTable.Hash(password);
                index = table.Reduce(global::RainbowTable.ChainTable.Hash(password), column);
            }
        }
        Check(allFound, "rainbow table: every password on the first 20 chains is found again from its hash value");
        Check(table.Password(0) == "aaa" && table.Password(17575) == "zzz" && table.Index("abc") == 28 && table.Password(28) == "abc", "rainbow table: passwords and indexes (aaa = 0, abc = 28, zzz = 17575)");
        Check(table.Reduce(5, 1) != table.Reduce(5, 2), "rainbow table: the reduction function is different in every column");
        int covered = table.Coverage(), hits = 0;
        var random = new Random(3);
        for (int i = 0; i < 200; i++) { if (table.Lookup(global::RainbowTable.ChainTable.Hash(table.Password(random.Next(17576))), out long w) != null) { hits++; } }
        Check(covered > 0.4 * 17576 && covered <= 17576 && hits >= 0.35 * 200 && hits <= 200, $"rainbow table: the table covers {covered} of 17576 passwords and finds {hits} of 200 random passwords");
        Check(global::RainbowTable.ChainTable.Hash("abc") == BitConverter.ToUInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes("abc")), 0), "rainbow table: the hash function is the first 8 bytes of SHA-256");

        // side channels: the S-box of AES (known values), SPA reads the exponent, DPA finds the key byte
        var box = global::SideChannel.SideChannelModel.SBox;
        Check(box[0x00] == 0x63 && box[0x01] == 0x7c && box[0x53] == 0xed && box[0xff] == 0x16 && box.Distinct().Count() == 256, "AES S-box computed from the inverse in GF(2^8): S(00) = 63, S(01) = 7c, S(53) = ed, S(ff) = 16, and a permutation");
        bool spa = Enumerable.Range(1, 20).All(seed => { var r = global::SideChannel.SideChannelModel.Spa(seed, 0.3); return r.Recovered == r.Exponent && r.Exponent >= 1 << 15; });
        Check(spa, "SPA: the exponent is read from the power trace for 20 seeds (16-bit exponents)");
        bool dpa = Enumerable.Range(1, 20).All(seed => { var r = global::SideChannel.SideChannelModel.Dpa(seed, 200, 1.0); return r.Recovered == r.Key; });
        Check(dpa, "DPA: the key byte is found with 200 traces for 20 seeds");
        int noisy = Enumerable.Range(1, 20).Count(seed => { var r = global::SideChannel.SideChannelModel.Dpa(seed, 3, 3.0); return r.Recovered == r.Key; });
        Check(noisy < 20, $"DPA: with 3 traces and strong noise the attack does not work every time ({noisy} of 20)");
        Check(Math.Abs(global::SideChannel.SideChannelModel.Pearson(new double[] { 1, 2, 3, 4 }, new double[] { 2, 4, 6, 8 }) - 1) < 1e-12 && Math.Abs(global::SideChannel.SideChannelModel.Pearson(new double[] { 1, 2, 3 }, new double[] { 3, 2, 1 }) + 1) < 1e-12, "Pearson correlation: 1 for a line, -1 for a falling line");

        // Huffman coding with Hamming(7,4)
        var codes = global::HuffmanErrors.HuffmanCoding.Build("aaabbc");
        Check(codes['a'] == "0" && codes['c'] == "10" && codes['b'] == "11" && global::HuffmanErrors.HuffmanCoding.Encode("aaabbc", codes).Length == 9, "Huffman: aaabbc gives a = 0, c = 10, b = 11 and 9 bits");
        string sentence = "hello world, this is a test";
        var all = global::HuffmanErrors.HuffmanCoding.Build(sentence);
        Check(global::HuffmanErrors.HuffmanCoding.Decode(global::HuffmanErrors.HuffmanCoding.Encode(sentence, all), all) == sentence && global::HuffmanErrors.HuffmanCoding.Encode(sentence, all).Length < sentence.Length * 8, "Huffman: decoding gives the text back, and the code is shorter than 8 bits per character");
        Check(global::HuffmanErrors.HuffmanCoding.HammingEncode("1011") == "0110011" && global::HuffmanErrors.HuffmanCoding.HammingDecode("0110011", out int none) == "1011" && none == 0, "Hamming(7,4): 1011 becomes 0110011 (the standard example)");
        int length = global::HuffmanErrors.HuffmanCoding.Transmit(sentence, true, 0).Sent.Length;
        bool corrected = Enumerable.Range(1, length).All(p => global::HuffmanErrors.HuffmanCoding.Transmit(sentence, true, p).Decoded == sentence);
        bool garbled = Enumerable.Range(1, global::HuffmanErrors.HuffmanCoding.Transmit(sentence, false, 0).Sent.Length).All(p => global::HuffmanErrors.HuffmanCoding.Transmit(sentence, false, p).Wrong > 0);
        Check(corrected, $"Huffman with Hamming(7,4): a bit error at any of the {length} positions is corrected");
        Check(garbled, "Huffman without protection: a bit error at any position garbles the text");
        Check(global::HuffmanErrors.HuffmanCoding.Transmit("aaaa", false, 0).Decoded == "aaaa" && global::HuffmanErrors.HuffmanCoding.Transmit("", false, 0).Decoded == "", "Huffman: a text of one character and the empty text");
    }

    static void ProtocolVisualization()
    {
        // TLS 1.3 key schedule: HKDF against RFC 5869 (test case 1) and .NET, the labels against RFC 8448 (early secret and "derived" secret), then the handshake
        {
            byte[] ikm = Enumerable.Repeat((byte)0x0b, 22).ToArray(), salt = Enumerable.Range(0, 13).Select(i => (byte)i).ToArray(), info = Enumerable.Range(0xf0, 10).Select(i => (byte)i).ToArray();
            byte[] prk = global::ProtocolVisualization.Calculations.HkdfExtract(salt, ikm);
            byte[] okm = global::ProtocolVisualization.Calculations.HkdfExpand(prk, info, 42);
            Check(Convert.ToHexString(prk) == "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5" && Convert.ToHexString(okm) == "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865", "HKDF: RFC 5869 test case 1 (PRK and OKM)");
            Check(okm.SequenceEqual(System.Security.Cryptography.HKDF.Expand(System.Security.Cryptography.HashAlgorithmName.SHA256, prk, 42, info)), "HKDF-Expand equals the .NET implementation");
            byte[] zero = new byte[32], empty = System.Security.Cryptography.SHA256.HashData(new byte[0]);
            byte[] early = global::ProtocolVisualization.Calculations.HkdfExtract(zero, zero);
            byte[] derived = global::ProtocolVisualization.Calculations.DeriveSecret(early, "derived", empty);
            Check(Convert.ToHexString(early) == "33AD0A1C607EC03B09E6CD9893680CE210ADF300AA1F2660E1B22E10F170F92A" && Convert.ToHexString(derived) == "6F2615A108C702C5678F54FC9DBAB69716C076189C48250CEBEAC3576C3611BA", "TLS 1.3 key schedule: early secret and derived secret equal RFC 8448");
            var tls = global::ProtocolVisualization.Calculations.Tls("23 5 6 15");
            Check(tls.PublicA == 8 && tls.PublicB == 19 && tls.Shared == 2 && tls.Verified && tls.ClientVerified && tls.EveFound == 6 && tls.EveHasKeys && tls.ClientKey.Length == 32 && tls.ClientIv.Length == 24, "TLS 1.3 handshake: p = 23, g = 5, a = 6, b = 15 give the secret 2 on both sides, both Finished messages verify, and Eve finds a = 6 by trial");
            Check(global::ProtocolVisualization.Calculations.Tls("23 5 6 15").ClientKey == tls.ClientKey && global::ProtocolVisualization.Calculations.Tls("23 5 4 9").ClientKey != tls.ClientKey, "TLS 1.3 handshake: the same parameters give the same keys, other exponents other keys");
            Check(global::ProtocolVisualization.Calculations.Tls("1000000007 5 123456 654321").EveFound == 0, "TLS 1.3 handshake: for a large prime Eve does not try");
        }

        // Diffie-Hellman: the example of Wikipedia (p = 23, g = 5, a = 6, b = 15: A = 8, B = 19, K = 2); a second session with other exponents
        var exchange = global::ProtocolVisualization.Calculations.Exchange("23 5 6 15 4 9");
        var first = exchange.Sessions[0];
        var second = exchange.Sessions[1];
        Check(first.PublicA == 8 && first.PublicB == 19 && first.Key == 2 && first.KeyAlice == first.KeyBob && first.Found == 6, "Diffie-Hellman visualization: p = 23, g = 5, a = 6, b = 15 gives A = 8, B = 19, K = 2, and Eve finds a = 6 by trial");
        Check(second.PublicA == 4 && second.PublicB == 11 && second.Key == 13 && Proto(0, "23 5 6 15 4 9") == "2,13", "Diffie-Hellman visualization: the second session (a = 4, b = 9) gives K = 13, another key; the result lists both");
        Check(Proto(0, "21 5 6 15") == null && Proto(0, "23 23 6 15") == null && Proto(0, "23 5 6") == null, "Diffie-Hellman visualization: a modulus that is not prime, a generator that is too large and missing exponents are refused");

        // signature: the numbers are computed here again with .NET arithmetic
        var n = new System.Numerics.BigInteger(1009) * 1013;
        var phi = new System.Numerics.BigInteger(1008) * 1012;
        System.Numerics.BigInteger d = 1;
        while (17 * d % phi != 1) d++;
        System.Numerics.BigInteger H(string text) => new System.Numerics.BigInteger(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)), isUnsigned: true, isBigEndian: true) % n;
        var signature = global::ProtocolVisualization.Calculations.Signature("1009 1013 17", "Pay Bob 10 euros", "Pay Bob 10 euros");
        var expectedSignature = System.Numerics.BigInteger.ModPow(H("Pay Bob 10 euros"), d, n);
        Check(signature.N == n && signature.D == d && signature.Reduced == H("Pay Bob 10 euros") && signature.Signature == expectedSignature && signature.Valid && signature.Recovered == signature.Reduced && Proto(1, "1009 1013 17", "Pay Bob 10 euros") == "valid",
              "signature visualization: n, d, the reduced SHA-256 value and the signature equal the calculation with .NET; the message arrives unchanged and is valid");
        var tampered = global::ProtocolVisualization.Calculations.Signature("1009 1013 17", "Pay Bob 10 euros", "Pay Bob 1000 euros");
        Check(tampered.Tampered && tampered.Valid == (H("Pay Bob 10 euros") == H("Pay Bob 1000 euros")) && !tampered.Valid && Proto(1, "1009 1013 17", "Pay Bob 10 euros", "Pay Bob 1000 euros") == "invalid", "signature visualization: a message that was changed on the way is not valid");
        // authentication in a network: the responses are HMAC-SHA-256 of the challenges with the password as key
        var auth = global::ProtocolVisualization.Calculations.Authentication("secret 8271 9034");
        string HmacHex(string key, string text) => Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key), System.Text.Encoding.UTF8.GetBytes(text)));
        Check(auth.Response1 == HmacHex("secret", "8271") && auth.Response2 == HmacHex("secret", "9034") && auth.PasswordHash == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("secret"))) && auth.Accepted && !auth.ReplayAccepted && Proto(2, "secret 8271 9034") == "ok",
              "authentication visualization: the password hash and the responses equal SHA-256 and HMAC-SHA-256 of .NET; the response to the old challenge is not accepted for the new one");
        Check(global::ProtocolVisualization.Calculations.Authentication("secret 8271 8271").ReplayAccepted && Proto(2, "secret 8271") == null, "authentication visualization: a repeated challenge lets the replay succeed; fewer than three words are refused");

        // S/MIME: every step is calculated again with .NET
        var smime = global::ProtocolVisualization.Calculations.Smime("1009 1013 17 1019 1021 11", "Meet me at noon");
        var sessionKey = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("session key for Meet me at noon"))[..16];
        var nAlice = new System.Numerics.BigInteger(1009) * 1013;
        var nBob = new System.Numerics.BigInteger(1019) * 1021;
        System.Numerics.BigInteger Inv(System.Numerics.BigInteger e, System.Numerics.BigInteger phi) { System.Numerics.BigInteger x = 1; while (e * x % phi != 1) x++; return x; }
        var dBob = Inv(11, new System.Numerics.BigInteger(1018) * 1020);
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = sessionKey;
        var clear = aes.DecryptCbc(Convert.FromHexString(smime.Cipher), new byte[16]);
        var text = System.Text.Encoding.UTF8.GetString(clear);
        Check(smime.SessionKey == Convert.ToHexString(sessionKey) && smime.WrappedKey.Count == 16 && smime.WrappedKey.Select((c, i) => System.Numerics.BigInteger.ModPow(c, dBob, nBob) == sessionKey[i]).All(b => b)
              && text == "Meet me at noon|" + smime.Signature && smime.Signature == System.Numerics.BigInteger.ModPow(new System.Numerics.BigInteger(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Meet me at noon")), isUnsigned: true, isBigEndian: true) % nAlice, Inv(17, new System.Numerics.BigInteger(1008) * 1012), nAlice),
              "S/MIME visualization: the session key, its RSA-encrypted bytes, the AES ciphertext (message | signature) and the signature equal the calculation with .NET");
        Check(smime.Valid && smime.Decrypted == "Meet me at noon" && Proto(3, "1009 1013 17 1019 1021 11", "Meet me at noon") == "valid" && Proto(3, "1009 1013 17 11 13 7", "x") == null && Proto(3, "1009 1013 17", "x") == null, "S/MIME visualization: Bob reads the message and the signature is valid; a modulus below 256 for Bob and missing parameters are refused");
        Check(Proto(1, "1009 1009 17", "x") == null && Proto(1, "1008 1013 17", "x") == null && Proto(1, "1009 1013 2", "x") == null, "signature visualization: equal primes, a number that is not prime and an exponent that is not coprime to phi are refused");
    }

    static void RandomVisualization()
    {
        var uniform = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        var statistics = global::RandomVisualization.Statistics.Of(uniform);
        Check(statistics.Count == 1024 && Math.Abs(statistics.Mean - 127.5) < 1e-9 && Math.Abs(statistics.ChiSquare) < 1e-9, "random visualization: every byte value four times: mean 127.5 and chi-square 0");
        var ramp = global::RandomVisualization.Statistics.Of(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
        var alternating = global::RandomVisualization.Statistics.Of(Enumerable.Range(0, 1000).Select(i => (byte)(i % 2 == 0 ? 0 : 255)).ToArray());
        var constant = global::RandomVisualization.Statistics.Of(new byte[1024]);
        Check(ramp.Correlation > 0.98 && alternating.Correlation < -0.99 && constant.ChiSquare > 100000 && constant.Correlation == 0, "random visualization: a rising ramp has a correlation near +1, an alternating sequence near -1; a constant sequence has an enormous chi-square value");

        // random bytes of the system: mean, chi-square and correlation are in their usual range (about 127.5; 255 +- 23, here allowed +- 6 sigma; about 0 +- 0.03)
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20000);
        var good = global::RandomVisualization.Statistics.Of(random);
        Check(Math.Abs(good.Mean - 127.5) < 3 && good.ChiSquare > 255 - 6 * 23 && good.ChiSquare < 255 + 6 * 23 && Math.Abs(good.Correlation) < 0.05, $"random visualization: 20000 random bytes: mean {good.Mean:F2}, chi-square {good.ChiSquare:F1}, correlation {good.Correlation:F4}");

        // a linear congruential generator with a small modulus: the low byte of x -> (1103515245 x + 12345) mod 2^31 repeats after 256 steps; its bytes are far from random
        long x = 1;
        var lcg = new byte[20000];
        for (var i = 0; i < lcg.Length; i++) { x = (1103515245 * x + 12345) % (1L << 31); lcg[i] = (byte)x; }
        var weak = global::RandomVisualization.Statistics.Of(lcg);
        Check(weak.Counts.Count(c => c > 0) == 256 && weak.Counts.Distinct().Count() <= 2, $"random visualization: the low byte of this LCG has a period of 256 and repeats exactly: every value occurs equally often (chi-square {weak.ChiSquare:F1}), which a histogram cannot tell from random data");
        var component = Component("RandomVisualization.RandomVisualization");
        component.PreExecution();
        component.Set("InputData", uniform);
        component.Execute();
        var summary = (string?)component.Get("Summary");
        component.PostExecution();
        Check(summary == "bytes=1024 mean=127.500 chi2=0.00 correlation=0.9961" || (summary != null && summary.StartsWith("bytes=1024 mean=127.500 chi2=0.00 correlation=")), $"random visualization: the summary line ({summary})");
    }

    static void Digest(string name, int algorithm, string text, string expected)
    {
        var component = Component("MessageDigests.MessageDigests", ("Algorithm", algorithm.ToString()));
        component.PreExecution();
        var input = new CStreamWriter();
        var data = System.Text.Encoding.ASCII.GetBytes(text);
        input.Write(data, 0, data.Length);
        input.Close();
        component.Set("InputData", input);
        component.Execute();
        var actual = Convert.ToHexString((byte[])component.Get("OutputData")!);
        component.PostExecution();
        Check(actual == expected, $"{name}(\"{text}\") = {actual}");
    }

    /// <summary>The hash value of one block from the cipher: the initial value plus the encrypted initial value, word by word modulo 2^32; and decryption must return the initial value.</summary>
    static void Shacal(string name, int algorithm, byte[] blockAsKey, string initialValue, string expectedHash)
    {
        var iv = Convert.FromHexString(initialValue);
        var encrypt = Component("BlockCiphers.BlockCiphers", ("Algorithm", algorithm.ToString()), ("Action", "0"), ("Mode", "0"), ("Padding", "0"));
        var encrypted = Run(encrypt, iv, blockAsKey);
        var hash = new byte[iv.Length];
        for (var i = 0; i < iv.Length; i += 4)
        {
            var sum = (uint)(iv[i] << 24 | iv[i + 1] << 16 | iv[i + 2] << 8 | iv[i + 3]) + (uint)(encrypted[i] << 24 | encrypted[i + 1] << 16 | encrypted[i + 2] << 8 | encrypted[i + 3]);
            hash[i] = (byte)(sum >> 24); hash[i + 1] = (byte)(sum >> 16); hash[i + 2] = (byte)(sum >> 8); hash[i + 3] = (byte)sum;
        }
        var decrypt = Component("BlockCiphers.BlockCiphers", ("Algorithm", algorithm.ToString()), ("Action", "1"), ("Mode", "0"), ("Padding", "0"));
        var back = Run(decrypt, encrypted, blockAsKey);
        Check(Convert.ToHexString(hash) == expectedHash && back.SequenceEqual(iv), $"{name}: IV + encrypted IV = {Convert.ToHexString(hash)}, and decryption returns the IV");
    }

    static void RoundTrip(string name, int algorithm, int keySize)
    {
        var data = System.Text.Encoding.ASCII.GetBytes("Round trip: 37 bytes of plain text!!!");
        var key = Enumerable.Range(1, keySize).Select(i => (byte)(i * 7)).ToArray();
        var ok = true;
        foreach (var mode in new[] { 0, 1, 2, 3 })
        {
            // padding 2 = PKCS7
            var encrypted = Run(Component("BlockCiphers.BlockCiphers", ("Algorithm", algorithm.ToString()), ("Action", "0"), ("Mode", mode.ToString()), ("Padding", "2")), data, key, new byte[16]);
            var decrypted = Run(Component("BlockCiphers.BlockCiphers", ("Algorithm", algorithm.ToString()), ("Action", "1"), ("Mode", mode.ToString()), ("Padding", "2")), encrypted, key, new byte[16]);
            ok &= decrypted.SequenceEqual(data) && !encrypted.SequenceEqual(data);
        }
        Check(ok, $"{name} encrypt/decrypt round trip in ECB, CBC, CFB, OFB with PKCS7 ({keySize * 8}-bit key)");
    }
}

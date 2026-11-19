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
        KeySearchTests();
        AnalysisTests();
        ToolTests();
        KeyAndCertificateTests();
        EncryptionSchemeTests();
        EcAndHomomorphicTests();
        SignatureSchemeTests();
        MlKemTests();
        CodeAndIdentificationTests();
        CfsTests();
        DesVariantTests();
        ExtraCipherTests();

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

    static void KeySearchTests()
    {
        byte[] Hex(string h) => global::KeySearch.KeySearching.FromHex(h);
        string ToHex(byte[] b) => global::KeySearch.KeySearching.ToHex(b);
        Check(ToHex(global::KeySearch.KeySearching.Encrypt(2, System.Text.Encoding.ASCII.GetBytes("Key"), System.Text.Encoding.ASCII.GetBytes("Plaintext"))) == "BBF316E8D940AF0AD3", "RC4: key \"Key\", plaintext \"Plaintext\" gives BBF316E8D940AF0AD3 (the well-known example)");
        Check(ToHex(global::KeySearch.KeySearching.Encrypt(7, Hex("000102030405060708090A0B0C0D0E0F"), Hex("00112233445566778899AABBCCDDEEFF"))) == "69C4E0D86A7B0430D8CDB78070B4C55A", "AES in the key search: FIPS 197 example");
        Check(ToHex(global::KeySearch.KeySearching.Encrypt(8, Hex("133457799BBCDFF1"), Hex("0123456789ABCDEF"))) == "85E813540F0AB405", "DES in the key search: the classic example");
        // every cipher: key search with 10 unknown bits finds the key (the found key encrypts the plaintext to the ciphertext)
        for (int cipher = 0; cipher < global::KeySearch.KeySearching.Names.Length; cipher++)
        {
            var random = new Random(cipher + 1);
            var key = new byte[global::KeySearch.KeySearching.KeyLength(cipher)];
            random.NextBytes(key);
            int block = cipher == 2 ? 11 : (cipher == 0 || cipher == 6 || cipher == 8 || cipher == 1 || cipher == 9 || cipher == 10 || cipher == 12) ? 8 : 16;
            var plaintext = new byte[block * 2];
            random.NextBytes(plaintext);
            var ciphertext = global::KeySearch.KeySearching.Encrypt(cipher, key, plaintext);
            var guess = (byte[])key.Clone();
            guess[guess.Length - 1] ^= 0xff; guess[guess.Length - 2] ^= 0x03;   // the last 10 bits are wrong
            var found = global::KeySearch.KeySearching.Search(cipher, guess, 10, plaintext, ciphertext, out long tries);
            Check(found != null && global::KeySearch.KeySearching.Encrypt(cipher, found, plaintext).SequenceEqual(ciphertext) && tries <= 1024, $"key search {(global::KeySearch.KeySearching.Names[cipher])}: a key with 10 unknown bits is found after {tries} of 1024 tries and encrypts the plaintext to the ciphertext");
        }
        long none;
        Check(global::KeySearch.KeySearching.Search(0, new byte[16], 4, new byte[8], new byte[8], out none) == null && none == 16, "key search: a wrong ciphertext finds nothing after all 2^4 tries");
    }

    static void AnalysisTests()
    {
        // N-grams, normalized compression distance, byte-wise addition
        var grams = global::TextAnalysis.TextStatistics.NGrams("Hello, hello!", 1, 3);
        Check(grams[0].Text == "L" && grams[0].Count == 4 && grams[1].Text == "E" && grams.Sum(g => g.Count) == 8, "N-gram: letters of \"Hello, hello!\": L four times, then E, H, O twice (punctuation ignored); the top 3 are L, E, H");
        var bigrams = global::TextAnalysis.TextStatistics.NGrams("abab", 2, 5);
        Check(bigrams.Count == 2 && bigrams[0].Text == "AB" && bigrams[0].Count == 2 && bigrams[1].Text == "BA" && Math.Abs(bigrams[0].Percent - 66.666) < 0.01, "N-gram: bigrams of abab are AB twice and BA once");
        string english = string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog while the cat sleeps in the sun and the birds sing in the trees ", 8));
        string similar = string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog while the cat sleeps in the shade and the birds sing in the trees ", 8));
        var randomText = new string(Enumerable.Range(0, english.Length).Select(i => (char)('a' + new Random(i).Next(26) * 1)).ToArray());
        double same = global::TextAnalysis.TextStatistics.Ncd(english, english), near = global::TextAnalysis.TextStatistics.Ncd(english, similar), far = global::TextAnalysis.TextStatistics.Ncd(english, new string(Enumerable.Range(0, 900).Select(i => (char)(33 + (i * 7919 + i * i * 31) % 90)).ToArray()));
        Check(same < 0.1 && near < far && far > 0.6, $"Vitanyi distance: the same text {same:F3}, a similar text {near:F3}, an unrelated text {far:F3}");
        var secret = System.Text.Encoding.ASCII.GetBytes("Meet me at the old bridge at midnight and bring the documents.");
        var shifted = secret.Select(b => (byte)(b + 77)).ToArray();
        var ranked = global::TextAnalysis.TextStatistics.ByteAddition(shifted);
        Check(ranked[0].Value == 77 && ranked[0].Plaintext.SequenceEqual(secret), "byte-wise addition: the constant 77 is found and the text is read");

        // Hill cipher (the example of Wikipedia: key GYBNQKURP, ACT gives POH) and the known-plaintext attack
        var key3 = new int[,] { { 6, 24, 1 }, { 13, 16, 10 }, { 20, 17, 15 } };
        Check(global::ClassicAnalysis.ClassicAnalyses.HillEncrypt("ACT", key3) == "POH", "Hill cipher: key GYBNQKURP turns ACT into POH");
        string hillPlain = "HELLOWORLDFROMTHEHILLCIPHERXYZ", hillCipher = global::ClassicAnalysis.ClassicAnalyses.HillEncrypt(hillPlain, key3);
        var recovered = global::ClassicAnalysis.ClassicAnalyses.HillKey(hillPlain, hillCipher, 3);
        Check(Enumerable.Range(0, 9).All(i => recovered[i / 3, i % 3] == key3[i / 3, i % 3]), "Hill analysis: the key matrix is recovered from 10 blocks of known plaintext");
        var key2 = new int[,] { { 3, 3 }, { 2, 5 } };
        var inverse2 = global::ClassicAnalysis.ClassicAnalyses.Inverse(key2);
        Check(inverse2 != null && inverse2[0, 0] == 15 && inverse2[0, 1] == 17 && inverse2[1, 0] == 20 && inverse2[1, 1] == 9 && global::ClassicAnalysis.ClassicAnalyses.Inverse(new int[,] { { 2, 4 }, { 1, 2 } }) == null, "Hill: the inverse of (3 3; 2 5) modulo 26 is (15 17; 20 9), a matrix with determinant 0 has none");

        // Fleissner grille: every cell is a hole exactly once in four turns, round trip, and the analysis finds the text
        int[] choice = { 0, 1, 2, 3 };
        var cells = Enumerable.Range(0, 4).SelectMany(t => global::ClassicAnalysis.ClassicAnalyses.Holes(4, choice, t, 1)).ToList();
        Check(cells.Distinct().Count() == 16, "Fleissner grille: the 4 x 4 grille covers every cell once in its four positions");
        string plain = "THEMEETINGWILLBEHELDATTHEOLDBRIDGEATMIDNIGHTANDTHENEVERTHATENDSTHEMISSION";
        string padded = plain.PadRight((plain.Length + 15) / 16 * 16, 'X'), padded6 = plain.PadRight((plain.Length + 35) / 36 * 36, 'X');
        string cipher = global::ClassicAnalysis.ClassicAnalyses.FleissnerEncrypt(plain, 4, choice);
        Check(cipher != padded && cipher.OrderBy(c => c).SequenceEqual(padded.OrderBy(c => c)) && global::ClassicAnalysis.ClassicAnalyses.FleissnerDecrypt(cipher, 4, choice) == padded, "Fleissner grille: encryption permutes the letters and decryption gives the text back");
        var candidates = global::ClassicAnalysis.ClassicAnalyses.FleissnerAnalysis(cipher, 4, 3);
        Check(candidates[0].Text == padded, $"Fleissner analysis: of 512 grilles (both turning directions) the best one gives the text back (score {candidates[0].Score}, the next {candidates[1].Score})");
        var big = global::ClassicAnalysis.ClassicAnalyses.FleissnerEncrypt(plain, 6, Enumerable.Range(0, 9).Select(i => (i * 5 + 1) % 4).ToArray());
        Check(global::ClassicAnalysis.ClassicAnalyses.FleissnerAnalysis(big, 6, 1)[0].Text == padded6, "Fleissner analysis: a 6 x 6 grille (262144 grilles) is found as well");
    }

    static void ToolTests()
    {
        // number tools
        var big = System.Numerics.BigInteger.Pow(2, 100);
        Check(global::NumberTools.NumberFacts.Digits(big, 10) == 31 && global::NumberTools.NumberFacts.Bits(big) == 101 && global::NumberTools.NumberFacts.Bytes(big) == 13 && global::NumberTools.NumberFacts.Digits(big, 16) == 26 && global::NumberTools.NumberFacts.Digits(0, 10) == 1 && global::NumberTools.NumberFacts.Digits(999, 10) == 3 && global::NumberTools.NumberFacts.Digits(1000, 10) == 4, "number length: 2^100 has 31 decimal digits, 101 bits, 13 bytes and 26 hexadecimal digits; 999 has 3 and 1000 has 4 digits");
        var steps = new List<global::NumberTools.NumberFacts.Step>();
        Check(global::NumberTools.NumberFacts.ReciprocalSubtraction(252, 105, steps) == 21 && steps.Count == 3 && steps[0].Quotient == 2 && steps[0].Remainder == 42 && steps[1].Quotient == 2 && steps[2].Quotient == 2, "reciprocal subtraction: gcd(252, 105) = 21 in three steps (252 = 2 * 105 + 42, 105 = 2 * 42 + 21, 42 = 2 * 21)");
        var rnd = new Random(5);
        bool gcdOk = true;
        for (int i = 0; i < 200; i++) { var a = new System.Numerics.BigInteger(rnd.NextInt64(1, 1000000000000)); var b = new System.Numerics.BigInteger(rnd.NextInt64(1, 1000000000000)); gcdOk &= global::NumberTools.NumberFacts.ReciprocalSubtraction(a, b, new List<global::NumberTools.NumberFacts.Step>()) == System.Numerics.BigInteger.GreatestCommonDivisor(a, b); }
        Check(gcdOk, "reciprocal subtraction: equals the greatest common divisor of .NET for 200 random pairs");

        // convolutional code (7, 5): 1011 gives 11 10 00 01 01 11, and every combination of two bit errors is corrected
        Check(global::ConvolutionalCode.ConvolutionalCoding.Encode("1011") == "111000010111", "convolutional code (7, 5): 1011 is coded as 11 10 00 01 01 11 (with the two tail bits)");
        Check(global::ConvolutionalCode.ConvolutionalCoding.Decode("111000010111") == "1011", "Viterbi decoder: the error-free code gives the message back");
        string message = "10110100111010", coded = global::ConvolutionalCode.ConvolutionalCoding.Encode(message);
        bool single = Enumerable.Range(1, coded.Length).All(p => global::ConvolutionalCode.ConvolutionalCoding.Decode(global::ConvolutionalCode.ConvolutionalCoding.Flip(coded, new[] { p })) == message);
        bool pairs = true;
        for (int p = 1; p <= coded.Length; p++) { for (int q = p + 1; q <= coded.Length; q++) { pairs &= global::ConvolutionalCode.ConvolutionalCoding.Decode(global::ConvolutionalCode.ConvolutionalCoding.Flip(coded, new[] { p, q })) == message; } }
        Check(single && pairs, $"Viterbi decoder: every single error and every pair of errors in the {coded.Length} coded bits is corrected (free distance 5)");
        Check(global::ConvolutionalCode.ConvolutionalCoding.Decode(global::ConvolutionalCode.ConvolutionalCoding.Flip(coded, new[] { 1, 2, 3, 4, 5, 6 })) != message, "Viterbi decoder: six errors in a row are too many");

        // one-time passwords: RFC 4226 appendix D and RFC 6238 appendix B
        byte[] secret = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");
        var hotp = Enumerable.Range(0, 10).Select(c => global::OneTimePassword.OneTimePasswords.Hotp(secret, c, 6, 0)).ToArray();
        Check(string.Join(" ", hotp) == "755224 287082 359152 969429 338314 254676 287922 162583 399871 520489", "HOTP: the ten test values of RFC 4226 (755224 287082 359152 969429 338314 254676 287922 162583 399871 520489)");
        var totp = new long[] { 59, 1111111109, 1111111111, 1234567890, 2000000000, 20000000000 }.Select(t => global::OneTimePassword.OneTimePasswords.Totp(secret, t, 30, 8, 0)).ToArray();
        Check(string.Join(" ", totp) == "94287082 07081804 14050471 89005924 69279037 65353130", "TOTP (SHA-1): the test values of RFC 6238 for 59, 1111111109, 1111111111, 1234567890, 2000000000 and 20000000000");
        Check(global::OneTimePassword.OneTimePasswords.Totp(System.Text.Encoding.ASCII.GetBytes("12345678901234567890123456789012"), 59, 30, 8, 1) == "46119246" && global::OneTimePassword.OneTimePasswords.Totp(System.Text.Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234"), 59, 30, 8, 2) == "90693936", "TOTP: the SHA-256 and SHA-512 test values of RFC 6238 at time 59");
        Check(global::OneTimePassword.OneTimePasswords.Base32("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ").SequenceEqual(secret), "Base32: GEZDGNBVGY3TQOJQ... is 12345678901234567890");

        // Exposure Notification: HKDF against .NET, RPI against AES of .NET, and the metadata round trip
        byte[] tek = Enumerable.Range(0, 16).Select(i => (byte)(i * 17 + 3)).ToArray();
        byte[] rpik = global::ExposureNotification.ExposureNotificationKeys.RollingProximityKey(tek);
        Check(rpik.SequenceEqual(System.Security.Cryptography.HKDF.DeriveKey(System.Security.Cryptography.HashAlgorithmName.SHA256, tek, 16, null, System.Text.Encoding.ASCII.GetBytes("EN-RPIK"))) && global::ExposureNotification.ExposureNotificationKeys.MetadataKey(tek).SequenceEqual(System.Security.Cryptography.HKDF.DeriveKey(System.Security.Cryptography.HashAlgorithmName.SHA256, tek, 16, null, System.Text.Encoding.ASCII.GetBytes("EN-AEMK"))), "Exposure Notification: RPIK and AEMK equal HKDF-SHA-256 of .NET with the labels EN-RPIK and EN-AEMK");
        uint interval = 2670000;
        var block = new byte[16]; System.Text.Encoding.ASCII.GetBytes("EN-RPI").CopyTo(block, 0); BitConverter.GetBytes(interval).CopyTo(block, 12);
        byte[] expected = System.Security.Cryptography.Aes.Create().Let(a => { a.Key = rpik; return a.EncryptEcb(block, System.Security.Cryptography.PaddingMode.None); });
        byte[] rpi = global::ExposureNotification.ExposureNotificationKeys.RollingProximityIdentifier(tek, interval);
        Check(rpi.SequenceEqual(expected) && !rpi.SequenceEqual(global::ExposureNotification.ExposureNotificationKeys.RollingProximityIdentifier(tek, interval + 1)), "Exposure Notification: the identifier is AES-128(RPIK, \"EN-RPI\" || 0 || interval) and changes with every interval");
        byte[] metadata = { 0x40, 0xe2, 0x00, 0x00 };
        byte[] encrypted = global::ExposureNotification.ExposureNotificationKeys.EncryptMetadata(tek, rpi, metadata);
        Check(!encrypted.SequenceEqual(metadata) && global::ExposureNotification.ExposureNotificationKeys.EncryptMetadata(tek, rpi, encrypted).SequenceEqual(metadata), "Exposure Notification: the metadata are encrypted and the same operation decrypts them");
    }

    static T2 Let<T, T2>(this T value, Func<T, T2> f) => f(value);

    static void KeyAndCertificateTests()
    {
        // ASN.1 viewer: SEQUENCE { INTEGER 5, OCTET STRING "ab" }
        string dump = global::Asn1Viewer.Asn1Tools.Dump(global::Asn1Viewer.Asn1Tools.ReadBytes("30 07 02 01 05 04 02 61 62"));
        Check(dump.Contains("Sequence") && dump.Contains("5") && dump.Contains("6162"), "ASN.1 viewer: SEQUENCE { INTEGER 5, OCTET STRING 6162 } is shown with its parts");
        Check(global::Asn1Viewer.Asn1Tools.ReadBytes("MAcCAQUEAmFi").SequenceEqual(global::Asn1Viewer.Asn1Tools.ReadBytes("3007020105040261 62")), "ASN.1 viewer: Base64 and hexadecimal give the same bytes");

        // keys: RSA and EC written as PEM are read by .NET, and the other way round
        var rsaPair = global::KeyTool.KeyTools.GenerateRsa(1024);
        string rsaPrivate = global::KeyTool.KeyTools.PrivatePem(rsaPair.Private), rsaPublic = global::KeyTool.KeyTools.PublicPem(rsaPair.Public);
        using (var net = System.Security.Cryptography.RSA.Create())
        {
            net.ImportFromPem(rsaPrivate);
            var signature = net.SignData(new byte[] { 1, 2, 3 }, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using (var pub = System.Security.Cryptography.RSA.Create())
            {
                pub.ImportFromPem(rsaPublic);
                Check(net.KeySize == 1024 && pub.VerifyData(new byte[] { 1, 2, 3 }, signature, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1) && rsaPrivate.StartsWith("-----BEGIN PRIVATE KEY-----"), "key tool: the RSA key pair (1024 bits) as PEM is read by .NET, a signature with the private key verifies with the public key");
            }
            string pkcs1 = net.ExportRSAPrivateKeyPem();
            AsymmetricKeyParameterHolder.Test(pkcs1, rsaPublic);
        }
        var ecPair = global::KeyTool.KeyTools.GenerateEc("secp256r1");
        using (var ec = System.Security.Cryptography.ECDsa.Create())
        {
            ec.ImportFromPem(global::KeyTool.KeyTools.PrivatePem(ecPair.Private));
            var signature = ec.SignData(new byte[] { 9 }, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using (var pub = System.Security.Cryptography.ECDsa.Create())
            {
                pub.ImportFromPem(global::KeyTool.KeyTools.PublicPem(ecPair.Public));
                Check(ec.KeySize == 256 && pub.VerifyData(new byte[] { 9 }, signature, System.Security.Cryptography.HashAlgorithmName.SHA256), "key tool: the EC key pair (P-256) as PEM is read by .NET and a signature verifies");
            }
            Org.BouncyCastle.Crypto.AsymmetricKeyParameter pubKey;
            var priv = global::KeyTool.KeyTools.ReadKey(ec.ExportECPrivateKeyPem(), out pubKey);
            Check(priv != null && global::KeyTool.KeyTools.Describe(priv, pubKey).Contains("curve prime256v1 (256 bits)") && global::KeyTool.KeyTools.PublicPem(pubKey) == global::KeyTool.KeyTools.PublicPem(ecPair.Public), "key tool: an EC key in SEC 1 format written by .NET is read, the curve is named (prime256v1 = secp256r1) and the public key is the same");
        }

        // certificates: the chain is valid, other chains, dates and a wrong issuer are not; .NET builds the same chain
        var now = DateTime.UtcNow;
        var demo = global::CertificateTool.Certificates.CreateChain("www.example.org", now);
        var report = new System.Text.StringBuilder();
        var chain = new List<Org.BouncyCastle.X509.X509Certificate> { demo.Leaf, demo.Intermediate, demo.Root };
        Check(global::CertificateTool.Certificates.VerifyChain(chain, now, report) && report.ToString().Contains("The chain is valid"), "certificates: the chain root - intermediate - server certificate is valid");
        Check(!global::CertificateTool.Certificates.VerifyChain(chain, now.AddYears(20), new System.Text.StringBuilder()), "certificates: after the validity period the chain is not valid");
        var other = global::CertificateTool.Certificates.CreateChain("www.example.org", now);
        Check(!global::CertificateTool.Certificates.VerifyChain(new List<Org.BouncyCastle.X509.X509Certificate> { demo.Leaf, other.Intermediate, demo.Root }, now, new System.Text.StringBuilder()), "certificates: an intermediate certificate of another chain does not fit (wrong signature)");
        Check(!global::CertificateTool.Certificates.VerifyChain(new List<Org.BouncyCastle.X509.X509Certificate> { demo.Intermediate, demo.Leaf }, now, new System.Text.StringBuilder()), "certificates: a server certificate as issuer is refused (not a CA, wrong signature)");
        var read = global::CertificateTool.Certificates.Read(demo.Chain);
        Check(read.Count == 3 && read[0].SubjectDN.ToString().Contains("www.example.org") && global::CertificateTool.Certificates.Describe(read[2]).Contains("Certification authority: yes"), "certificates: the PEM blocks are read again; the server certificate carries the host name, the root is a CA");
        using (var netRoot = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(global::CertificateTool.Certificates.ToPem(demo.Root)))
        using (var netIntermediate = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(global::CertificateTool.Certificates.ToPem(demo.Intermediate)))
        using (var netLeaf = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(global::CertificateTool.Certificates.ToPem(demo.Leaf)))
        using (var netChain = new System.Security.Cryptography.X509Certificates.X509Chain())
        {
            netChain.ChainPolicy.TrustMode = System.Security.Cryptography.X509Certificates.X509ChainTrustMode.CustomRootTrust;
            netChain.ChainPolicy.CustomTrustStore.Add(netRoot);
            netChain.ChainPolicy.ExtraStore.Add(netIntermediate);
            netChain.ChainPolicy.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
            Check(netChain.Build(netLeaf) && netChain.ChainElements.Count == 3, "certificates: .NET builds and trusts the chain with our root certificate");
        }

        // CMS SignedData: the signature extraction shows signer and signature, and the message verifies
        var gen = new Org.BouncyCastle.Cms.CmsSignedDataGenerator();
        gen.AddSigner(demo.LeafKeys.Private, demo.Leaf, Org.BouncyCastle.Cms.CmsSignedGenerator.DigestSha256);
        gen.AddCertificates(Org.BouncyCastle.Utilities.Collections.CollectionUtilities.CreateStore(new List<Org.BouncyCastle.X509.X509Certificate> { demo.Leaf }));
        var signed = gen.Generate(new Org.BouncyCastle.Cms.CmsProcessableByteArray(System.Text.Encoding.UTF8.GetBytes("Pay Bob 10 euros")), true);
        string extracted = global::Asn1Viewer.Asn1Tools.Signatures(signed.GetEncoded());
        var signerInfo = signed.GetSignerInfos().GetSigners().First();
        Check(extracted.Contains("Pay Bob 10 euros") && extracted.Contains(global::Asn1Viewer.Asn1Tools.ToHex(signerInfo.GetSignature())) && extracted.Contains("www.example.org") && signerInfo.Verify(demo.Leaf), "signature extraction: the CMS message shows the content, the signer, the certificate and the signature value, and the signature is valid");
    }

    static class AsymmetricKeyParameterHolder
    {
        public static void Test(string pkcs1Pem, string publicPem)
        {
            Org.BouncyCastle.Crypto.AsymmetricKeyParameter pub;
            var priv = global::KeyTool.KeyTools.ReadKey(pkcs1Pem, out pub);
            Check(priv != null && global::KeyTool.KeyTools.Describe(priv, pub).Contains("RSA, modulus of 1024 bits") && global::KeyTool.KeyTools.PublicPem(pub) == publicPem, "key tool: an RSA key in PKCS#1 format written by .NET is read, it has 1024 bits and the public key is the same");
            Org.BouncyCastle.Crypto.AsymmetricKeyParameter pub2;
            Check(global::KeyTool.KeyTools.ReadKey(publicPem, out pub2) == null && pub2 != null, "key tool: a public key alone is read without a private key");
        }
    }

    static void EncryptionSchemeTests()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        byte[] pkcs8 = rsa.ExportPkcs8PrivateKey();

        // PBES2: a key encrypted by .NET is decrypted by us (all three hash functions), and a structure made by us is read by .NET
        var hashes = new[] { System.Security.Cryptography.HashAlgorithmName.SHA1, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.HashAlgorithmName.SHA512 };
        for (int hash = 0; hash < 3; hash++)
        {
            byte[] netEncrypted = rsa.ExportEncryptedPkcs8PrivateKey("correct horse", new System.Security.Cryptography.PbeParameters(System.Security.Cryptography.PbeEncryptionAlgorithm.Aes256Cbc, hashes[hash], 2000));
            Check(global::PasswordBasedEncryption.Pbes2.Decrypt(netEncrypted, "correct horse").SequenceEqual(pkcs8), $"PBES2: a private key encrypted by .NET (PBKDF2 with {hashes[hash].Name}, AES-256-CBC) is decrypted by our component");
            byte[] ours = global::PasswordBasedEncryption.Pbes2.Encrypt(pkcs8, "battery staple", Enumerable.Range(1, 16).Select(i => (byte)i).ToArray(), Enumerable.Range(17, 16).Select(i => (byte)i).ToArray(), 1500, hash);
            using var imported = System.Security.Cryptography.RSA.Create();
            imported.ImportEncryptedPkcs8PrivateKey("battery staple", ours, out _);
            Check(imported.ExportPkcs8PrivateKey().SequenceEqual(pkcs8), $"PBES2: the structure made by our component (PBKDF2 with {hashes[hash].Name}) is read by .NET and gives the same key");
        }
        bool refused = false;
        try { global::PasswordBasedEncryption.Pbes2.Decrypt(global::PasswordBasedEncryption.Pbes2.Encrypt(new byte[] { 1, 2, 3 }, "a", new byte[8], new byte[16], 10, 1), "b"); } catch (ArgumentException) { refused = true; }
        Check(refused, "PBES2: a wrong password is refused (the padding is wrong)");
        Check(global::PasswordBasedEncryption.Pbes2.Decrypt(global::PasswordBasedEncryption.Pbes2.Encrypt(System.Text.Encoding.UTF8.GetBytes("hello world"), "pw", new byte[8], new byte[16], 10, 1), "pw").SequenceEqual(System.Text.Encoding.UTF8.GetBytes("hello world")), "PBES2: encryption and decryption of a short text");

        // hybrid encryption against RSA-OAEP and AES-GCM of .NET
        string privatePem = rsa.ExportPkcs8PrivateKeyPem(), publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        byte[] message = System.Text.Encoding.UTF8.GetBytes("Meet me at the old bridge at midnight.");
        byte[] packet = global::HybridEncryption.HybridEncryptionScheme.Encrypt(message, global::HybridEncryption.HybridEncryptionScheme.ReadKey(publicPem));
        int length = packet[0] << 8 | packet[1];
        byte[] wrapped = packet.Skip(2).Take(length).ToArray(), nonce = packet.Skip(2 + length).Take(12).ToArray(), rest = packet.Skip(14 + length).ToArray();
        byte[] aesKey = rsa.Decrypt(wrapped, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA256);
        var plain = new byte[message.Length];
        using (var gcm = new System.Security.Cryptography.AesGcm(aesKey, 16)) { gcm.Decrypt(nonce, rest.Take(message.Length).ToArray(), rest.Skip(message.Length).ToArray(), plain); }
        Check(length == 256 && aesKey.Length == 32 && plain.SequenceEqual(message), "hybrid encryption: .NET unwraps the AES key (RSA-OAEP, SHA-256) and decrypts the message (AES-256-GCM) of our packet");
        // a packet made by .NET is decrypted by us
        byte[] netKey = new byte[32], netNonce = new byte[12], netCipher = new byte[message.Length], netTag = new byte[16];
        new Random(8).NextBytes(netKey); new Random(9).NextBytes(netNonce);
        using (var gcm = new System.Security.Cryptography.AesGcm(netKey, 16)) { gcm.Encrypt(netNonce, message, netCipher, netTag); }
        byte[] netWrapped = rsa.Encrypt(netKey, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA256);
        byte[] netPacket = new[] { (byte)(netWrapped.Length >> 8), (byte)netWrapped.Length }.Concat(netWrapped).Concat(netNonce).Concat(netCipher).Concat(netTag).ToArray();
        Check(global::HybridEncryption.HybridEncryptionScheme.Decrypt(netPacket, global::HybridEncryption.HybridEncryptionScheme.ReadKey(privatePem)).SequenceEqual(message), "hybrid encryption: a packet made with .NET is decrypted by our component");
        packet[packet.Length - 1] ^= 1;
        bool tampered = false;
        try { global::HybridEncryption.HybridEncryptionScheme.Decrypt(packet, global::HybridEncryption.HybridEncryptionScheme.ReadKey(privatePem)); } catch (ArgumentException) { tampered = true; }
        Check(tampered, "hybrid encryption: a changed packet is refused (authentication tag)");

        // Burmester-Desmedt: three to six parties get the same key g^(r1 r2 + r2 r3 + ... + rn r1)
        bool all = true;
        for (int parties = 3; parties <= 6; parties++)
        {
            var secrets = Enumerable.Range(0, parties).Select(i => new System.Numerics.BigInteger(7 + i * 13 + i * i)).ToList();
            var result = global::MultipartyKeyExchange.BurmesterDesmedt.Run(1019, 2, secrets);
            System.Numerics.BigInteger exponent = 0;
            for (int i = 0; i < parties; i++) { exponent += secrets[i] * secrets[(i + 1) % parties]; }
            all &= result.Agree && result.Parties.All(x => x.Key == System.Numerics.BigInteger.ModPow(2, exponent, 1019));
        }
        Check(all, "Burmester-Desmedt: 3, 4, 5 and 6 parties all compute g^(r1 r2 + r2 r3 + ... + rn r1) mod p");
        bool bad = false;
        try { global::MultipartyKeyExchange.BurmesterDesmedt.Run(1021 * 3, 2, new System.Numerics.BigInteger[] { 3, 4, 5 }); } catch (ArgumentException) { bad = true; }
        try { global::MultipartyKeyExchange.BurmesterDesmedt.Run(1019, 2, new System.Numerics.BigInteger[] { 3, 4 }); bad = false; } catch (ArgumentException) { }
        Check(bad, "Burmester-Desmedt: a modulus that is not prime and fewer than three parties are refused");
    }

    static void EcAndHomomorphicTests()
    {
        // elliptic curve generator: the backdoor predicts the output; the points are on the curve and P = e * Q
        var generator = new global::EcRandom.EcGenerator(0, 3, 3);
        Check(generator.P.IsValid() && generator.Q.IsValid() && generator.P.Equals(generator.Q.Multiply(generator.Backdoor)) && generator.BlockBits == 240, "EC generator: P and Q are points of P-256, P = e * Q, and the blocks have 240 bits");
        var output = Enumerable.Range(0, 6).Select(i => generator.Next()).ToList();
        var predicted = generator.Predict(output, 6);
        Check(predicted.Count == 6 && predicted.Zip(output, (a, b) => a.Equals(b)).All(x => x) && output.Distinct().Count() == 6, "EC generator: from the first two blocks the backdoor predicts the next four blocks (16 bits tried) exactly");
        var wrong = new global::EcRandom.EcGenerator(0, 3, 3);
        var other = new global::EcRandom.EcGenerator(0, 4, 3);
        Check(other.Predict(new List<Org.BouncyCastle.Math.BigInteger> { wrong.Next(), wrong.Next() }, 4).Count == 0, "EC generator: without the right backdoor number the attack finds nothing");

        // homomorphic encryption
        var rsa = new global::HomomorphicEncryption.Homomorphic.RsaScheme(1);
        System.Numerics.BigInteger a = 12345, b = 6789;
        Check(rsa.Decrypt(rsa.Encrypt(a)) == a && rsa.Decrypt(rsa.Multiply(rsa.Encrypt(a), rsa.Encrypt(b))) == a * b % rsa.N, "homomorphic RSA: D(E(a) * E(b)) = a * b mod n");
        var paillier = new global::HomomorphicEncryption.Homomorphic.PaillierScheme(1);
        var ca = paillier.Encrypt(a); var cb = paillier.Encrypt(b);
        Check(paillier.Decrypt(ca) == a && ca != paillier.Encrypt(a) && paillier.Decrypt(paillier.Add(ca, cb)) == a + b && paillier.Decrypt(paillier.Scale(ca, b)) == a * b % paillier.N, "Paillier: encryption is randomized, D(E(a) * E(b)) = a + b and D(E(a)^b) = a * b");
        var dghv = new global::HomomorphicEncryption.Homomorphic.DghvScheme(1);
        bool gates = true;
        for (int x = 0; x <= 1; x++) { for (int y = 0; y <= 1; y++) { var cx = dghv.Encrypt(x); var cy = dghv.Encrypt(y); gates &= dghv.Decrypt(cx) == x && dghv.Decrypt(dghv.Xor(cx, cy)) == (x ^ y) && dghv.Decrypt(dghv.And(cx, cy)) == (x & y); } }
        Check(gates, "DGHV: the sum of two ciphertexts decrypts to the XOR and the product to the AND of the bits, for all four pairs");
        var deep = dghv.Encrypt(1);
        int levels = 0, noise0 = dghv.Noise(deep);
        for (; levels < 40; levels++) { deep = dghv.And(deep, dghv.Encrypt(1)); if (dghv.Decrypt(deep) != 1) { break; } }
        Check(levels > 3 && levels < 40 && dghv.Noise(deep) > noise0, $"DGHV: the noise grows with every multiplication (from {noise0} bits); the decryption is wrong after {levels} multiplications, bootstrapping would be needed");
    }

    static void SignatureSchemeTests()
    {
        var gq = new global::SignatureSchemes.GuillouQuisquater(1, "alice");
        Check(System.Numerics.BigInteger.ModPow(gq.B, gq.E, gq.N) * gq.J % gq.N == 1, "Guillou-Quisquater: the secret B satisfies B^e * J = 1 mod n");
        bool gqOk = true, gqBad = true;
        for (int i = 0; i < 20; i++)
        {
            string message = "message " + i;
            gq.Sign(message, out var d, out var big);
            gqOk &= gq.Verify(message, d, big);
            gqBad &= !gq.Verify(message + "!", d, big) && !gq.Verify(message, d, big + 1);
        }
        Check(gqOk && gqBad, "Guillou-Quisquater: 20 signatures verify, and a changed message or a changed D is refused");

        var red = new global::SignatureSchemes.RedactableSignature(2);
        red.Sign("The meeting is on Friday at the old bridge with Mr Smith");
        var full = red.Redact(new int[0]);
        var partly = red.Redact(new[] { 7, 8, 9, 10, 11 });
        Check(red.Verify(full) && red.Verify(partly) && partly.Words.Count(w => w == null) == 5 && partly.Salts[7] == null, "redactable signature: the signature is valid for the whole message and for the message with five words removed");
        partly.Words[2] = "Monday";
        Check(!red.Verify(partly), "redactable signature: a changed word that is still visible makes the signature invalid");
        var other = red.Redact(new[] { 1 });
        other.Hashes[0] = global::SignatureSchemes.Hashing.H(new byte[] { 1 });
        Check(!red.Verify(other), "redactable signature: a hash value put in place of a hidden word makes the signature invalid");
        Check(global::SignatureSchemes.RedactableSignature.Root(new List<byte[]> { new byte[] { 1 } }).SequenceEqual(new byte[] { 1 }) && !global::SignatureSchemes.RedactableSignature.Root(new List<byte[]> { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 } }).SequenceEqual(global::SignatureSchemes.RedactableSignature.Root(new List<byte[]> { new byte[] { 1 }, new byte[] { 3 }, new byte[] { 2 } })), "Merkle tree: one leaf is its own root, and the order of the leaves matters");

        var tree = new global::SignatureSchemes.MultiTreeSignature(3, 2, 2);
        byte[] publicKey = tree.PublicKey;
        bool treeOk = true;
        foreach (long index in new long[] { 0, 1, 5, 14, 15 })
        {
            byte[] signature = tree.Sign("message " + index, index);
            treeOk &= global::SignatureSchemes.MultiTreeSignature.Verify("message " + index, signature, publicKey, 2, 2) && signature.Length == 4 + 2 * (67 + 2) * 32;
        }
        Check(treeOk && tree.Capacity == 16, "multi-tree Merkle signature: 2 layers of height 2 (16 signatures): signatures with the numbers 0, 1, 5, 14 and 15 verify against the public key");
        byte[] sig = tree.Sign("hello", 6);
        byte[] damaged = (byte[])sig.Clone(); damaged[200] ^= 1;
        Check(!global::SignatureSchemes.MultiTreeSignature.Verify("hellO", sig, publicKey, 2, 2) && !global::SignatureSchemes.MultiTreeSignature.Verify("hello", damaged, publicKey, 2, 2) && !global::SignatureSchemes.MultiTreeSignature.Verify("hello", sig, new global::SignatureSchemes.MultiTreeSignature(4, 2, 2).PublicKey, 2, 2), "multi-tree Merkle signature: a changed message, a changed signature and another public key are refused");
        var three = new global::SignatureSchemes.MultiTreeSignature(5, 3, 1);
        Check(three.Capacity == 8 && Enumerable.Range(0, 8).All(i => global::SignatureSchemes.MultiTreeSignature.Verify("m", three.Sign("m", i), three.PublicKey, 3, 1)), "multi-tree Merkle signature: 3 layers of height 1 give 8 signatures, all valid");
        bool refused = false;
        try { tree.Sign("x", 16); } catch (ArgumentException) { refused = true; }
        Check(refused, "multi-tree Merkle signature: a signature number beyond the capacity is refused");

        var uov = new global::SignatureSchemes.OilAndVinegar(7, 4, 8);
        bool uovOk = true, uovBad = true;
        for (int i = 0; i < 10; i++)
        {
            var s = uov.Sign("message " + i);
            uovOk &= uov.Verify("message " + i, s);
            uovBad &= !uov.Verify("message " + i + "x", s);
            var damagedSignature = (int[])s.Clone(); damagedSignature[3] = (damagedSignature[3] + 1) % 251;
            uovBad &= !uov.Verify("message " + i, damagedSignature);
        }
        Check(uovOk && uovBad, "Oil and Vinegar: 10 signatures verify (the public polynomials give the target values), a changed message or signature is refused");
        var identity = global::SignatureSchemes.OilAndVinegar.Multiply(new int[,] { { 3, 7 }, { 2, 5 } }, global::SignatureSchemes.OilAndVinegar.Invert(new int[,] { { 3, 7 }, { 2, 5 } }));
        Check(identity[0, 0] == 1 && identity[1, 1] == 1 && identity[0, 1] == 0 && identity[1, 0] == 0, "Oil and Vinegar: the matrix inverse modulo 251 is correct");
        Check(uov.Public.All(p => p.GetLength(0) == 12) && uov.Public.Length == 4, "Oil and Vinegar: 4 public polynomials in 12 variables");
    }

    static void MlKemVector(int set, string seed, string ekSha256, string ciphertext, string secret)
    {
        var scheme = new global::MlKem.MlKemScheme(set);
        byte[] seedBytes = global::MlKem.MlKemScheme.FromHex(seed);
        scheme.KeyGen(seedBytes.Take(32).ToArray(), seedBytes.Skip(32).ToArray(), out var ek, out var dk);
        Check(ek.Length == scheme.EncapsulationKeySize && dk.Length == scheme.DecapsulationKeySize && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ek)) == ekSha256, $"{scheme.Name}: the public key made from the seed of OpenSSL has the same SHA-256 ({ekSha256[..8]}...) as the public key of OpenSSL");
        Check(global::MlKem.MlKemScheme.Hex(scheme.Decapsulate(dk, global::MlKem.MlKemScheme.FromHex(ciphertext))) == secret, $"{scheme.Name}: our decapsulation of a ciphertext made by OpenSSL gives the shared secret of OpenSSL");
        var random = new Random(set + 1);
        var m = new byte[32]; random.NextBytes(m);
        scheme.Encapsulate(ek, m, out var secretBytes, out var ct);
        var damaged = (byte[])ct.Clone(); damaged[5] ^= 0x10;
        var rejected = scheme.Decapsulate(dk, damaged);
        Check(ct.Length == scheme.CiphertextSize && scheme.Decapsulate(dk, ct).SequenceEqual(secretBytes) && !rejected.SequenceEqual(secretBytes) && rejected.SequenceEqual(scheme.Decapsulate(dk, damaged)), $"{scheme.Name}: our encapsulation and decapsulation agree ({ct.Length}-byte ciphertext); a changed ciphertext gives another, but always the same secret (implicit rejection)");
    }

    static void MlKemTests()
    {
        MlKemVector(0, "BD192E543B5D33CFC2DFCE54371F911C0320767707EF588A9D438FF7A533965C231828C72B7632AF03DFB1EEED11D2AF79094CA74E96B77BCB7E809ADDA8F920", "ED2E181D7B1791403CB2B168E1214000C8D2285C64F985E6751FC786037921E2", "73292BD55E143DB29D6BD898B58E7D894889FF348E679A40175451FB39CB7E40BCFB6E64A26A7DB4CAA288C1CC5B5EACB8337971AAE62EC76BB482490C0BC9613BF82F4E74891CCEA7BB4195FAACCC2AB3CD6534368969A82268AEAF67A3DCA7ED0538C003BF6968B99064287D22CDD83A9F86B4B82416AA1DD87648EF66E5E8FC607C6C35B5E883A642F299C8FEDF9847E01CCAACE0926CF7A4F345B0562C3B063B2BFC5F2397B4C8F827C7795BDB424C43E9AE4097306B965BB2AC42BA742F6DCAAC1C2A190E1BD3E109BBDBA6BBE637DF156943E17A29F35F9032537E61B4746EA4BB6BDC3A7E1B9EFC5084622C85171DEEA6459AEFFC156BFE7D53A9779748752DBCCC95554FE5B835051D404CAD5111DF432D1754F3BFF9C7E070B9BAC8F9B4080B09D0FCCB5BE91E9688C8B61311CF6AA2C93D3FE6934040206B87FEBF5D25486BBD9018F7EB6625F2347FD55614282CD0DA2AA0B46C973E0EE13E734DDD4CA958B8388C9A1E834FF3D0800BF3429ECD224B0927B8EA7C618DF48712E45BBC40BBAE429A0126AC5F60C1360C091F78DFB404CE7F748E427532967EE5D13CF0371CEAA902402839F263782F7A4547A76C187009CC9B410CBA7216AD96FD7E9543D668B73F7A2F23D31E86B94D8E15A1EDF48DA04703B4FA3D2A5F5384AAC64A47F2091E6ACA4DA4413052C5A4E2FE19E03FF6DBA978AF8416BB34E53706CFA127336940A131A3EA574E2BCA20A56E2C8F85F3E85D46B1A91F8513151FD0C07BCD4660B2703694EC338A86AE3B4F41F844F9533B0B3ADC0D7A2026C0C7B8B3E4B254F10C556EDC4C72F2C75E137FD28E2D6CE532FCD02E13D053E8479523D95A4FD55C8E6B28B039741B4A03A732D6D973C32C4309A229BAB8BDE1E48D210C3BAB2808D20E5A8F7D765114CD52A503A44CCDFB20CDDDD13C04F9C6A4F4EFB85D365992D4F91C192A524DD0D7E5B946477687D81EAD32CECE02FA26433C57D32614A09273BCF0974D3CA01B7CB852E0E4E6815111975680245B9813E67E74687A667E5C9FE3DFF65243DEF93D4B7A2BA9D36EC5392B2C9069557BAC8BA60A", "83FCF16A38079D2405A79087AA79A092DDE7D28C4521A955F8963B876620498F");
        MlKemVector(1, "496BB7FE13E008EC41227F0CAC3C6087B83130F75C725C0586EAB5EC0E5402EB0B4C7AF326C96695DCAEE210FAAA5791F8A8D82BF8238D0D28A88525588D5BCD", "186E3F70E71F4BFE91A99902E47AA5E91605BA9CFEC91F98EF7EB31FF67EA9E0", "2C5D67CCC69A825FD16D7BA5C97BBAB3ED63A6FFAD7860BEEDE832A945FEBD1AB4759B99B80A84C560A5199EE2EA5301309E13BAACFAB3C62FDD776F9A87716270DFD1E844CB3F72DFD66AE9EFF98FF90DC1D0E1475A2835E88717C82DD2E9234B074CD090035AED08250BD9A4BA1A8230EB8B02EA662B9331E347289A675C36B0C2DA3C04E8F6105EB85DD1D6C566F05EFB40D819F23861920D7B73CF0E771B5200A6D97C638154C38F1F1462734EE0C3810564B18728D02B3CCB8F76C31E11E0BFA5A451F71B592996BC9801A4F39653F669975143AEE15F3A8CB29C8D0B06ECFB2E09CC85E4BA84D28B747A940D019D4C9E2C8866F562E9F395DBFD73C8C419A250D5C3323E1E8D84C1BBFCE42D202699BD9F4664BEA7840DA4EC6554AE0862BB605990E099E8F24C005A586138DE27BF908C05D8FE6A9E6BB114A369803F3928FEF50D3F67BD865DA60018C5D51783EABF4FC4FA3EF8CAD9232F37199DD223EFC4F63D853D7637BD8A77A310E0A295A30C031E2D6FD3AE3D79DA8C5DA90643A98D3A1312C523E356C64165A03C1DC5D489BD2C69E1BF30C5AD2E024EEB67F9B6F4E8AD05CAC121B38C726ADAFA0DF7E80EB0EC704B74CE7A8F1F0533B575FD79FCF99BEB6FFB91F4B5E50A9D3CEF42898523DEE84BE764D195806E46ACC5BBC386D1D493AF3561F79675EFB32941C648AE3C7E36C0BF0EAE1FD89099898F8219C47F1F36D58E3303CF8F6A53836BA282FA52F0E05DA30144333E29D31E52D3A1D87670EA0D86717B069B1D4902BA637ECD3369DDA844875199738656DD60BD09266298CDDB534D9BF70D8AE67BF558B8E0A55B7C16CB950FE89021A104913D709FFAA8FCD8A2A3252B485A1B1DF230DFE7BFF2920BABAE4260DF3D90E9583B0F69BC25756AEAE7E8D47CC9358A7C5F4238BE15C7E1F9E0419789B5A72EBCE4C4A9DD73997F8A5FB77EEAC30B0140EAF63CDCC0EC3C63E31EE7E775E5D278499B9C957E31879812209B7C392308DE241E623204FE13DBD58AA0041608E848CA95D2924B455BB288C5C74EFAC0066CF7931B768FF2661431A8E86809369DFAAEF193504A7FFA06995E908F215822415F01E069DF1751BCE9AD12AE8BF3F95CDF23977513AF832C7AF46E06833C213075DFDA74F4E4C9434B64E0EA4B44A6A56A0B6E065CDD9AA6EFAF3FC607640A903DD61A751E54C0D3385F6E43CE1634EF632158DFECBC389EC14A4AA823361C5167A232589C3EA0EC8AC8E441378953ED0F355C3CC667854A01DF9F5C460118A4DAD0B5649618D6B4052EB5F1EC6CC2F6455FA3A3CD10DE2682E7326621EA38A24D4E906FB717B895D549F7F1B0F320327F7B8DE5102D8983386C8EAD4B762F282338BDB160448118C3612239580D549399472F281A9F67BA3AE0AE96AD8B9DF5A5E306D65D428F5D734D8BCEB626D21E449A24B0146B035AAAB78C6B9F8E1FD7E6C09E483BCD47B2F3984512211834BF07B9F509FFD9EBA685AB45D7B2F28904AADC74B4520AA202B83C9D5FA6023AB8", "379D675A6109E604356D0B7FB9D33F1907AB4AA058B2B51F8FF45AD4574887CA");
        MlKemVector(2, "369ECD170AB1F241FC58720B8B4C8102D917703E1E975019324075509D8D26425BCFF6303077E793F6BA1FBFCEBF2679C2FC2B667F621E9E786DD23EC53A3741", "43ED3EF939E54EC2F32848ED151FCA4C5A27F94FDA3B12478825A95E33C7E385", "90E7679736B3EEE182A4E93C8A7A7784A0442EC14DFAFA19986EDDDB2991C3197AD3AC44C82F53B410EDE4902AAC6C69AA7E5758F24D118A9B1685558F158DD89088F4640E44B7A9F925E6CC29EAE2455F1DAE109582BF16016FD6D906E0AA467009F6708967744696B37784DE99955723A3093BC492400030E6B8C95E15BF35047097FE39AF735D075BD7A3AB9E2F633D821B71B4EE3ED259877E6897FA05A5344792AA495A304267CAB0422F7CC6B5CBB060A29FE3F7AFD6F7B016BEFB2E5181A1B55D73D7CD1185F7688E3D20242FDBE565F28CE2E9A0C5453B82FDAFEA89AC5B6B10F621B3CDA9144266936D3666B762DA39139DD8BFD644ED5F803462F3DACA76AEE0F27569294F0689BCBBE4216F57046D7942CF9E338EB93B901945923CBCBC7D1801824D738F72F26E425CE2556B243C5F0FCC8AA2A8AB72BAD9D02FC31CB8054D10A71E8EEF38BD88BEE3F653A424038C6267E3072EA11760292A49DEFDC511A91AE253CEF057E88368AD7A96BA5B7C5EF08586502B2084867EDA2E48F5D793288485B1A6CD9E623FCB4155FAA10F7FEECF27AF993036D494DE5EF54C5EAD64B4E84C3DCB33EC71ABFEF27873FBB534F910578B5C76336B9A1D9924B91CFD6E855B55E01A06EA513BF45B95B07A8C6A3EB25434ACF12DCD1F4AF6C1918F8F6B5890E71CA49E4A3FBDF6860243F35D864013F0CF927B71A756C479CE801EF509E66E3D81DD653292E37F7D868B45E9555CCC019771B762797C32D54BC731103AF03BF9239B75F7DB9F1424C215DBE19F48C728969D7BCDCAC03438C047EB575D6402A89E82C040577E6640AFBC1E0067839432F390854AE919A5CF4241CC399A8E254132EF54A29EB250134AD6B312F5B164B0A57F1FC2189AE7987ABB5D6085DB855261634F991521D2FAFF58F54E0019CEF80330AB15B482B221B974B62A3ECFF04F2B2B7352F3DF4E2581ABDFD3A391F4AD944F801686B02E97F393114B92635EC6A9C650014AF4E30DEA83606A276837C7EF147224D2241359A2400C0538BAD84397B53302CD5E870F66FC0BB27C24756A1545D00B4168D29A968736AC70B8B822CD00C820321744393DA6D4C325EE03CB75BE93123D003BC83C218636DC44DB1AEF314043C6013B73E07635BFB1F9679C25108036D9A86B2E3215B1DF8A0354779B212A5F90C00B2E452B8E06C38F37F4BDDBE01D101F029073F7B492852C2767FCD13FFE834F657DEBC82C4AA3486201ED301C758D8BA39308F2F2895C7B6945F8F9293B2F33D79A7D8E25A6FCBC52E269372620FF339E3A98F3AC93C3B70F2C31B9B5A6568B63C1222B498B49F21C2D2AD06892C9C857658AE059C85B9222D5AE0A20773650E9EEC601365AEF5BBB90A88E932C8665C3DA07658ED53F32D52895348A76E6F918239657100350D6DDA35C047876F555D5AA4D7FB64FFDAAEFFE89A34864E15337292317DEC7364CD2FE98AA1B11541B0A82D37D6C5FAB284B9C36C7BF1405CFCD360B47DC932024B88193056715A21326ADA97B0573B21CEBFF9874125FB9C33CCC187E3F63B41A96AB26A8CF883BBA13A27287DCFD6A2A26B6FBBCFE583ABCBFEB693EC90C1C2A45576A6D1F1BD22292EB70886BEADCF7DE9001C3C7CCEC2156B5EB649E437233CFB16AEA3FEA3FF75747FDBE8CEE15C88CC8677027067E6F1968F09FE3DCE66CA9FE25DAB99B2F964810405EF5C676C0B523481D1F412BAD0020991DCE7C66CEFD69B80541BFC994A3E790F530738E4EF25EE064E0FA44DB2A6EFCA3AFD0D030AD1D6FF53E6BCEEA7880DEB63B20983275D43076E0BDA0A456DEE5FDAEF4493515930E71D5344605AC63B9240485BCEC2E2E60669C0762EDB027EDD1878F555E1C4AAD20986FD495F6436E01DDDD4A7C298CF060C4B5D0868D8CD199FCBCA45DDD2C1A9F4D26F0AD30FC750BDEE5E9839930FBBE358D025620B9E45448AB07F4CDB7442055B33DC99C170A30AB649ECD36BB77586EE2892C35B88A8EF8C578A1BB42863331393AF1AC5B665779970457DA721602F3F4880537E2C6DE31C099A765D8219BF58F01D1EEBB88D43F8C912C520056B1E1D6C98C9332B399584CB4F7F204E2C8B4338295A7B3FAC8896FE4D3FA9460636661CF860F0A85560E9533A0C2366FDC63F0B4542257F11DA6EF0EAD226731E0CFFDA65722ECA772B9EA9EF5914431C205A68FF951BE9A", "303AB8A0D0B974FF14ACCDF545D2E33110B6CAD0DEE2F1CAD189B6A5E6304287");
        var poly = Enumerable.Range(0, 256).Select(i => (i * 37 + 11) % 3329).ToArray();
        var other = Enumerable.Range(0, 256).Select(i => (i * i * 5 + 3) % 3329).ToArray();
        Check(global::MlKem.MlKemScheme.InverseNtt(global::MlKem.MlKemScheme.Ntt(poly)).SequenceEqual(poly), "ML-KEM: the inverse transform undoes the number theoretic transform");
        var product = global::MlKem.MlKemScheme.InverseNtt(global::MlKem.MlKemScheme.MultiplyNtt(global::MlKem.MlKemScheme.Ntt(poly), global::MlKem.MlKemScheme.Ntt(other)));
        var school = new int[256];
        for (int i = 0; i < 256; i++) { for (int j = 0; j < 256; j++) { int k = i + j; long c = (long)poly[i] * other[j] % 3329; if (k >= 256) { school[k - 256] = (int)((school[k - 256] - c + 3329) % 3329); } else { school[k] = (int)((school[k] + c) % 3329); } } }
        Check(product.SequenceEqual(school), "ML-KEM: the product in the transformed domain equals the schoolbook product modulo x^256 + 1");
        Check(Enumerable.Range(0, 3329).All(x => global::MlKem.MlKemScheme.Decompress(global::MlKem.MlKemScheme.Compress(x, 11), 11) is var y && Math.Min((y - x + 3329) % 3329, (x - y + 3329) % 3329) <= 3329 / 2048 + 1), "ML-KEM: compression with 11 bits changes a coefficient by at most 2");
        var encoded = global::MlKem.MlKemScheme.ByteEncode(poly, 12);
        Check(encoded.Length == 384 && global::MlKem.MlKemScheme.ByteDecode(encoded, 12).SequenceEqual(poly), "ML-KEM: encoding of 256 coefficients with 12 bits gives 384 bytes and decodes again");
        var scheme = new global::MlKem.MlKemScheme(1);
        scheme.KeyGen(new byte[32], new byte[32], out var key, out _);
        var bad = (byte[])key.Clone(); bad[0] = 0xff; bad[1] = 0xff;
        Check(scheme.IsValidEncapsulationKey(key) && !scheme.IsValidEncapsulationKey(bad) && !scheme.IsValidEncapsulationKey(key.Take(100).ToArray()), "ML-KEM: the check of the encapsulation key (length, coefficients below q) refuses a damaged key");
    }

    static void CodeAndIdentificationTests()
    {
        // ASCII
        Check(global::AsciiCodes.AsciiConversion.Encode("Hi!", 10) == "72 105 33" && global::AsciiCodes.AsciiConversion.Encode("Hi!", 16) == "48 69 21" && global::AsciiCodes.AsciiConversion.Encode("Hi!", 2) == "01001000 01101001 00100001", "ASCII: Hi! is 72 105 33 = 48 69 21 = 01001000 01101001 00100001");
        Check(global::AsciiCodes.AsciiConversion.Decode("72 105 33", 10) == "Hi!" && global::AsciiCodes.AsciiConversion.Decode("48 69 21", 16) == "Hi!" && global::AsciiCodes.AsciiConversion.Decode("01001000 01101001 00100001", 2) == "Hi!", "ASCII: the codes are converted back in all three bases");
        bool refused = false;
        try { global::AsciiCodes.AsciiConversion.Encode("é", 10); } catch (ArgumentException) { refused = true; }
        try { global::AsciiCodes.AsciiConversion.Decode("200", 10); refused = false; } catch (ArgumentException) { }
        var table = global::AsciiCodes.AsciiConversion.Table().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Check(refused && table.Length == 129 && table[66].Contains(" A") && table[33].Contains("space"), "ASCII: characters beyond 127 are refused; the table has 128 lines (65 is A, 32 is the space)");

        // EAN: check digits of known numbers, structure and decoding
        Check(global::Barcode.Ean.CheckDigit("400638133393") == 1 && global::Barcode.Ean.CheckDigit("9638507") == 4 && global::Barcode.Ean.CheckDigit("590123412345") == 7 && global::Barcode.Ean.CheckDigit("7351353") == 7, "EAN check digits: 4006381333931, 96385074, 5901234123457 and 73513537");
        string completed;
        string modules = global::Barcode.Ean.Modules("400638133393", true, out completed);
        Check(completed == "4006381333931" && modules.Length == 95 && modules.StartsWith("101") && modules.EndsWith("101") && modules.Substring(45, 5) == "01010", "EAN-13: the number is completed with its check digit; 95 modules with the start, middle (01010) and end pattern");
        Check(global::Barcode.Ean.Decode(modules, true) == "4006381333931" && global::Barcode.Ean.Decode(global::Barcode.Ean.Modules("96385074", false, out _), false) == "96385074" && global::Barcode.Ean.Modules("96385074", false, out _).Length == 67, "EAN: the digits are read back from the modules (EAN-13 with its first digit from the L/G pattern, EAN-8 with 67 modules)");
        bool all = true;
        for (int first = 0; first <= 9; first++) { string digits = first + "23456789012"; all &= global::Barcode.Ean.Decode(global::Barcode.Ean.Modules(digits, true, out string done), true) == done; }
        Check(all, "EAN-13: all ten patterns of the first digit decode");
        Check(global::Barcode.Ean.R(0) == "1110010" && global::Barcode.Ean.R(9) == "1110100", "EAN: the R codes are the complements of the L codes (0: 1110010, 9: 1110100)");
        bool wrong = false;
        try { global::Barcode.Ean.Modules("4006381333932", true, out _); } catch (ArgumentException) { wrong = true; }
        Check(wrong, "EAN: a wrong check digit is refused");

        // cipher identification
        string english = "It is a truth universally acknowledged, that a single man in possession of a good fortune, must be in want of a wife. However little known the feelings or views of such a man may be on his first entering a neighbourhood, this truth is so well fixed in the minds of the surrounding families, that he is considered the rightful property of some one or other of their daughters. My dear Mr Bennet, said his lady to him one day, have you heard that Netherfield Park is let at last? Mr Bennet replied that he had not. But it is, returned she; for Mrs Long has just been here, and she told me all about it. Mr Bennet made no answer. Do you not want to know who has taken it? cried his wife impatiently. You want to tell me, and I have no objection to hearing it. This was invitation enough.";
        string letters = new string(english.ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());
        string caesar = new string(letters.Select(c => (char)('A' + (c - 'A' + 7) % 26)).ToArray());
        string key = "LEMON";
        string vigenere = new string(letters.Select((c, i) => (char)('A' + (c - 'A' + key[i % 5] - 'A') % 26)).ToArray());
        var columns = new string(Enumerable.Range(0, 9).SelectMany(c => letters.Where((x, i) => i % 9 == c)).ToArray());
        var random = new Random(4);
        string noise = new string(Enumerable.Range(0, 700).Select(i => (char)('A' + random.Next(26))).ToArray());
        var plainAnalysis = global::CipherIdentification.CipherIdentifier.Analyse(english);
        var caesarAnalysis = global::CipherIdentification.CipherIdentifier.Analyse(caesar);
        var transposed = global::CipherIdentification.CipherIdentifier.Analyse(columns);
        var vigenereAnalysis = global::CipherIdentification.CipherIdentifier.Analyse(vigenere);
        var noiseAnalysis = global::CipherIdentification.CipherIdentifier.Analyse(noise);
        Check(plainAnalysis.IndexOfCoincidence > 0.060 && caesarAnalysis.IndexOfCoincidence > 0.060 && Math.Abs(plainAnalysis.IndexOfCoincidence - caesarAnalysis.IndexOfCoincidence) < 1e-12 && vigenereAnalysis.IndexOfCoincidence < 0.050 && noiseAnalysis.IndexOfCoincidence < 0.045, $"cipher identification: the index of coincidence of English is {plainAnalysis.IndexOfCoincidence:F4}, unchanged by a Caesar shift, {vigenereAnalysis.IndexOfCoincidence:F4} for a Vigenere cipher and {noiseAnalysis.IndexOfCoincidence:F4} for random letters");
        Check(caesarAnalysis.Guess.StartsWith("a monoalphabetic substitution") && plainAnalysis.Guess.StartsWith("plaintext or a transposition") && transposed.Guess.StartsWith("plaintext or a transposition"), "cipher identification: a Caesar cipher is a monoalphabetic substitution, plaintext and a transposition keep the letter frequencies of English");
        Check(vigenereAnalysis.BestPeriod == 5 && vigenereAnalysis.Guess.Contains("key of length 5") && noiseAnalysis.Guess.StartsWith("a polyalphabetic cipher with a long key"), "cipher identification: the Vigenere cipher with the key LEMON is found with period 5, random letters have no structure");
        Check(global::CipherIdentification.CipherIdentifier.Analyse("0110100101101001").Alphabet == "binary digits" && global::CipherIdentification.CipherIdentifier.Analyse("4a7b9c0d1e2f3a4b").Alphabet == "hexadecimal digits" && global::CipherIdentification.CipherIdentifier.Analyse("U29tZSBkYXRhIQ==").Alphabet == "Base64" && global::CipherIdentification.CipherIdentifier.Analyse("12 34 56 78").Alphabet == "decimal digits", "cipher identification: binary digits, hexadecimal digits, Base64 and decimal numbers are recognised");

        // Very Smooth Hash: the definition recomputed with BigInteger
        var n = new System.Numerics.BigInteger(1000003) * 1000033;
        var vsh = new global::VerySmoothHash.VshFunction(n);
        var hash = vsh.Hash(System.Text.Encoding.UTF8.GetBytes("abc"), out var steps);
        System.Numerics.BigInteger x = 1;
        var bits = System.Text.Encoding.UTF8.GetBytes("abc").SelectMany(b => Enumerable.Range(0, 8).Select(i => b >> (7 - i) & 1)).ToList();
        int blockBits = vsh.BlockBits;
        while (bits.Count % blockBits != 0) { bits.Add(0); }
        var lengthBits = Enumerable.Range(0, 64).Select(i => (int)((24L >> (63 - i)) & 1)).ToList();
        while (lengthBits.Count % blockBits != 0) { lengthBits.Insert(0, 0); }
        bits.AddRange(lengthBits);
        for (int b = 0; b < bits.Count; b += blockBits) { System.Numerics.BigInteger f = 1; for (int j = 0; j < blockBits; j++) { if (bits[b + j] == 1) { f *= vsh.Primes[j]; } } x = x * x % n * f % n; }
        Check(hash == x && steps.Count == bits.Count / blockBits && hash < n && vsh.Primes.Take(5).SequenceEqual(new[] { 2, 3, 5, 7, 11 }), $"VSH: the hash value equals the definition (block size {blockBits} bits, primes 2, 3, 5, 7, 11, ...)");
        Check(!hash.Equals(vsh.Hash(System.Text.Encoding.UTF8.GetBytes("abd"), out _)) && vsh.Hash(System.Text.Encoding.UTF8.GetBytes("abc"), out _) == hash && !hash.Equals(vsh.Hash(System.Text.Encoding.UTF8.GetBytes("abc\0"), out _)), "VSH: another text, and the same text with a zero byte, gives another hash value; the same text always the same");
    }

    static void CfsTests()
    {
        // GF(2^8): the AES-like field of the code is a field (every nonzero element has an inverse), x^8 + x^4 + x^3 + x^2 + 1
        Check(Enumerable.Range(1, 255).All(a => global::CfsSignature.Gf.Mul((byte)a, global::CfsSignature.Gf.Inv((byte)a)) == 1) && global::CfsSignature.Gf.Mul(2, 128) == 0x1d, "GF(2^8): every nonzero element has an inverse, and 2 * 128 = x^8 = 0x1d");
        var scheme = new global::CfsSignature.CfsScheme(1);
        Check(global::CfsSignature.CfsScheme.IsIrreducible(scheme.Goppa) && !global::CfsSignature.CfsScheme.IsIrreducible(global::CfsSignature.Gf.Multiply(new byte[] { 3, 1 }, new byte[] { 5, 0, 0, 1 })) && !global::CfsSignature.CfsScheme.IsIrreducible(global::CfsSignature.Gf.Multiply(new byte[] { 3, 1, 1 }, new byte[] { 7, 9, 1 })), "Goppa polynomial: the secret polynomial of degree 4 is irreducible; products of a linear and a cubic or of two quadratics are not");
        // Patterson's algorithm: every error of weight 0 to 4 is found from its syndrome
        var random = new Random(3);
        bool decoded = true;
        for (int trial = 0; trial < 300; trial++)
        {
            int weight = trial % 5;
            var positions = Enumerable.Range(0, 256).OrderBy(x => random.Next()).Take(weight).OrderBy(x => x).ToList();
            var found = scheme.Decode(scheme.Syndrome(positions));
            decoded &= found != null && found.OrderBy(x => x).SequenceEqual(positions);
        }
        Check(decoded, "Goppa code: Patterson's algorithm finds every error of weight 0 to 4 from its syndrome (300 random errors)");
        int decodable = 0, total = 4000;
        for (uint s = 1; s <= total; s++) { if (scheme.Decode(s * 1000003u) != null) { decodable++; } }
        Check(decodable > total / 40 / 2 && decodable < total / 24 * 2, $"Goppa code: {decodable} of {total} random syndromes can be decoded (expected about 1/24 = {total / 24})");
        // signatures
        bool signed_ = true, rejected = true;
        int maxCounter = 0;
        for (int i = 0; i < 8; i++)
        {
            string message = "message " + i;
            var positions = scheme.Sign(message, out int counter);
            maxCounter = Math.Max(maxCounter, counter);
            signed_ &= positions.Length <= 4 && global::CfsSignature.CfsScheme.Verify(message, positions, counter, scheme.PublicColumns);
            rejected &= !global::CfsSignature.CfsScheme.Verify(message + "!", positions, counter, scheme.PublicColumns) && !global::CfsSignature.CfsScheme.Verify(message, positions, counter + 1, scheme.PublicColumns);
        }
        Check(signed_ && rejected, $"CFS: 8 signatures verify (counters up to {maxCounter}), and a changed message or counter is refused");
        var other = new global::CfsSignature.CfsScheme(2);
        var sig = scheme.Sign("hello", out int c0);
        Check(!global::CfsSignature.CfsScheme.Verify("hello", sig, c0, other.PublicColumns) && Enumerable.Range(0, 32).All(j => scheme.PublicColumns[j] == 1u << j), "CFS: the public matrix is in systematic form (the first 32 columns are the unit vectors), and another key does not verify");
    }

    static void DesVariantTests()
    {
        // DES of our tables against DES of .NET (random keys and blocks), and the classic example
        byte[] classicKey = global::DesVariants.DesFamily.FromHex("133457799BBCDFF1"), classicBlock = global::DesVariants.DesFamily.FromHex("0123456789ABCDEF");
        Check(global::DesVariants.DesFamily.ToHex(global::DesVariants.DesFamily.Process(0, classicKey, classicBlock, true)) == "85E813540F0AB405", "DES (own tables): key 133457799BBCDFF1, plaintext 0123456789ABCDEF gives 85E813540F0AB405");
        var random = new Random(11);
        bool same = true;
        for (int i = 0; i < 200; i++)
        {
            var key = new byte[8]; var data = new byte[16];
            random.NextBytes(key); random.NextBytes(data);
            using var des = System.Security.Cryptography.DES.Create();
            des.Key = key; des.Mode = System.Security.Cryptography.CipherMode.ECB; des.Padding = System.Security.Cryptography.PaddingMode.None;
            using var encryptor = des.CreateEncryptor();
            byte[] expected = encryptor.TransformFinalBlock(data, 0, 16);
            byte[] ours = global::DesVariants.DesFamily.Process(0, key, data, true);
            same &= ours.SequenceEqual(expected) && global::DesVariants.DesFamily.Process(0, key, ours, false).SequenceEqual(data);
        }
        Check(same, "DES (own tables, the eight S-boxes): 200 random keys and blocks give the same ciphertext as DES of .NET and decrypt again");

        // DESL: round trip, different from DES, and the S-box properties
        bool roundTrip = true, differs = true;
        for (int i = 0; i < 50; i++)
        {
            var key = new byte[24]; var data = new byte[16];
            random.NextBytes(key); random.NextBytes(data);
            foreach (int variant in new[] { 1, 2, 3 })
            {
                byte[] k = key.Take(global::DesVariants.DesFamily.KeyLength(variant)).ToArray();
                roundTrip &= global::DesVariants.DesFamily.Process(variant, k, global::DesVariants.DesFamily.Process(variant, k, data, true), false).SequenceEqual(data);
            }
            differs &= !global::DesVariants.DesFamily.Process(1, key.Take(8).ToArray(), data, true).SequenceEqual(global::DesVariants.DesFamily.Process(0, key.Take(8).ToArray(), data, true));
        }
        Check(roundTrip && differs, "DESL, DESX, DESXL: encryption and decryption of 50 random keys and blocks are inverse; DESL differs from DES");
        var box = global::DesVariants.DesEngineVariant.LightweightBox;
        bool rows = Enumerable.Range(0, 4).All(r => box.Skip(16 * r).Take(16).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 16)));
        int violations = 0;
        Func<int, int> S = x => box[(((x >> 4) & 2) | (x & 1)) * 16 + ((x >> 1) & 15)];
        for (int x = 0; x < 64; x++) { for (int b = 0; b < 6; b++) { if (System.Numerics.BitOperations.PopCount((uint)(S(x) ^ S(x ^ (1 << b)))) < 2) { violations++; } } }
        bool standardRows = global::DesVariants.DesEngineVariant.StandardBoxes.All(b => Enumerable.Range(0, 4).All(r => b.Skip(16 * r).Take(16).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 16))));
        Check(rows && violations == 0 && standardRows, "DESL: every row of the S-box is a permutation of 0 to 15 and one flipped input bit changes at least two output bits (as for the DES S-boxes)");
        // DESX: whitening with the pre- and post-whitening key around DES
        byte[] pre = new byte[8], post = new byte[8], dkey = new byte[8], plain = new byte[8];
        random.NextBytes(pre); random.NextBytes(post); random.NextBytes(dkey); random.NextBytes(plain);
        byte[] manual = global::DesVariants.DesFamily.Process(0, dkey, plain.Select((b, i) => (byte)(b ^ pre[i])).ToArray(), true).Select((b, i) => (byte)(b ^ post[i])).ToArray();
        Check(global::DesVariants.DesFamily.Process(2, dkey.Concat(pre).Concat(post).ToArray(), plain, true).SequenceEqual(manual), "DESX: the result is E(P xor K2) xor K3 with DES");
    }

    // MARS: the "tables" and "variable key" known-answer tests of IBM (NIST AES submission): key | plaintext | ciphertext
    static readonly string[] MarsVectors =
    {
            "00000000000000000000000000000000|00000000000000000000000000000000|DCC07B8DFB0738D6E30A22DFCF27E886",
            "00000000000000000000000000000000|DCC07B8DFB0738D6E30A22DFCF27E886|33CAFFBDDC7F1DDA0F9C15FA2F30E2FF",
            "00000000000000000000000000000000|33CAFFBDDC7F1DDA0F9C15FA2F30E2FF|62D0D531FD18BAC77177CEB2395EF18F",
            "00000000000000000000000000000000|62D0D531FD18BAC77177CEB2395EF18F|FC444017478822797D800F790622D77B",
            "00000000000000000000000000000000|FC444017478822797D800F790622D77B|D584ED203C50582BCBF74B0469417EDC",
            "00000000000000000000000000000000|D584ED203C50582BCBF74B0469417EDC|7134D3EF595D573FE88E18CE5CB2E612",
            "00000000000000000000000000000000|7134D3EF595D573FE88E18CE5CB2E612|9172072F22CFC72234A7697D73412DB9",
            "00000000000000000000000000000000|9172072F22CFC72234A7697D73412DB9|06B0DDCAAE6EA44BD0C509CE2BC838AC",
            "00000000000000000000000000000000|06B0DDCAAE6EA44BD0C509CE2BC838AC|77512E7A4A90D6D5CD66637DEEF1FD52",
            "00000000000000000000000000000000|77512E7A4A90D6D5CD66637DEEF1FD52|8BC4E466ABE0E2F8FD76019B8237F36E",
            "00000000000000000000000000000000|8BC4E466ABE0E2F8FD76019B8237F36E|77D40822866C0DD51E0594AA81E5F1B5",
            "00000000000000000000000000000000|77D40822866C0DD51E0594AA81E5F1B5|B8AB0608C7976E1D8F57B2BA493C7503",
            "00000000000000000000000000000000|B8AB0608C7976E1D8F57B2BA493C7503|ED4AFE77CE3A2F8C00D0A5E4FDF23426",
            "00000000000000000000000000000000|ED4AFE77CE3A2F8C00D0A5E4FDF23426|1B52C34EDB3826E2BF564E4E3A8C9FA6",
            "00000000000000000000000000000000|1B52C34EDB3826E2BF564E4E3A8C9FA6|AE7E643A355B4847D4F1DBD126518993",
            "00000000000000000000000000000000|AE7E643A355B4847D4F1DBD126518993|3E144191008102E869184D9714BAE7CF",
            "00000000000000000000000000000000|3E144191008102E869184D9714BAE7CF|89ABFA208A52F85CA60F296CE99FC97A",
            "00000000000000000000000000000000|89ABFA208A52F85CA60F296CE99FC97A|FA092A3DD37EF94837715BBC72C9008F",
            "00000000000000000000000000000000|FA092A3DD37EF94837715BBC72C9008F|8A2288E53B2F0DD376ED385911BB4BB9",
            "00000000000000000000000000000000|8A2288E53B2F0DD376ED385911BB4BB9|0CD12B0AEC2BA1C57F06E268A4BF5966",
            "00000000000000000000000000000000|0CD12B0AEC2BA1C57F06E268A4BF5966|0B86524D05D0963C1BDF538290156686",
            "00000000000000000000000000000000|0B86524D05D0963C1BDF538290156686|79031361A2E423A7C4D1613C3873C0A0",
            "00000000000000000000000000000000|79031361A2E423A7C4D1613C3873C0A0|DD66BC447E835A9F426D0A2267850461",
            "00000000000000000000000000000000|DD66BC447E835A9F426D0A2267850461|4294DCCF31F54E693179449F25B514CC",
            "00000000000000000000000000000000|4294DCCF31F54E693179449F25B514CC|0A402C1A0F2EB568EDB7252AFDA4192B",
            "00000000000000000000000000000000|0A402C1A0F2EB568EDB7252AFDA4192B|FFACF106A37C74D17009E2566E611BE0",
            "00000000000000000000000000000000|FFACF106A37C74D17009E2566E611BE0|99E058DA9540E6B5C5A869CB9204010B",
            "00000000000000000000000000000000|99E058DA9540E6B5C5A869CB9204010B|8D616AB19B75842DC5EF102CE4BB2A51",
            "00000000000000000000000000000000|8D616AB19B75842DC5EF102CE4BB2A51|06C7C640672895C3CCFD52DB2CD9D51B",
            "00000000000000000000000000000000|06C7C640672895C3CCFD52DB2CD9D51B|EB2B67E27ABCB5C06DA3D3EECEC8A25B",
            "00000000000000000000000000000000|EB2B67E27ABCB5C06DA3D3EECEC8A25B|62EC35DC05A0BA40FE5D33A0B974C152",
            "00000000000000000000000000000000|62EC35DC05A0BA40FE5D33A0B974C152|A4CB55D7881ACC44F868C8BCFE098091",
            "00000000000000000000000000000000|A4CB55D7881ACC44F868C8BCFE098091|5CEE73CEBCAA22DF78C8C9FB8A47E5D3",
            "00000000000000000000000000000000|5CEE73CEBCAA22DF78C8C9FB8A47E5D3|62FCD19C3AC4370A4CC58AA619B0EA2F",
            "00000000000000000000000000000000|62FCD19C3AC4370A4CC58AA619B0EA2F|3583E618DAC3CE4347FD0E8AE6D6F5E9",
            "00000000000000000000000000000000|3583E618DAC3CE4347FD0E8AE6D6F5E9|2B307B42E0B9EF2D2EC3EEA07D339A87",
            "00000000000000000000000000000000|2B307B42E0B9EF2D2EC3EEA07D339A87|29A212AF4220054975D7C2AE9242AFA8",
            "00000000000000000000000000000000|29A212AF4220054975D7C2AE9242AFA8|67D5BD878FF66CDB33A4DD07EB634F91",
            "00000000000000000000000000000000|67D5BD878FF66CDB33A4DD07EB634F91|53944CA7C3BBBBE32C9BA9D8A8B765D6",
            "00000000000000000000000000000000|53944CA7C3BBBBE32C9BA9D8A8B765D6|9213B43D06D0AB7ECCC5CA751C5DBAA8",
            "000000000000000000000000000000000000000000000000|AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA|97778747D60E425C2B4202599DB856FB",
            "000000000000000000000000000000000000000000000000|97778747D60E425C2B4202599DB856FB|885C6C280610DA436936E3C5AEEB9512",
            "000000000000000000000000000000000000000000000000|885C6C280610DA436936E3C5AEEB9512|749935A9971B7060043018F95329A530",
            "000000000000000000000000000000000000000000000000|749935A9971B7060043018F95329A530|05F0D089DE79801B3BE3D3D1361628DD",
            "000000000000000000000000000000000000000000000000|05F0D089DE79801B3BE3D3D1361628DD|35F8044736D1FF4142460E4E72610700",
            "000000000000000000000000000000000000000000000000|35F8044736D1FF4142460E4E72610700|B7028947854FE0C670E1E7416C2803DC",
            "000000000000000000000000000000000000000000000000|B7028947854FE0C670E1E7416C2803DC|D4F8A703F24F7E0AC5807EADADCA8CC9",
            "000000000000000000000000000000000000000000000000|D4F8A703F24F7E0AC5807EADADCA8CC9|77F1D30164CD2984C52ACEC02CAE2CD9",
            "000000000000000000000000000000000000000000000000|77F1D30164CD2984C52ACEC02CAE2CD9|CE509EEFF43FB5C22A9756B023573775",
            "000000000000000000000000000000000000000000000000|CE509EEFF43FB5C22A9756B023573775|B9D5ACCA79E9719979DA1E657C391F07",
            "000000000000000000000000000000000000000000000000|B9D5ACCA79E9719979DA1E657C391F07|9E3B2E46FC6E2D71D46AF0D5CF36B456",
            "000000000000000000000000000000000000000000000000|9E3B2E46FC6E2D71D46AF0D5CF36B456|D8104637E81C52CCEFB0AFE87F4BAF85",
            "000000000000000000000000000000000000000000000000|D8104637E81C52CCEFB0AFE87F4BAF85|C7F9AF5B41E9EB5387DB1D122E483F0E",
            "000000000000000000000000000000000000000000000000|C7F9AF5B41E9EB5387DB1D122E483F0E|D7ACF96A441BB267B591FBCEECB0494F",
            "000000000000000000000000000000000000000000000000|D7ACF96A441BB267B591FBCEECB0494F|DA2234B06D83F32064A78AD3C5303D73",
            "000000000000000000000000000000000000000000000000|DA2234B06D83F32064A78AD3C5303D73|70EF6DCDD349CD7F1D1A0EB3CB18C8C7",
            "000000000000000000000000000000000000000000000000|70EF6DCDD349CD7F1D1A0EB3CB18C8C7|DA093FB2C0F00258D8D9F216C91C0C9C",
            "000000000000000000000000000000000000000000000000|DA093FB2C0F00258D8D9F216C91C0C9C|26EAEEAE8FBE4835E5C43CBF3249CBB6",
            "000000000000000000000000000000000000000000000000|26EAEEAE8FBE4835E5C43CBF3249CBB6|35692A1CC8E9FBB99DEE2AD149EE7F33",
            "000000000000000000000000000000000000000000000000|35692A1CC8E9FBB99DEE2AD149EE7F33|6929CAD6052F809A2850D7E754C11C8D",
            "000000000000000000000000000000000000000000000000|6929CAD6052F809A2850D7E754C11C8D|5F4E499D26ED1D290D9A187FB7B7FB14",
            "000000000000000000000000000000000000000000000000|5F4E499D26ED1D290D9A187FB7B7FB14|BC5B30B2BAE077710A9DF5686794965F",
            "000000000000000000000000000000000000000000000000|BC5B30B2BAE077710A9DF5686794965F|41661F98160A6556815A15B1C1C2E080",
            "000000000000000000000000000000000000000000000000|41661F98160A6556815A15B1C1C2E080|EC2E71697E742ABDBB5FE516E50DBAD5",
            "000000000000000000000000000000000000000000000000|EC2E71697E742ABDBB5FE516E50DBAD5|CB104FF2203633E97573D308923C7AE9",
            "000000000000000000000000000000000000000000000000|CB104FF2203633E97573D308923C7AE9|C47B77D5FAD3535CFAE0BE60B48C741E",
            "000000000000000000000000000000000000000000000000|C47B77D5FAD3535CFAE0BE60B48C741E|BCA5C36E33821A60CAF6E2DA72956C5E",
            "000000000000000000000000000000000000000000000000|BCA5C36E33821A60CAF6E2DA72956C5E|B780AA350F6EFB3B805E505A2D32249F",
            "000000000000000000000000000000000000000000000000|B780AA350F6EFB3B805E505A2D32249F|8857D872D91FE8937E292C8948C0AF24",
            "000000000000000000000000000000000000000000000000|8857D872D91FE8937E292C8948C0AF24|4FCF851C845CBD7D7C528F55B8E84662",
            "000000000000000000000000000000000000000000000000|4FCF851C845CBD7D7C528F55B8E84662|BDDEE7D374D810E3658670F46054F99B",
            "000000000000000000000000000000000000000000000000|BDDEE7D374D810E3658670F46054F99B|16A4F2C3A95B296FBB7F8DEC6F70A9D0",
            "000000000000000000000000000000000000000000000000|16A4F2C3A95B296FBB7F8DEC6F70A9D0|2583F970ED6970C6D0963015D67217B6",
            "000000000000000000000000000000000000000000000000|2583F970ED6970C6D0963015D67217B6|0E7BB2CDF9CD553709D2BE4E9497D2F3",
            "000000000000000000000000000000000000000000000000|0E7BB2CDF9CD553709D2BE4E9497D2F3|A6D82EE721AAF1BA1BC3EBD8C75ABA9F",
            "000000000000000000000000000000000000000000000000|A6D82EE721AAF1BA1BC3EBD8C75ABA9F|C1211304A35315EC827DAE80B3F29BBB",
            "000000000000000000000000000000000000000000000000|C1211304A35315EC827DAE80B3F29BBB|4F0842F2D9EA5CD1AB743A8626DA6BE7",
            "000000000000000000000000000000000000000000000000|4F0842F2D9EA5CD1AB743A8626DA6BE7|BF39E62B863AFFFAD3DEE960962E03AE",
            "000000000000000000000000000000000000000000000000|BF39E62B863AFFFAD3DEE960962E03AE|147C3CB084958D3FBB9D4223D3126BF3",
            "000000000000000000000000000000000000000000000000|147C3CB084958D3FBB9D4223D3126BF3|6E76BEF9304B115EFC1C9002FBB848A0",
            "0000000000000000000000000000000000000000000000000000000000000000|62E45B4CF3477F1DD65063729D9ABA8F|0F4B897EA014D21FBC20F1054A42F719",
            "0000000000000000000000000000000000000000000000000000000000000000|0F4B897EA014D21FBC20F1054A42F719|9A4D42A7F27D42E542286ACF6650CF8A",
            "0000000000000000000000000000000000000000000000000000000000000000|9A4D42A7F27D42E542286ACF6650CF8A|5E5F71257B5156C075FDC4BCBE996527",
            "0000000000000000000000000000000000000000000000000000000000000000|5E5F71257B5156C075FDC4BCBE996527|5A8112FB681B184766069A229BC0E1B6",
            "0000000000000000000000000000000000000000000000000000000000000000|5A8112FB681B184766069A229BC0E1B6|2169853AD199721728B2F41FF6C3F316",
            "0000000000000000000000000000000000000000000000000000000000000000|2169853AD199721728B2F41FF6C3F316|FD3954D96EF5B4078F5C301BAF15A3DE",
            "0000000000000000000000000000000000000000000000000000000000000000|FD3954D96EF5B4078F5C301BAF15A3DE|DB1F3D03E0012505E354B2F4D7C7A255",
            "0000000000000000000000000000000000000000000000000000000000000000|DB1F3D03E0012505E354B2F4D7C7A255|375CECE4E897512373B86A82A6A660D5",
            "0000000000000000000000000000000000000000000000000000000000000000|375CECE4E897512373B86A82A6A660D5|4CDA1DDDD8849D47609D7D9803FD67D6",
            "0000000000000000000000000000000000000000000000000000000000000000|4CDA1DDDD8849D47609D7D9803FD67D6|20A6C90C54A118487C173AE3166EF219",
            "0000000000000000000000000000000000000000000000000000000000000000|20A6C90C54A118487C173AE3166EF219|F4A0164DFC1F38E967F3DDE74B7B0CAD",
            "0000000000000000000000000000000000000000000000000000000000000000|F4A0164DFC1F38E967F3DDE74B7B0CAD|638D1B47820C0FD0A1BFA5FB05AE9087",
            "0000000000000000000000000000000000000000000000000000000000000000|638D1B47820C0FD0A1BFA5FB05AE9087|CDE141CD9E784912B6BEFD62003FE253",
            "0000000000000000000000000000000000000000000000000000000000000000|CDE141CD9E784912B6BEFD62003FE253|84276E8EB17CB38307F3ACFE9480A771",
            "0000000000000000000000000000000000000000000000000000000000000000|84276E8EB17CB38307F3ACFE9480A771|27BA4090E1929A7E65D0C5078039A677",
            "0000000000000000000000000000000000000000000000000000000000000000|27BA4090E1929A7E65D0C5078039A677|264E4F150552D0D5F81583635189EED1",
            "0000000000000000000000000000000000000000000000000000000000000000|264E4F150552D0D5F81583635189EED1|F052005B58BE2C8981575BA5C31D812C",
            "0000000000000000000000000000000000000000000000000000000000000000|F052005B58BE2C8981575BA5C31D812C|807E73C57A18CA88CA0F58C2FFD06394",
            "0000000000000000000000000000000000000000000000000000000000000000|807E73C57A18CA88CA0F58C2FFD06394|C317C6C0411947887DFCE0A9B2270B5F",
            "0000000000000000000000000000000000000000000000000000000000000000|C317C6C0411947887DFCE0A9B2270B5F|8ACD4164453A4C1C0DACBC8C2ED0896C",
            "0000000000000000000000000000000000000000000000000000000000000000|8ACD4164453A4C1C0DACBC8C2ED0896C|2BA5D280D05FBAF78A8B55BF5FD8D343",
            "0000000000000000000000000000000000000000000000000000000000000000|2BA5D280D05FBAF78A8B55BF5FD8D343|DDF70B8350ED845F6D875B0C85961D39",
            "0000000000000000000000000000000000000000000000000000000000000000|DDF70B8350ED845F6D875B0C85961D39|039FD445F0A6D65698D5146D498C8BDB",
            "0000000000000000000000000000000000000000000000000000000000000000|039FD445F0A6D65698D5146D498C8BDB|22AE4D552D09558DEEEFC9518C01F4E2",
            "0000000000000000000000000000000000000000000000000000000000000000|22AE4D552D09558DEEEFC9518C01F4E2|D5947EA67A7BD88DA577A28254EE44EF",
            "0000000000000000000000000000000000000000000000000000000000000000|D5947EA67A7BD88DA577A28254EE44EF|22698C92691EE76CAD72880F577EFEC3",
            "0000000000000000000000000000000000000000000000000000000000000000|22698C92691EE76CAD72880F577EFEC3|10ECA6E5E499742DDEC48434B767EBA1",
            "0000000000000000000000000000000000000000000000000000000000000000|10ECA6E5E499742DDEC48434B767EBA1|3D97E93A44C2CB1CE9AF5770A4BD1A34",
            "0000000000000000000000000000000000000000000000000000000000000000|3D97E93A44C2CB1CE9AF5770A4BD1A34|F89C438D5311FC8235C632629CE1F3A0",
            "0000000000000000000000000000000000000000000000000000000000000000|F89C438D5311FC8235C632629CE1F3A0|4114C623ADF6DB0958FFA2DC27A2A828",
            "0000000000000000000000000000000000000000000000000000000000000000|4114C623ADF6DB0958FFA2DC27A2A828|980089430C89563A5510DF5196E07040",
            "0000000000000000000000000000000000000000000000000000000000000000|980089430C89563A5510DF5196E07040|1D1FEFBD55AA23487CD2DEC4D370302D",
            "0000000000000000000000000000000000000000000000000000000000000000|1D1FEFBD55AA23487CD2DEC4D370302D|73A8AAE732EF445F3462353181168B23",
            "0000000000000000000000000000000000000000000000000000000000000000|73A8AAE732EF445F3462353181168B23|1D8116306AF7D70FE419226F35251687",
            "0000000000000000000000000000000000000000000000000000000000000000|1D8116306AF7D70FE419226F35251687|84EFA8A595B06C91A5A083440DAF5B50",
            "0000000000000000000000000000000000000000000000000000000000000000|84EFA8A595B06C91A5A083440DAF5B50|630EECD4A8A957A610765E01A1D07C45",
            "0000000000000000000000000000000000000000000000000000000000000000|630EECD4A8A957A610765E01A1D07C45|825A47AD6FCE09FEDB5D8F07DE6A2707",
            "0000000000000000000000000000000000000000000000000000000000000000|825A47AD6FCE09FEDB5D8F07DE6A2707|0C472D25510DD0592B5130FB4272DECA",
            "0000000000000000000000000000000000000000000000000000000000000000|0C472D25510DD0592B5130FB4272DECA|3DA22A91B605A540E474383672C8D06C",
            "0000000000000000000000000000000000000000000000000000000000000000|3DA22A91B605A540E474383672C8D06C|9A1C14309E4B246C9E7B485A7F41D046"
    };

    static void ExtraCipherTests()
    {
        bool mars = true, marsBack = true;
        foreach (string row in MarsVectors)
        {
            var parts = row.Split('|');
            var engine = new global::ExtraBlockCiphers.MarsEngine(global::ExtraBlockCiphers.ExtraCiphers.FromHex(parts[0]));
            byte[] plaintext = global::ExtraBlockCiphers.ExtraCiphers.FromHex(parts[1]), expected = global::ExtraBlockCiphers.ExtraCiphers.FromHex(parts[2]);
            mars &= global::ExtraBlockCiphers.ExtraCiphers.ToHex(engine.Encrypt(plaintext)) == parts[2];
            marsBack &= engine.Decrypt(expected).SequenceEqual(plaintext);
        }
        Check(mars, $"MARS: {MarsVectors.Length} known-answer tests of IBM (keys of 128, 192 and 256 bits, the table and variable key tests) are reproduced");
        Check(marsBack, "MARS: decryption gives the plaintext of all known-answer tests");
        // SAFER+: the bias words are calculated (the first row and entries of other rows of the published table), and the three public test vectors of 128, 192 and 256 bits
        Check(Enumerable.Range(0, 16).Select(i => global::ExtraBlockCiphers.SaferPlusEngine.Bias(2, i)).SequenceEqual(new[] { 70, 151, 177, 186, 163, 183, 16, 10, 197, 55, 179, 201, 90, 40, 172, 100 }) && global::ExtraBlockCiphers.SaferPlusEngine.Bias(17, 0) == 136 && global::ExtraBlockCiphers.SaferPlusEngine.Bias(18, 0) == 220 && global::ExtraBlockCiphers.SaferPlusEngine.Bias(33, 0) == 62 && global::ExtraBlockCiphers.SaferPlusEngine.Bias(33, 15) == 51, "SAFER+: the bias words are calculated and equal the table of the reference code (p = 2 to 17 with two exponentiations, p = 18 to 33 with one)");
        var saferVectors = new[]
        {
            (new byte[] { 41, 35, 190, 132, 225, 108, 214, 174, 82, 144, 73, 241, 241, 187, 233, 235 }, new byte[] { 179, 166, 219, 60, 135, 12, 62, 153, 36, 94, 13, 28, 6, 183, 71, 222 }, new byte[] { 224, 31, 182, 10, 12, 255, 84, 70, 127, 13, 89, 249, 9, 57, 165, 220 }),
            (new byte[] { 72, 211, 143, 117, 230, 217, 29, 42, 229, 192, 247, 43, 120, 129, 135, 68, 14, 95, 80, 0, 212, 97, 141, 190 }, new byte[] { 123, 5, 21, 7, 59, 51, 130, 31, 24, 112, 146, 218, 100, 84, 206, 177 }, new byte[] { 92, 136, 4, 63, 57, 95, 100, 0, 150, 130, 130, 16, 193, 111, 219, 133 }),
            (new byte[] { 243, 168, 141, 254, 190, 242, 235, 113, 255, 160, 208, 59, 117, 6, 140, 126, 135, 120, 115, 77, 208, 190, 130, 190, 219, 194, 70, 65, 43, 140, 250, 48 }, new byte[] { 127, 112, 240, 167, 84, 134, 50, 149, 170, 91, 104, 19, 11, 230, 252, 245 }, new byte[] { 88, 11, 25, 36, 172, 229, 202, 213, 170, 65, 105, 153, 220, 104, 153, 138 })
        };
        bool safer = true;
        foreach (var (key, plain, cipher) in saferVectors)
        {
            var engine = new global::ExtraBlockCiphers.SaferPlusEngine(key);
            safer &= engine.Encrypt(plain).SequenceEqual(cipher) && engine.Decrypt(cipher).SequenceEqual(plain);
            var x = new byte[16];
            for (int i = 0; i < 1000; i++) { x = engine.Encrypt(x); }
            for (int i = 0; i < 1000; i++) { x = engine.Decrypt(x); }
            safer &= x.All(b => b == 0);
        }
        Check(safer, "SAFER+: the three test vectors (128, 192 and 256-bit keys) are reproduced, and 1000 encryptions of zero bytes decrypt again");
        // MISTY1: round trips and the structure of RFC 2994
        var misty = new global::ExtraBlockCiphers.MistyEngine(global::ExtraBlockCiphers.ExtraCiphers.FromHex("00112233445566778899AABBCCDDEEFF"));
        byte[] mistyPlain = global::ExtraBlockCiphers.ExtraCiphers.FromHex("0123456789ABCDEF");
        Check(global::ExtraBlockCiphers.ExtraCiphers.ToHex(misty.Encrypt(mistyPlain)) == "8B1DA5F56AB3D07C" && global::ExtraBlockCiphers.ExtraCiphers.ToHex(misty.Encrypt(global::ExtraBlockCiphers.ExtraCiphers.FromHex("FEDCBA9876543210"))) == "04B68240B13BE95D" && misty.Decrypt(misty.Encrypt(mistyPlain)).SequenceEqual(mistyPlain), "MISTY1: the published test vectors (key 00112233445566778899AABBCCDDEEFF, 0123456789ABCDEF gives 8B1DA5F56AB3D07C and FEDCBA9876543210 gives 04B68240B13BE95D) and decryption");
        Check(global::ExtraBlockCiphers.MarsEngine.SBox.Length == 512 && global::ExtraBlockCiphers.MarsEngine.SBox.All(w => w != 0 && w != 0xffffffff), "MARS: the S-box has 512 words and no all-zero or all-one word (a design criterion of the paper)");
    }

    static void ProtocolVisualization()
    {
        // zero-knowledge (Fiat-Shamir): n = 11 * 13 = 143, s = 5, v = 25; the checks are made again here with .NET arithmetic
        {
            var zk = global::ProtocolVisualization.Calculations.ZeroKnowledge("11 13 5 7 1 9 0 2 1");
            var modulus = new System.Numerics.BigInteger(143);
            bool checks = zk.N == 143 && zk.V == 25 && zk.Rounds.Count == 3 && zk.HonestAccepted;
            foreach (var round in zk.Rounds)
            {
                var y = round.Challenge == 0 ? round.R : round.R * 5 % modulus;
                checks &= round.X == round.R * round.R % modulus && round.Y == y && y * y % modulus == (round.Challenge == 0 ? round.X : round.X * 25 % modulus);
            }
            Check(checks, "zero-knowledge (Fiat-Shamir): n = 143, s = 5, v = 25; in all three rounds y^2 = x * v^c, so the honest prover is accepted");
            Check(zk.CheaterCaughtInRound == 1 && zk.Rounds[0].Challenge == 1 && !zk.Rounds[0].CheatAccepted && zk.Rounds[1].CheatAccepted, "zero-knowledge: the cheater (who bets on c = 0) passes a round with c = 0 and is caught in the first round with c = 1");
            Check(global::ProtocolVisualization.Calculations.ZeroKnowledge("11 13 5 7 0 9 0").CheaterCaughtInRound == 0, "zero-knowledge: if all challenges are 0 the cheater gets through, which is why the verifier chooses at random");
            bool refused = false;
            try { global::ProtocolVisualization.Calculations.ZeroKnowledge("11 13 5 7 2"); } catch (ArgumentException) { refused = true; }
            try { global::ProtocolVisualization.Calculations.ZeroKnowledge("11 11 5 7 1"); refused = false; } catch (ArgumentException) { }
            Check(refused, "zero-knowledge: a challenge other than 0 and 1 and equal primes are refused");
        }

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

using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace PublicKeyEncryption
{
    /// <summary>
    /// The public-key encryption schemes ElGamal (1985) and Rabin (1979), written out with big-number arithmetic so that the
    /// computations can be followed. Keys are DER encoded sequences of integers: public ElGamal key (p, g, y), private ElGamal
    /// key (p, g, x), public Rabin key (n), private Rabin key (p, q).
    ///
    /// Messages are cut into blocks. Every block starts with the byte 0x01, so that leading zero bytes survive the conversion
    /// to a number. Rabin blocks end with four check bytes (the start of the SHA-256 hash of the data), by which the receiver
    /// finds the right one of the four square roots.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("PublicKeyEncryption.Properties.Resources", "PluginCaption", "PluginTooltip", "PublicKeyEncryption/DetailedDescription/doc.xml", "PublicKeyEncryption/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class PublicKeyEncryption : ICrypComponent
    {
        private PublicKeyEncryptionSettings _settings = new PublicKeyEncryptionSettings();

        // the safe primes of RFC 2409 (Oakley group 2, 1024 bits) and RFC 3526 (group 14, 2048 bits); the generator is 2
        private const string Prime1024 = "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE65381FFFFFFFFFFFFFFFF";
        private const string Prime2048 = "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF";

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", false)]
        public ICrypToolStream InputData { get; set; }

        [PropertyInfo(Direction.InputData, "InputPrivateKeyCaption", "InputPrivateKeyTooltip", false)]
        public byte[] InputPrivateKey { get; set; }

        [PropertyInfo(Direction.InputData, "InputPublicKeyCaption", "InputPublicKeyTooltip", false)]
        public byte[] InputPublicKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPrivateKeyCaption", "OutputPrivateKeyTooltip", false)]
        public byte[] OutputPrivateKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPublicKeyCaption", "OutputPublicKeyTooltip", false)]
        public byte[] OutputPublicKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputDataCaption", "OutputDataTooltip", false)]
        public ICrypToolStream OutputData { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (PublicKeyEncryptionSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        private bool IsElGamal => _settings.Algorithm == 0;
        private bool IsKnapsack => _settings.Algorithm == 2;
        private bool IsEcies => _settings.Algorithm == 3;

        private static readonly string[] CurveNames = { "secp256r1", "secp384r1", "secp521r1" };

        /// <summary>Number of items of the knapsack: a block of 64 bits (8 bytes) is encrypted at a time.</summary>
        private const int KnapsackItems = 64;

        public void Execute()
        {
            try
            {
                switch (_settings.Action)
                {
                    case 1:
                        Encrypt();
                        break;
                    case 2:
                        Decrypt();
                        break;
                    default:
                        GenerateKeyPair();
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidCastException || ex is IOException || ex is InvalidOperationException)
            {
                GuiLogMessage("The operation failed, probably because the key or the data does not fit: " + ex.Message, NotificationLevel.Error);
            }
        }

        // ---------------------------------------------------------------- keys

        private static byte[] Encode(params BigInteger[] numbers)
        {
            var vector = new Asn1EncodableVector();
            foreach (BigInteger number in numbers) { vector.Add(new DerInteger(number)); }
            return new DerSequence(vector).GetDerEncoded();
        }

        private static BigInteger[] Decode(byte[] key, int count)
        {
            var sequence = (Asn1Sequence)Asn1Object.FromByteArray(key);
            if (sequence.Count != count) { throw new ArgumentException("the key has " + sequence.Count + " numbers instead of " + count); }
            var numbers = new BigInteger[count];
            for (int i = 0; i < count; i++) { numbers[i] = ((DerInteger)sequence[i]).Value; }
            return numbers;
        }

        private void GenerateKeyPair()
        {
            var random = new SecureRandom();
            int bits = _settings.KeySize == 0 ? 1024 : 2048;
            if (IsEcies)
            {
                GenerateEciesKeys(random);
                return;
            }
            if (IsKnapsack)
            {
                GenerateKnapsackKeys(random);
                return;
            }
            if (IsElGamal)
            {
                BigInteger p = new BigInteger(_settings.KeySize == 0 ? Prime1024 : Prime2048, 16);
                BigInteger g = BigInteger.Two;
                BigInteger x;
                do { x = new BigInteger(bits - 1, random); } while (x.CompareTo(BigInteger.Two) < 0);
                OutputPrivateKey = Encode(p, g, x);
                OutputPublicKey = Encode(p, g, g.ModPow(x, p));
            }
            else
            {
                BigInteger p = RabinPrime(bits / 2, random);
                BigInteger q;
                do { q = RabinPrime(bits / 2, random); } while (q.Equals(p));
                OutputPrivateKey = Encode(p, q);
                OutputPublicKey = Encode(p.Multiply(q));
            }
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("OutputPublicKey");
        }

        /// <summary>A prime congruent to 3 modulo 4, so that square roots modulo p are easy to compute.</summary>
        private static BigInteger RabinPrime(int bits, SecureRandom random)
        {
            BigInteger p;
            do { p = BigInteger.ProbablePrime(bits, random); } while (p.Mod(BigInteger.Four).IntValue != 3);
            return p;
        }

        // ---------------------------------------------------------------- ECIES

        private void GenerateEciesKeys(SecureRandom random)
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(ECNamedCurveTable.GetOid(CurveNames[_settings.Curve]), random));
            AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
            OutputPrivateKey = PrivateKeyInfoFactory.CreatePrivateKeyInfo(pair.Private).GetDerEncoded();
            OutputPublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pair.Public).GetDerEncoded();
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("OutputPublicKey");
        }

        /// <summary>The shared secret Z of the elliptic curve Diffie-Hellman agreement, and from it, by KDF2 with SHA-256, 16 bytes for an AES key and 32 bytes for a MAC key.</summary>
        private static void DeriveKeys(ECPrivateKeyParameters own, ECPublicKeyParameters other, out byte[] encryptionKey, out byte[] macKey)
        {
            var agreement = new ECDHBasicAgreement();
            agreement.Init(own);
            byte[] z = BigIntegers.AsUnsignedByteArray(agreement.GetFieldSize(), agreement.CalculateAgreement(other));
            var kdf = new Kdf2BytesGenerator(new Sha256Digest());
            kdf.Init(new KdfParameters(z, null));
            byte[] keys = new byte[48];
            kdf.GenerateBytes(keys, 0, keys.Length);
            encryptionKey = new byte[16];
            macKey = new byte[32];
            Array.Copy(keys, 0, encryptionKey, 0, 16);
            Array.Copy(keys, 16, macKey, 0, 32);
        }

        private static byte[] Tag(byte[] macKey, byte[] ephemeralPoint, byte[] ciphertext)
        {
            var mac = new HMac(new Sha256Digest());
            mac.Init(new KeyParameter(macKey));
            mac.BlockUpdate(ephemeralPoint, 0, ephemeralPoint.Length);
            mac.BlockUpdate(ciphertext, 0, ciphertext.Length);
            byte[] tag = new byte[mac.GetMacSize()];
            mac.DoFinal(tag, 0);
            return tag;
        }

        private static byte[] Aes(byte[] key, byte[] data)
        {
            // AES in counter mode with a zero counter start: every message has a new key, so the counter may start at 0
            var cipher = new BufferedBlockCipher(new SicBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), new byte[16]));
            return cipher.DoFinal(data);
        }

        /// <summary>
        /// ECIES in the manner of SEC 1 and IEEE 1363a: an ephemeral key pair on the curve of the recipient's public key; ECDH with the
        /// recipient key and KDF2 give an AES key and a MAC key; the result is the ephemeral public point (uncompressed), the AES-CTR
        /// encrypted message and the HMAC-SHA-256 tag over both.
        /// </summary>
        private void EncryptEcies(byte[] message, MemoryStream output, SecureRandom random)
        {
            var recipient = (ECPublicKeyParameters)PublicKeyFactory.CreateKey(InputPublicKey);
            var generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(recipient.Parameters, random));
            AsymmetricCipherKeyPair ephemeral = generator.GenerateKeyPair();
            DeriveKeys((ECPrivateKeyParameters)ephemeral.Private, recipient, out byte[] encryptionKey, out byte[] macKey);

            byte[] point = ((ECPublicKeyParameters)ephemeral.Public).Q.GetEncoded(false);
            byte[] ciphertext = Aes(encryptionKey, message);
            byte[] tag = Tag(macKey, point, ciphertext);
            output.Write(point, 0, point.Length);
            output.Write(ciphertext, 0, ciphertext.Length);
            output.Write(tag, 0, tag.Length);
        }

        private bool DecryptEcies(byte[] data, MemoryStream output)
        {
            var own = (ECPrivateKeyParameters)PrivateKeyFactory.CreateKey(InputPrivateKey);
            int pointLength = 1 + 2 * ((own.Parameters.Curve.FieldSize + 7) / 8);
            if (data.Length < pointLength + 32)
            {
                GuiLogMessage("The ciphertext is too short: it must hold the ephemeral point (" + pointLength + " bytes), the encrypted message and the 32-byte tag.", NotificationLevel.Error);
                return false;
            }

            byte[] point = new byte[pointLength];
            Array.Copy(data, 0, point, 0, pointLength);
            byte[] ciphertext = new byte[data.Length - pointLength - 32];
            Array.Copy(data, pointLength, ciphertext, 0, ciphertext.Length);
            byte[] tag = new byte[32];
            Array.Copy(data, data.Length - 32, tag, 0, 32);

            var ephemeral = new ECPublicKeyParameters(own.Parameters.Curve.DecodePoint(point), own.Parameters);   // checks that the point is on the curve
            DeriveKeys(own, ephemeral, out byte[] encryptionKey, out byte[] macKey);
            if (!Arrays.ConstantTimeAreEqual(Tag(macKey, point, ciphertext), tag))
            {
                GuiLogMessage("Decryption failed: the authentication tag does not match (wrong key, or the ciphertext was changed).", NotificationLevel.Error);
                return false;
            }
            byte[] message = Aes(encryptionKey, ciphertext);
            output.Write(message, 0, message.Length);
            return true;
        }

        // ---------------------------------------------------------------- Merkle-Hellman knapsack

        /// <summary>
        /// Private key: a superincreasing sequence w_1..w_n (each element larger than the sum of the earlier ones), a modulus q larger than their
        /// sum and a multiplier r coprime to q. Public key: the "hard" knapsack b_i = r * w_i mod q.
        /// </summary>
        private void GenerateKnapsackKeys(SecureRandom random)
        {
            var w = new BigInteger[KnapsackItems];
            BigInteger sum = BigInteger.Zero;
            for (int i = 0; i < KnapsackItems; i++)
            {
                w[i] = sum.Add(BigInteger.One).Add(new BigInteger(KnapsackItems, random));
                sum = sum.Add(w[i]);
            }
            BigInteger q = sum.Add(BigInteger.One).Add(new BigInteger(KnapsackItems, random));
            BigInteger r;
            do { r = new BigInteger(q.BitLength - 1, random); } while (r.CompareTo(BigInteger.One) <= 0 || !r.Gcd(q).Equals(BigInteger.One));

            OutputPrivateKey = Encode(w.Concat(new[] { q, r }).ToArray());
            OutputPublicKey = Encode(w.Select(x => x.Multiply(r).Mod(q)).ToArray());
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("OutputPublicKey");
        }

        /// <summary>The length in bytes of an encrypted block: the sum of up to n numbers b_i.</summary>
        private static int KnapsackBlockLength(BigInteger[] publicKey)
        {
            return (publicKey.Max(x => x.BitLength) + 6 + 7) / 8;
        }

        /// <summary>
        /// Every block of 7 data bytes gets a first byte with the number of data bytes in it; the 64 bits of the block (most significant first)
        /// select the elements of the public knapsack, and their sum is the ciphertext.
        /// </summary>
        private void EncryptKnapsack(byte[] message, MemoryStream output)
        {
            BigInteger[] publicKey = Decode(InputPublicKey, KnapsackItems);
            int length = KnapsackBlockLength(publicKey);
            for (int offset = 0; offset < Math.Max(message.Length, 1); offset += 7)
            {
                int count = Math.Min(7, message.Length - offset);
                byte[] block = new byte[8];
                block[0] = (byte)count;
                Array.Copy(message, offset, block, 1, count);
                BigInteger sum = BigInteger.Zero;
                for (int i = 0; i < KnapsackItems; i++)
                {
                    if ((block[i / 8] >> (7 - i % 8) & 1) == 1) { sum = sum.Add(publicKey[i]); }
                }
                byte[] c = ToBytes(sum, length);
                output.Write(c, 0, length);
            }
        }

        private bool DecryptKnapsack(byte[] data, MemoryStream output)
        {
            BigInteger[] key = Decode(InputPrivateKey, KnapsackItems + 2);
            BigInteger q = key[KnapsackItems], r = key[KnapsackItems + 1];
            BigInteger[] w = key.Take(KnapsackItems).ToArray();
            int length = KnapsackBlockLength(w.Select(x => x.Multiply(r).Mod(q)).ToArray());
            if (data.Length == 0 || data.Length % length != 0)
            {
                GuiLogMessage("The ciphertext must consist of blocks of " + length + " bytes.", NotificationLevel.Error);
                return false;
            }

            BigInteger inverse = r.ModInverse(q);
            for (int offset = 0; offset < data.Length; offset += length)
            {
                // r^-1 * c = sum of the chosen w_i (mod q), and this sum is small enough to be read off greedily from the largest element
                BigInteger rest = new BigInteger(1, data, offset, length).Multiply(inverse).Mod(q);
                byte[] block = new byte[8];
                for (int i = KnapsackItems - 1; i >= 0; i--)
                {
                    if (w[i].CompareTo(rest) <= 0)
                    {
                        rest = rest.Subtract(w[i]);
                        block[i / 8] |= (byte)(1 << (7 - i % 8));
                    }
                }
                if (!rest.Equals(BigInteger.Zero) || block[0] > 7)
                {
                    GuiLogMessage("Decryption failed: the block does not have the expected form (wrong key?).", NotificationLevel.Error);
                    return false;
                }
                output.Write(block, 1, block[0]);
            }
            return true;
        }

        // ---------------------------------------------------------------- blocks

        private static int ByteLength(BigInteger modulus) => (modulus.BitLength + 7) / 8;

        /// <summary>The number as an unsigned big-endian byte array of exactly the given length.</summary>
        private static byte[] ToBytes(BigInteger number, int length)
        {
            byte[] bytes = number.ToByteArrayUnsigned();
            if (bytes.Length > length) { throw new ArgumentException("number too large"); }
            byte[] result = new byte[length];
            Array.Copy(bytes, 0, result, length - bytes.Length, bytes.Length);
            return result;
        }

        private static byte[] Check(byte[] data, int offset, int count)
        {
            var digest = new Sha256Digest();
            digest.BlockUpdate(data, offset, count);
            byte[] hash = new byte[32];
            digest.DoFinal(hash, 0);
            return new[] { hash[0], hash[1], hash[2], hash[3] };
        }

        // ---------------------------------------------------------------- encryption

        private void Encrypt()
        {
            if (InputData == null || InputPublicKey == null)
            {
                GuiLogMessage("To encrypt, both the data and the public key are needed.", NotificationLevel.Error);
                return;
            }

            byte[] message = InputData.CreateReader().ReadFully();
            var random = new SecureRandom();
            var output = new MemoryStream();
            if (IsEcies)
            {
                EncryptEcies(message, output, random);
            }
            else if (IsKnapsack)
            {
                EncryptKnapsack(message, output);
            }
            else if (IsElGamal)
            {
                BigInteger[] key = Decode(InputPublicKey, 3);
                BigInteger p = key[0], g = key[1], y = key[2];
                int length = ByteLength(p);
                int chunk = length - 2;   // marker byte and one byte less than the modulus
                for (int offset = 0; offset < Math.Max(message.Length, 1); offset += chunk)
                {
                    int count = Math.Min(chunk, message.Length - offset);
                    byte[] block = new byte[count + 1];
                    block[0] = 1;
                    Array.Copy(message, offset, block, 1, count);
                    BigInteger m = new BigInteger(1, block);
                    BigInteger k;
                    do { k = new BigInteger(p.BitLength - 1, random); } while (k.CompareTo(BigInteger.Two) < 0);
                    byte[] c1 = ToBytes(g.ModPow(k, p), length);
                    byte[] c2 = ToBytes(m.Multiply(y.ModPow(k, p)).Mod(p), length);
                    output.Write(c1, 0, length);
                    output.Write(c2, 0, length);
                }
            }
            else
            {
                BigInteger n = Decode(InputPublicKey, 1)[0];
                int length = ByteLength(n);
                int chunk = length - 6;   // marker byte, four check bytes and one byte less than the modulus
                for (int offset = 0; offset < Math.Max(message.Length, 1); offset += chunk)
                {
                    int count = Math.Min(chunk, message.Length - offset);
                    byte[] block = new byte[count + 5];
                    block[0] = 1;
                    Array.Copy(message, offset, block, 1, count);
                    Array.Copy(Check(message, offset, count), 0, block, count + 1, 4);
                    BigInteger m = new BigInteger(1, block);
                    byte[] c = ToBytes(m.Multiply(m).Mod(n), length);
                    output.Write(c, 0, length);
                }
            }

            WriteOutput(output.ToArray());
        }

        private void Decrypt()
        {
            if (InputData == null || InputPrivateKey == null)
            {
                GuiLogMessage("To decrypt, both the data and the private key are needed.", NotificationLevel.Error);
                return;
            }

            byte[] data = InputData.CreateReader().ReadFully();
            var output = new MemoryStream();
            if (IsEcies)
            {
                if (!DecryptEcies(data, output)) { return; }
            }
            else if (IsKnapsack)
            {
                if (!DecryptKnapsack(data, output)) { return; }
            }
            else if (IsElGamal)
            {
                BigInteger[] key = Decode(InputPrivateKey, 3);
                BigInteger p = key[0], x = key[2];
                int length = ByteLength(p);
                if (data.Length == 0 || data.Length % (2 * length) != 0)
                {
                    GuiLogMessage("The ciphertext must consist of blocks of " + 2 * length + " bytes.", NotificationLevel.Error);
                    return;
                }
                for (int offset = 0; offset < data.Length; offset += 2 * length)
                {
                    BigInteger c1 = new BigInteger(1, data, offset, length);
                    BigInteger c2 = new BigInteger(1, data, offset + length, length);
                    // m = c2 / c1^x = c2 * c1^(p-1-x)
                    byte[] block = c2.Multiply(c1.ModPow(p.Subtract(BigInteger.One).Subtract(x), p)).Mod(p).ToByteArrayUnsigned();
                    if (block.Length == 0 || block[0] != 1)
                    {
                        GuiLogMessage("Decryption failed: the block does not have the expected form (wrong key?).", NotificationLevel.Error);
                        return;
                    }
                    output.Write(block, 1, block.Length - 1);
                }
            }
            else
            {
                BigInteger[] key = Decode(InputPrivateKey, 2);
                BigInteger p = key[0], q = key[1], n = p.Multiply(q);
                int length = ByteLength(n);
                if (data.Length == 0 || data.Length % length != 0)
                {
                    GuiLogMessage("The ciphertext must consist of blocks of " + length + " bytes.", NotificationLevel.Error);
                    return;
                }
                for (int offset = 0; offset < data.Length; offset += length)
                {
                    byte[] plain = RabinRoot(new BigInteger(1, data, offset, length), p, q, n);
                    if (plain == null)
                    {
                        GuiLogMessage("Decryption failed: none of the four square roots has the expected form (wrong key?).", NotificationLevel.Error);
                        return;
                    }
                    output.Write(plain, 0, plain.Length);
                }
            }

            WriteOutput(output.ToArray());
        }

        /// <summary>The four square roots of c modulo n = p*q; returns the data of the one that has the expected form.</summary>
        private static byte[] RabinRoot(BigInteger c, BigInteger p, BigInteger q, BigInteger n)
        {
            // for p = 3 mod 4: a square root of c modulo p is c^((p+1)/4)
            BigInteger mp = c.ModPow(p.Add(BigInteger.One).ShiftRight(2), p);
            BigInteger mq = c.ModPow(q.Add(BigInteger.One).ShiftRight(2), q);
            BigInteger yp = p.ModInverse(q);   // Chinese remainder theorem: r = mp + p * ((mq - mp) * p^-1 mod q)
            foreach (BigInteger sp in new[] { mp, p.Subtract(mp) })
            {
                foreach (BigInteger sq in new[] { mq, q.Subtract(mq) })
                {
                    BigInteger r = sp.Add(p.Multiply(sq.Subtract(sp).Multiply(yp).Mod(q))).Mod(n);
                    byte[] block = r.ToByteArrayUnsigned();
                    if (block.Length < 5 || block[0] != 1) { continue; }
                    int count = block.Length - 5;
                    byte[] check = Check(block, 1, count);
                    if (check[0] == block[count + 1] && check[1] == block[count + 2] && check[2] == block[count + 3] && check[3] == block[count + 4])
                    {
                        byte[] data = new byte[count];
                        Array.Copy(block, 1, data, 0, count);
                        return data;
                    }
                }
            }
            return null;
        }

        private void WriteOutput(byte[] data)
        {
            OutputData = new CStreamWriter(data);
            OnPropertyChanged("OutputData");
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            Dispose();
        }

        public void PostExecution()
        {
            Dispose();
        }

        public void Dispose()
        {
            InputData = null;
            InputPrivateKey = null;
            InputPublicKey = null;
            OutputPrivateKey = null;
            OutputPublicKey = null;
            OutputData = null;
        }

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string message, NotificationLevel level)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, message, level);
        }
    }
}

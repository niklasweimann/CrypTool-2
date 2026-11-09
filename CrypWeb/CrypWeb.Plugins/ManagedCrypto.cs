// The browser runtime has no OS crypto, so System.Security.Cryptography block ciphers (Aes, DES, 3DES, RC2, Rijndael) throw
// PlatformNotSupportedException there. These classes use the same names in the GLOBAL namespace: C# name lookup finds a global
// type before types imported by `using System.Security.Cryptography;`, so plugin code compiles against them unchanged.
// The implementation is BouncyCastle (managed).
using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

public abstract class ManagedBlockCipher : SymmetricAlgorithm
{
    protected abstract IBlockCipher NewEngine();
    protected ManagedBlockCipher(int blockBits, int keyBits, KeySizes[] keySizes)
    {
        BlockSizeValue = blockBits; KeySizeValue = keyBits; LegalKeySizesValue = keySizes; LegalBlockSizesValue = new[] { new KeySizes(blockBits, blockBits, 0) };
        FeedbackSizeValue = 8; ModeValue = CipherMode.CBC; PaddingValue = PaddingMode.PKCS7;
    }
    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) => new Transform(this, true, rgbKey, rgbIV);
    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) => new Transform(this, false, rgbKey, rgbIV);
    public override void GenerateKey() { var k = new byte[KeySizeValue / 8]; new Random().NextBytes(k); KeyValue = k; }
    public override void GenerateIV() { var v = new byte[BlockSizeValue / 8]; new Random().NextBytes(v); IVValue = v; }

    sealed class Transform : ICryptoTransform
    {
        readonly bool enc; readonly PaddingMode pad; readonly int bs; readonly int pbs; readonly BufferedBlockCipher cipher; readonly bool stream;
        byte[] pending = new byte[0];  // decrypt: last full block withheld until we know whether it is the final one
        public Transform(ManagedBlockCipher a, bool encrypt, byte[] key, byte[] iv)
        {
            enc = encrypt; pad = a.Padding; bs = a.BlockSize / 8; pbs = a.Mode == CipherMode.CFB ? Math.Max(1, a.FeedbackSize / 8) : bs; // CFB pads in units of its feedback size
            var engine = a.NewEngine();
            IBlockCipher mode = a.Mode == CipherMode.CBC ? new CbcBlockCipher(engine) : a.Mode == CipherMode.CFB ? new CfbBlockCipher(engine, a.FeedbackSize) : engine;
            stream = a.Mode == CipherMode.CFB && a.FeedbackSize != a.BlockSize;
            cipher = new BufferedBlockCipher(mode);
            ICipherParameters p = a.Mode == CipherMode.ECB || iv == null ? new KeyParameter(key) : new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(encrypt, p);
        }
        public int InputBlockSize => stream ? 1 : bs;
        public int OutputBlockSize => stream ? 1 : bs;
        public bool CanTransformMultipleBlocks => true;
        public bool CanReuseTransform => false;
        public void Dispose() { }

        bool Depad => !enc && pad is PaddingMode.PKCS7 or PaddingMode.ANSIX923 or PaddingMode.ISO10126;

        byte[] Run(byte[] data, int off, int count)
        {
            var o = new byte[cipher.GetUpdateOutputSize(count) + bs];
            int n = cipher.ProcessBytes(data, off, count, o, 0);
            if (n != o.Length) Array.Resize(ref o, n);
            return o;
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            var o = Run(inputBuffer, inputOffset, inputCount);
            if (Depad)
            {   // hold the newest decrypted block back
                var all = new byte[pending.Length + o.Length]; Array.Copy(pending, all, pending.Length); Array.Copy(o, 0, all, pending.Length, o.Length);
                int keep = Math.Min(all.Length, pbs), give = all.Length - keep;
                Array.Copy(all, 0, outputBuffer, outputOffset, give);
                pending = new byte[keep]; Array.Copy(all, give, pending, 0, keep);
                return give;
            }
            Array.Copy(o, 0, outputBuffer, outputOffset, o.Length); return o.Length;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var data = new byte[inputCount]; Array.Copy(inputBuffer, inputOffset, data, 0, inputCount);
            if (enc) data = Pad(data);
            if (!enc && !stream && data.Length % bs != 0) throw new CryptographicException("Length of the data to decrypt is invalid.");
            var o = Run(data, 0, data.Length);
            var tail = new byte[cipher.GetOutputSize(0) + bs]; int t = 0; try { t = cipher.DoFinal(tail, 0); } catch (DataLengthException) { }
            var res = new byte[pending.Length + o.Length + t];
            Array.Copy(pending, res, pending.Length); Array.Copy(o, 0, res, pending.Length, o.Length); Array.Copy(tail, 0, res, pending.Length + o.Length, t);
            pending = new byte[0];
            return Depad ? Unpad(res) : res;
        }

        byte[] Pad(byte[] d)
        {
            int rem = d.Length % pbs;
            int add = pad switch { PaddingMode.None => 0, PaddingMode.Zeros => rem == 0 ? 0 : pbs - rem, _ => pbs - rem };
            if (add == 0) { if (pad == PaddingMode.None && rem != 0) throw new CryptographicException("Length of the data to encrypt is invalid."); return d; }
            var r = new byte[d.Length + add]; Array.Copy(d, r, d.Length);
            var rnd = new Random();
            for (int i = d.Length; i < r.Length; i++)
                r[i] = pad switch { PaddingMode.PKCS7 => (byte)add, PaddingMode.ANSIX923 => 0, PaddingMode.ISO10126 => (byte)rnd.Next(256), _ => 0 };
            if (pad == PaddingMode.ANSIX923 || pad == PaddingMode.ISO10126) r[^1] = (byte)add;
            return r;
        }

        byte[] Unpad(byte[] d)
        {
            if (d.Length == 0) return d;
            int n = d[^1];
            if (n < 1 || n > pbs || n > d.Length) throw new CryptographicException("Padding is invalid and cannot be removed.");
            if (pad == PaddingMode.PKCS7) for (int i = d.Length - n; i < d.Length; i++) if (d[i] != n) throw new CryptographicException("Padding is invalid and cannot be removed.");
            var r = new byte[d.Length - n]; Array.Copy(d, r, r.Length); return r;
        }
    }
}

public class RijndaelManaged : ManagedBlockCipher
{
    public RijndaelManaged() : base(128, 256, new[] { new KeySizes(128, 256, 64) }) { LegalBlockSizesValue = new[] { new KeySizes(128, 256, 64) }; }
    protected override IBlockCipher NewEngine() => BlockSize == 128 ? new AesEngine() : new RijndaelEngine(BlockSize);
}
public class AesManaged : ManagedBlockCipher
{
    public AesManaged() : base(128, 256, new[] { new KeySizes(128, 256, 64) }) { }
    protected override IBlockCipher NewEngine() => new AesEngine();
}
public class AesCryptoServiceProvider : AesManaged { }
public class Aes : AesManaged { public static Aes Create() => new Aes(); }
public class DESCryptoServiceProvider : ManagedBlockCipher
{
    public DESCryptoServiceProvider() : base(64, 64, new[] { new KeySizes(64, 64, 0) }) { }
    protected override IBlockCipher NewEngine() => new DesEngine();
}
public class TripleDESCryptoServiceProvider : ManagedBlockCipher
{
    public TripleDESCryptoServiceProvider() : base(64, 192, new[] { new KeySizes(128, 192, 64) }) { }
    protected override IBlockCipher NewEngine() => new DesEdeEngine();
}
public class RC2CryptoServiceProvider : ManagedBlockCipher
{
    public RC2CryptoServiceProvider() : base(64, 128, new[] { new KeySizes(40, 128, 8) }) { }
    protected override IBlockCipher NewEngine() => new RC2Engine();
}

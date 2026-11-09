// Managed stand-in for LibSource/NativeCryptography (C++/CLI wrapper around Windows crypto used for fast key searching).
// The browser has no such library and no System.Security.Cryptography block ciphers, so BouncyCastle does the work.
using System;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace NativeCryptography
{
    public static class Crypto
    {
        static byte[] Decrypt(IBlockCipher engine, string mode, byte[] input, byte[] key, byte[] iv, int length)
        {
            int bs = engine.GetBlockSize();
            length = Math.Min(length, input.Length);
            if (mode != "CFB") length -= length % bs; // the native code only handles whole blocks
            IBlockCipher c = mode == "CBC" ? new CbcBlockCipher(engine) : mode == "CFB" ? new CfbBlockCipher(engine, 8) : engine;
            var cipher = new BufferedBlockCipher(c);
            ICipherParameters p = iv == null || mode == "ECB" ? new KeyParameter(key) : new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(false, p);
            var output = new byte[cipher.GetOutputSize(length)];
            int n = cipher.ProcessBytes(input, 0, length, output, 0);
            n += cipher.DoFinal(output, n);
            if (n != output.Length) Array.Resize(ref output, n);
            return output;
        }

        public static byte[] decryptDES_ECB(byte[] Input, byte[] Key, int length) => Decrypt(new DesEngine(), "ECB", Input, Key, null, length);
        public static byte[] decryptDES_CBC(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new DesEngine(), "CBC", Input, Key, IV, length);
        public static byte[] decryptDES_CFB(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new DesEngine(), "CFB", Input, Key, IV, length);
        public static byte[] decrypt3DES_ECB(byte[] Input, byte[] Key, int length) => Decrypt(new DesEdeEngine(), "ECB", Input, Key, null, length);
        public static byte[] decrypt3DES_CBC(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new DesEdeEngine(), "CBC", Input, Key, IV, length);
        public static byte[] decrypt3DES_CFB(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new DesEdeEngine(), "CFB", Input, Key, IV, length);

        public static byte[] decryptAES128_ECB(byte[] Input, byte[] Key, int length) => Decrypt(new AesEngine(), "ECB", Input, Key, null, length);
        public static byte[] decryptAES128_CBC(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new AesEngine(), "CBC", Input, Key, IV, length);
        public static byte[] decryptAES128_ECB_NI(byte[] Input, byte[] Key, int length) => decryptAES128_ECB(Input, Key, length);
        public static byte[] decryptAES128_CBC_NI(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CBC(Input, Key, IV, length);
        public static byte[] decryptAES128_CFB(byte[] Input, byte[] Key, byte[] IV, int length) => Decrypt(new AesEngine(), "CFB", Input, Key, IV, length);
        public static byte[] decryptAES192_ECB(byte[] Input, byte[] Key, int length) => decryptAES128_ECB(Input, Key, length);
        public static byte[] decryptAES192_CBC(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CBC(Input, Key, IV, length);
        public static byte[] decryptAES192_ECB_NI(byte[] Input, byte[] Key, int length) => decryptAES128_ECB(Input, Key, length);
        public static byte[] decryptAES192_CBC_NI(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CBC(Input, Key, IV, length);
        public static byte[] decryptAES192_CFB(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CFB(Input, Key, IV, length);
        public static byte[] decryptAES256_ECB(byte[] Input, byte[] Key, int length) => decryptAES128_ECB(Input, Key, length);
        public static byte[] decryptAES256_CBC(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CBC(Input, Key, IV, length);
        public static byte[] decryptAES256_ECB_NI(byte[] Input, byte[] Key, int length) => decryptAES128_ECB(Input, Key, length);
        public static byte[] decryptAES256_CBC_NI(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CBC(Input, Key, IV, length);
        public static byte[] decryptAES256_CFB(byte[] Input, byte[] Key, byte[] IV, int length) => decryptAES128_CFB(Input, Key, IV, length);
        public static bool supportsAESNI() => false;

        public static byte[] decryptRC2(byte[] Input, byte[] Key, byte[] IV, int length, int mode) => Decrypt(new RC2Engine(), mode == 0 ? "ECB" : "CBC", Input, Key, IV, length);

        public static byte[] decryptRC4(byte[] Input, byte[] Key, int length, int keylength)
        {
            var key = keylength > 0 && keylength < Key.Length ? Key[..keylength] : Key;
            var rc4 = new RC4Engine(); rc4.Init(false, new KeyParameter(key));
            length = Math.Min(length, Input.Length);
            var o = new byte[length]; rc4.ProcessBytes(Input, 0, length, o, 0); return o;
        }

        public static double calculateEntropy(byte[] text, int bytesToUse)
        {
            int n = Math.Min(bytesToUse, text.Length); if (n == 0) return 0;
            var f = new int[256]; for (int i = 0; i < n; i++) f[text[i]]++;
            double e = 0; foreach (var c in f) if (c > 0) { double p = (double)c / n; e -= p * Math.Log(p, 2); }
            return e;
        }
    }
}

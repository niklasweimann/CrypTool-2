using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System;

namespace BlockCiphers
{
    /// <summary>
    /// DESX (Rivest, 1984; analysed by Kilian and Rogaway): DES with key whitening. The key of 24 bytes is the DES key K (8 bytes) followed by
    /// the whitening keys K1 and K2 (8 bytes each): C = K2 xor DES_K(P xor K1). The whitening makes an exhaustive search for the key
    /// much harder than for plain DES, although it does not make the key longer than about 120 effective bits.
    /// </summary>
    public class DesxEngine : IBlockCipher
    {
        private readonly DesEngine _des = new DesEngine();
        private byte[] _k1, _k2;
        private bool _encrypt;

        public string AlgorithmName => "DESX";

        public bool IsPartialBlockOkay => false;

        public int GetBlockSize() => 8;

        public void Init(bool forEncryption, ICipherParameters parameters)
        {
            var key = parameters as KeyParameter;
            if (key == null || key.GetKey().Length != 24) { throw new ArgumentException("DESX needs a key of 24 bytes (DES key, whitening keys K1 and K2)."); }
            byte[] k = key.GetKey();
            _encrypt = forEncryption;
            _k1 = new byte[8];
            _k2 = new byte[8];
            Array.Copy(k, 8, _k1, 0, 8);
            Array.Copy(k, 16, _k2, 0, 8);
            _des.Init(forEncryption, new KeyParameter(k, 0, 8));
        }

        public int ProcessBlock(byte[] input, int inOff, byte[] output, int outOff)
        {
            if (_k1 == null) { throw new InvalidOperationException("DESX is not initialized."); }
            if (inOff + 8 > input.Length || outOff + 8 > output.Length) { throw new DataLengthException("The block does not fit into the array."); }

            byte[] block = new byte[8];
            for (int i = 0; i < 8; i++) { block[i] = (byte)(input[inOff + i] ^ (_encrypt ? _k1[i] : _k2[i])); }
            _des.ProcessBlock(block, 0, block, 0);
            for (int i = 0; i < 8; i++) { output[outOff + i] = (byte)(block[i] ^ (_encrypt ? _k2[i] : _k1[i])); }
            return 8;
        }


#if NET6_0_OR_GREATER
        // newer versions of BouncyCastle (as used by the browser edition) add this overload to IBlockCipher; the desktop version does not have it
        public int ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            byte[] inputBlock = input.Slice(0, GetBlockSize()).ToArray();
            byte[] outputBlock = new byte[GetBlockSize()];
            ProcessBlock(inputBlock, 0, outputBlock, 0);
            outputBlock.CopyTo(output);
            return outputBlock.Length;
        }
#endif

        public void Reset()
        {
        }
    }
}

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Numerics;

namespace BlockCiphers
{
    /// <summary>
    /// Shacal-1 and Shacal-2 (Handschuh and Naccache, 2000): the compression functions of SHA-1 and SHA-256 used as block ciphers.
    /// The block is the state of the hash function (160 or 256 bits), the key (128 to 512 bits, padded with zeros) takes the place of
    /// the message block: it is expanded to the round words exactly as SHA expands a message block, and the rounds are those of SHA
    /// without the final addition of the old state. Encrypting the initial value of SHA with a message block as the key and adding
    /// the initial value to the result gives the SHA hash value of that block.
    /// </summary>
    public class ShacalEngine : IBlockCipher
    {
        private readonly bool _sha256;
        private bool _encrypt;
        private uint[] _w;
        private static readonly uint[] K256 = CubeRootConstants();

        /// <param name="sha256">true for Shacal-2 (SHA-256, 256-bit blocks, 64 rounds), false for Shacal-1 (SHA-1, 160-bit blocks, 80 rounds)</param>
        public ShacalEngine(bool sha256)
        {
            _sha256 = sha256;
        }

        public string AlgorithmName => _sha256 ? "Shacal-2" : "Shacal-1";

        public bool IsPartialBlockOkay => false;

        public int GetBlockSize() => _sha256 ? 32 : 20;

        /// <summary>The 64 SHA-256 round constants: the first 32 bits of the fractional parts of the cube roots of the first 64 primes.</summary>
        private static uint[] CubeRootConstants()
        {
            var constants = new uint[64];
            int count = 0;
            for (int candidate = 2; count < 64; candidate++)
            {
                bool prime = true;
                for (int d = 2; d * d <= candidate; d++) { if (candidate % d == 0) { prime = false; break; } }
                if (!prime) { continue; }
                // floor(cbrt(p * 2^96)) = integer part (high bits) and 32 fractional bits (low 32 bits)
                BigInteger target = new BigInteger(candidate) << 96;
                BigInteger low = 0, high = BigInteger.One << 60;
                while (low < high)
                {
                    BigInteger middle = (low + high + 1) >> 1;
                    if (middle * middle * middle <= target) { low = middle; } else { high = middle - 1; }
                }
                constants[count++] = (uint)(low & 0xFFFFFFFF);
            }
            return constants;
        }

        private static uint RotL(uint x, int n) => x << n | x >> (32 - n);
        private static uint RotR(uint x, int n) => x >> n | x << (32 - n);

        public void Init(bool forEncryption, ICipherParameters parameters)
        {
            var key = parameters as KeyParameter;
            if (key == null) { throw new ArgumentException("A key is needed."); }
            byte[] k = key.GetKey();
            if (k.Length < 16 || k.Length > 64) { throw new ArgumentException("The key of " + AlgorithmName + " must have 16 to 64 bytes."); }

            _encrypt = forEncryption;
            int rounds = _sha256 ? 64 : 80;
            _w = new uint[rounds];
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int index = 4 * i + j;
                    _w[i] = _w[i] << 8 | (index < k.Length ? k[index] : (uint)0);
                }
            }
            for (int t = 16; t < rounds; t++)
            {
                if (_sha256)
                {
                    uint s0 = RotR(_w[t - 15], 7) ^ RotR(_w[t - 15], 18) ^ (_w[t - 15] >> 3);
                    uint s1 = RotR(_w[t - 2], 17) ^ RotR(_w[t - 2], 19) ^ (_w[t - 2] >> 10);
                    _w[t] = s1 + _w[t - 7] + s0 + _w[t - 16];
                }
                else
                {
                    _w[t] = RotL(_w[t - 3] ^ _w[t - 8] ^ _w[t - 14] ^ _w[t - 16], 1);
                }
            }
        }

        private static uint Load(byte[] data, int offset) => (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

        private static void Store(uint value, byte[] data, int offset)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        public int ProcessBlock(byte[] input, int inOff, byte[] output, int outOff)
        {
            if (_w == null) { throw new InvalidOperationException(AlgorithmName + " is not initialized."); }
            int size = GetBlockSize();
            if (inOff + size > input.Length || outOff + size > output.Length) { throw new DataLengthException("The block does not fit into the array."); }

            var state = new uint[size / 4];
            for (int i = 0; i < state.Length; i++) { state[i] = Load(input, inOff + 4 * i); }
            if (_sha256) { Sha256(state); } else { Sha1(state); }
            for (int i = 0; i < state.Length; i++) { Store(state[i], output, outOff + 4 * i); }
            return size;
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

        private void Sha256(uint[] s)
        {
            if (_encrypt)
            {
                uint a = s[0], b = s[1], c = s[2], d = s[3], e = s[4], f = s[5], g = s[6], h = s[7];
                for (int t = 0; t < 64; t++)
                {
                    uint t1 = h + (RotR(e, 6) ^ RotR(e, 11) ^ RotR(e, 25)) + ((e & f) ^ (~e & g)) + K256[t] + _w[t];
                    uint t2 = (RotR(a, 2) ^ RotR(a, 13) ^ RotR(a, 22)) + ((a & b) ^ (a & c) ^ (b & c));
                    h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
                }
                s[0] = a; s[1] = b; s[2] = c; s[3] = d; s[4] = e; s[5] = f; s[6] = g; s[7] = h;
            }
            else
            {
                // the rounds backwards: the new a is t1 + t2, the new e is d + t1, all others are shifted
                uint a1 = s[0], b1 = s[1], c1 = s[2], d1 = s[3], e1 = s[4], f1 = s[5], g1 = s[6], h1 = s[7];
                for (int t = 63; t >= 0; t--)
                {
                    uint a = b1, b = c1, c = d1, e = f1, f = g1, g = h1;
                    uint t2 = (RotR(a, 2) ^ RotR(a, 13) ^ RotR(a, 22)) + ((a & b) ^ (a & c) ^ (b & c));
                    uint t1 = a1 - t2;
                    uint d = e1 - t1;
                    uint h = t1 - (RotR(e, 6) ^ RotR(e, 11) ^ RotR(e, 25)) - ((e & f) ^ (~e & g)) - K256[t] - _w[t];
                    a1 = a; b1 = b; c1 = c; d1 = d; e1 = e; f1 = f; g1 = g; h1 = h;
                }
                s[0] = a1; s[1] = b1; s[2] = c1; s[3] = d1; s[4] = e1; s[5] = f1; s[6] = g1; s[7] = h1;
            }
        }

        private static uint F1(int t, uint b, uint c, uint d)
        {
            if (t < 20) { return (b & c) | (~b & d); }
            if (t < 40 || t >= 60) { return b ^ c ^ d; }
            return (b & c) | (b & d) | (c & d);
        }

        private static uint K1(int t) => t < 20 ? 0x5A827999u : t < 40 ? 0x6ED9EBA1u : t < 60 ? 0x8F1BBCDCu : 0xCA62C1D6u;

        private void Sha1(uint[] s)
        {
            if (_encrypt)
            {
                uint a = s[0], b = s[1], c = s[2], d = s[3], e = s[4];
                for (int t = 0; t < 80; t++)
                {
                    uint temp = RotL(a, 5) + F1(t, b, c, d) + e + K1(t) + _w[t];
                    e = d; d = c; c = RotL(b, 30); b = a; a = temp;
                }
                s[0] = a; s[1] = b; s[2] = c; s[3] = d; s[4] = e;
            }
            else
            {
                uint a1 = s[0], b1 = s[1], c1 = s[2], d1 = s[3], e1 = s[4];
                for (int t = 79; t >= 0; t--)
                {
                    uint a = b1, b = RotR(c1, 30), c = d1, d = e1;
                    uint e = a1 - RotL(a, 5) - F1(t, b, c, d) - K1(t) - _w[t];
                    a1 = a; b1 = b; c1 = c; d1 = d; e1 = e;
                }
                s[0] = a1; s[1] = b1; s[2] = c1; s[3] = d1; s[4] = e1;
            }
        }
    }
}

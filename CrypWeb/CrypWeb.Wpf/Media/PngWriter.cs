// Pixels of WPF bitmaps: decoding, conversion between pixel formats, and the encoders (PNG, BMP).
//
// Pictures are normally drawn by the browser from their address. Plugins that work with the pixels themselves
// (CopyPixels, BitmapSource.Create, CroppedBitmap, encoders) get them from here: encoded pictures are decoded with
// StbImageSharp, and every picture is kept as 32-bit BGRA, the layout of WPF's Bgra32 format.
using System.IO;
using System.IO.Compression;

namespace System.Windows.Media.Imaging
{
    /// <summary>Writes pixels as a PNG file (8-bit RGBA, no filtering).</summary>
    internal static class PngWriter
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        internal static byte[] Encode(BgraPixels pixels)
        {
            using var output = new MemoryStream();
            output.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });

            var header = new byte[13];
            WriteBigEndian(header, 0, (uint)pixels.Width);
            WriteBigEndian(header, 4, (uint)pixels.Height);
            header[8] = 8;      // bits per channel
            header[9] = 6;      // colour type: RGBA
            WriteChunk(output, "IHDR", header);

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                var row = new byte[pixels.Width * 4 + 1];   // filter byte 0, then the row
                for (int y = 0; y < pixels.Height; y++)
                {
                    for (int x = 0; x < pixels.Width; x++)
                    {
                        int source = (y * pixels.Width + x) * 4;
                        row[1 + x * 4] = pixels.Data[source + 2];
                        row[2 + x * 4] = pixels.Data[source + 1];
                        row[3 + x * 4] = pixels.Data[source];
                        row[4 + x * 4] = pixels.Data[source + 3];
                    }
                    zlib.Write(row);
                }
            }
            WriteChunk(output, "IDAT", compressed.ToArray());
            WriteChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            output.Write(length);
            byte[] typeBytes = Text.Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);
            uint crc = 0xFFFFFFFF;
            foreach (byte b in typeBytes)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            foreach (byte b in data)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFF);
            output.Write(crcBytes);
        }

        private static void WriteBigEndian(byte[] target, int offset, uint value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }
    }
}

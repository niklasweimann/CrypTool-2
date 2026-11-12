// Pixels of WPF bitmaps: decoding, conversion between pixel formats, and the encoders (PNG, BMP).
//
// Pictures are normally drawn by the browser from their address. Plugins that work with the pixels themselves
// (CopyPixels, BitmapSource.Create, CroppedBitmap, encoders) get them from here: encoded pictures are decoded with
// StbImageSharp, and every picture is kept as 32-bit BGRA, the layout of WPF's Bgra32 format.
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using StbImageSharp;

namespace System.Windows.Media.Imaging
{
    /// <summary>A picture as 32-bit BGRA pixels, rows from top to bottom without padding.</summary>
    internal sealed class BgraPixels
    {
        internal BgraPixels(int width, int height, byte[] data)
        {
            Width = width;
            Height = height;
            Data = data;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal byte[] Data { get; }

        /// <summary>Decodes a PNG, JPEG, BMP, GIF, ... file; null when there is none or it cannot be read.</summary>
        internal static BgraPixels Decode(byte[] encoded)
        {
            if (encoded == null || encoded.Length == 0)
            {
                return null;
            }
            try
            {
                ImageResult image = ImageResult.FromMemory(encoded, ColorComponents.RedGreenBlueAlpha);
                byte[] data = image.Data;
                for (int i = 0; i < data.Length; i += 4)
                {
                    (data[i], data[i + 2]) = (data[i + 2], data[i]);     // RGBA -> BGRA
                }
                return new BgraPixels(image.Width, image.Height, data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Reads pixels in a WPF pixel format (rows of <paramref name="stride"/> bytes).</summary>
        internal static BgraPixels Import(byte[] raw, int width, int height, PixelFormat format, BitmapPalette palette, int stride)
        {
            var data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int target = (y * width + x) * 4;
                    int row = y * stride;
                    byte blue, green, red, alpha = 255;
                    switch (format.Name)
                    {
                        case "Gray8":
                            blue = green = red = raw[row + x];
                            break;
                        case "Indexed8":
                            Color color = palette != null && raw[row + x] < palette.Colors.Count ? palette.Colors[raw[row + x]] : Colors.Black;
                            (blue, green, red, alpha) = (color.B, color.G, color.R, color.A);
                            break;
                        case "Bgr24":
                            (blue, green, red) = (raw[row + x * 3], raw[row + x * 3 + 1], raw[row + x * 3 + 2]);
                            break;
                        case "Rgb24":
                            (red, green, blue) = (raw[row + x * 3], raw[row + x * 3 + 1], raw[row + x * 3 + 2]);
                            break;
                        case "Bgr32":
                            (blue, green, red) = (raw[row + x * 4], raw[row + x * 4 + 1], raw[row + x * 4 + 2]);
                            break;
                        default:    // Bgra32, Pbgra32
                            (blue, green, red, alpha) = (raw[row + x * 4], raw[row + x * 4 + 1], raw[row + x * 4 + 2], raw[row + x * 4 + 3]);
                            break;
                    }
                    data[target] = blue;
                    data[target + 1] = green;
                    data[target + 2] = red;
                    data[target + 3] = alpha;
                }
            }
            return new BgraPixels(width, height, data);
        }

        /// <summary>An area of the pixels in a WPF pixel format, rows of <paramref name="stride"/> bytes.</summary>
        internal byte[] Export(Int32Rect area, PixelFormat format, int stride)
        {
            var result = new byte[stride * area.Height];
            int bytesPerPixel = Math.Max(format.BitsPerPixel / 8, 1);
            for (int y = 0; y < area.Height; y++)
            {
                for (int x = 0; x < area.Width; x++)
                {
                    int source = ((area.Y + y) * Width + area.X + x) * 4;
                    int target = y * stride + x * bytesPerPixel;
                    if (source + 3 >= Data.Length || target + bytesPerPixel > result.Length)
                    {
                        continue;
                    }
                    byte blue = Data[source], green = Data[source + 1], red = Data[source + 2], alpha = Data[source + 3];
                    switch (format.Name)
                    {
                        case "Gray8":
                        case "Indexed8":
                            result[target] = (byte)((red * 299 + green * 587 + blue * 114) / 1000);
                            break;
                        case "Bgr24":
                            (result[target], result[target + 1], result[target + 2]) = (blue, green, red);
                            break;
                        case "Rgb24":
                            (result[target], result[target + 1], result[target + 2]) = (red, green, blue);
                            break;
                        default:
                            (result[target], result[target + 1], result[target + 2], result[target + 3]) = (blue, green, red, alpha);
                            break;
                    }
                }
            }
            return result;
        }

        internal BgraPixels Crop(Int32Rect area)
        {
            int left = Math.Clamp(area.X, 0, Width);
            int top = Math.Clamp(area.Y, 0, Height);
            int width = Math.Clamp(area.Width, 0, Width - left);
            int height = Math.Clamp(area.Height, 0, Height - top);
            var data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(Data, ((top + y) * Width + left) * 4, data, y * width * 4, width * 4);
            }
            return new BgraPixels(width, height, data);
        }
    }

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

    /// <summary>Writes pixels as a 32-bit BMP file (bottom-up rows, as Windows writes them).</summary>
    internal static class BmpWriter
    {
        internal static byte[] Encode(BgraPixels pixels)
        {
            int imageSize = pixels.Width * pixels.Height * 4;
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(14 + 40 + imageSize);      // file size
            writer.Write(0);                        // reserved
            writer.Write(14 + 40);                  // offset of the pixels
            writer.Write(40);                       // BITMAPINFOHEADER
            writer.Write(pixels.Width);
            writer.Write(pixels.Height);            // positive: bottom-up
            writer.Write((short)1);                 // planes
            writer.Write((short)32);                // bits per pixel
            writer.Write(0);                        // BI_RGB
            writer.Write(imageSize);
            writer.Write(3780);                     // 96 dpi in pixels per metre
            writer.Write(3780);
            writer.Write(0);
            writer.Write(0);
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                writer.Write(pixels.Data, y * pixels.Width * 4, pixels.Width * 4);
            }
            writer.Flush();
            return output.ToArray();
        }
    }

    /// <summary>Encodes the pictures in <see cref="Frames"/> into a file (only the first frame is written).</summary>
    public abstract class BitmapEncoder
    {
        public IList<BitmapFrame> Frames { get; set; } = new List<BitmapFrame>();

        public void Save(Stream stream)
        {
            if (Frames.Count == 0)
            {
                throw new InvalidOperationException("The encoder has no frames.");
            }
            BitmapFrame frame = Frames[0];
            BgraPixels pixels = frame.Pixels;
            if (pixels == null)
            {
                // a picture known only by its address: its encoded data is all there is
                byte[] encoded = frame.EncodedBytes;
                if (encoded != null)
                {
                    stream.Write(encoded);
                }
                return;
            }
            stream.Write(Encode(pixels));
        }

        private protected abstract byte[] Encode(BgraPixels pixels);
    }

    public sealed class PngBitmapEncoder : BitmapEncoder
    {
        private protected override byte[] Encode(BgraPixels pixels) => PngWriter.Encode(pixels);
    }

    public sealed class BmpBitmapEncoder : BitmapEncoder
    {
        private protected override byte[] Encode(BgraPixels pixels) => BmpWriter.Encode(pixels);
    }

    /// <summary>Writes PNG: a JPEG encoder is not available in the browser build (readers detect the format).</summary>
    public sealed class JpegBitmapEncoder : BitmapEncoder
    {
        public int QualityLevel { get; set; } = 75;

        private protected override byte[] Encode(BgraPixels pixels) => PngWriter.Encode(pixels);
    }

    /// <summary>Writes PNG, see <see cref="JpegBitmapEncoder"/>.</summary>
    public sealed class GifBitmapEncoder : BitmapEncoder
    {
        private protected override byte[] Encode(BgraPixels pixels) => PngWriter.Encode(pixels);
    }
}

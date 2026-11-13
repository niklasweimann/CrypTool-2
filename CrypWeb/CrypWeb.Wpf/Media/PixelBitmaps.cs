// Pixels of WPF bitmaps: decoding, conversion between pixel formats, and the encoders (PNG, BMP).
//
// Pictures are normally drawn by the browser from their address. Plugins that work with the pixels themselves
// (CopyPixels, BitmapSource.Create, CroppedBitmap, encoders) get them from here: encoded pictures are decoded with
// StbImageSharp, and every picture is kept as 32-bit BGRA, the layout of WPF's Bgra32 format.
using System.Collections.Generic;
using System.IO;

namespace System.Windows.Media.Imaging
{

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

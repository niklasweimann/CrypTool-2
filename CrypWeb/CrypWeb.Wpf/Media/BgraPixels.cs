// Pixels of WPF bitmaps: decoding, conversion between pixel formats, and the encoders (PNG, BMP).
//
// Pictures are normally drawn by the browser from their address. Plugins that work with the pixels themselves
// (CopyPixels, BitmapSource.Create, CroppedBitmap, encoders) get them from here: encoded pictures are decoded with
// StbImageSharp, and every picture is kept as 32-bit BGRA, the layout of WPF's Bgra32 format.
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
                            Color color = palette != null
                                && raw[row + x] < palette.Colors.Count ? palette.Colors[raw[row + x]] : Colors.Black;
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
                            (blue, green, red, alpha) = (raw[row + x * 4],
                                raw[row + x * 4 + 1],
                                raw[row + x * 4 + 2],
                                raw[row + x * 4 + 3]);
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
}

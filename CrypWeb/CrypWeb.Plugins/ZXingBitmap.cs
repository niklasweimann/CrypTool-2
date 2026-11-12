// ZXing for System.Drawing bitmaps in the browser build.
//
// CT2's zxing.dll (.NET Framework) has BarcodeReader and BarcodeWriter classes that read and write
// System.Drawing.Bitmap. The ZXing.Net package for .NET only has the generic versions, so these two classes connect
// it with CrypWeb's managed System.Drawing (CrypWeb.Drawing): the reader hands the bitmap's pixels to ZXing as a
// luminance source, the writer paints the encoded matrix into a bitmap.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing.Common;
using ZXing.Rendering;

namespace ZXing
{
    /// <summary>Decodes barcodes in a <see cref="Bitmap"/>.</summary>
    public class BarcodeReader : BarcodeReader<Bitmap>
    {
        public BarcodeReader() : base(null, ToLuminanceSource, null) { }

        // the old zxing.dll had the decoding options on the reader itself; ZXing.Net keeps them in Options

        public System.Collections.Generic.IList<BarcodeFormat> PossibleFormats
        {
            get => Options.PossibleFormats;
            set => Options.PossibleFormats = value;
        }

        public bool PureBarcode
        {
            get => Options.PureBarcode;
            set => Options.PureBarcode = value;
        }

        public bool TryHarder
        {
            get => Options.TryHarder;
            set => Options.TryHarder = value;
        }

        /// <summary>The bitmap's pixels as 32-bit BGRA data, the layout ZXing reads with BitmapFormat.BGRA32.</summary>
        private static LuminanceSource ToLuminanceSource(Bitmap bitmap)
        {
            var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4);
                }
                return new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }

    /// <summary>Encodes text as a barcode picture (<see cref="Bitmap"/>).</summary>
    public class BarcodeWriter : BarcodeWriter<Bitmap>
    {
        public BarcodeWriter() => Renderer = new BitmapRenderer();
    }
}

namespace ZXing.Rendering
{
    /// <summary>Paints a barcode matrix: black modules on white, one pixel per matrix cell.</summary>
    public class BitmapRenderer : IBarcodeRenderer<Bitmap>
    {
        public Color Foreground { get; set; } = Color.Black;

        public Color Background { get; set; } = Color.White;

        public Bitmap Render(BitMatrix matrix, BarcodeFormat format, string content) => Render(matrix, format, content, null);

        public Bitmap Render(BitMatrix matrix, BarcodeFormat format, string content, EncodingOptions options)
        {
            var bitmap = new Bitmap(matrix.Width, matrix.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < matrix.Height; y++)
            {
                for (int x = 0; x < matrix.Width; x++)
                {
                    bitmap.SetPixel(x, y, matrix[x, y] ? Foreground : Background);
                }
            }
            return bitmap;
        }
    }
}

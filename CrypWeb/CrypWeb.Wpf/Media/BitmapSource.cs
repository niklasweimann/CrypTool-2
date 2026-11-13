
namespace System.Windows.Media.Imaging
{
    /// <summary>
    /// A raster picture. Its pixels are known when it was made from pixels (<see cref="Create"/>, a crop) or from
    /// encoded data (a stream); a picture loaded by address is only shown by the browser and has no pixels here.
    /// </summary>
    public abstract class BitmapSource : ImageSource
    {
        private double? _width;
        private double? _height;
        private BgraPixels _pixels;
        private bool _pixelsLoaded;

        /// <summary>The pixels (BGRA, decoded once), or null when they are not available.</summary>
        internal BgraPixels Pixels
        {
            get
            {
                if (!_pixelsLoaded)
                {
                    _pixels = LoadPixels();
                    _pixelsLoaded = true;
                }
                return _pixels;
            }
        }

        /// <summary>Supplies the pixels; the default decodes <see cref="EncodedBytes"/>.</summary>
        private protected virtual BgraPixels LoadPixels() => BgraPixels.Decode(EncodedBytes);

        /// <summary>The picture as an encoded file (PNG, JPEG, ...), when it was loaded from one.</summary>
        internal virtual byte[] EncodedBytes => null;

        public virtual int PixelWidth => Pixels?.Width ?? (int)(_width ?? 0);

        public virtual int PixelHeight => Pixels?.Height ?? (int)(_height ?? 0);

        /// <summary>Size in device-independent units; at 96 dpi the pixel size unless set explicitly.</summary>
        public override double Width
        {
            get => _width ?? PixelWidth;
            set => _width = value;
        }

        public override double Height
        {
            get => _height ?? PixelHeight;
            set => _height = value;
        }

        public double DpiX => 96;

        public double DpiY => 96;

        /// <summary>The layout of <see cref="CopyPixels(Array, int, int)"/>; decoded pictures are 32-bit BGRA.</summary>
        public virtual PixelFormat Format => PixelFormats.Bgra32;

        public BitmapPalette Palette => null;

        public void CopyPixels(Array pixels, int stride, int offset) =>
            CopyPixels(new Int32Rect(0, 0, PixelWidth, PixelHeight), pixels, stride, offset);

        /// <summary>Copies an area of the pixels into an array, in <see cref="Format"/>.</summary>
        public void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset)
        {
            if (Pixels == null)
            {
                return;     // nothing known about the picture: the array stays as it is
            }
            if (sourceRect.Width == 0 && sourceRect.Height == 0)
            {
                sourceRect = new Int32Rect(0, 0, PixelWidth, PixelHeight);
            }
            byte[] bytes = Pixels.Export(sourceRect, Format, stride);
            Buffer.BlockCopy(bytes, 0, pixels, offset, Math.Min(bytes.Length, Buffer.ByteLength(pixels) - offset));
        }

        public static BitmapSource Create(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat,
            BitmapPalette palette, Array pixels, int stride)
        {
            var raw = new byte[Buffer.ByteLength(pixels)];
            Buffer.BlockCopy(pixels, 0, raw, 0, raw.Length);
            return new PixelBitmap(BgraPixels.Import(raw, pixelWidth, pixelHeight, pixelFormat, palette, stride), pixelFormat);
        }

        public static BitmapSource Create(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat,
            BitmapPalette palette, IntPtr buffer, int bufferSize, int stride)
        {
            var raw = new byte[bufferSize];
            Runtime.InteropServices.Marshal.Copy(buffer, raw, 0, bufferSize);
            return Create(pixelWidth, pixelHeight, dpiX, dpiY, pixelFormat, palette, raw, stride);
        }

        public new bool IsFrozen => false;

        public new BitmapSource Clone() => this;

        public new void Freeze() { }
    }
}

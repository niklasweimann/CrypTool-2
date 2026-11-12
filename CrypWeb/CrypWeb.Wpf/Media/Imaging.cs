using System.IO;

namespace System.Windows.Media
{
    /// <summary>A picture that an <c>Image</c> element or an <c>ImageBrush</c> shows; drawn through its URL.</summary>
    public abstract class ImageSource : Animation.Animatable
    {
        public virtual double Width { get; set; }
        public virtual double Height { get; set; }

        /// <summary>
        /// Address of the picture as written by the plugin ("images/x.png", "/Asm;component/x.png", a pack URI) or a
        /// <c>data:</c> URL; null when there is nothing to show. The renderer maps resource paths to web paths.
        /// </summary>
        internal abstract string Address { get; }
    }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCacheOption { Default, OnDemand = 0, OnLoad, None }

    [Flags]
    public enum BitmapCreateOptions { None = 0, PreservePixelFormat = 1, DelayCreation = 2, IgnoreColorProfile = 4, IgnoreImageCache = 8 }

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

        public bool IsFrozen => false;

        public BitmapSource Clone() => this;

        public void Freeze() { }
    }

    /// <summary>A picture made from pixels; shown as a PNG.</summary>
    internal sealed class PixelBitmap : BitmapSource
    {
        private readonly BgraPixels _pixels;
        private readonly PixelFormat _format;
        private string _dataUrl;

        internal PixelBitmap(BgraPixels pixels, PixelFormat format)
        {
            _pixels = pixels;
            _format = format;
        }

        private protected override BgraPixels LoadPixels() => _pixels;

        public override PixelFormat Format => _format;

        internal override string Address => _dataUrl ??= DataUrl.FromBytes(PngWriter.Encode(_pixels));
    }

    /// <summary>A rectangular part of another picture.</summary>
    public class CroppedBitmap : BitmapSource
    {
        private string _dataUrl;

        public CroppedBitmap() { }

        public CroppedBitmap(BitmapSource source, Int32Rect sourceRect)
        {
            Source = source;
            SourceRect = sourceRect;
        }

        public BitmapSource Source { get; set; }

        public Int32Rect SourceRect { get; set; }

        private protected override BgraPixels LoadPixels() => Source?.Pixels?.Crop(SourceRect);

        internal override string Address => Pixels == null ? Source?.Address : _dataUrl ??= DataUrl.FromBytes(PngWriter.Encode(Pixels));
    }

    /// <summary>A picture from a resource address or from a stream (which becomes a <c>data:</c> URL).</summary>
    public class BitmapImage : BitmapSource
    {
        private byte[] _encoded;
        private string _dataUrl;
        private Stream _streamSource;

        public BitmapImage() { }

        public BitmapImage(Uri uriSource) => UriSource = uriSource;

        public Uri UriSource { get; set; }

        public Stream StreamSource
        {
            get => _streamSource;
            set
            {
                _streamSource = value;
                _encoded = value == null ? null : DataUrl.ReadAll(value);
                _dataUrl = _encoded == null ? null : DataUrl.FromBytes(_encoded);
                RenderVersion.Bump();
            }
        }

        public BitmapCacheOption CacheOption { get; set; }
        public BitmapCreateOptions CreateOptions { get; set; }
        public int DecodePixelWidth { get; set; }
        public int DecodePixelHeight { get; set; }

        public void BeginInit() { }

        public void EndInit() { }

        internal override byte[] EncodedBytes => _encoded;

        internal override string Address => _dataUrl ?? UriSource?.OriginalString;

        public override string ToString() => UriSource?.OriginalString ?? "";
    }

    /// <summary>One picture of an encoded file, or a picture prepared for an encoder.</summary>
    public class BitmapFrame : BitmapSource
    {
        private string _address;
        private byte[] _encoded;
        private BitmapSource _source;

        public static BitmapFrame Create(Uri uri) => new BitmapFrame { _address = uri?.OriginalString };

        public static BitmapFrame Create(Stream stream)
        {
            byte[] encoded = DataUrl.ReadAll(stream);
            return new BitmapFrame { _encoded = encoded, _address = DataUrl.FromBytes(encoded) };
        }

        public static BitmapFrame Create(Stream stream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption) => Create(stream);

        public static BitmapFrame Create(BitmapSource source) => new BitmapFrame { _source = source };

        internal override byte[] EncodedBytes => _encoded;

        private protected override BgraPixels LoadPixels() => _source != null ? _source.Pixels : base.LoadPixels();

        public override PixelFormat Format => _source?.Format ?? base.Format;

        internal override string Address => _source?.Address ?? _address;
    }

    internal static class DataUrl
    {
        public static string FromStream(Stream stream) => FromBytes(ReadAll(stream));

        /// <summary>The whole content of a stream (from its start, when it can seek).</summary>
        public static byte[] ReadAll(Stream stream)
        {
            using var buffer = new MemoryStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>
        /// A data URL for an encoded picture. BMP files (uncompressed) are shown as PNG, which is far smaller to
        /// send to the page; the plugin's own bytes are not changed.
        /// </summary>
        public static string FromBytes(byte[] bytes)
        {
            if (MimeType(bytes) == "image/bmp" && BgraPixels.Decode(bytes) is { } pixels)
            {
                bytes = PngWriter.Encode(pixels);
            }
            return "data:" + MimeType(bytes) + ";base64," + Convert.ToBase64String(bytes);
        }

        private static string MimeType(byte[] bytes)
        {
            if (bytes.Length > 3 && bytes[0] == 0x89 && bytes[1] == 'P')
            {
                return "image/png";
            }
            if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return "image/jpeg";
            }
            if (bytes.Length > 2 && bytes[0] == 'G' && bytes[1] == 'I')
            {
                return "image/gif";
            }
            if (bytes.Length > 1 && bytes[0] == 'B' && bytes[1] == 'M')
            {
                return "image/bmp";
            }
            return "image/png";
        }
    }
}

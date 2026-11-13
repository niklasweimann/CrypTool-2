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

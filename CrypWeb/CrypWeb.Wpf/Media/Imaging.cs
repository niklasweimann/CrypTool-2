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

    public abstract class BitmapSource : ImageSource
    {
        public virtual int PixelWidth => (int)Width;
        public virtual int PixelHeight => (int)Height;
        public double DpiX => 96;
        public double DpiY => 96;
    }

    /// <summary>A picture from a resource address or from a stream (which becomes a <c>data:</c> URL).</summary>
    public class BitmapImage : BitmapSource
    {
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
                _dataUrl = value == null ? null : DataUrl.FromStream(value);
                RenderVersion.Bump();
            }
        }

        public BitmapCacheOption CacheOption { get; set; }
        public BitmapCreateOptions CreateOptions { get; set; }
        public int DecodePixelWidth { get; set; }
        public int DecodePixelHeight { get; set; }

        public void BeginInit() { }

        public void EndInit() { }

        internal override string Address => _dataUrl ?? UriSource?.OriginalString;

        public override string ToString() => UriSource?.OriginalString ?? "";
    }

    public class BitmapFrame : BitmapSource
    {
        private string _address;

        public static BitmapFrame Create(Uri uri) => new BitmapFrame { _address = uri?.OriginalString };

        public static BitmapFrame Create(Stream stream) => new BitmapFrame { _address = DataUrl.FromStream(stream) };

        public static BitmapFrame Create(Stream stream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption) => Create(stream);

        public static BitmapFrame Create(BitmapSource source) => new BitmapFrame { _address = source?.Address };

        internal override string Address => _address;
    }

    internal static class DataUrl
    {
        public static string FromStream(Stream stream)
        {
            using var buffer = new MemoryStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            stream.CopyTo(buffer);
            return FromBytes(buffer.ToArray());
        }

        public static string FromBytes(byte[] bytes)
        {
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

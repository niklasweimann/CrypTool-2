// System.Drawing for the browser build.
//
// The real System.Drawing.Common only works on Windows. This is a managed implementation of the part of its API that
// CT2's plugins use (Bitmap, Graphics, Pen, Brush, Font, Color helpers, ...) on a 32 bit BGRA pixel buffer; PNG, JPEG,
// GIF and BMP are decoded and encoded by StbImageSharp / StbImageWriteSharp.
//
// Text is laid out (measured) but not drawn: the plugins only need the measurements.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace System.Drawing
{
    public abstract class Image : IDisposable, ICloneable
    {
        public abstract int Width { get; }
        public abstract int Height { get; }
        public Size Size => new Size(Width, Height);
        public SizeF PhysicalDimension => new SizeF(Width, Height);
        public ImageFormat RawFormat { get; protected set; } = ImageFormat.Png;
        public PixelFormat PixelFormat { get; protected set; } = PixelFormat.Format32bppArgb;

        /// <summary>Bits per pixel of a format (bits 8-15 of its value, as in System.Drawing).</summary>
        public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;
        public float HorizontalResolution { get; protected set; } = 96f;
        public float VerticalResolution { get; protected set; } = 96f;
        public object Tag { get; set; }
        public abstract void Save(Stream stream, ImageFormat format);
        public void Save(Stream stream) => Save(stream, ImageFormat.Png);

        /// <summary>Saves with an encoder chosen by <see cref="ImageCodecInfo"/> (its parameters, such as the JPEG quality, are not used).</summary>
        public void Save(Stream stream,
            ImageCodecInfo encoder,
            EncoderParameters encoderParams) => Save(stream, encoder?.Format ?? ImageFormat.Png);

        public void Save(string filename) => Save(filename, ImageFormat.FromExtension(Path.GetExtension(filename)));

        public void Save(string filename, ImageFormat format)
        {
            using var fs = File.Create(filename);
            Save(fs, format);
        }

        public abstract object Clone();
        public virtual void Dispose() { }
        public static Image FromStream(Stream stream) => new Bitmap(stream);
        public static Image FromFile(string filename) => new Bitmap(filename);
        public void RotateFlip(RotateFlipType t) => ((Bitmap)this).RotateFlipCore(t);

        public void SetResolution(float x, float y)
        {
            HorizontalResolution = x;
            VerticalResolution = y;
        }

        public Image GetThumbnailImage(int w, int h, GetThumbnailImageAbort cb, IntPtr data)
        {
            var b = new Bitmap((Bitmap)this);
            return Bitmap.Resize(b, w, h);
        }

        public delegate bool GetThumbnailImageAbort();
    }

    public enum RotateFlipType
    {
        RotateNoneFlipNone = 0,
        Rotate90FlipNone = 1,
        Rotate180FlipNone = 2,
        Rotate270FlipNone = 3,
        RotateNoneFlipX = 4,
        Rotate90FlipX = 5,
        Rotate180FlipX = 6,
        Rotate270FlipX = 7,
        RotateNoneFlipY = Rotate180FlipX,
        Rotate90FlipY = Rotate270FlipX,
        Rotate180FlipY = RotateNoneFlipX,
        Rotate270FlipY = Rotate90FlipX
    }

    public abstract class Brush : IDisposable, ICloneable {
    public virtual void Dispose() { }
    public virtual object Clone() => this; }
    public sealed class SolidBrush : Brush {
    public Color Color { get; set; }

    public SolidBrush(Color c)
    {
        Color = c;
    } }
    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black);
        public static Brush White => new SolidBrush(Color.White);
        public static Brush Red => new SolidBrush(Color.Red);
        public static Brush Green => new SolidBrush(Color.Green);
        public static Brush Blue => new SolidBrush(Color.Blue);
        public static Brush Gray => new SolidBrush(Color.Gray);
        public static Brush LightGray => new SolidBrush(Color.LightGray);
        public static Brush Yellow => new SolidBrush(Color.Yellow);
        public static Brush Transparent => new SolidBrush(Color.Transparent);
    }
    public sealed class Pen : IDisposable
    {
        public Color Color { get; set; }
        public float Width { get; set; } = 1;
        public DashStyle DashStyle { get; set; }

        public Pen(Color c)
        {
            Color = c;
        }

        public Pen(Color c, float w)
        {
            Color = c;
            Width = w;
        }

        public Pen(Brush b)
        {
            Color = b is SolidBrush s ? s.Color : Color.Black;
        }

        public Pen(Brush b, float w) : this(b)
        {
            Width = w;
        }

        public void Dispose() { }
    }
    public static class Pens {
    public static Pen Black => new Pen(Color.Black);
    public static Pen White => new Pen(Color.White);
    public static Pen Red => new Pen(Color.Red);
    public static Pen Gray => new Pen(Color.Gray);
    public static Pen Blue => new Pen(Color.Blue);
    public static Pen Green => new Pen(Color.Green); }

    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum GraphicsUnit { World, Display, Pixel, Point, Inch, Document, Millimeter }
    public sealed class FontFamily : IDisposable {
    public string Name { get; }

    public FontFamily(string name)
    {
        Name = name;
    }

    public void Dispose() { }
    public static FontFamily GenericSansSerif => new FontFamily("Sans");
    public static FontFamily GenericMonospace => new FontFamily("Mono");
    public static FontFamily GenericSerif => new FontFamily("Serif"); }
    public sealed class Font : IDisposable
    {
        public string Name { get; }
        public float Size { get; }
        public FontStyle Style { get; }
        public FontFamily FontFamily => new FontFamily(Name);
        public float SizeInPoints => Size;
        public int Height => (int)(Size * 1.3f);
        public Font(string name, float size) : this(name, size, FontStyle.Regular) { }

        public Font(string name, float size, FontStyle style)
        {
            Name = name;
            Size = size;
            Style = style;
        }

        public Font(string name, float size, FontStyle style, GraphicsUnit unit) : this(name, size, style) { }
        public Font(FontFamily f, float size) : this(f.Name, size) { }
        public Font(FontFamily f, float size, FontStyle style) : this(f.Name, size, style) { }
        public void Dispose() { }
    }
    public sealed class StringFormat : IDisposable {
    public StringAlignment Alignment { get; set; }
    public StringAlignment LineAlignment { get; set; }
    public StringFormatFlags FormatFlags { get; set; }
    public void Dispose() { }
    public static StringFormat GenericTypographic => new StringFormat();
    public static StringFormat GenericDefault => new StringFormat(); }
    public enum StringAlignment { Near, Center, Far }
    [Flags] public enum StringFormatFlags
    {
        DirectionRightToLeft = 1,
        DirectionVertical = 2,
        FitBlackBox = 4,
        NoWrap = 4096,
        NoClip = 16384
    }
    public sealed class Icon : IDisposable {
    public Icon(Stream s) { }
    public Icon(string f) { }
    public void Dispose() { }
    public Bitmap ToBitmap() => new Bitmap(16, 16); }
    /// <summary>Image to/from encoded bytes (PNG) like the original ImageConverter.</summary>
    public class ImageConverter : System.ComponentModel.TypeConverter
    {
        public override bool CanConvertFrom(System.ComponentModel.ITypeDescriptorContext c, Type t) => t == typeof(byte[])
            || base.CanConvertFrom(c, t);

        public override bool CanConvertTo(System.ComponentModel.ITypeDescriptorContext c, Type t) => t == typeof(byte[])
            || base.CanConvertTo(c, t);

        public override object ConvertFrom(System.ComponentModel.ITypeDescriptorContext c,
            System.Globalization.CultureInfo ci,
            object value) =>
            value is byte[] b ? new Bitmap(new MemoryStream(b)) : base.ConvertFrom(c, ci, value);

        public override object ConvertTo(System.ComponentModel.ITypeDescriptorContext c,
            System.Globalization.CultureInfo ci,
            object value,
            Type destinationType)
        {
            if (destinationType == typeof(byte[]) && value is Image img)
            {
                var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            return base.ConvertTo(c, ci, value, destinationType);
        }
    }

    public static class ColorTranslator
    {
        public static Color FromHtml(string h)
        {
            if (h.StartsWith("#"))
            {
                h = h[1..];
                if (h.Length == 6) h = "FF" + h;
                return Color.FromArgb(Convert.ToInt32(h, 16));
            }
            return Color.FromName(h);
        }

        public static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        public static int ToWin32(Color c) => c.R | (c.G << 8) | (c.B << 16);
        public static Color FromWin32(int v) => Color.FromArgb(v & 255, (v >> 8) & 255, (v >> 16) & 255);
    }
    public static class SystemColors {
    public static Color Control => Color.FromArgb(240, 240, 240);
    public static Color ControlText => Color.Black;
    public static Color Window => Color.White;
    public static Color WindowText => Color.Black;
    public static Color Highlight => Color.FromArgb(51, 153, 255); }
}

namespace System.Drawing.Imaging
{
    /// <summary>An image encoder or decoder, as listed by <see cref="GetImageEncoders"/>; one per format this System.Drawing writes.</summary>
    public sealed class ImageCodecInfo
    {
        internal ImageCodecInfo(ImageFormat format, string mimeType)
        {
            Format = format;
            FormatID = format.Guid;
            MimeType = mimeType;
        }

        internal ImageFormat Format { get; }
        public Guid FormatID { get; }
        public string MimeType { get; }

        private static readonly ImageCodecInfo[] Codecs =
        {
            new ImageCodecInfo(ImageFormat.Png, "image/png"),
            new ImageCodecInfo(ImageFormat.Jpeg, "image/jpeg"),
            new ImageCodecInfo(ImageFormat.Bmp, "image/bmp"),
        };

        public static ImageCodecInfo[] GetImageEncoders() => (ImageCodecInfo[])Codecs.Clone();

        public static ImageCodecInfo[] GetImageDecoders() => (ImageCodecInfo[])Codecs.Clone();
    }

    public sealed class Encoder
    {
        public Encoder(Guid guid) => Guid = guid;

        public Guid Guid { get; }

        public static readonly Encoder Quality = new Encoder(new Guid("1d5be4b5-fa4a-452d-9cdd-5db35105e7eb"));
        public static readonly Encoder ColorDepth = new Encoder(new Guid("66087055-ad66-4c7c-9a18-38a2310b8337"));
        public static readonly Encoder Compression = new Encoder(new Guid("e09d739d-ccd4-44ee-8eba-3fbf8be4fc58"));
    }

    public sealed class EncoderParameter : IDisposable
    {
        public EncoderParameter(Encoder encoder, long value)
        {
            Encoder = encoder;
            Value = value;
        }

        public EncoderParameter(Encoder encoder, int value) : this(encoder, (long)value) { }

        public Encoder Encoder { get; }

        internal long Value { get; }

        public void Dispose() { }
    }

    public sealed class EncoderParameters : IDisposable
    {
        public EncoderParameters() : this(1) { }

        public EncoderParameters(int count) => Param = new EncoderParameter[count];

        public EncoderParameter[] Param { get; set; }

        public void Dispose() { }
    }

    public sealed class ImageFormat
    {
        readonly string name;

        ImageFormat(string n)
        {
            name = n;
            Guid = new Guid(System.Text.Encoding.ASCII.GetBytes(n.PadRight(16, '_').Substring(0, 16)));
        }   // a fixed id per format name

        /// <summary>Identifies the format (ImageCodecInfo.FormatID compares with it).</summary>
        public Guid Guid { get; }
        public static readonly ImageFormat Png = new ImageFormat("png"), Jpeg = new ImageFormat("jpeg"), Bmp = new ImageFormat("bmp"), Gif = new ImageFormat("gif"), Tiff = new ImageFormat("tiff"), Icon = new ImageFormat("icon"), MemoryBmp = new ImageFormat("membmp");

        public static ImageFormat FromExtension(string ext) => ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => Jpeg,
            ".bmp" => Bmp,
            ".gif" => Gif,
            _ => Png
        };

        public override string ToString() => name;
    }
    public enum PixelFormat
    {
        DontCare = 0,
        Undefined = 0,
        Format1bppIndexed = 196865,
        Format4bppIndexed = 197634,
        Format8bppIndexed = 198659,
        Format16bppGrayScale = 1052676,
        Format16bppRgb555 = 135173,
        Format16bppRgb565 = 135174,
        Format16bppArgb1555 = 397319,
        Format24bppRgb = 137224,
        Format32bppRgb = 139273,
        Format32bppArgb = 2498570,
        Format32bppPArgb = 925707,
        Format48bppRgb = 1060876,
        Format64bppArgb = 3424269,
        Format64bppPArgb = 1851406,
        Canonical = 2498570
    }
    [Flags] public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }
    public sealed class BitmapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public PixelFormat PixelFormat { get; set; }
        public IntPtr Scan0 { get; set; }
        public int Reserved { get; set; }
        internal GCHandle Handle;
        internal byte[] Buffer;
        internal Rectangle Rect;
    }
    public sealed class ColorPalette {
    public int Flags { get; set; }
    public Color[] Entries { get; } = new Color[256]; }
    public sealed class ColorMatrix {
    public float[][] Matrix { get; }

    public ColorMatrix()
    {
        Matrix = new float[5][];
        for (int i = 0; i < 5; i++) Matrix[i] = new float[5];
    }

    public ColorMatrix(float[][] m)
    {
        Matrix = m;
    }

    public float this[int r, int c] { get => Matrix[r][c]; set => Matrix[r][c] = value; } }
    public sealed class ImageAttributes : IDisposable {
    public void SetColorMatrix(ColorMatrix m) { }
    public void Dispose() { } }
}

namespace System.Drawing.Drawing2D
{
    public enum SmoothingMode { Invalid = -1, Default, HighSpeed, HighQuality, None, AntiAlias }
    public enum InterpolationMode
    {
        Invalid = -1,
        Default,
        Low,
        High,
        Bilinear,
        Bicubic,
        NearestNeighbor,
        HighQualityBilinear,
        HighQualityBicubic
    }
    public enum CompositingQuality { Invalid = -1, Default, HighSpeed, HighQuality, GammaCorrected, AssumeLinear }
    public enum CompositingMode { SourceOver, SourceCopy }
    public enum PixelOffsetMode { Invalid = -1, Default, HighSpeed, HighQuality, None, Half }
    public enum DashStyle { Solid, Dash, Dot, DashDot, DashDotDot, Custom }
    public enum LinearGradientMode { Horizontal, Vertical, ForwardDiagonal, BackwardDiagonal }
    public sealed class LinearGradientBrush : Brush {
    public Color C1, C2;

    public LinearGradientBrush(Rectangle r, Color a, Color b, LinearGradientMode m)
    {
        C1 = a;
        C2 = b;
    }

    public LinearGradientBrush(RectangleF r, Color a, Color b, float angle)
    {
        C1 = a;
        C2 = b;
    }

    public LinearGradientBrush(Point p1, Point p2, Color a, Color b)
    {
        C1 = a;
        C2 = b;
    } }
    public sealed class Matrix : IDisposable {
    public float[] Elements { get; } = { 1, 0, 0, 1, 0, 0 };
    public void Translate(float x, float y) { }
    public void Scale(float x, float y) { }
    public void Rotate(float a) { }
    public void Dispose() { } }
    public sealed class GraphicsPath : IDisposable {
    public void AddRectangle(Rectangle r) { }
    public void AddEllipse(Rectangle r) { }
    public void Dispose() { } }
}

namespace System.Drawing.Text
{
    public abstract class FontCollection : IDisposable {
    public virtual FontFamily[] Families { get; protected set; } = new FontFamily[0];
    public void Dispose() { } }
    public sealed class InstalledFontCollection : FontCollection {
    public InstalledFontCollection()
    {
        Families = new[] { new FontFamily("Arial"),
            new FontFamily("Courier New"),
            new FontFamily("Times New Roman"),
            new FontFamily("Verdana"),
            new FontFamily("Segoe UI") };
    } }
    public enum TextRenderingHint
    {
        SystemDefault,
        SingleBitPerPixelGridFit,
        SingleBitPerPixel,
        AntiAliasGridFit,
        AntiAlias,
        ClearTypeGridFit
    } }
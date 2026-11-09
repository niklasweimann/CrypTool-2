using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using StbImageSharp;
using StbImageWriteSharp;

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
        public float HorizontalResolution { get; protected set; } = 96f;
        public float VerticalResolution { get; protected set; } = 96f;
        public object Tag { get; set; }
        public abstract void Save(Stream stream, ImageFormat format);
        public void Save(Stream stream) => Save(stream, ImageFormat.Png);
        public void Save(string filename) => Save(filename, ImageFormat.FromExtension(Path.GetExtension(filename)));
        public void Save(string filename, ImageFormat format) { using var fs = File.Create(filename); Save(fs, format); }
        public abstract object Clone();
        public virtual void Dispose() { }
        public static Image FromStream(Stream stream) => new Bitmap(stream);
        public static Image FromFile(string filename) => new Bitmap(filename);
        public void RotateFlip(RotateFlipType t) => ((Bitmap)this).RotateFlipCore(t);
        public void SetResolution(float x, float y) { HorizontalResolution = x; VerticalResolution = y; }
        public Image GetThumbnailImage(int w, int h, GetThumbnailImageAbort cb, IntPtr data) { var b = new Bitmap((Bitmap)this); return Bitmap.Resize(b, w, h); }
        public delegate bool GetThumbnailImageAbort();
    }

    public enum RotateFlipType { RotateNoneFlipNone = 0, Rotate90FlipNone = 1, Rotate180FlipNone = 2, Rotate270FlipNone = 3, RotateNoneFlipX = 4, Rotate90FlipX = 5, Rotate180FlipX = 6, Rotate270FlipX = 7, RotateNoneFlipY = Rotate180FlipX, Rotate90FlipY = Rotate270FlipX, Rotate180FlipY = RotateNoneFlipX, Rotate270FlipY = Rotate90FlipX }

    /// <summary>32-bit BGRA pixel buffer (non-premultiplied).</summary>
    public sealed class Bitmap : Image
    {
        internal byte[] Data; int w, h;
        public override int Width => w;
        public override int Height => h;
        public Bitmap(int width, int height) : this(width, height, PixelFormat.Format32bppArgb) { }
        public Bitmap(int width, int height, PixelFormat format) { w = width; h = height; Data = new byte[Math.Max(1, w * h * 4)]; PixelFormat = format; if (!HasAlpha(format)) for (int i = 3; i < Data.Length; i += 4) Data[i] = 255; }
        public Bitmap(int width, int height, Graphics g) : this(width, height) { }
        public Bitmap(Image src) : this(src.Width, src.Height) { Array.Copy(((Bitmap)src).Data, Data, Data.Length); }
        public Bitmap(Image src, int width, int height) : this(Resize(new Bitmap(src), width, height)) { }
        public Bitmap(Image src, Size size) : this(src, size.Width, size.Height) { }
        public Bitmap(Stream stream) { Decode(stream); }
        public Bitmap(Stream stream, bool useIcm) { Decode(stream); }
        public Bitmap(string filename) { using var fs = File.OpenRead(filename); Decode(fs); }
        Bitmap(Bitmap b) { w = b.w; h = b.h; Data = b.Data; PixelFormat = b.PixelFormat; }
        static bool HasAlpha(PixelFormat f) => f == PixelFormat.Format32bppArgb || f == PixelFormat.Format32bppPArgb || f == PixelFormat.Format64bppArgb || f == PixelFormat.Canonical;

        void Decode(Stream s)
        {
            var img = ImageResult.FromStream(s, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            w = img.Width; h = img.Height; Data = img.Data; // RGBA -> BGRA
            for (int i = 0; i < Data.Length; i += 4) (Data[i], Data[i + 2]) = (Data[i + 2], Data[i]);
        }
        public override void Save(Stream stream, ImageFormat format)
        {
            var rgba = new byte[Data.Length]; Array.Copy(Data, rgba, Data.Length);
            for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]); // BGRA -> RGBA
            var wr = new ImageWriter();
            if (format == ImageFormat.Jpeg) wr.WriteJpg(rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream, 90);
            else if (format == ImageFormat.Bmp) wr.WriteBmp(rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
            else wr.WritePng(rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream); // GIF/TIFF/others are written as PNG
        }
        public override object Clone() => new Bitmap((Image)this);
        public Bitmap Clone(Rectangle r, PixelFormat f)
        {
            var b = new Bitmap(r.Width, r.Height, f);
            for (int y = 0; y < r.Height; y++) for (int x = 0; x < r.Width; x++) if (r.X + x >= 0 && r.X + x < w && r.Y + y >= 0 && r.Y + y < h) Array.Copy(Data, ((r.Y + y) * w + r.X + x) * 4, b.Data, (y * r.Width + x) * 4, 4);
            return b;
        }
        public Bitmap Clone(Rectangle r, PixelFormat f, int _) => Clone(r, f);
        internal static Bitmap Resize(Bitmap b, int nw, int nh)
        {
            var r = new Bitmap(nw, nh);
            // bilinear sampling
            for (int y = 0; y < nh; y++)
            {
                double fy = Math.Max(0, (y + .5) * b.h / nh - .5); int y0 = Math.Min(b.h - 1, (int)fy), y1 = Math.Min(b.h - 1, y0 + 1); double ty = fy - y0;
                for (int x = 0; x < nw; x++)
                {
                    double fx = Math.Max(0, (x + .5) * b.w / nw - .5); int x0 = Math.Min(b.w - 1, (int)fx), x1 = Math.Min(b.w - 1, x0 + 1); double tx = fx - x0;
                    for (int c = 0; c < 4; c++)
                    {
                        double top = b.Data[(y0 * b.w + x0) * 4 + c] * (1 - tx) + b.Data[(y0 * b.w + x1) * 4 + c] * tx;
                        double bot = b.Data[(y1 * b.w + x0) * 4 + c] * (1 - tx) + b.Data[(y1 * b.w + x1) * 4 + c] * tx;
                        r.Data[(y * nw + x) * 4 + c] = (byte)Math.Round(top * (1 - ty) + bot * ty);
                    }
                }
            }
            return r;
        }
        internal void RotateFlipCore(RotateFlipType t)
        {
            int rot = (int)t % 4; bool flipX = ((int)t & 4) != 0;
            int nw = rot % 2 == 1 ? h : w, nh = rot % 2 == 1 ? w : h;
            var nd = new byte[nw * nh * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx, dy;
                    switch (rot) { case 1: dx = h - 1 - y; dy = x; break; case 2: dx = w - 1 - x; dy = h - 1 - y; break; case 3: dx = y; dy = w - 1 - x; break; default: dx = x; dy = y; break; }
                    if (flipX) dx = nw - 1 - dx; // flip after rotation, like ImageSharp's Mutate order
                    Array.Copy(Data, (y * w + x) * 4, nd, (dy * nw + dx) * 4, 4);
                }
            w = nw; h = nh; Data = nd;
        }
        public Color GetPixel(int x, int y)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) throw new ArgumentOutOfRangeException();
            int i = (y * w + x) * 4; return Color.FromArgb(Data[i + 3], Data[i + 2], Data[i + 1], Data[i]);
        }
        public void SetPixel(int x, int y, Color c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) throw new ArgumentOutOfRangeException();
            int i = (y * w + x) * 4; Data[i] = c.B; Data[i + 1] = c.G; Data[i + 2] = c.R; Data[i + 3] = c.A;
        }
        public void MakeTransparent() { }
        public void MakeTransparent(Color c) { for (int i = 0; i < Data.Length; i += 4) if (Data[i] == c.B && Data[i + 1] == c.G && Data[i + 2] == c.R) Data[i + 3] = 0; }
        public IntPtr GetHbitmap() => IntPtr.Zero;
        ColorPalette palette;
        public ColorPalette Palette { get { if (palette == null) { palette = new ColorPalette(); for (int i = 0; i < 256; i++) palette.Entries[i] = Color.FromArgb(i, i, i); } return palette; } set { palette = value; } }

        static int Bpp(PixelFormat f) => f switch { PixelFormat.Format24bppRgb => 3, PixelFormat.Format8bppIndexed => 1, PixelFormat.Format16bppRgb565 or PixelFormat.Format16bppRgb555 or PixelFormat.Format16bppGrayScale => 2, _ => 4 };

        public BitmapData LockBits(Rectangle rect, ImageLockMode mode, PixelFormat format)
        {
            int bpp = Bpp(format), stride = (rect.Width * bpp + 3) & ~3;
            var buf = new byte[stride * rect.Height + 4];
            var gc = GCHandle.Alloc(buf, GCHandleType.Pinned);
            for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                {
                    int s = ((rect.Y + y) * w + rect.X + x) * 4, d = y * stride + x * bpp;
                    if (bpp == 4) Array.Copy(Data, s, buf, d, 4);
                    else if (bpp == 3) { buf[d] = Data[s]; buf[d + 1] = Data[s + 1]; buf[d + 2] = Data[s + 2]; }
                    else if (bpp == 1) buf[d] = (byte)((Data[s + 2] * 299 + Data[s + 1] * 587 + Data[s] * 114) / 1000);
                }
            return new BitmapData { Width = rect.Width, Height = rect.Height, Stride = stride, PixelFormat = format, Scan0 = gc.AddrOfPinnedObject(), Reserved = (int)mode, Handle = gc, Buffer = buf, Rect = rect };
        }
        public BitmapData LockBits(Rectangle rect, ImageLockMode mode, PixelFormat format, BitmapData data) => LockBits(rect, mode, format);
        public void UnlockBits(BitmapData d)
        {
            if (d.Buffer != null && ((ImageLockMode)d.Reserved & ImageLockMode.WriteOnly) != 0)
            {
                int bpp = Bpp(d.PixelFormat);
                for (int y = 0; y < d.Rect.Height; y++)
                    for (int x = 0; x < d.Rect.Width; x++)
                    {
                        int s = y * d.Stride + x * bpp, o = ((d.Rect.Y + y) * w + d.Rect.X + x) * 4;
                        if (bpp == 4) Array.Copy(d.Buffer, s, Data, o, 4);
                        else if (bpp == 3) { Data[o] = d.Buffer[s]; Data[o + 1] = d.Buffer[s + 1]; Data[o + 2] = d.Buffer[s + 2]; Data[o + 3] = 255; }
                        else if (bpp == 1) { Data[o] = Data[o + 1] = Data[o + 2] = d.Buffer[s]; Data[o + 3] = 255; }
                    }
            }
            if (d.Handle.IsAllocated) d.Handle.Free();
        }
    }

    public sealed class Graphics : IDisposable
    {
        readonly Bitmap bmp;
        Graphics(Bitmap b) { bmp = b; }
        public static Graphics FromImage(Image image) => new Graphics((Bitmap)image);
        public SmoothingMode SmoothingMode { get; set; }
        public InterpolationMode InterpolationMode { get; set; }
        public CompositingQuality CompositingQuality { get; set; }
        public CompositingMode CompositingMode { get; set; }
        public PixelOffsetMode PixelOffsetMode { get; set; }
        public System.Drawing.Text.TextRenderingHint TextRenderingHint { get; set; }
        public GraphicsUnit PageUnit { get; set; }
        public void Dispose() { }
        public void Flush() { }
        public void Clear(Color c) { for (int i = 0; i < bmp.Data.Length; i += 4) { bmp.Data[i] = c.B; bmp.Data[i + 1] = c.G; bmp.Data[i + 2] = c.R; bmp.Data[i + 3] = c.A; } }
        static Color Of(Brush b) => b is SolidBrush s ? s.Color : Color.Black;
        void Blend(int x, int y, Color c)
        {
            if ((uint)x >= (uint)bmp.Width || (uint)y >= (uint)bmp.Height || c.A == 0) return;
            int i = (y * bmp.Width + x) * 4;
            if (c.A == 255) { bmp.Data[i] = c.B; bmp.Data[i + 1] = c.G; bmp.Data[i + 2] = c.R; bmp.Data[i + 3] = 255; return; }
            double a = c.A / 255.0;
            bmp.Data[i] = (byte)(c.B * a + bmp.Data[i] * (1 - a)); bmp.Data[i + 1] = (byte)(c.G * a + bmp.Data[i + 1] * (1 - a)); bmp.Data[i + 2] = (byte)(c.R * a + bmp.Data[i + 2] * (1 - a)); bmp.Data[i + 3] = (byte)Math.Max(bmp.Data[i + 3], c.A);
        }
        public void FillRectangle(Brush b, Rectangle r) { var c = Of(b); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) Blend(x, y, c); }
        public void FillRectangle(Brush b, RectangleF r) => FillRectangle(b, Rectangle.Round(r));
        public void FillRectangle(Brush b, int x, int y, int w, int h) => FillRectangle(b, new Rectangle(x, y, w, h));
        public void FillRectangle(Brush b, float x, float y, float w, float h) => FillRectangle(b, new Rectangle((int)x, (int)y, (int)w, (int)h));
        public void DrawRectangle(Pen p, Rectangle r) { DrawLine(p, r.Left, r.Top, r.Right, r.Top); DrawLine(p, r.Right, r.Top, r.Right, r.Bottom); DrawLine(p, r.Right, r.Bottom, r.Left, r.Bottom); DrawLine(p, r.Left, r.Bottom, r.Left, r.Top); }
        public void DrawRectangle(Pen p, int x, int y, int w, int h) => DrawRectangle(p, new Rectangle(x, y, w, h));
        public void DrawLine(Pen p, Point a, Point b) => DrawLine(p, a.X, a.Y, b.X, b.Y);
        public void DrawLine(Pen p, PointF a, PointF b) => DrawLine(p, (int)a.X, (int)a.Y, (int)b.X, (int)b.Y);
        public void DrawLine(Pen p, float x1, float y1, float x2, float y2) => DrawLine(p, (int)x1, (int)y1, (int)x2, (int)y2);
        public void DrawLine(Pen p, int x1, int y1, int x2, int y2)
        {
            int dx = Math.Abs(x2 - x1), dy = -Math.Abs(y2 - y1), sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1, err = dx + dy; int wd = Math.Max(1, (int)p.Width);
            while (true)
            {
                for (int a = 0; a < wd; a++) for (int b = 0; b < wd; b++) Blend(x1 + a, y1 + b, p.Color);
                if (x1 == x2 && y1 == y2) break;
                int e2 = 2 * err; if (e2 >= dy) { err += dy; x1 += sx; } if (e2 <= dx) { err += dx; y1 += sy; }
            }
        }
        public void FillEllipse(Brush br, Rectangle r)
        {
            var c = Of(br); double rx = r.Width / 2.0, ry = r.Height / 2.0, cx = r.X + rx, cy = r.Y + ry;
            for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) { double ux = (x + .5 - cx) / rx, uy = (y + .5 - cy) / ry; if (ux * ux + uy * uy <= 1) Blend(x, y, c); }
        }
        public void FillEllipse(Brush br, int x, int y, int w, int h) => FillEllipse(br, new Rectangle(x, y, w, h));
        public void DrawEllipse(Pen p, Rectangle r) { }
        public void DrawEllipse(Pen p, int x, int y, int w, int h) { }
        public void DrawImage(Image img, int x, int y) => DrawImage(img, new Rectangle(x, y, img.Width, img.Height));
        public void DrawImage(Image img, Point p) => DrawImage(img, p.X, p.Y);
        public void DrawImage(Image img, int x, int y, int w, int h) => DrawImage(img, new Rectangle(x, y, w, h));
        public void DrawImage(Image img, Rectangle dest) { var s = (Bitmap)img; DrawImage(img, dest, new Rectangle(0, 0, s.Width, s.Height), GraphicsUnit.Pixel); }
        public void DrawImage(Image img, Rectangle dest, Rectangle src, GraphicsUnit unit)
        {
            var s = (Bitmap)img;
            for (int y = 0; y < dest.Height; y++) for (int x = 0; x < dest.Width; x++)
            {
                int sx = src.X + x * src.Width / Math.Max(1, dest.Width), sy = src.Y + y * src.Height / Math.Max(1, dest.Height);
                if (sx < 0 || sy < 0 || sx >= s.Width || sy >= s.Height) continue;
                int i = (sy * s.Width + sx) * 4; Blend(dest.X + x, dest.Y + y, Color.FromArgb(s.Data[i + 3], s.Data[i + 2], s.Data[i + 1], s.Data[i]));
            }
        }
        public void DrawImage(Image img, int x, int y, Rectangle src, GraphicsUnit unit) => DrawImage(img, new Rectangle(x, y, src.Width, src.Height), src, unit);
        public void DrawImage(Image img, Rectangle dest, int srcX, int srcY, int srcW, int srcH, GraphicsUnit unit) => DrawImage(img, dest, new Rectangle(srcX, srcY, srcW, srcH), unit);
        public void DrawImage(Image img, Rectangle dest, int srcX, int srcY, int srcW, int srcH, GraphicsUnit unit, ImageAttributes attrs) => DrawImage(img, dest, srcX, srcY, srcW, srcH, unit);
        public void DrawImage(Image img, Rectangle dest, float srcX, float srcY, float srcW, float srcH, GraphicsUnit unit) => DrawImage(img, dest, (int)srcX, (int)srcY, (int)srcW, (int)srcH, unit);
        public void DrawImageUnscaled(Image img, int x, int y) => DrawImage(img, x, y);
        // text needs a font rasterizer, which there is none here: text is laid out but not drawn
        public void DrawString(string s, Font f, Brush b, float x, float y) { }
        public void DrawString(string s, Font f, Brush b, PointF p) { }
        public void DrawString(string s, Font f, Brush b, RectangleF r) { }
        public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat sf) { }
        public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat sf) { }
        public void DrawString(string s, Font f, Brush b, PointF p, StringFormat sf) { }
        public SizeF MeasureString(string s, Font f) => new SizeF((s?.Length ?? 0) * f.Size * 0.6f, f.Size * 1.3f);
        public SizeF MeasureString(string s, Font f, int width) => MeasureString(s, f);
        public SizeF MeasureString(string s, Font f, SizeF layout) => MeasureString(s, f);
        public void TranslateTransform(float x, float y) { }
        public void ScaleTransform(float x, float y) { }
        public void RotateTransform(float a) { }
        public void ResetTransform() { }
        public void SetClip(Rectangle r) { }
        public void ResetClip() { }
    }

    public abstract class Brush : IDisposable, ICloneable { public virtual void Dispose() { } public virtual object Clone() => this; }
    public sealed class SolidBrush : Brush { public Color Color { get; set; } public SolidBrush(Color c) { Color = c; } }
    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black); public static Brush White => new SolidBrush(Color.White); public static Brush Red => new SolidBrush(Color.Red);
        public static Brush Green => new SolidBrush(Color.Green); public static Brush Blue => new SolidBrush(Color.Blue); public static Brush Gray => new SolidBrush(Color.Gray);
        public static Brush LightGray => new SolidBrush(Color.LightGray); public static Brush Yellow => new SolidBrush(Color.Yellow); public static Brush Transparent => new SolidBrush(Color.Transparent);
    }
    public sealed class Pen : IDisposable
    {
        public Color Color { get; set; } public float Width { get; set; } = 1; public DashStyle DashStyle { get; set; }
        public Pen(Color c) { Color = c; } public Pen(Color c, float w) { Color = c; Width = w; } public Pen(Brush b) { Color = b is SolidBrush s ? s.Color : Color.Black; } public Pen(Brush b, float w) : this(b) { Width = w; }
        public void Dispose() { }
    }
    public static class Pens { public static Pen Black => new Pen(Color.Black); public static Pen White => new Pen(Color.White); public static Pen Red => new Pen(Color.Red); public static Pen Gray => new Pen(Color.Gray); public static Pen Blue => new Pen(Color.Blue); public static Pen Green => new Pen(Color.Green); }

    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum GraphicsUnit { World, Display, Pixel, Point, Inch, Document, Millimeter }
    public sealed class FontFamily : IDisposable { public string Name { get; } public FontFamily(string name) { Name = name; } public void Dispose() { } public static FontFamily GenericSansSerif => new FontFamily("Sans"); public static FontFamily GenericMonospace => new FontFamily("Mono"); public static FontFamily GenericSerif => new FontFamily("Serif"); }
    public sealed class Font : IDisposable
    {
        public string Name { get; } public float Size { get; } public FontStyle Style { get; } public FontFamily FontFamily => new FontFamily(Name); public float SizeInPoints => Size; public int Height => (int)(Size * 1.3f);
        public Font(string name, float size) : this(name, size, FontStyle.Regular) { } public Font(string name, float size, FontStyle style) { Name = name; Size = size; Style = style; }
        public Font(string name, float size, FontStyle style, GraphicsUnit unit) : this(name, size, style) { } public Font(FontFamily f, float size) : this(f.Name, size) { } public Font(FontFamily f, float size, FontStyle style) : this(f.Name, size, style) { }
        public void Dispose() { }
    }
    public sealed class StringFormat : IDisposable { public StringAlignment Alignment { get; set; } public StringAlignment LineAlignment { get; set; } public StringFormatFlags FormatFlags { get; set; } public void Dispose() { } public static StringFormat GenericTypographic => new StringFormat(); public static StringFormat GenericDefault => new StringFormat(); }
    public enum StringAlignment { Near, Center, Far }
    [Flags] public enum StringFormatFlags { DirectionRightToLeft = 1, DirectionVertical = 2, FitBlackBox = 4, NoWrap = 4096, NoClip = 16384 }
    public sealed class Icon : IDisposable { public Icon(Stream s) { } public Icon(string f) { } public void Dispose() { } public Bitmap ToBitmap() => new Bitmap(16, 16); }
    /// <summary>Image to/from encoded bytes (PNG) like the original ImageConverter.</summary>
    public class ImageConverter : System.ComponentModel.TypeConverter
    {
        public override bool CanConvertFrom(System.ComponentModel.ITypeDescriptorContext c, Type t) => t == typeof(byte[]) || base.CanConvertFrom(c, t);
        public override bool CanConvertTo(System.ComponentModel.ITypeDescriptorContext c, Type t) => t == typeof(byte[]) || base.CanConvertTo(c, t);
        public override object ConvertFrom(System.ComponentModel.ITypeDescriptorContext c, System.Globalization.CultureInfo ci, object value) =>
            value is byte[] b ? new Bitmap(new MemoryStream(b)) : base.ConvertFrom(c, ci, value);
        public override object ConvertTo(System.ComponentModel.ITypeDescriptorContext c, System.Globalization.CultureInfo ci, object value, Type destinationType)
        {
            if (destinationType == typeof(byte[]) && value is Image img) { var ms = new MemoryStream(); img.Save(ms, ImageFormat.Png); return ms.ToArray(); }
            return base.ConvertTo(c, ci, value, destinationType);
        }
    }

    public static class ColorTranslator
    {
        public static Color FromHtml(string h) { if (h.StartsWith("#")) { h = h[1..]; if (h.Length == 6) h = "FF" + h; return Color.FromArgb(Convert.ToInt32(h, 16)); } return Color.FromName(h); }
        public static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        public static int ToWin32(Color c) => c.R | (c.G << 8) | (c.B << 16);
        public static Color FromWin32(int v) => Color.FromArgb(v & 255, (v >> 8) & 255, (v >> 16) & 255);
    }
    public static class SystemColors { public static Color Control => Color.FromArgb(240, 240, 240); public static Color ControlText => Color.Black; public static Color Window => Color.White; public static Color WindowText => Color.Black; public static Color Highlight => Color.FromArgb(51, 153, 255); }
}

namespace System.Drawing.Imaging
{
    public sealed class ImageFormat
    {
        readonly string name; ImageFormat(string n) { name = n; }
        public static readonly ImageFormat Png = new ImageFormat("png"), Jpeg = new ImageFormat("jpeg"), Bmp = new ImageFormat("bmp"), Gif = new ImageFormat("gif"), Tiff = new ImageFormat("tiff"), Icon = new ImageFormat("icon"), MemoryBmp = new ImageFormat("membmp");
        public static ImageFormat FromExtension(string ext) => ext.ToLowerInvariant() switch { ".jpg" or ".jpeg" => Jpeg, ".bmp" => Bmp, ".gif" => Gif, _ => Png };
        public override string ToString() => name;
    }
    public enum PixelFormat { DontCare = 0, Undefined = 0, Format1bppIndexed = 196865, Format4bppIndexed = 197634, Format8bppIndexed = 198659, Format16bppGrayScale = 1052676, Format16bppRgb555 = 135173, Format16bppRgb565 = 135174, Format16bppArgb1555 = 397319, Format24bppRgb = 137224, Format32bppRgb = 139273, Format32bppArgb = 2498570, Format32bppPArgb = 925707, Format48bppRgb = 1060876, Format64bppArgb = 3424269, Format64bppPArgb = 1851406, Canonical = 2498570 }
    [Flags] public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }
    public sealed class BitmapData
    {
        public int Width { get; set; } public int Height { get; set; } public int Stride { get; set; } public PixelFormat PixelFormat { get; set; } public IntPtr Scan0 { get; set; } public int Reserved { get; set; }
        internal GCHandle Handle; internal byte[] Buffer; internal Rectangle Rect;
    }
    public sealed class ColorPalette { public int Flags { get; set; } public Color[] Entries { get; } = new Color[256]; }
    public sealed class ColorMatrix { public float[][] Matrix { get; } public ColorMatrix() { Matrix = new float[5][]; for (int i = 0; i < 5; i++) Matrix[i] = new float[5]; } public ColorMatrix(float[][] m) { Matrix = m; } public float this[int r, int c] { get => Matrix[r][c]; set => Matrix[r][c] = value; } }
    public sealed class ImageAttributes : IDisposable { public void SetColorMatrix(ColorMatrix m) { } public void Dispose() { } }
}

namespace System.Drawing.Drawing2D
{
    public enum SmoothingMode { Invalid = -1, Default, HighSpeed, HighQuality, None, AntiAlias }
    public enum InterpolationMode { Invalid = -1, Default, Low, High, Bilinear, Bicubic, NearestNeighbor, HighQualityBilinear, HighQualityBicubic }
    public enum CompositingQuality { Invalid = -1, Default, HighSpeed, HighQuality, GammaCorrected, AssumeLinear }
    public enum CompositingMode { SourceOver, SourceCopy }
    public enum PixelOffsetMode { Invalid = -1, Default, HighSpeed, HighQuality, None, Half }
    public enum DashStyle { Solid, Dash, Dot, DashDot, DashDotDot, Custom }
    public enum LinearGradientMode { Horizontal, Vertical, ForwardDiagonal, BackwardDiagonal }
    public sealed class LinearGradientBrush : Brush { public Color C1, C2; public LinearGradientBrush(Rectangle r, Color a, Color b, LinearGradientMode m) { C1 = a; C2 = b; } public LinearGradientBrush(RectangleF r, Color a, Color b, float angle) { C1 = a; C2 = b; } public LinearGradientBrush(Point p1, Point p2, Color a, Color b) { C1 = a; C2 = b; } }
    public sealed class Matrix : IDisposable { public float[] Elements { get; } = { 1, 0, 0, 1, 0, 0 }; public void Translate(float x, float y) { } public void Scale(float x, float y) { } public void Rotate(float a) { } public void Dispose() { } }
    public sealed class GraphicsPath : IDisposable { public void AddRectangle(Rectangle r) { } public void AddEllipse(Rectangle r) { } public void Dispose() { } }
}

namespace System.Drawing.Text
{
    public abstract class FontCollection : IDisposable { public virtual FontFamily[] Families { get; protected set; } = new FontFamily[0]; public void Dispose() { } }
    public sealed class InstalledFontCollection : FontCollection { public InstalledFontCollection() { Families = new[] { new FontFamily("Arial"), new FontFamily("Courier New"), new FontFamily("Times New Roman"), new FontFamily("Verdana"), new FontFamily("Segoe UI") }; } }
    public enum TextRenderingHint { SystemDefault, SingleBitPerPixelGridFit, SingleBitPerPixel, AntiAliasGridFit, AntiAlias, ClearTypeGridFit } }
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using StbImageSharp;
using StbImageWriteSharp;

namespace System.Drawing
{
    /// <summary>32-bit BGRA pixel buffer (non-premultiplied).</summary>
    public sealed class Bitmap : Image
    {
        internal byte[] Data;
        int w, h;
        public override int Width => w;
        public override int Height => h;
        public Bitmap(int width, int height) : this(width, height, PixelFormat.Format32bppArgb) { }

        public Bitmap(int width, int height, PixelFormat format)
        {
            w = width;
            h = height;
            Data = new byte[Math.Max(1, w * h * 4)];
            PixelFormat = format;
            if (!HasAlpha(format)) for (int i = 3; i < Data.Length; i += 4) Data[i] = 255;
        }

        public Bitmap(int width, int height, Graphics g) : this(width, height) { }

        public Bitmap(Image src) : this(src.Width, src.Height)
        {
            Array.Copy(((Bitmap)src).Data, Data, Data.Length);
        }

        public Bitmap(Image src, int width, int height) : this(Resize(new Bitmap(src), width, height)) { }
        public Bitmap(Image src, Size size) : this(src, size.Width, size.Height) { }

        public Bitmap(Stream stream)
        {
            Decode(stream);
        }

        public Bitmap(Stream stream, bool useIcm)
        {
            Decode(stream);
        }

        public Bitmap(string filename)
        {
            using var fs = File.OpenRead(filename);
            Decode(fs);
        }

        Bitmap(Bitmap b)
        {
            w = b.w;
            h = b.h;
            Data = b.Data;
            PixelFormat = b.PixelFormat;
        }

        static bool HasAlpha(PixelFormat f) => f == PixelFormat.Format32bppArgb
            || f == PixelFormat.Format32bppPArgb
            || f == PixelFormat.Format64bppArgb
            || f == PixelFormat.Canonical;

        void Decode(Stream s)
        {
            var img = ImageResult.FromStream(s, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            w = img.Width;
            h = img.Height;
            Data = img.Data; // RGBA -> BGRA
            for (int i = 0; i < Data.Length; i += 4) (Data[i], Data[i + 2]) = (Data[i + 2], Data[i]);
        }

        public override void Save(Stream stream, ImageFormat format)
        {
            var rgba = new byte[Data.Length];
            Array.Copy(Data, rgba, Data.Length);
            for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]); // BGRA -> RGBA
            var wr = new ImageWriter();
            if (format == ImageFormat.Jpeg) wr.WriteJpg(rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream, 90);
            else if (format == ImageFormat.Bmp) wr.WriteBmp(rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
            else wr.WritePng(rgba,
            w,
            h,
            StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha,
            stream); // GIF/TIFF/others are written as PNG
        }

        public override object Clone() => new Bitmap((Image)this);

        public Bitmap Clone(Rectangle r, PixelFormat f)
        {
            var b = new Bitmap(r.Width, r.Height, f);
            for (int y = 0; y < r.Height; y++) for (int x = 0; x < r.Width; x++) if (r.X + x >= 0
                && r.X + x < w
                && r.Y + y >= 0
                && r.Y + y < h) Array.Copy(Data,
                ((r.Y + y) * w + r.X + x) * 4,
                b.Data,
                (y * r.Width + x) * 4,
                4);
            return b;
        }

        public Bitmap Clone(Rectangle r, PixelFormat f, int _) => Clone(r, f);

        internal static Bitmap Resize(Bitmap b, int nw, int nh)
        {
            var r = new Bitmap(nw, nh);
            // bilinear sampling
            for (int y = 0; y < nh; y++)
            {
                double fy = Math.Max(0, (y + .5) * b.h / nh - .5);
                int y0 = Math.Min(b.h - 1, (int)fy), y1 = Math.Min(b.h - 1, y0 + 1);
                double ty = fy - y0;
                for (int x = 0; x < nw; x++)
                {
                    double fx = Math.Max(0, (x + .5) * b.w / nw - .5);
                    int x0 = Math.Min(b.w - 1, (int)fx), x1 = Math.Min(b.w - 1, x0 + 1);
                    double tx = fx - x0;
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
            int rot = (int)t % 4;
            bool flipX = ((int)t & 4) != 0;
            int nw = rot % 2 == 1 ? h : w, nh = rot % 2 == 1 ? w : h;
            var nd = new byte[nw * nh * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx, dy;
                    switch (rot) { case 1:
                        dx = h - 1 - y;
                        dy = x;
                        break; case 2:
                        dx = w - 1 - x;
                        dy = h - 1 - y;
                        break; case 3:
                        dx = y;
                        dy = w - 1 - x;
                        break; default:
                        dx = x;
                        dy = y;
                        break; }
                    if (flipX) dx = nw - 1 - dx; // flip after rotation, like ImageSharp's Mutate order
                    Array.Copy(Data, (y * w + x) * 4, nd, (dy * nw + dx) * 4, 4);
                }
            w = nw;
            h = nh;
            Data = nd;
        }

        public Color GetPixel(int x, int y)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) throw new ArgumentOutOfRangeException();
            int i = (y * w + x) * 4;
            return Color.FromArgb(Data[i + 3], Data[i + 2], Data[i + 1], Data[i]);
        }

        public void SetPixel(int x, int y, Color c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) throw new ArgumentOutOfRangeException();
            int i = (y * w + x) * 4;
            Data[i] = c.B;
            Data[i + 1] = c.G;
            Data[i + 2] = c.R;
            Data[i + 3] = c.A;
        }

        public void MakeTransparent() { }

        public void MakeTransparent(Color c)
        {
            for (int i = 0; i < Data.Length; i += 4) if (Data[i] == c.B && Data[i + 1] == c.G && Data[i + 2] == c.R) Data[i + 3] = 0;
        }

        public IntPtr GetHbitmap() => IntPtr.Zero;
        ColorPalette palette;

        public ColorPalette Palette { get
        {
            if (palette == null)
            {
                palette = new ColorPalette();
                for (int i = 0; i < 256; i++) palette.Entries[i] = Color.FromArgb(i, i, i);
            }
            return palette;
        } set { palette = value; } }

        static int Bpp(PixelFormat f) => f switch
        {
            PixelFormat.Format24bppRgb => 3,
            PixelFormat.Format8bppIndexed => 1,
            PixelFormat.Format16bppRgb565 or PixelFormat.Format16bppRgb555 or PixelFormat.Format16bppGrayScale => 2,
            _ => 4
        };

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
                    else if (bpp == 3)
                    {
                        buf[d] = Data[s];
                        buf[d + 1] = Data[s + 1];
                        buf[d + 2] = Data[s + 2];
                    }
                    else if (bpp == 1) buf[d] = (byte)((Data[s + 2] * 299 + Data[s + 1] * 587 + Data[s] * 114) / 1000);
                }
            return new BitmapData { Width = rect.Width,
                Height = rect.Height,
                Stride = stride,
                PixelFormat = format,
                Scan0 = gc.AddrOfPinnedObject(),
                Reserved = (int)mode,
                Handle = gc,
                Buffer = buf,
                Rect = rect };
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
                        else if (bpp == 3)
                        {
                            Data[o] = d.Buffer[s];
                            Data[o + 1] = d.Buffer[s + 1];
                            Data[o + 2] = d.Buffer[s + 2];
                            Data[o + 3] = 255;
                        }
                        else if (bpp == 1)
                        {
                            Data[o] = Data[o + 1] = Data[o + 2] = d.Buffer[s];
                            Data[o + 3] = 255;
                        }
                    }
            }
            if (d.Handle.IsAllocated) d.Handle.Free();
        }
    }
}

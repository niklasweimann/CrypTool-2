using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace System.Drawing
{
    public sealed class Graphics : IDisposable
    {
        readonly Bitmap bmp;

        Graphics(Bitmap b)
        {
            bmp = b;
        }

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

        public void Clear(Color c)
        {
            for (int i = 0; i < bmp.Data.Length; i += 4)
            {
                bmp.Data[i] = c.B;
                bmp.Data[i + 1] = c.G;
                bmp.Data[i + 2] = c.R;
                bmp.Data[i + 3] = c.A;
            }
        }

        static Color Of(Brush b) => b is SolidBrush s ? s.Color : Color.Black;

        void Blend(int x, int y, Color c)
        {
            if ((uint)x >= (uint)bmp.Width || (uint)y >= (uint)bmp.Height || c.A == 0) return;
            int i = (y * bmp.Width + x) * 4;
            if (c.A == 255)
            {
                bmp.Data[i] = c.B;
                bmp.Data[i + 1] = c.G;
                bmp.Data[i + 2] = c.R;
                bmp.Data[i + 3] = 255;
                return;
            }
            double a = c.A / 255.0;
            bmp.Data[i] = (byte)(c.B * a + bmp.Data[i] * (1 - a));
            bmp.Data[i + 1] = (byte)(c.G * a + bmp.Data[i + 1] * (1 - a));
            bmp.Data[i + 2] = (byte)(c.R * a + bmp.Data[i + 2] * (1 - a));
            bmp.Data[i + 3] = (byte)Math.Max(bmp.Data[i + 3], c.A);
        }

        public void FillRectangle(Brush b, Rectangle r)
        {
            var c = Of(b);
            for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) Blend(x, y, c);
        }

        public void FillRectangle(Brush b, RectangleF r) => FillRectangle(b, Rectangle.Round(r));
        public void FillRectangle(Brush b, int x, int y, int w, int h) => FillRectangle(b, new Rectangle(x, y, w, h));
        public void FillRectangle(Brush b, float x, float y, float w, float h) => FillRectangle(b, new Rectangle((int)x, (int)y, (int)w, (int)h));

        public void DrawRectangle(Pen p, Rectangle r)
        {
            DrawLine(p, r.Left, r.Top, r.Right, r.Top);
            DrawLine(p, r.Right, r.Top, r.Right, r.Bottom);
            DrawLine(p, r.Right, r.Bottom, r.Left, r.Bottom);
            DrawLine(p, r.Left, r.Bottom, r.Left, r.Top);
        }

        public void DrawRectangle(Pen p, int x, int y, int w, int h) => DrawRectangle(p, new Rectangle(x, y, w, h));
        public void DrawLine(Pen p, Point a, Point b) => DrawLine(p, a.X, a.Y, b.X, b.Y);
        public void DrawLine(Pen p, PointF a, PointF b) => DrawLine(p, (int)a.X, (int)a.Y, (int)b.X, (int)b.Y);
        public void DrawLine(Pen p, float x1, float y1, float x2, float y2) => DrawLine(p, (int)x1, (int)y1, (int)x2, (int)y2);

        public void DrawLine(Pen p, int x1, int y1, int x2, int y2)
        {
            int dx = Math.Abs(x2 - x1), dy = -Math.Abs(y2 - y1), sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1, err = dx + dy;
            int wd = Math.Max(1, (int)p.Width);
            while (true)
            {
                for (int a = 0; a < wd; a++) for (int b = 0; b < wd; b++) Blend(x1 + a, y1 + b, p.Color);
                if (x1 == x2 && y1 == y2) break;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x1 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        public void FillEllipse(Brush br, Rectangle r)
        {
            var c = Of(br);
            double rx = r.Width / 2.0, ry = r.Height / 2.0, cx = r.X + rx, cy = r.Y + ry;
            for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++)
            {
                double ux = (x + .5 - cx) / rx, uy = (y + .5 - cy) / ry;
                if (ux * ux + uy * uy <= 1) Blend(x, y, c);
            }
        }

        public void FillEllipse(Brush br, int x, int y, int w, int h) => FillEllipse(br, new Rectangle(x, y, w, h));
        public void DrawEllipse(Pen p, Rectangle r) { }
        public void DrawEllipse(Pen p, int x, int y, int w, int h) { }
        public void DrawImage(Image img, int x, int y) => DrawImage(img, new Rectangle(x, y, img.Width, img.Height));
        public void DrawImage(Image img, Point p) => DrawImage(img, p.X, p.Y);
        public void DrawImage(Image img, int x, int y, int w, int h) => DrawImage(img, new Rectangle(x, y, w, h));
        public void DrawImage(Image img, RectangleF dest) => DrawImage(img, Rectangle.Round(dest));

        public void DrawImage(Image img, Rectangle dest)
        {
            var s = (Bitmap)img;
            DrawImage(img, dest, new Rectangle(0, 0, s.Width, s.Height), GraphicsUnit.Pixel);
        }

        public void DrawImage(Image img, Rectangle dest, Rectangle src, GraphicsUnit unit)
        {
            var s = (Bitmap)img;
            for (int y = 0; y < dest.Height; y++) for (int x = 0; x < dest.Width; x++)
            {
                int sx = src.X + x * src.Width / Math.Max(1, dest.Width), sy = src.Y + y * src.Height / Math.Max(1, dest.Height);
                if (sx < 0 || sy < 0 || sx >= s.Width || sy >= s.Height) continue;
                int i = (sy * s.Width + sx) * 4;
                Blend(dest.X + x, dest.Y + y, Color.FromArgb(s.Data[i + 3], s.Data[i + 2], s.Data[i + 1], s.Data[i]));
            }
        }

        public void DrawImage(Image img, int x, int y, Rectangle src, GraphicsUnit unit) => DrawImage(img,
            new Rectangle(x, y, src.Width, src.Height),
            src,
            unit);

        public void DrawImage(Image img,
            Rectangle dest,
            int srcX,
            int srcY,
            int srcW,
            int srcH,
            GraphicsUnit unit) => DrawImage(img, dest, new Rectangle(srcX, srcY, srcW, srcH), unit);

        public void DrawImage(Image img,
            Rectangle dest,
            int srcX,
            int srcY,
            int srcW,
            int srcH,
            GraphicsUnit unit,
            ImageAttributes attrs) => DrawImage(img, dest, srcX, srcY, srcW, srcH, unit);

        public void DrawImage(Image img,
            Rectangle dest,
            float srcX,
            float srcY,
            float srcW,
            float srcH,
            GraphicsUnit unit) => DrawImage(img, dest, (int)srcX, (int)srcY, (int)srcW, (int)srcH, unit);

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
}

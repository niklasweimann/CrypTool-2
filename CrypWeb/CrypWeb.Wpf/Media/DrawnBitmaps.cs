// Bitmaps that plugins draw into: RenderTargetBitmap (renders a visual) and WriteableBitmap (a pixel buffer).
//
// WPF rasterizes a visual into pixels. The browser renderer has no rasterizer, but it can show vector drawings as
// SVG, which is also sharper. So these bitmaps keep the drawing that was rendered into them instead of pixels:
//
//   RenderTargetBitmap.Render(drawingVisual)    keeps the visual's drawing commands
//   rtb.CopyPixels(rect, writeable.BackBuffer…)  hands the drawing on to the WriteableBitmap that owns the buffer
//   Image.Source = writeable                     the renderer writes the drawing as an SVG picture
//
// That is the pattern of the ECC curve visualizer. Pixels written straight into BackBuffer are not shown.
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace System.Windows.Media
{
    public struct PixelFormat
    {
        internal PixelFormat(string name, int bitsPerPixel)
        {
            Name = name;
            BitsPerPixel = bitsPerPixel;
        }

        internal string Name { get; }

        public int BitsPerPixel { get; }

        public override string ToString() => Name;
    }

    public static class PixelFormats
    {
        public static PixelFormat Default { get; } = new PixelFormat("Default", 32);
        public static PixelFormat Bgra32 { get; } = new PixelFormat("Bgra32", 32);
        public static PixelFormat Pbgra32 { get; } = new PixelFormat("Pbgra32", 32);
        public static PixelFormat Bgr32 { get; } = new PixelFormat("Bgr32", 32);
        public static PixelFormat Bgr24 { get; } = new PixelFormat("Bgr24", 24);
        public static PixelFormat Rgb24 { get; } = new PixelFormat("Rgb24", 24);
        public static PixelFormat Gray8 { get; } = new PixelFormat("Gray8", 8);
        public static PixelFormat Indexed8 { get; } = new PixelFormat("Indexed8", 8);
    }
}

namespace System.Windows.Media.Imaging
{
    public sealed class BitmapPalette
    {
        public BitmapPalette(IList<Color> colors) => Colors = colors;

        public IList<Color> Colors { get; }
    }

    /// <summary>A bitmap whose content is a recorded drawing (see the file comment).</summary>
    public abstract class DrawnBitmap : BitmapSource
    {
        protected DrawnBitmap(int pixelWidth, int pixelHeight, PixelFormat format)
        {
            DrawnWidth = pixelWidth;
            DrawnHeight = pixelHeight;
            Format = format;
            Width = pixelWidth;
            Height = pixelHeight;
        }

        internal int DrawnWidth { get; }

        internal int DrawnHeight { get; }

        public override int PixelWidth => DrawnWidth;

        public override int PixelHeight => DrawnHeight;

        public PixelFormat Format { get; }

        /// <summary>What the bitmap shows; null while nothing was drawn into it.</summary>
        internal DrawingContext Drawing { get; set; }

        /// <summary>Not used: the renderer writes <see cref="Drawing"/> itself.</summary>
        internal override string Address => null;
    }

    /// <summary>Renders visuals: keeps the drawing of the last rendered <see cref="DrawingVisual"/>.</summary>
    public sealed class RenderTargetBitmap : DrawnBitmap
    {
        public RenderTargetBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat)
            : base(pixelWidth, pixelHeight, pixelFormat) { }

        public void Render(Visual visual)
        {
            if (visual is DrawingVisual drawingVisual && drawingVisual.Drawing != null)
            {
                Drawing = drawingVisual.Drawing;
                RenderVersion.Bump();
            }
        }

        public void Clear()
        {
            Drawing = null;
            RenderVersion.Bump();
        }

        /// <summary>
        /// Copying into the back buffer of a <see cref="WriteableBitmap"/> passes the drawing on to it.
        /// Copying into any other memory leaves that memory unchanged.
        /// </summary>
        public void CopyPixels(Int32Rect sourceRect, IntPtr buffer, int bufferSize, int stride)
        {
            WriteableBitmap target = WriteableBitmap.OwnerOf(buffer);
            if (target != null)
            {
                target.Drawing = Drawing;
            }
        }

        public void CopyPixels(Array pixels, int stride, int offset) { }

        public void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset) { }
    }

    /// <summary>
    /// A bitmap with a pixel buffer. The buffer exists (code may write into it), but the bitmap shows the drawing a
    /// <see cref="RenderTargetBitmap"/> copied into it.
    /// </summary>
    public sealed class WriteableBitmap : DrawnBitmap
    {
        private static readonly Dictionary<IntPtr, WeakReference<WriteableBitmap>> Owners = new Dictionary<IntPtr, WeakReference<WriteableBitmap>>();

        public WriteableBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat, BitmapPalette palette)
            : base(pixelWidth, pixelHeight, pixelFormat)
        {
            BackBufferStride = (pixelWidth * Math.Max(pixelFormat.BitsPerPixel, 8) + 7) / 8;
            BackBuffer = Marshal.AllocHGlobal(BackBufferStride * Math.Max(pixelHeight, 1));
            lock (Owners)
            {
                Owners[BackBuffer] = new WeakReference<WriteableBitmap>(this);
            }
        }

        public WriteableBitmap(BitmapSource source)
            : this(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32, null)
        {
            Drawing = (source as DrawnBitmap)?.Drawing;
        }

        ~WriteableBitmap()
        {
            lock (Owners)
            {
                Owners.Remove(BackBuffer);
            }
            Marshal.FreeHGlobal(BackBuffer);
        }

        public IntPtr BackBuffer { get; }

        public int BackBufferStride { get; }

        internal static WriteableBitmap OwnerOf(IntPtr buffer)
        {
            lock (Owners)
            {
                return Owners.TryGetValue(buffer, out WeakReference<WriteableBitmap> owner) && owner.TryGetTarget(out WriteableBitmap bitmap) ? bitmap : null;
            }
        }

        public void Lock() { }

        public bool TryLock(Duration timeout) => true;

        /// <summary>Unlocking after drawing shows the new content.</summary>
        public void Unlock() => RenderVersion.Bump();

        public void AddDirtyRect(Int32Rect dirtyRect) { }

        public void WritePixels(Int32Rect sourceRect, Array pixels, int stride, int offset) { }

        public void WritePixels(Int32Rect sourceRect, IntPtr buffer, int bufferSize, int stride) { }
    }
}

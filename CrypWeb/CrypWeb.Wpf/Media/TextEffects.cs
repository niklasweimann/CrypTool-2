using System.Collections.ObjectModel;

namespace System.Windows.Media
{
    /// <summary>Formatting of a range of characters of a text block (only the colour is drawn).</summary>
    public class TextEffect : Animation.Animatable
    {
        public Brush Foreground { get; set; }
        public int PositionStart { get; set; }
        public int PositionCount { get; set; }
        public Transform Transform { get; set; }
        public Geometry Clip { get; set; }
    }

    public class TextEffectCollection : Collection<TextEffect>
    {
        /// <summary>WPF exposes its enumerator as a nested struct; code that names the type needs it.</summary>
        public struct Enumerator : System.Collections.Generic.IEnumerator<TextEffect>
        {
            private readonly System.Collections.Generic.IEnumerator<TextEffect> _inner;

            internal Enumerator(System.Collections.Generic.IEnumerator<TextEffect> inner) => _inner = inner;

            public TextEffect Current => _inner.Current;

            object System.Collections.IEnumerator.Current => _inner.Current;

            public bool MoveNext() => _inner.MoveNext();

            public void Reset() => _inner.Reset();

            public void Dispose() => _inner.Dispose();
        }

        public new Enumerator GetEnumerator() => new Enumerator(base.GetEnumerator());

        protected override void InsertItem(int index, TextEffect item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    // ---- drawings: kept as data, not painted --------------------------------------------------------------------

    public abstract class Drawing : Animation.Animatable { }

    public class GeometryDrawing : Drawing
    {
        public GeometryDrawing() { }

        public GeometryDrawing(Brush brush, Pen pen, Geometry geometry)
        {
            Brush = brush;
            Pen = pen;
            Geometry = geometry;
        }

        public Brush Brush { get; set; }
        public Pen Pen { get; set; }
        public Geometry Geometry { get; set; }
    }

    [Markup.ContentProperty(nameof(Children))]
    public class DrawingGroup : Drawing
    {
        public Collection<Drawing> Children { get; } = new Collection<Drawing>();
    }

    public enum TileMode { None, Tile, FlipX, FlipY, FlipXY }

    /// <summary>Paints with a drawing. Not supported in the browser: drawn as nothing.</summary>
    public sealed class DrawingBrush : TileBrush
    {
        public DrawingBrush() { }

        public DrawingBrush(Drawing drawing) => Drawing = drawing;

        public Drawing Drawing { get; set; }
        public TileMode TileMode { get; set; }
        public BrushMappingMode ViewportUnits { get; set; }
        public BrushMappingMode ViewboxUnits { get; set; }
    }

    public class ImageSourceConverter
    {
        public object ConvertFrom(object value)
        {
            switch (value)
            {
                case byte[] bytes: return new Imaging.BitmapImage { StreamSource = new IO.MemoryStream(bytes) };
                case IO.Stream stream: return new Imaging.BitmapImage { StreamSource = stream };
                case string text: return new Imaging.BitmapImage(new Uri(text, UriKind.RelativeOrAbsolute));
                case Uri uri: return new Imaging.BitmapImage(uri);
                default: return null;
            }
        }

        public object ConvertFromString(string text) => ConvertFrom(text);
    }
}

namespace System.Windows
{
    public struct Int32Rect
    {
        public Int32Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public static Int32Rect Empty => default;
    }

    public class FontSizeConverter
    {
        public object ConvertFromString(string text) => Markup.XamlValueConverter.ParseLength(text);

        public object ConvertFrom(object value) => ConvertFromString(Convert.ToString(value, Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Tree queries by name and parent/children (logical and visual tree are the same here).</summary>
    public static class LogicalTreeHelper
    {
        public static DependencyObject FindLogicalNode(DependencyObject logicalTreeNode, string elementName)
        {
            if (logicalTreeNode is FrameworkElement element && element.Name == elementName)
            {
                return element;
            }
            if (logicalTreeNode is UIElement parent)
            {
                foreach (UIElement child in parent.ChildElements)
                {
                    DependencyObject found = FindLogicalNode(child, elementName);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        public static Collections.IEnumerable GetChildren(DependencyObject current)
        {
            return current is UIElement element ? new Collections.Generic.List<UIElement>(element.ChildElements) : new Collections.Generic.List<UIElement>();
        }

        public static DependencyObject GetParent(DependencyObject current) => (current as UIElement)?.ParentElement;
    }
}

namespace System.Windows.Media
{
    public static class VisualTreeHelper
    {
        public static int GetChildrenCount(DependencyObject reference)
        {
            return reference is UIElement element ? new Collections.Generic.List<UIElement>(element.ChildElements).Count : 0;
        }

        public static DependencyObject GetChild(DependencyObject reference, int childIndex)
        {
            return reference is UIElement element ? new Collections.Generic.List<UIElement>(element.ChildElements)[childIndex] : null;
        }

        public static DependencyObject GetParent(DependencyObject reference) => (reference as UIElement)?.ParentElement;

        public static DpiScale GetDpi(Visual visual) => new DpiScale(1, 1);
    }
}

namespace System.Windows.Media.Imaging
{
    public class BitmapSizeOptions
    {
        public static BitmapSizeOptions FromEmptyOptions() => new BitmapSizeOptions();

        public static BitmapSizeOptions FromWidthAndHeight(int pixelWidth, int pixelHeight) => new BitmapSizeOptions();
    }

    /// <summary>Decoders hand the encoded picture to the browser unchanged.</summary>
    public abstract class BitmapDecoder
    {
        protected BitmapDecoder(IO.Stream stream)
        {
            Frames = new Collections.ObjectModel.ReadOnlyCollection<BitmapFrame>(new[] { BitmapFrame.Create(stream) });
        }

        public Collections.ObjectModel.ReadOnlyCollection<BitmapFrame> Frames { get; }

        /// <summary>Any format: the browser decodes the picture.</summary>
        public static BitmapDecoder Create(IO.Stream bitmapStream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption)
        {
            return new PngBitmapDecoder(bitmapStream, createOptions, cacheOption);
        }

        public static BitmapDecoder Create(Uri bitmapUri, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption)
        {
            return new UriDecoder(bitmapUri);
        }

        private sealed class UriDecoder : BitmapDecoder
        {
            public UriDecoder(Uri uri) : base(BitmapFrame.Create(uri)) { }
        }

        private protected BitmapDecoder(BitmapFrame frame)
        {
            Frames = new Collections.ObjectModel.ReadOnlyCollection<BitmapFrame>(new[] { frame });
        }
    }

    public sealed class BmpBitmapDecoder : BitmapDecoder
    {
        public BmpBitmapDecoder(IO.Stream stream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption) : base(stream) { }
    }

    public sealed class PngBitmapDecoder : BitmapDecoder
    {
        public PngBitmapDecoder(IO.Stream stream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption) : base(stream) { }
    }

    public sealed class JpegBitmapDecoder : BitmapDecoder
    {
        public JpegBitmapDecoder(IO.Stream stream, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption) : base(stream) { }
    }
}

namespace System.Windows.Interop
{
    public static class Imaging
    {
        /// <summary>There are no GDI bitmap handles in the browser; the result is an empty picture.</summary>
        public static Media.Imaging.BitmapSource CreateBitmapSourceFromHBitmap(IntPtr bitmap, IntPtr palette, Int32Rect sourceRect, Media.Imaging.BitmapSizeOptions sizeOptions)
        {
            return new Media.Imaging.BitmapImage();
        }
    }
}

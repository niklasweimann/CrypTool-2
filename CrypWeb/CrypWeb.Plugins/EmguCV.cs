// Emgu CV (OpenCV for .NET) for the browser build: the part of its API that CT2's image plugins use
// (ImageProcessor, ImageHash, Transcriptor), implemented in managed code.
//
// Emgu CV wraps the native OpenCV library, which does not exist in the browser. The operations here follow
// OpenCV's definitions (grey conversion weights, Gaussian sigma, saturation of 8-bit values, normalized
// correlation coefficient), so results match the desktop plugins.
//
// An image is kept as double values [row, column, channel]; TColor gives the channels (Bgr: blue, green, red;
// Gray: one), TDepth the value type (byte values are kept within 0..255).
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Emgu.CV.CvEnum
{
    public enum FlipType
    {
        None = 0,
        Vertical = 1,
        Horizontal = 2,
    }

    public enum Inter
    {
        Nearest = 0,
        Linear = 1,
        Cubic = 2,
        Area = 3,
        Lanczos4 = 4,
    }

    /// <summary>Camera properties (only stored in the browser: the browser camera has no such controls).</summary>
    public enum CapProp
    {
        PosMsec = 0,
        FrameWidth = 3,
        FrameHeight = 4,
        Fps = 5,
        Brightness = 10,
        Contrast = 11,
        Saturation = 12,
        Hue = 13,
        Gain = 14,
        Exposure = 15,
        Sharpness = 20,
    }

    public enum TemplateMatchingType
    {
        Sqdiff = 0,
        SqdiffNormed = 1,
        Ccorr = 2,
        CcorrNormed = 3,
        Ccoeff = 4,
        CcoeffNormed = 5,
    }
}

namespace Emgu.CV.Structure
{
    /// <summary>A colour type of an image: how many channels it has and how a colour is written in them.</summary>
    public interface IColor
    {
        int Dimension { get; }

        double[] Values { get; }
    }

    public struct Bgr : IColor
    {
        public Bgr(double blue, double green, double red)
        {
            Blue = blue;
            Green = green;
            Red = red;
        }

        public Bgr(Color color) : this(color.B, color.G, color.R) { }

        public double Blue { get; set; }
        public double Green { get; set; }
        public double Red { get; set; }

        public int Dimension => 3;

        public double[] Values => new[] { Blue, Green, Red };
    }

    public struct Gray : IColor
    {
        public Gray(double intensity) => Intensity = intensity;

        public double Intensity { get; set; }

        public int Dimension => 1;

        public double[] Values => new[] { Intensity };
    }
}

namespace Emgu.CV
{
    /// <summary>An image with colour type <typeparamref name="TColor"/> and value type <typeparamref name="TDepth"/>.</summary>
    public class Image<TColor, TDepth> : IDisposable
        where TColor : struct, IColor
        where TDepth : new()
    {
        private readonly double[,,] _values;

        public Image(int width, int height)
        {
            _values = new double[height, width, Channels];
        }

        /// <summary>The picture of a bitmap, converted to this image's colour type.</summary>
        public Image(Bitmap bitmap) : this(bitmap.Width, bitmap.Height)
        {
            byte[] bgra = ReadBgra(bitmap);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = (y * Width + x) * 4;
                    SetFromBgr(y, x, bgra[i], bgra[i + 1], bgra[i + 2]);
                }
            }
        }

        private Image(double[,,] values) => _values = values;

        private static int Channels => default(TColor).Dimension;

        private static bool IsByte => typeof(TDepth) == typeof(byte);

        public int Width => _values.GetLength(1);

        public int Height => _values.GetLength(0);

        public Size Size => new Size(Width, Height);

        public int NumberOfChannels => Channels;

        /// <summary>The values as an array [row, column, channel] of the value type (a copy).</summary>
        public TDepth[,,] Data
        {
            get
            {
                var data = new TDepth[Height, Width, Channels];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        for (int c = 0; c < Channels; c++)
                        {
                            data[y, x, c] = (TDepth)System.Convert.ChangeType(_values[y, x, c], typeof(TDepth));
                        }
                    }
                }
                return data;
            }
        }

        /// <summary>The image as a bitmap (a copy in this implementation).</summary>
        public Bitmap Bitmap => ToBitmap();

        public Bitmap ToBitmap()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var area = new Rectangle(0, 0, Width, Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[Width * 4];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        byte blue = Saturate(_values[y, x, 0]);
                        byte green = Channels == 3 ? Saturate(_values[y, x, 1]) : blue;
                        byte red = Channels == 3 ? Saturate(_values[y, x, 2]) : blue;
                        row[x * 4] = blue;
                        row[x * 4 + 1] = green;
                        row[x * 4 + 2] = red;
                        row[x * 4 + 3] = 255;
                    }
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        // ---- conversion and copies ---------------------------------------------------------------------------

        public Image<TColor, TDepth> Copy() => new Image<TColor, TDepth>((double[,,])_values.Clone());

        /// <summary>The image in another colour and value type (grey = 0.299 R + 0.587 G + 0.114 B, as OpenCV).</summary>
        public Image<TOtherColor, TOtherDepth> Convert<TOtherColor, TOtherDepth>()
            where TOtherColor : struct, IColor
            where TOtherDepth : new()
        {
            var result = new Image<TOtherColor, TOtherDepth>(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    double blue = _values[y, x, 0];
                    double green = Channels == 3 ? _values[y, x, 1] : blue;
                    double red = Channels == 3 ? _values[y, x, 2] : blue;
                    result.SetFromBgr(y, x, blue, green, red);
                }
            }
            return result;
        }

        private void SetFromBgr(int y, int x, double blue, double green, double red)
        {
            if (Channels == 1)
            {
                _values[y, x, 0] = Store(0.299 * red + 0.587 * green + 0.114 * blue);
            }
            else
            {
                _values[y, x, 0] = Store(blue);
                _values[y, x, 1] = Store(green);
                _values[y, x, 2] = Store(red);
            }
        }

        // ---- geometry ----------------------------------------------------------------------------------------

        public Image<TColor, TDepth> Resize(int width, int height, Inter interpolationType)
        {
            var result = new Image<TColor, TDepth>(width, height);
            double scaleX = (double)Width / width;
            double scaleY = (double)Height / height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < Channels; c++)
                    {
                        result._values[y, x, c] = interpolationType == Inter.Nearest
                            ? _values[Math.Min((int)(y * scaleY), Height - 1), Math.Min((int)(x * scaleX), Width - 1), c]
                            : Store(Bilinear((x + 0.5) * scaleX - 0.5, (y + 0.5) * scaleY - 0.5, c, null));
                    }
                }
            }
            return result;
        }

        /// <summary>Rotates clockwise by <paramref name="angle"/> degrees around the centre; the size stays.</summary>
        public Image<TColor, TDepth> Rotate(double angle, TColor background)
        {
            var result = new Image<TColor, TDepth>(Width, Height);
            double radians = angle * Math.PI / 180;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double centerX = (Width - 1) / 2.0;
            double centerY = (Height - 1) / 2.0;
            double[] fill = background.Values;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // the source point that lands on (x, y)
                    double dx = x - centerX;
                    double dy = y - centerY;
                    double sourceX = cos * dx + sin * dy + centerX;
                    double sourceY = -sin * dx + cos * dy + centerY;
                    for (int c = 0; c < Channels; c++)
                    {
                        result._values[y, x, c] = Store(Bilinear(sourceX, sourceY, c, fill[c]));
                    }
                }
            }
            return result;
        }

        public Image<TColor, TDepth> Flip(FlipType flipType)
        {
            Image<TColor, TDepth> result = Copy();
            result._Flip(flipType);
            return result;
        }

        /// <summary>Mirrors the image in place: Horizontal swaps left and right, Vertical top and bottom.</summary>
        public void _Flip(FlipType flipType)
        {
            double[,,] source = (double[,,])_values.Clone();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int sourceX = flipType.HasFlag(FlipType.Horizontal) ? Width - 1 - x : x;
                    int sourceY = flipType.HasFlag(FlipType.Vertical) ? Height - 1 - y : y;
                    for (int c = 0; c < Channels; c++)
                    {
                        _values[y, x, c] = source[sourceY, sourceX, c];
                    }
                }
            }
        }

        /// <summary>Value at a fractional position; outside the image <paramref name="outside"/> (or the nearest edge).</summary>
        private double Bilinear(double x, double y, int channel, double? outside)
        {
            if (outside.HasValue && (x < -0.5 || y < -0.5 || x > Width - 0.5 || y > Height - 0.5))
            {
                return outside.Value;
            }
            x = Math.Clamp(x, 0, Width - 1);
            y = Math.Clamp(y, 0, Height - 1);
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, Width - 1);
            int y1 = Math.Min(y0 + 1, Height - 1);
            double fx = x - x0;
            double fy = y - y0;
            double top = _values[y0, x0, channel] * (1 - fx) + _values[y0, x1, channel] * fx;
            double bottom = _values[y1, x0, channel] * (1 - fx) + _values[y1, x1, channel] * fx;
            return top * (1 - fy) + bottom * fy;
        }

        // ---- per-pixel operations ----------------------------------------------------------------------------

        public Image<TColor, TDepth> Not() => Map(value => IsByte ? 255 - value : ~(long)value);

        public Image<TColor, TDepth> And(Image<TColor, TDepth> other) => Combine(other, (a, b) => (long)a & (long)b);

        public Image<TColor, TDepth> Or(Image<TColor, TDepth> other) => Combine(other, (a, b) => (long)a | (long)b);

        public Image<TColor, TDepth> Xor(Image<TColor, TDepth> other) => Combine(other, (a, b) => (long)a ^ (long)b);

        public Image<TColor, TDepth> AbsDiff(Image<TColor, TDepth> other) => Combine(other, (a, b) => Math.Abs(a - b));

        public Image<TColor, TDepth> Add(Image<TColor, TDepth> other) => Combine(other, (a, b) => a + b);

        public static Image<TColor, TDepth> operator +(Image<TColor, TDepth> image1, Image<TColor, TDepth> image2) => image1.Add(image2);

        /// <summary>Pixels above the threshold become <paramref name="maxValue"/>, the others 0.</summary>
        public Image<TColor, TDepth> ThresholdBinary(TColor threshold, TColor maxValue)
        {
            double[] limit = threshold.Values;
            double[] high = maxValue.Values;
            var result = new Image<TColor, TDepth>(Width, Height);
            ForEach((y, x, c) => result._values[y, x, c] = _values[y, x, c] > limit[c] ? Store(high[c]) : 0);
            return result;
        }

        /// <summary>value = (value / 255) ^ gamma * 255, in place.</summary>
        public void _GammaCorrect(double gamma)
        {
            ForEach((y, x, c) => _values[y, x, c] = Store(Math.Pow(_values[y, x, c] / 255.0, gamma) * 255.0));
        }

        /// <summary>
        /// Gaussian blur with a square kernel of the given (odd) size, in place. The deviation is OpenCV's default for
        /// the size: 0.3 * ((size - 1) * 0.5 - 1) + 0.8. Edges are reflected (OpenCV's BORDER_REFLECT_101).
        /// </summary>
        public void _SmoothGaussian(int kernelSize)
        {
            if (kernelSize <= 1)
            {
                return;
            }
            int radius = kernelSize / 2;
            double sigma = 0.3 * ((kernelSize - 1) * 0.5 - 1) + 0.8;
            var kernel = new double[2 * radius + 1];
            double sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                kernel[i + radius] = Math.Exp(-(i * i) / (2 * sigma * sigma));
                sum += kernel[i + radius];
            }
            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= sum;
            }

            // separable: rows first, then columns
            var rows = new double[Height, Width, Channels];
            ForEach((y, x, c) =>
            {
                double value = 0;
                for (int i = -radius; i <= radius; i++)
                {
                    value += kernel[i + radius] * _values[y, Reflect(x + i, Width), c];
                }
                rows[y, x, c] = value;
            });
            ForEach((y, x, c) =>
            {
                double value = 0;
                for (int i = -radius; i <= radius; i++)
                {
                    value += kernel[i + radius] * rows[Reflect(y + i, Height), x, c];
                }
                _values[y, x, c] = Store(value);
            });
        }

        private static int Reflect(int index, int length)
        {
            if (length == 1)
            {
                return 0;
            }
            while (index < 0 || index >= length)
            {
                index = index < 0 ? -index : 2 * length - 2 - index;
            }
            return index;
        }

        /// <summary>
        /// Compares the template at every position: the result has one value per position where the template fits
        /// completely ((W - w + 1) x (H - h + 1)). Only the normalized correlation coefficient is implemented.
        /// </summary>
        public Image<Gray, float> MatchTemplate(Image<TColor, TDepth> template, TemplateMatchingType method)
        {
            if (method != TemplateMatchingType.CcoeffNormed)
            {
                throw new NotSupportedException("MatchTemplate: only CcoeffNormed is available in the browser.");
            }
            int resultWidth = Width - template.Width + 1;
            int resultHeight = Height - template.Height + 1;
            if (resultWidth <= 0 || resultHeight <= 0)
            {
                throw new ArgumentException("The template is larger than the image.");
            }
            int count = template.Width * template.Height * Channels;
            double templateMean = 0;
            template.ForEach((y, x, c) => templateMean += template._values[y, x, c]);
            templateMean /= count;
            double templateNorm = 0;
            template.ForEach((y, x, c) => templateNorm += Math.Pow(template._values[y, x, c] - templateMean, 2));

            var result = new Image<Gray, float>(resultWidth, resultHeight);
            for (int top = 0; top < resultHeight; top++)
            {
                for (int left = 0; left < resultWidth; left++)
                {
                    double mean = 0;
                    template.ForEach((y, x, c) => mean += _values[top + y, left + x, c]);
                    mean /= count;
                    double products = 0;
                    double norm = 0;
                    template.ForEach((y, x, c) =>
                    {
                        double value = _values[top + y, left + x, c] - mean;
                        products += value * (template._values[y, x, c] - templateMean);
                        norm += value * value;
                    });
                    double denominator = Math.Sqrt(norm * templateNorm);
                    result.SetValue(top, left, denominator > 1e-12 ? products / denominator : 0);
                }
            }
            return result;
        }

        private void SetValue(int y, int x, double value) => _values[y, x, 0] = value;

        private Image<TColor, TDepth> Map(Func<double, double> operation)
        {
            var result = new Image<TColor, TDepth>(Width, Height);
            ForEach((y, x, c) => result._values[y, x, c] = Store(operation(_values[y, x, c])));
            return result;
        }

        private Image<TColor, TDepth> Combine(Image<TColor, TDepth> other, Func<double, double, double> operation)
        {
            if (other.Width != Width || other.Height != Height)
            {
                throw new ArgumentException("The images have different sizes.");
            }
            var result = new Image<TColor, TDepth>(Width, Height);
            ForEach((y, x, c) => result._values[y, x, c] = Store(operation(_values[y, x, c], other._values[y, x, c])));
            return result;
        }

        private void ForEach(Action<int, int, int> action)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int c = 0; c < Channels; c++)
                    {
                        action(y, x, c);
                    }
                }
            }
        }

        /// <summary>A value as the value type keeps it: 8-bit values are rounded and limited to 0..255.</summary>
        private static double Store(double value) => IsByte ? Saturate(value) : value;

        private static byte Saturate(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

        private static byte[] ReadBgra(Bitmap bitmap)
        {
            var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4);
                }
                return pixels;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        public void Dispose() { }
    }
}

namespace Emgu.CV
{
    using System.Collections.Generic;
    using System.IO;
    using System.Windows.Hosting;

    /// <summary>
    /// A camera (Emgu CV's VideoCapture under its older name). In the browser it is the camera the page opens with
    /// getUserMedia (see System.Windows.Hosting.BrowserDevices); the camera index is not used.
    /// </summary>
    public class Capture : IDisposable
    {
        private readonly Dictionary<CapProp, double> _properties = new Dictionary<CapProp, double>();
        private bool _open;

        public Capture() : this(0) { }

        public Capture(int camIndex)
        {
            BrowserDevices.StartCamera();
            _open = true;
        }

        public void SetCaptureProperty(CapProp property, double value) => _properties[property] = value;

        public double GetCaptureProperty(CapProp property) => _properties.TryGetValue(property, out double value) ? value : 0;

        /// <summary>
        /// The newest picture of the camera. Plugins call this on the UI thread, which also receives the pictures, so
        /// it does not wait: before the first picture arrives the frame is black.
        /// </summary>
        public Mat QueryFrame()
        {
            bool onUiThread = System.Windows.Threading.Dispatcher.CurrentDispatcher?.CheckAccess() == true;
            byte[] picture = BrowserDevices.LatestCameraFrame(onUiThread ? TimeSpan.Zero : TimeSpan.FromSeconds(5));
            if (picture == null)
            {
                return new Mat(new Bitmap(640, 480, PixelFormat.Format24bppRgb));
            }
            using var stream = new MemoryStream(picture);
            return new Mat(new Bitmap(stream));
        }

        public Mat QuerySmallFrame() => QueryFrame();

        public bool Grab() => true;

        public void Dispose()
        {
            if (_open)
            {
                _open = false;
                BrowserDevices.StopCamera();
            }
        }
    }

    /// <summary>A picture (Emgu CV's matrix type), here only as the holder of a bitmap.</summary>
    public class Mat : IDisposable
    {
        internal Mat(Bitmap bitmap) => Bitmap = bitmap;

        public Bitmap Bitmap { get; }

        public int Width => Bitmap.Width;

        public int Height => Bitmap.Height;

        public Image<TColor, TDepth> ToImage<TColor, TDepth>()
            where TColor : struct, IColor
            where TDepth : new() => new Image<TColor, TDepth>(Bitmap);

        public void Dispose() { }
    }
}

namespace DirectShowLib
{
    /// <summary>Device categories of DirectShow (only video input exists in the browser).</summary>
    public static class FilterCategory
    {
        public static readonly Guid VideoInputDevice = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        public static readonly Guid AudioInputDevice = new Guid("33D9A762-90C8-11d0-BD43-00A0C911CE86");
    }

    /// <summary>A capture device: in the browser there is one camera, the one the page may open.</summary>
    public class DsDevice
    {
        private DsDevice(string name) => Name = name;

        public string Name { get; }

        public static DsDevice[] GetDevicesOfCat(Guid filterCategory) =>
            filterCategory == FilterCategory.VideoInputDevice ? new[] { new DsDevice("Browser camera") } : Array.Empty<DsDevice>();
    }
}

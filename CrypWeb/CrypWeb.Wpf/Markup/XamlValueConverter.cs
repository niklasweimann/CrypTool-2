using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace System.Windows.Markup
{
    /// <summary>Turns XAML attribute text into a value of the property type ("1,2,3,4" -> Thickness, "Red" -> Brush, ...).</summary>
    public static class XamlValueConverter
    {
        private static readonly char[] ListSeparators = { ',', ' ' };

        public static bool TryConvert(string text, Type targetType, out object value)
        {
            try
            {
                value = Convert(text, targetType);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        /// <summary>Converts or throws <see cref="FormatException"/>/<see cref="NotSupportedException"/>.</summary>
        public static object Convert(string text, Type targetType)
        {
            Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (type == typeof(string) || type == typeof(object))
            {
                return text;
            }
            string trimmed = text.Trim();

            if (type == typeof(double)) return ParseLength(trimmed);
            if (type == typeof(bool)) return bool.Parse(trimmed);
            if (type == typeof(char)) return trimmed.Length > 0 ? trimmed[0] : '\0';
            if (type == typeof(Input.ModifierKeys)) return ParseModifierKeys(trimmed);
            if (type.IsEnum) return ParseEnum(type, trimmed);
            if (type == typeof(RoutedEvent)) return new RoutedEvent { Name = trimmed };
            if (type == typeof(Input.ICommand)) return Input.CommandLookup.Find(trimmed);
            if (type.IsPrimitive || type == typeof(decimal)) return System.Convert.ChangeType(trimmed, type, CultureInfo.InvariantCulture);

            if (type == typeof(Thickness)) return ParseThickness(trimmed);
            if (type == typeof(CornerRadius)) return ParseCornerRadius(trimmed);
            if (type == typeof(GridLength)) return GridLength.Parse(trimmed);
            if (type == typeof(Controls.DataGridLength)) return Controls.DataGridLength.Parse(trimmed);
            if (type == typeof(Point)) { double[] n = Numbers(trimmed, 2); return new Point(n[0], n[1]); }
            if (type == typeof(Vector)) { double[] n = Numbers(trimmed, 2); return new Vector(n[0], n[1]); }
            if (type == typeof(Size)) { double[] n = Numbers(trimmed, 2); return new Size(n[0], n[1]); }
            if (type == typeof(Rect)) { double[] n = Numbers(trimmed, 4); return new Rect(n[0], n[1], n[2], n[3]); }
            if (type == typeof(Matrix)) { double[] n = Numbers(trimmed, 6); return new Matrix(n[0], n[1], n[2], n[3], n[4], n[5]); }

            if (type == typeof(Color)) return Color.TryParse(trimmed, out Color color) ? color : throw new FormatException("Unknown colour '" + trimmed + "'.");
            if (type == typeof(Brush) || type == typeof(SolidColorBrush))
            {
                return Color.TryParse(trimmed, out Color color) ? new SolidColorBrush(color) : throw new FormatException("Unknown brush '" + trimmed + "'.");
            }

            if (type == typeof(Media.Media3D.Point3D)) return Media.Media3D.Point3D.Parse(trimmed);
            if (type == typeof(Media.Media3D.Vector3D)) return Media.Media3D.Vector3D.Parse(trimmed);
            if (type == typeof(Media.Media3D.Point3DCollection)) return Media.Media3D.Point3DCollection.Parse(trimmed);
            if (type == typeof(Media.Media3D.Vector3DCollection)) return Media.Media3D.Vector3DCollection.Parse(trimmed);
            if (type == typeof(Int32Collection)) return Int32Collection.Parse(trimmed);

            if (type == typeof(Geometry)) return Geometry.Parse(trimmed);
            if (type == typeof(PathFigureCollection)) return PathFigureCollection.Parse(trimmed);
            if (type == typeof(PointCollection)) return PointCollection.Parse(trimmed);
            if (type == typeof(DoubleCollection)) return DoubleCollection.Parse(trimmed);
            if (type == typeof(Transform) || type == typeof(GeneralTransform)) return ParseTransform(trimmed);

            if (type == typeof(FontFamily)) return new FontFamily(trimmed);
            if (type == typeof(FontWeight)) { FontWeights.TryParse(trimmed, out FontWeight weight); return weight; }
            if (type == typeof(FontStyle)) return new FontStyleConverter().ConvertFromString(trimmed);
            if (type == typeof(FontStretch)) return default(FontStretch);
            if (type == typeof(TextDecorationCollection)) return TextDecorations.Parse(trimmed);

            if (type == typeof(ImageSource) || type == typeof(BitmapSource) || type == typeof(BitmapImage)) return new BitmapImage(new Uri(trimmed, UriKind.RelativeOrAbsolute));
            if (type == typeof(Uri)) return new Uri(trimmed, UriKind.RelativeOrAbsolute);
            if (type == typeof(Cursor)) return Cursors.Parse(trimmed);
            if (type == typeof(PropertyPath)) return new PropertyPath(trimmed);
            if (type == typeof(CultureInfo)) return new CultureInfo(trimmed);

            if (type == typeof(TimeSpan)) return TimeSpan.Parse(trimmed, CultureInfo.InvariantCulture);
            if (type == typeof(Duration)) return ParseDuration(trimmed);
            if (type == typeof(KeyTime)) return KeyTime.FromTimeSpan(TimeSpan.Parse(trimmed, CultureInfo.InvariantCulture));
            if (type == typeof(RepeatBehavior)) return ParseRepeatBehavior(trimmed);
            if (type == typeof(Controls.DataGridLength)) return ParseDataGridLength(trimmed);

            // types that bring their own converter ([TypeConverter])
            TypeConverter converter = TypeDescriptor.GetConverter(type);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(trimmed);
            }

            throw new NotSupportedException("No conversion from text to " + type.FullName + ".");
        }

        /// <summary>A length in device independent pixels: "12", "Auto" (NaN), or with a unit ("1in", "2cm", "10pt", "5px").</summary>
        public static double ParseLength(string text)
        {
            text = text.Trim();
            if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase) || text.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }
            if (text.Equals("Infinity", StringComparison.OrdinalIgnoreCase))
            {
                return double.PositiveInfinity;
            }

            double factor = 1;
            string lower = text.ToLowerInvariant();
            if (lower.EndsWith("px", StringComparison.Ordinal)) { text = text.Substring(0, text.Length - 2); }
            else if (lower.EndsWith("in", StringComparison.Ordinal)) { factor = 96; text = text.Substring(0, text.Length - 2); }
            else if (lower.EndsWith("cm", StringComparison.Ordinal)) { factor = 96 / 2.54; text = text.Substring(0, text.Length - 2); }
            else if (lower.EndsWith("pt", StringComparison.Ordinal)) { factor = 96 / 72.0; text = text.Substring(0, text.Length - 2); }
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) * factor;
        }

        private static double[] Numbers(string text, int expected)
        {
            string[] parts = text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != expected)
            {
                throw new FormatException("Expected " + expected + " numbers in '" + text + "'.");
            }
            double[] numbers = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                numbers[i] = ParseLength(parts[i]);
            }
            return numbers;
        }

        /// <summary>"Ctrl+Shift" style key modifiers (Ctrl is the XAML short form of Control).</summary>
        private static Input.ModifierKeys ParseModifierKeys(string text)
        {
            Input.ModifierKeys keys = Input.ModifierKeys.None;
            foreach (string part in text.Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ? "Control" : part;
                keys |= (Input.ModifierKeys)Enum.Parse(typeof(Input.ModifierKeys), name, ignoreCase: true);
            }
            return keys;
        }

        private static object ParseEnum(Type type, string text)
        {
            if (int.TryParse(text, out int number))
            {
                return Enum.ToObject(type, number);
            }
            return Enum.Parse(type, text.Replace(' ', ','), ignoreCase: true);
        }

        private static Thickness ParseThickness(string text)
        {
            string[] parts = text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);
            double N(int i) => ParseLength(parts[i]);
            switch (parts.Length)
            {
                case 1: return new Thickness(N(0));
                case 2: return new Thickness(N(0), N(1), N(0), N(1));
                case 4: return new Thickness(N(0), N(1), N(2), N(3));
                default: throw new FormatException("Invalid thickness '" + text + "'.");
            }
        }

        private static CornerRadius ParseCornerRadius(string text)
        {
            string[] parts = text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);
            double N(int i) => ParseLength(parts[i]);
            switch (parts.Length)
            {
                case 1: return new CornerRadius(N(0));
                case 4: return new CornerRadius(N(0), N(1), N(2), N(3));
                default: throw new FormatException("Invalid corner radius '" + text + "'.");
            }
        }

        private static Transform ParseTransform(string text)
        {
            if (text.Equals("Identity", StringComparison.OrdinalIgnoreCase))
            {
                return Transform.Identity;
            }
            double[] n = Numbers(text, 6);
            return new MatrixTransform(n[0], n[1], n[2], n[3], n[4], n[5]);
        }

        private static Duration ParseDuration(string text)
        {
            if (text.Equals("Automatic", StringComparison.OrdinalIgnoreCase) || text.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                return Duration.Automatic;
            }
            return new Duration(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        private static RepeatBehavior ParseRepeatBehavior(string text)
        {
            if (text.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                return RepeatBehavior.Forever;
            }
            if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                return new RepeatBehavior(double.Parse(text.Substring(0, text.Length - 1), CultureInfo.InvariantCulture));
            }
            return new RepeatBehavior(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        private static Controls.DataGridLength ParseDataGridLength(string text)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels)
                ? new Controls.DataGridLength(pixels)
                : Controls.DataGridLength.Auto;
        }
    }
}

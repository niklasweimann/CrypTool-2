using System.Globalization;

namespace System.Windows
{
    public enum Visibility { Visible, Hidden, Collapsed }

    public enum HorizontalAlignment { Left, Center, Right, Stretch }

    public enum VerticalAlignment { Top, Center, Bottom, Stretch }

    public enum TextAlignment { Left, Right, Center, Justify }

    public enum TextWrapping { WrapWithOverflow, NoWrap, Wrap }

    public enum TextTrimming { None, CharacterEllipsis, WordEllipsis }

    public enum FlowDirection { LeftToRight, RightToLeft }

    public enum GridUnitType { Auto, Pixel, Star }

    public struct Thickness : IEquatable<Thickness>
    {
        public Thickness(double uniformLength)
        {
            Left = Top = Right = Bottom = uniformLength;
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }

        public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

        public bool Equals(Thickness other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override bool Equals(object obj) => obj is Thickness other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

        public static bool operator ==(Thickness a, Thickness b) => a.Equals(b);

        public static bool operator !=(Thickness a, Thickness b) => !a.Equals(b);
    }

    public struct CornerRadius
    {
        public CornerRadius(double uniformRadius)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = uniformRadius;
        }

        public CornerRadius(double topLeft, double topRight, double bottomRight, double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public double TopLeft { get; set; }
        public double TopRight { get; set; }
        public double BottomRight { get; set; }
        public double BottomLeft { get; set; }
    }

    /// <summary>Size of a grid row or column: pixels, "Auto" or a share of the remaining space ("2*").</summary>
    public struct GridLength
    {
        public GridLength(double pixels) : this(pixels, GridUnitType.Pixel) { }

        public GridLength(double value, GridUnitType type)
        {
            Value = type == GridUnitType.Auto ? 1 : value;
            GridUnitType = type;
        }

        public double Value { get; }
        public GridUnitType GridUnitType { get; }

        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;

        public static GridLength Auto => new GridLength(1, GridUnitType.Auto);

        public static GridLength Parse(string text)
        {
            text = text.Trim();
            if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return Auto;
            }
            if (text.EndsWith("*", StringComparison.Ordinal))
            {
                string factor = text.Substring(0, text.Length - 1);
                return new GridLength(factor.Length == 0 ? 1 : double.Parse(factor, CultureInfo.InvariantCulture), GridUnitType.Star);
            }
            return new GridLength(double.Parse(text, CultureInfo.InvariantCulture));
        }
    }

    public class GridLengthConverter
    {
        public object ConvertFromString(string text) => GridLength.Parse(text);

        public object ConvertFrom(object value) => GridLength.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    public class LengthConverter
    {
        public object ConvertFromString(string text) => Markup.XamlValueConverter.ParseLength(text);

        public object ConvertFrom(object value) => ConvertFromString(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    public struct Duration
    {
        public Duration(TimeSpan timeSpan)
        {
            TimeSpan = timeSpan;
            HasTimeSpan = true;
        }

        public TimeSpan TimeSpan { get; }
        public bool HasTimeSpan { get; }

        public static Duration Automatic => default;
        public static Duration Forever => default;

        public static implicit operator Duration(TimeSpan timeSpan) => new Duration(timeSpan);
    }
}

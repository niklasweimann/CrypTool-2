namespace System.Windows.Media.Effects
{
    /// <summary>A pixel effect on an element (UIElement.Effect), drawn with a CSS filter.</summary>
    public abstract class Effect : DependencyObject
    {
        /// <summary>The CSS filter function that draws this effect.</summary>
        internal abstract string CssFilter { get; }
    }

    /// <summary>A shadow behind the element; Direction is in degrees counter-clockwise from the right.</summary>
    public sealed class DropShadowEffect : Effect
    {
        public Color Color { get; set; } = Colors.Black;
        public double BlurRadius { get; set; } = 5;
        public double ShadowDepth { get; set; } = 5;
        public double Direction { get; set; } = 315;
        public double Opacity { get; set; } = 1;

        internal override string CssFilter
        {
            get
            {
                double radians = Direction * Math.PI / 180;
                double x = Math.Cos(radians) * ShadowDepth;
                double y = -Math.Sin(radians) * ShadowDepth;
                string color = Rendering.CssValues.Color(Color, Math.Clamp(Opacity, 0, 1));
                return "drop-shadow(" + Rendering.CssValues.Pixels(x) + " " + Rendering.CssValues.Pixels(y) + " "
                    + Rendering.CssValues.Pixels(BlurRadius / 2) + " " + color + ")";
            }
        }
    }

    public sealed class BlurEffect : Effect
    {
        public double Radius { get; set; } = 5;

        internal override string CssFilter => "blur(" + Rendering.CssValues.Pixels(Radius / 2) + ")";
    }
}

namespace System.Windows.Media.Effects
{
    /// <summary>
    /// The bitmap effects of .NET 3.0 (UIElement.BitmapEffect). WPF has not drawn them since .NET 4, so neither
    /// does the browser; the types exist so that the XAML loads.
    /// </summary>
    public abstract class BitmapEffect : DependencyObject { }

    public sealed class DropShadowBitmapEffect : BitmapEffect
    {
        public Color Color { get; set; }
        public double ShadowDepth { get; set; }
        public double Softness { get; set; }
        public double Direction { get; set; }
        public double Opacity { get; set; }
        public double Noise { get; set; }
    }

    public sealed class OuterGlowBitmapEffect : BitmapEffect
    {
        public Color GlowColor { get; set; }
        public double GlowSize { get; set; }
        public double Opacity { get; set; }
        public double Noise { get; set; }
    }

    public sealed class BevelBitmapEffect : BitmapEffect
    {
        public double BevelWidth { get; set; }
        public double Relief { get; set; }
        public double Smoothness { get; set; }
        public double LightAngle { get; set; }
    }
}

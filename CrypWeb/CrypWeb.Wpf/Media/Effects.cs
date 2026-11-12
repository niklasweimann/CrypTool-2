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
    /// The bitmap effects of .NET 3.0 (UIElement.BitmapEffect). Since .NET 4 WPF still draws the drop shadow and the
    /// blur (as the corresponding Effect) and ignores the others; the same here.
    /// </summary>
    public abstract class BitmapEffect : DependencyObject
    {
        /// <summary>The effect WPF draws instead, or null when this one is not drawn.</summary>
        internal virtual Effect AsEffect => null;
    }

    /// <summary>Defaults as in WPF: black, 5 pixels deep, softness 0.5, down to the right.</summary>
    public sealed class DropShadowBitmapEffect : BitmapEffect
    {
        public Color Color { get; set; } = Colors.Black;
        public double ShadowDepth { get; set; } = 5;
        public double Softness { get; set; } = 0.5;
        public double Direction { get; set; } = 315;
        public double Opacity { get; set; } = 1;
        public double Noise { get; set; }

        // WPF maps Softness (0..1) to the blur radius of a DropShadowEffect (0..10 pixels)
        internal override Effect AsEffect => new DropShadowEffect
        {
            Color = Color, ShadowDepth = ShadowDepth, Direction = Direction, Opacity = Opacity, BlurRadius = Softness * 10,
        };
    }

    public sealed class BlurBitmapEffect : BitmapEffect
    {
        public double Radius { get; set; } = 5;

        internal override Effect AsEffect => new BlurEffect { Radius = Radius };
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

// Minimal stand-ins for the WPF types referenced by the CrypPluginBase API surface.
namespace System.Windows {  public class UIElement : DependencyObject { public double Width, Height; public bool IsVisible => true; } public class FrameworkElement : UIElement { public string Name { get; set; } } public enum Visibility { Visible, Hidden, Collapsed } public struct Thickness { public double Left,Top,Right,Bottom; public Thickness(double u) { Left = Top = Right = Bottom = u; } public Thickness(double l, double t, double r, double b) { Left = l; Top = t; Right = r; Bottom = b; } } public enum HorizontalAlignment{Left,Center,Right,Stretch} public enum VerticalAlignment{Top,Center,Bottom,Stretch} }
namespace System.Windows.Controls { public class Control : System.Windows.FrameworkElement {} public class UserControl : Control {} public class Panel : System.Windows.FrameworkElement {} public class Image : System.Windows.FrameworkElement { public System.Windows.Media.ImageSource Source; } public enum Orientation{Horizontal,Vertical} }
namespace System.Windows.Media { public class Brush : System.Windows.DependencyObject {} public class ImageSource : System.Windows.DependencyObject { public double Width, Height; } public struct Color { public byte A,R,G,B;
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color { A = a, R = r, G = g, B = b };
    public static Color FromRgb(byte r, byte g, byte b) => new Color { A = 255, R = r, G = g, B = b };
    public override string ToString() => "#" + A.ToString("X2") + R.ToString("X2") + G.ToString("X2") + B.ToString("X2"); }
  /// <summary>Remembers its colour so what a plugin paints into its presentation reaches the web view ("#AARRGGBB").</summary>
  public class SolidColorBrush : Brush { public Color Color { get; set; } public SolidColorBrush() { } public SolidColorBrush(Color c) { Color = c; } public override string ToString() => Color.ToString(); }
  public static class Colors {
    static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    public static Color Black => C(0, 0, 0); public static Color White => C(255, 255, 255); public static Color Red => C(255, 0, 0); public static Color Green => C(0, 128, 0); public static Color Lime => C(0, 255, 0);
    public static Color Blue => C(0, 0, 255); public static Color Yellow => C(255, 255, 0); public static Color Orange => C(255, 165, 0); public static Color Gray => C(128, 128, 128); public static Color LightGray => C(211, 211, 211);
    public static Color DarkGray => C(169, 169, 169); public static Color Transparent => Color.FromArgb(0, 255, 255, 255); public static Color Silver => C(192, 192, 192); public static Color Purple => C(128, 0, 128); public static Color Cyan => C(0, 255, 255);
    public static Color Magenta => C(255, 0, 255); public static Color Brown => C(165, 42, 42); public static Color Pink => C(255, 192, 203); public static Color DarkRed => C(139, 0, 0); public static Color DarkGreen => C(0, 100, 0); public static Color DarkBlue => C(0, 0, 139);
    public static Color LightGreen => C(144, 238, 144); public static Color LightBlue => C(173, 216, 230); public static Color Gold => C(255, 215, 0); public static Color Crimson => C(220, 20, 60); public static Color Navy => C(0, 0, 128);
  } }
namespace System.Windows.Media.Imaging { public class BitmapImage : System.Windows.Media.ImageSource { public System.Uri UriSource { get; set; } public BitmapImage() { } public BitmapImage(System.Uri u) { UriSource = u; } public override string ToString() => UriSource?.ToString() ?? ""; } public class BitmapFrame : System.Windows.Media.ImageSource { public static BitmapFrame Create(System.Uri u) => new BitmapFrame(); } }
namespace System.Windows { public struct FontWeight {} public struct FontStyle {} public struct GridLength {} }
namespace System.Windows.Controls { public class TabItem : Control {} }
namespace CrypTool.PluginBase.Editor { public interface IEditor { } }
namespace CrypTool.PluginBase { public class TabInfo { } }

namespace System.Windows {
  public static class FontWeights { public static FontWeight Normal => default; }
  public static class FontStyles { public static FontStyle Normal => default; }
  public class FontWeightConverter { public object ConvertFromString(string s) => default(FontWeight); }
  public class FontStyleConverter { public object ConvertFromString(string s) => default(FontStyle); }
  public class GridLengthConverter { public object ConvertFromString(string s) => default(GridLength); }
}
namespace System.Windows.Media {
  public static class Brushes { public static Brush LightGray => new SolidColorBrush(Colors.LightGray); public static Brush DarkGray => new SolidColorBrush(Colors.DarkGray); public static Brush Silver => new SolidColorBrush(Colors.Silver); public static Brush Yellow => new SolidColorBrush(Colors.Yellow); public static Brush Orange => new SolidColorBrush(Colors.Orange); public static Brush LightGreen => new SolidColorBrush(Colors.LightGreen); public static Brush LightBlue => new SolidColorBrush(Colors.LightBlue); public static Brush DarkRed => new SolidColorBrush(Colors.DarkRed); public static Brush DarkGreen => new SolidColorBrush(Colors.DarkGreen); public static Brush DarkBlue => new SolidColorBrush(Colors.DarkBlue); public static Brush Gold => new SolidColorBrush(Colors.Gold); public static Brush Purple => new SolidColorBrush(Colors.Purple); public static Brush Lime => new SolidColorBrush(Colors.Lime); public static Brush Cyan => new SolidColorBrush(Colors.Cyan); public static Brush Magenta => new SolidColorBrush(Colors.Magenta); public static Brush Pink => new SolidColorBrush(Colors.Pink); public static Brush Brown => new SolidColorBrush(Colors.Brown); public static Brush Black => new SolidColorBrush(Colors.Black); public static Brush White => new SolidColorBrush(Colors.White); public static Brush Red => new SolidColorBrush(Colors.Red); public static Brush Green => new SolidColorBrush(Colors.Green); public static Brush Blue => new SolidColorBrush(Colors.Blue); public static Brush Gray => new SolidColorBrush(Colors.Gray); public static Brush Transparent => new SolidColorBrush(Colors.Transparent); }
  public class BrushConverter { public object ConvertFromString(string s) => new Brush(); }
}
namespace System.Windows.Threading
{
    public enum DispatcherPriority { Invalid = -1, Inactive = 0, SystemIdle = 1, ApplicationIdle = 2, ContextIdle = 3, Background = 4, Input = 5, Loaded = 6, Render = 7, DataBind = 8, Normal = 9, Send = 10 }
    /// <summary>There is no UI thread in the engine worker: everything runs inline.</summary>
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();
        public bool CheckAccess() => true;
        public void VerifyAccess() { }
        // Delegates are invoked with exactly as many arguments as they declare (callers pass null or too many/few).
        static object Call(System.Delegate d, object[] args)
        {
            int n = d.Method.GetParameters().Length;
            var a = new object[n];
            if (args != null) System.Array.Copy(args, a, System.Math.Min(n, args.Length));
            return d.DynamicInvoke(a);
        }
        public void Invoke(System.Action a) => a();
        public void Invoke(DispatcherPriority p, System.Action a) => a();
        public object Invoke(DispatcherPriority p, System.Delegate d, params object[] args) => Call(d, args);
        public object Invoke(System.Delegate d, params object[] args) => Call(d, args);
        public object BeginInvoke(DispatcherPriority p, System.Delegate d, params object[] args) => Call(d, args);
        public object BeginInvoke(System.Delegate d, params object[] args) => Call(d, args);
        public object BeginInvoke(DispatcherPriority p, System.Action a) { a(); return null; }
    }
}
namespace System.Windows
{
    public class Application { public static Application Current => null; public System.Windows.Threading.Dispatcher Dispatcher => System.Windows.Threading.Dispatcher.CurrentDispatcher; }
}

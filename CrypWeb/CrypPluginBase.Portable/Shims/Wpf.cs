// Minimal stand-ins for the WPF types referenced by the CrypPluginBase API surface.
namespace System.Windows {  public class UIElement : DependencyObject { public double Width, Height; } public class FrameworkElement : UIElement {} public enum Visibility { Visible, Hidden, Collapsed } public struct Thickness { public double Left,Top,Right,Bottom; } public enum HorizontalAlignment{Left,Center,Right,Stretch} public enum VerticalAlignment{Top,Center,Bottom,Stretch} }
namespace System.Windows.Controls { public class Control : System.Windows.FrameworkElement {} public class UserControl : Control {} public class Panel : System.Windows.FrameworkElement {} public class Image : System.Windows.FrameworkElement { public System.Windows.Media.ImageSource Source; } public enum Orientation{Horizontal,Vertical} }
namespace System.Windows.Media { public class Brush : System.Windows.DependencyObject {} public class ImageSource : System.Windows.DependencyObject { public double Width, Height; } public struct Color { public byte A,R,G,B; } }
namespace System.Windows.Media.Imaging { public class BitmapImage : System.Windows.Media.ImageSource {} public class BitmapFrame : System.Windows.Media.ImageSource { public static BitmapFrame Create(System.Uri u) => new BitmapFrame(); } }
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
  public static class Brushes { public static Brush Black => new Brush(); public static Brush White => new Brush(); }
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

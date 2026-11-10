// Types of CrypPluginBase itself that only exist in the desktop build.
namespace CrypTool.PluginBase.Editor { public interface IEditor { } }

namespace CrypTool.PluginBase { public class TabInfo { } }

namespace CrypTool.PluginBase.Properties
{
    /// <summary>Application settings of the desktop host; the web host keeps the defaults.</summary>
    public sealed class Settings : System.ComponentModel.INotifyPropertyChanged
    {
        public static Settings Default { get; } = new Settings();
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public string FontFamily { get; set; } = "Courier New";
        public int FontSize { get; set; } = 12;
        public double BB84_AnimationSpeed { get; set; } = 1.0;
        public void Save() { }
    }
}

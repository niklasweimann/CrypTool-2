// Types of CrypPluginBase itself that only exist in the desktop build.
namespace CrypTool.PluginBase.Editor
{
    public interface IEditor { }
}

namespace CrypTool.PluginBase
{
    public class TabInfo { }
}

namespace CrypTool.PluginBase.Properties
{
    /// <summary>Application settings of the desktop host; the web host keeps the defaults.</summary>
    public sealed class Settings : System.ComponentModel.INotifyPropertyChanged
    {
        public static Settings Default { get; } = new Settings();
#pragma warning disable CS0067 // required by INotifyPropertyChanged; the web host's settings never change
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
        public System.Windows.Media.FontFamily FontFamily { get; set; } = new System.Windows.Media.FontFamily("Courier New");
        public int FontSize { get; set; } = 12;
        public double BB84_AnimationSpeed { get; set; } = 1.0;
        public bool KeySearcher_Anonymize { get; set; } = false;
        public bool KeySearcher_EnableHighLoad { get; set; } = false;
        public double KeySearcher_KeyPoolTreeZoom { get; set; } = 1.0;
        public int KeySearcher_MachNameChars { get; set; } = 0;
        public void Save() { }
    }
}

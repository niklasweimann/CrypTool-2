using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ConvolutionalCode
{
    public class ConvolutionalCodeSettings : ISettings
    {

        public event TaskPaneAttributeChangedHandler TaskPaneAttributeChanged;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public void Initialize()
        {
        }

        protected void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }
    }
}

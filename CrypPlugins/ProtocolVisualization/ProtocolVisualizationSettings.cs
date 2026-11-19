using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ProtocolVisualization
{
    public class ProtocolVisualizationSettings : ISettings
    {
        private int _protocol; // 0=Diffie-Hellman with perfect forward secrecy, 1=digital signature, 2=authentication in a network, 3=S/MIME

        [TaskPane("ProtocolCaption", "ProtocolTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ProtocolList1", "ProtocolList2", "ProtocolList3", "ProtocolList4" })]
        public int Protocol
        {
            get => _protocol;
            set { if (value != _protocol) { _protocol = value; OnPropertyChanged("Protocol"); } }
        }

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

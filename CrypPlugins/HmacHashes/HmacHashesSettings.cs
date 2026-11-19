using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace HmacHashes
{
    public class HmacHashesSettings : ISettings
    {
        private int _hash = 5; // see the list in the resources; SHA-256 by default

        [TaskPane("HashCaption", "HashTooltip", null, 1, false, ControlType.ComboBox, new string[] { "HashList1", "HashList2", "HashList3", "HashList4", "HashList5", "HashList6", "HashList7", "HashList8", "HashList9", "HashList10", "HashList11", "HashList12", "HashList13", "HashList14", "HashList15", "HashList16" })]
        public int Hash
        {
            get => _hash;
            set { if (value != _hash) { _hash = value; OnPropertyChanged("Hash"); } }
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

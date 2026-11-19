using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ExtraBlockCiphers
{
    public class ExtraBlockCiphersSettings : ISettings
    {
        private int _cipher = 0;
        private int _mode = 0;

        [TaskPane("CipherCaption", "CipherTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CipherList1", "CipherList2", "CipherList3" })]
        public int Cipher
        {
            get => _cipher;
            set { if (value != _cipher) { _cipher = value; OnPropertyChanged("Cipher"); } }
        }

        [TaskPane("ModeCaption", "ModeTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2", "ModeList3", "ModeList4" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
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

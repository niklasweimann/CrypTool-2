using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace CipherMAC
{
    public class CipherMACSettings : ISettings
    {
        private int _cipher; // 0=AES, 1=DES, 2=Camellia, 3=IDEA, 4=RC2, 5=RC5, 6=RC6, 7=Serpent, 8=Twofish, 9=Shacal-1, 10=Shacal-2
        private int _mode;   // 0=CMAC (OMAC1), 1=CBC-MAC

        [TaskPane("CipherCaption", "CipherTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CipherList1", "CipherList2", "CipherList3", "CipherList4", "CipherList5", "CipherList6", "CipherList7", "CipherList8", "CipherList9", "CipherList10", "CipherList11" })]
        public int Cipher
        {
            get => _cipher;
            set { if (value != _cipher) { _cipher = value; OnPropertyChanged("Cipher"); } }
        }

        [TaskPane("ModeCaption", "ModeTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
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

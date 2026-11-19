using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace KeySearch
{
    public class KeySearchSettings : ISettings
    {
        private int _cipher = 0;
        private int _unknownBits = 16;

        [TaskPane("CipherCaption", "CipherTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CipherList1", "CipherList2", "CipherList3", "CipherList4", "CipherList5", "CipherList6", "CipherList7", "CipherList8", "CipherList9", "CipherList10", "CipherList11", "CipherList12", "CipherList13", "CipherList14" })]
        public int Cipher
        {
            get => _cipher;
            set { if (value != _cipher) { _cipher = value; OnPropertyChanged("Cipher"); } }
        }

        [TaskPane("UnknownBitsCaption", "UnknownBitsTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 24)]
        public int UnknownBits
        {
            get => _unknownBits;
            set { if (value != _unknownBits) { _unknownBits = value; OnPropertyChanged("UnknownBits"); } }
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

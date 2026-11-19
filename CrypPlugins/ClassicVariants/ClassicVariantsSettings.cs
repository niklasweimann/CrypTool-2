using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ClassicVariants
{
    public class ClassicVariantsSettings : ISettings
    {
        private int _cipher;   // 0=Porta, 1=Rail Fence / Redefence, 2=Kamasutra, 3=Two-Square (Doppelkasten)
        private int _action;   // 0=encrypt, 1=decrypt
        private int _rails = 3;
        private int _offset;

        [TaskPane("CipherCaption", "CipherTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CipherList1", "CipherList2", "CipherList3", "CipherList4" })]
        public int Cipher
        {
            get => _cipher;
            set { if (value != _cipher) { _cipher = value; OnPropertyChanged("Cipher"); } }
        }

        [TaskPane("ActionCaption", "ActionTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("RailsCaption", "RailsTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 2, 50)]
        public int Rails
        {
            get => _rails;
            set { if (value != _rails) { _rails = value; OnPropertyChanged("Rails"); } }
        }

        [TaskPane("OffsetCaption", "OffsetTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 100)]
        public int Offset
        {
            get => _offset;
            set { if (value != _offset) { _offset = value; OnPropertyChanged("Offset"); } }
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

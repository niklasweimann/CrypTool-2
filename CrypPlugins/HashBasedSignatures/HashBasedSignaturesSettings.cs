using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace HashBasedSignatures
{
    public class HashBasedSignaturesSettings : ISettings
    {
        private int _action;    // 0=generate key pair, 1=sign, 2=verify
        private int _scheme;    // 0=LMS (Merkle tree of Winternitz one-time signatures), 1=SPHINCS+
        private int _height;    // LMS: 0=5 (32 signatures), 1=10 (1024 signatures)
        private int _winternitz = 2; // LMS: Winternitz parameter w = 1, 2, 4 or 8 bits
        private int _variant;   // SPHINCS+: 0=128f, 1=128s, 2=192f, 3=256f

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2", "ActionList3" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("SchemeCaption", "SchemeTooltip", null, 2, false, ControlType.ComboBox, new string[] { "SchemeList1", "SchemeList2" })]
        public int Scheme
        {
            get => _scheme;
            set { if (value != _scheme) { _scheme = value; OnPropertyChanged("Scheme"); } }
        }

        [TaskPane("HeightCaption", "HeightTooltip", null, 3, false, ControlType.ComboBox, new string[] { "HeightList1", "HeightList2" })]
        public int Height
        {
            get => _height;
            set { if (value != _height) { _height = value; OnPropertyChanged("Height"); } }
        }

        [TaskPane("WinternitzCaption", "WinternitzTooltip", null, 4, false, ControlType.ComboBox, new string[] { "WinternitzList1", "WinternitzList2", "WinternitzList3", "WinternitzList4" })]
        public int Winternitz
        {
            get => _winternitz;
            set { if (value != _winternitz) { _winternitz = value; OnPropertyChanged("Winternitz"); } }
        }

        [TaskPane("VariantCaption", "VariantTooltip", null, 5, false, ControlType.ComboBox, new string[] { "VariantList1", "VariantList2", "VariantList3", "VariantList4" })]
        public int Variant
        {
            get => _variant;
            set { if (value != _variant) { _variant = value; OnPropertyChanged("Variant"); } }
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

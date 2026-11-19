using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace PasswordBasedEncryption
{
    public class PasswordBasedEncryptionSettings : ISettings
    {
        private int _mode = 0;
        private int _iterations = 10000;
        private int _hash = 1;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("IterationsCaption", "IterationsTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 1000000)]
        public int Iterations
        {
            get => _iterations;
            set { if (value != _iterations) { _iterations = value; OnPropertyChanged("Iterations"); } }
        }

        [TaskPane("HashCaption", "HashTooltip", null, 3, false, ControlType.ComboBox, new string[] { "HashList1", "HashList2", "HashList3" })]
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

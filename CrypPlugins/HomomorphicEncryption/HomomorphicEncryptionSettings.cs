using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace HomomorphicEncryption
{
    public class HomomorphicEncryptionSettings : ISettings
    {
        private int _scheme = 1;
        private int _seed = 1;

        [TaskPane("SchemeCaption", "SchemeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "SchemeList1", "SchemeList2", "SchemeList3" })]
        public int Scheme
        {
            get => _scheme;
            set { if (value != _scheme) { _scheme = value; OnPropertyChanged("Scheme"); } }
        }

        [TaskPane("SeedCaption", "SeedTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
        public int Seed
        {
            get => _seed;
            set { if (value != _seed) { _seed = value; OnPropertyChanged("Seed"); } }
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

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace CfsSignature
{
    public class CfsSignatureSettings : ISettings
    {
        private int _seed = 1;

        [TaskPane("SeedCaption", "SeedTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
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

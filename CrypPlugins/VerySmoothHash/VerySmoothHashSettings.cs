using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace VerySmoothHash
{
    public class VerySmoothHashSettings : ISettings
    {
        private int _modulusBits = 512;
        private int _seed = 1;

        [TaskPane("ModulusBitsCaption", "ModulusBitsTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 128, 2048)]
        public int ModulusBits
        {
            get => _modulusBits;
            set { if (value != _modulusBits) { _modulusBits = value; OnPropertyChanged("ModulusBits"); } }
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

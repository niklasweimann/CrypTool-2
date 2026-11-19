using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace SideChannel
{
    public class SideChannelSettings : ISettings
    {
        private int _mode = 0;
        private int _seed = 1;
        private int _traces = 200;
        private int _noise = 100;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("SeedCaption", "SeedTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
        public int Seed
        {
            get => _seed;
            set { if (value != _seed) { _seed = value; OnPropertyChanged("Seed"); } }
        }

        [TaskPane("TracesCaption", "TracesTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 5, 3000)]
        public int Traces
        {
            get => _traces;
            set { if (value != _traces) { _traces = value; OnPropertyChanged("Traces"); } }
        }

        [TaskPane("NoiseCaption", "NoiseTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 1000)]
        public int Noise
        {
            get => _noise;
            set { if (value != _noise) { _noise = value; OnPropertyChanged("Noise"); } }
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

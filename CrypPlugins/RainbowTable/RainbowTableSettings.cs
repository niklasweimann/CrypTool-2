using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace RainbowTable
{
    public class RainbowTableSettings : ISettings
    {
        private int _length = 3;
        private int _chains = 800;
        private int _chainLength = 40;
        private int _seed = 1;
        private string _target = "abc";

        [TaskPane("LengthCaption", "LengthTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 3, 4)]
        public int Length
        {
            get => _length;
            set { if (value != _length) { _length = value; OnPropertyChanged("Length"); } }
        }

        [TaskPane("ChainsCaption", "ChainsTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 10, 5000)]
        public int Chains
        {
            get => _chains;
            set { if (value != _chains) { _chains = value; OnPropertyChanged("Chains"); } }
        }

        [TaskPane("ChainLengthCaption", "ChainLengthTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 5, 300)]
        public int ChainLength
        {
            get => _chainLength;
            set { if (value != _chainLength) { _chainLength = value; OnPropertyChanged("ChainLength"); } }
        }

        [TaskPane("SeedCaption", "SeedTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
        public int Seed
        {
            get => _seed;
            set { if (value != _seed) { _seed = value; OnPropertyChanged("Seed"); } }
        }

        [TaskPane("TargetCaption", "TargetTooltip", null, 5, false, ControlType.TextBox)]
        public string Target
        {
            get => _target;
            set { if (value != _target) { _target = value; OnPropertyChanged("Target"); } }
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

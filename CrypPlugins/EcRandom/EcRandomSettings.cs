using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace EcRandom
{
    public class EcRandomSettings : ISettings
    {
        private int _curve = 0;
        private int _seed = 1;
        private int _blocks = 5;

        [TaskPane("CurveCaption", "CurveTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CurveList1", "CurveList2", "CurveList3" })]
        public int Curve
        {
            get => _curve;
            set { if (value != _curve) { _curve = value; OnPropertyChanged("Curve"); } }
        }

        [TaskPane("SeedCaption", "SeedTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
        public int Seed
        {
            get => _seed;
            set { if (value != _seed) { _seed = value; OnPropertyChanged("Seed"); } }
        }

        [TaskPane("BlocksCaption", "BlocksTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 3, 20)]
        public int Blocks
        {
            get => _blocks;
            set { if (value != _blocks) { _blocks = value; OnPropertyChanged("Blocks"); } }
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

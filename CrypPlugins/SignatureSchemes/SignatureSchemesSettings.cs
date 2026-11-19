using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace SignatureSchemes
{
    public class SignatureSchemesSettings : ISettings
    {
        private int _scheme = 0;
        private int _seed = 1;
        private int _layers = 2;
        private int _height = 2;
        private int _index = 0;

        [TaskPane("SchemeCaption", "SchemeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "SchemeList1", "SchemeList2", "SchemeList3", "SchemeList4" })]
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

        [TaskPane("LayersCaption", "LayersTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 3)]
        public int Layers
        {
            get => _layers;
            set { if (value != _layers) { _layers = value; OnPropertyChanged("Layers"); } }
        }

        [TaskPane("HeightCaption", "HeightTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 4)]
        public int Height
        {
            get => _height;
            set { if (value != _height) { _height = value; OnPropertyChanged("Height"); } }
        }

        [TaskPane("IndexCaption", "IndexTooltip", null, 5, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 1000000)]
        public int Index
        {
            get => _index;
            set { if (value != _index) { _index = value; OnPropertyChanged("Index"); } }
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

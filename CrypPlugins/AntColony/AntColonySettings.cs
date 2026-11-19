using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace AntColony
{
    public class AntColonySettings : ISettings
    {
        private int _cities = 12;
        private int _ants = 20;
        private int _alpha = 1;
        private int _beta = 3;
        private int _evaporation = 50;
        private int _seed = 1;

        [TaskPane("CitiesCaption", "CitiesTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 4, 40)]
        public int Cities
        {
            get => _cities;
            set { if (value != _cities) { _cities = value; OnPropertyChanged("Cities"); } }
        }

        [TaskPane("AntsCaption", "AntsTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 200)]
        public int Ants
        {
            get => _ants;
            set { if (value != _ants) { _ants = value; OnPropertyChanged("Ants"); } }
        }

        [TaskPane("AlphaCaption", "AlphaTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 10)]
        public int Alpha
        {
            get => _alpha;
            set { if (value != _alpha) { _alpha = value; OnPropertyChanged("Alpha"); } }
        }

        [TaskPane("BetaCaption", "BetaTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 10)]
        public int Beta
        {
            get => _beta;
            set { if (value != _beta) { _beta = value; OnPropertyChanged("Beta"); } }
        }

        [TaskPane("EvaporationCaption", "EvaporationTooltip", null, 5, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 99)]
        public int Evaporation
        {
            get => _evaporation;
            set { if (value != _evaporation) { _evaporation = value; OnPropertyChanged("Evaporation"); } }
        }

        [TaskPane("SeedCaption", "SeedTooltip", null, 6, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
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

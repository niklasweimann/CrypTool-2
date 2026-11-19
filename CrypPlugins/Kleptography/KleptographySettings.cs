using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace Kleptography
{
    public class KleptographySettings : ISettings
    {
        private int _method = 1;
        private int _seed = 1;

        [TaskPane("MethodCaption", "MethodTooltip", null, 1, false, ControlType.ComboBox, new string[] { "MethodList1", "MethodList2", "MethodList3", "MethodList4" })]
        public int Method
        {
            get => _method;
            set { if (value != _method) { _method = value; OnPropertyChanged("Method"); } }
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

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace TextAnalysis
{
    public class TextAnalysisSettings : ISettings
    {
        private int _mode = 0;
        private int _n = 2;
        private int _top = 20;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2", "ModeList3" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("NCaption", "NTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 6)]
        public int N
        {
            get => _n;
            set { if (value != _n) { _n = value; OnPropertyChanged("N"); } }
        }

        [TaskPane("TopCaption", "TopTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 200)]
        public int Top
        {
            get => _top;
            set { if (value != _top) { _top = value; OnPropertyChanged("Top"); } }
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

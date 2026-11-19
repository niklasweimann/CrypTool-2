using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ClassicAnalysis
{
    public class ClassicAnalysisSettings : ISettings
    {
        private int _mode = 0;
        private int _size = 2;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("SizeCaption", "SizeTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 2, 6)]
        public int Size
        {
            get => _size;
            set { if (value != _size) { _size = value; OnPropertyChanged("Size"); } }
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

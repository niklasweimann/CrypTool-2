using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NumberTools
{
    public class NumberToolsSettings : ISettings
    {
        private int _mode = 0;
        private int _base = 10;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("BaseCaption", "BaseTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 2, 36)]
        public int Base
        {
            get => _base;
            set { if (value != _base) { _base = value; OnPropertyChanged("Base"); } }
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

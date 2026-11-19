using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace MlKem
{
    public class MlKemSettings : ISettings
    {
        private int _mode = 0;
        private int _parameterSet = 1;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2", "ModeList3" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("ParameterSetCaption", "ParameterSetTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ParameterSetList1", "ParameterSetList2", "ParameterSetList3" })]
        public int ParameterSet
        {
            get => _parameterSet;
            set { if (value != _parameterSet) { _parameterSet = value; OnPropertyChanged("ParameterSet"); } }
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

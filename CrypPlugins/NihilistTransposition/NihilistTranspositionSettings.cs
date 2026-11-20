using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NihilistTransposition
{
    public class NihilistTranspositionSettings : ISettings
    {
        private int _action;
        private bool _byColumns = false;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("ByColumnsCaption", "ByColumnsTooltip", null, 2, false, ControlType.CheckBox)]
        public bool ByColumns
        {
            get => _byColumns;
            set { if (value != _byColumns) { _byColumns = value; OnPropertyChanged("ByColumns"); } }
        }

        public event TaskPaneAttributeChangedHandler TaskPaneAttributeChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Initialize()
        {
        }

        protected void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }
    }
}

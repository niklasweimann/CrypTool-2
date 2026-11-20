using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ProgressiveKeyCipher
{
    public class ProgressiveKeyCipherSettings : ISettings
    {
        private int _action;
        private int _table;
        private int _progression = 1;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("TableCaption", "TableTooltip", null, 2, false, ControlType.ComboBox, new string[] { "TableList1", "TableList2", "TableList3" })]
        public int Table
        {
            get => _table;
            set { if (value != _table) { _table = value; OnPropertyChanged("Table"); } }
        }

        [TaskPane("ProgressionCaption", "ProgressionTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 25)]
        public int Progression
        {
            get => _progression;
            set { if (value != _progression) { _progression = value; OnPropertyChanged("Progression"); } }
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

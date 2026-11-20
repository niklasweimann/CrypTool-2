using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NumberedKeyCipher
{
    public class NumberedKeyCipherSettings : ISettings
    {
        private int _action;
        private int _offset = 0;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("OffsetCaption", "OffsetTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 200)]
        public int Offset
        {
            get => _offset;
            set { if (value != _offset) { _offset = value; OnPropertyChanged("Offset"); } }
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

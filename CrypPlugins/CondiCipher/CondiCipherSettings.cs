using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace CondiCipher
{
    public class CondiCipherSettings : ISettings
    {
        private int _action;
        private int _shift = 0;
        private int _offset = 0;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("ShiftCaption", "ShiftTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 25)]
        public int Shift
        {
            get => _shift;
            set { if (value != _shift) { _shift = value; OnPropertyChanged("Shift"); } }
        }

        [TaskPane("OffsetCaption", "OffsetTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 25)]
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

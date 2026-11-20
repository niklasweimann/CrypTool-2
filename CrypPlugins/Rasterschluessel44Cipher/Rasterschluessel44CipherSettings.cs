using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace Rasterschluessel44Cipher
{
    public class Rasterschluessel44CipherSettings : ISettings
    {
        private int _action;
        private int _startRow = 1;
        private int _startColumn = 1;
        private int _readNumber = 1;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("StartRowCaption", "StartRowTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 50)]
        public int StartRow
        {
            get => _startRow;
            set { if (value != _startRow) { _startRow = value; OnPropertyChanged("StartRow"); } }
        }

        [TaskPane("StartColumnCaption", "StartColumnTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 50)]
        public int StartColumn
        {
            get => _startColumn;
            set { if (value != _startColumn) { _startColumn = value; OnPropertyChanged("StartColumn"); } }
        }

        [TaskPane("ReadNumberCaption", "ReadNumberTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 50)]
        public int ReadNumber
        {
            get => _readNumber;
            set { if (value != _readNumber) { _readNumber = value; OnPropertyChanged("ReadNumber"); } }
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

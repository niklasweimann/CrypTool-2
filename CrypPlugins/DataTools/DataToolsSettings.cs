using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace DataTools
{
    public class DataToolsSettings : ISettings
    {
        private int _operation;  // see the list in the resources
        private int _amount = 1; // bits for shifts and rotations

        [TaskPane("OperationCaption", "OperationTooltip", null, 1, false, ControlType.ComboBox, new string[] { "OperationList1", "OperationList2", "OperationList3", "OperationList4", "OperationList5", "OperationList6", "OperationList7", "OperationList8", "OperationList9", "OperationList10" })]
        public int Operation
        {
            get => _operation;
            set { if (value != _operation) { _operation = value; OnPropertyChanged("Operation"); } }
        }

        [TaskPane("AmountCaption", "AmountTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 7)]
        public int Amount
        {
            get => _amount;
            set { if (value != _amount) { _amount = value; OnPropertyChanged("Amount"); } }
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

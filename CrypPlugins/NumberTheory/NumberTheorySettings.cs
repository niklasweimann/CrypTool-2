using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NumberTheory
{
    public class NumberTheorySettings : ISettings
    {
        private int _operation; // 0=modular exponentiation (square and multiply), 1=baby-step giant-step, 2=Chinese remainder theorem

        [TaskPane("OperationCaption", "OperationTooltip", null, 1, false, ControlType.ComboBox, new string[] { "OperationList1", "OperationList2", "OperationList3" })]
        public int Operation
        {
            get => _operation;
            set { if (value != _operation) { _operation = value; OnPropertyChanged("Operation"); } }
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

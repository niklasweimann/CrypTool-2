using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace HuffmanErrors
{
    public class HuffmanErrorsSettings : ISettings
    {
        private int _protect = 0;
        private int _errorPosition = 5;

        [TaskPane("ProtectCaption", "ProtectTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ProtectList1", "ProtectList2" })]
        public int Protect
        {
            get => _protect;
            set { if (value != _protect) { _protect = value; OnPropertyChanged("Protect"); } }
        }

        [TaskPane("ErrorPositionCaption", "ErrorPositionTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 0, 100000)]
        public int ErrorPosition
        {
            get => _errorPosition;
            set { if (value != _errorPosition) { _errorPosition = value; OnPropertyChanged("ErrorPosition"); } }
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

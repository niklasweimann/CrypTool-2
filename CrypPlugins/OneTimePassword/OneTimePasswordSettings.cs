using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace OneTimePassword
{
    public class OneTimePasswordSettings : ISettings
    {
        private int _mode = 0;
        private int _algorithm = 0;
        private int _digits = 6;
        private int _period = 30;
        private int _secretFormat = 0;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("AlgorithmCaption", "AlgorithmTooltip", null, 2, false, ControlType.ComboBox, new string[] { "AlgorithmList1", "AlgorithmList2", "AlgorithmList3" })]
        public int Algorithm
        {
            get => _algorithm;
            set { if (value != _algorithm) { _algorithm = value; OnPropertyChanged("Algorithm"); } }
        }

        [TaskPane("DigitsCaption", "DigitsTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 6, 9)]
        public int Digits
        {
            get => _digits;
            set { if (value != _digits) { _digits = value; OnPropertyChanged("Digits"); } }
        }

        [TaskPane("PeriodCaption", "PeriodTooltip", null, 4, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 3600)]
        public int Period
        {
            get => _period;
            set { if (value != _period) { _period = value; OnPropertyChanged("Period"); } }
        }

        [TaskPane("SecretFormatCaption", "SecretFormatTooltip", null, 5, false, ControlType.ComboBox, new string[] { "SecretFormatList1", "SecretFormatList2", "SecretFormatList3" })]
        public int SecretFormat
        {
            get => _secretFormat;
            set { if (value != _secretFormat) { _secretFormat = value; OnPropertyChanged("SecretFormat"); } }
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

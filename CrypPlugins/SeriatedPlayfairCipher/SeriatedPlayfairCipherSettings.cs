using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace SeriatedPlayfairCipher
{
    public class SeriatedPlayfairCipherSettings : ISettings
    {
        private int _action;
        private int _period = 6;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("PeriodCaption", "PeriodTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 50)]
        public int Period
        {
            get => _period;
            set { if (value != _period) { _period = value; OnPropertyChanged("Period"); } }
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

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace DivisorGame
{
    public class DivisorGameSettings : ISettings
    {
        private int _number = 60;
        private bool _computerStarts = false;

        [TaskPane("NumberCaption", "NumberTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 2, 360)]
        public int Number
        {
            get => _number;
            set { if (value != _number) { _number = value; OnPropertyChanged("Number"); } }
        }

        [TaskPane("ComputerStartsCaption", "ComputerStartsTooltip", null, 2, false, ControlType.CheckBox)]
        public bool ComputerStarts
        {
            get => _computerStarts;
            set { if (value != _computerStarts) { _computerStarts = value; OnPropertyChanged("ComputerStarts"); } }
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

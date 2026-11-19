using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace PatternLock
{
    public class PatternLockSettings : ISettings
    {
        private bool _showNumbers = true;

        [TaskPane("ShowNumbersCaption", "ShowNumbersTooltip", null, 1, false, ControlType.CheckBox)]
        public bool ShowNumbers
        {
            get => _showNumbers;
            set { if (value != _showNumbers) { _showNumbers = value; OnPropertyChanged("ShowNumbers"); } }
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

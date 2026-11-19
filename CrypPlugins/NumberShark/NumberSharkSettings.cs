using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NumberShark
{
    public class NumberSharkSettings : ISettings
    {
        private int _size = 24;

        [TaskPane("SizeCaption", "SizeTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 6, 60)]
        public int Size
        {
            get => _size;
            set { if (value != _size) { _size = value; OnPropertyChanged("Size"); } }
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

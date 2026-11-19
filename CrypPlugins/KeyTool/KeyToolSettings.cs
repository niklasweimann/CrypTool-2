using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace KeyTool
{
    public class KeyToolSettings : ISettings
    {
        private int _mode = 0;
        private int _keySize = 2048;
        private int _curve = 0;

        [TaskPane("ModeCaption", "ModeTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2", "ModeList3" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
        }

        [TaskPane("KeySizeCaption", "KeySizeTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 512, 4096)]
        public int KeySize
        {
            get => _keySize;
            set { if (value != _keySize) { _keySize = value; OnPropertyChanged("KeySize"); } }
        }

        [TaskPane("CurveCaption", "CurveTooltip", null, 3, false, ControlType.ComboBox, new string[] { "CurveList1", "CurveList2", "CurveList3", "CurveList4" })]
        public int Curve
        {
            get => _curve;
            set { if (value != _curve) { _curve = value; OnPropertyChanged("Curve"); } }
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

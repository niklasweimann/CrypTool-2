using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace Barcode
{
    public class BarcodeSettings : ISettings
    {
        private int _format = 0;

        [TaskPane("FormatCaption", "FormatTooltip", null, 1, false, ControlType.ComboBox, new string[] { "FormatList1", "FormatList2" })]
        public int Format
        {
            get => _format;
            set { if (value != _format) { _format = value; OnPropertyChanged("Format"); } }
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

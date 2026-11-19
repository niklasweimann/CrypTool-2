using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace DesVariants
{
    public class DesVariantsSettings : ISettings
    {
        private int _variant = 1;
        private int _mode = 0;

        [TaskPane("VariantCaption", "VariantTooltip", null, 1, false, ControlType.ComboBox, new string[] { "VariantList1", "VariantList2", "VariantList3", "VariantList4" })]
        public int Variant
        {
            get => _variant;
            set { if (value != _variant) { _variant = value; OnPropertyChanged("Variant"); } }
        }

        [TaskPane("ModeCaption", "ModeTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ModeList1", "ModeList2" })]
        public int Mode
        {
            get => _mode;
            set { if (value != _mode) { _mode = value; OnPropertyChanged("Mode"); } }
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

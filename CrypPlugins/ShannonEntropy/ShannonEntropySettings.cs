using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ShannonEntropy
{
    public class ShannonEntropySettings : ISettings
    {
        private int _symbolSize; // 0=bytes, 1=pairs of bytes

        [TaskPane("SymbolCaption", "SymbolTooltip", null, 1, false, ControlType.ComboBox, new string[] { "SymbolList1", "SymbolList2" })]
        public int Symbol
        {
            get => _symbolSize;
            set { if (value != _symbolSize) { _symbolSize = value; OnPropertyChanged("Symbol"); } }
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

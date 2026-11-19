using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace MessageDigests
{
    public class MessageDigestsSettings : ISettings
    {
        private int _algorithm; // 0=MD2, 1=MD4

        [TaskPane("AlgorithmCaption", "AlgorithmTooltip", null, 1, false, ControlType.ComboBox, new string[] { "AlgorithmList1", "AlgorithmList2" })]
        public int Algorithm
        {
            get => _algorithm;
            set
            {
                if (value != _algorithm)
                {
                    _algorithm = value;
                    OnPropertyChanged("Algorithm");
                }
            }
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

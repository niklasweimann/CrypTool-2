using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace CipherVisualization
{
    public class CipherVisualizationSettings : ISettings
    {
        private int _cipher; // 0=Caesar, 1=Vigenère, 2=Nihilist
        private int _action; // 0=encrypt, 1=decrypt

        [TaskPane("CipherCaption", "CipherTooltip", null, 1, false, ControlType.ComboBox, new string[] { "CipherList1", "CipherList2", "CipherList3" })]
        public int Cipher
        {
            get => _cipher;
            set { if (value != _cipher) { _cipher = value; OnPropertyChanged("Cipher"); } }
        }

        [TaskPane("ActionCaption", "ActionTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
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

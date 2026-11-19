using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace ClassicMcEliece
{
    public class ClassicMcElieceSettings : ISettings
    {
        private int _action;      // 0=generate key pair, 1=encapsulate, 2=decapsulate
        private int _parameters;  // 0=mceliece348864, 1=mceliece460896

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2", "ActionList3" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("ParametersCaption", "ParametersTooltip", null, 2, false, ControlType.ComboBox, new string[] { "ParametersList1", "ParametersList2" })]
        public int Parameters
        {
            get => _parameters;
            set { if (value != _parameters) { _parameters = value; OnPropertyChanged("Parameters"); } }
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

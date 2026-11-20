using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace AmscoCipher
{
    public class AmscoCipherSettings : ISettings
    {
        private int _action;
        private bool _startWithDigraph = true;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("StartWithDigraphCaption", "StartWithDigraphTooltip", null, 2, false, ControlType.CheckBox)]
        public bool StartWithDigraph
        {
            get => _startWithDigraph;
            set { if (value != _startWithDigraph) { _startWithDigraph = value; OnPropertyChanged("StartWithDigraph"); } }
        }

        public event TaskPaneAttributeChangedHandler TaskPaneAttributeChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Initialize()
        {
        }

        protected void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }
    }
}

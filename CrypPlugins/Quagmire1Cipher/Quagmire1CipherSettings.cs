using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace Quagmire1Cipher
{
    public class Quagmire1CipherSettings : ISettings
    {
        private int _action;
        private string _under = "A";

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("UnderCaption", "UnderTooltip", null, 2, false, ControlType.TextBox, null)]
        public string Under
        {
            get => _under;
            set { if (value != _under) { _under = value; OnPropertyChanged("Under"); } }
        }

        /// <summary>The first letter of the setting, A if it is empty.</summary>
        public char UnderLetter => string.IsNullOrEmpty(_under) ? 'A' : _under[0];

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

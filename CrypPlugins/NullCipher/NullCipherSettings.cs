using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace NullCipher
{
    public class NullCipherSettings : ISettings
    {
        private int _action;
        private int _nullPosition = 1;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("NullPositionCaption", "NullPositionTooltip", null, 2, false, ControlType.ComboBox, new string[] { "NullPositionList1", "NullPositionList2", "NullPositionList3" })]
        public int NullPosition
        {
            get => _nullPosition;
            set { if (value != _nullPosition) { _nullPosition = value; OnPropertyChanged("NullPosition"); } }
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

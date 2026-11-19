using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace SecretSharing
{
    public class SecretSharingSettings : ISettings
    {
        private int _action;      // 0=split the secret, 1=reconstruct it, 2=verify a share
        private bool _verifiable; // Feldman's verifiable secret sharing: commitments are published as well

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2", "ActionList3" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("VerifiableCaption", "VerifiableTooltip", null, 2, false, ControlType.CheckBox)]
        public bool Verifiable
        {
            get => _verifiable;
            set { if (value != _verifiable) { _verifiable = value; OnPropertyChanged("Verifiable"); } }
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

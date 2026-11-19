using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace PublicKeyEncryption
{
    public class PublicKeyEncryptionSettings : ISettings
    {
        private int _action;     // 0=generate key pair, 1=encrypt, 2=decrypt
        private int _algorithm;  // 0=ElGamal, 1=Rabin, 2=Merkle-Hellman knapsack, 3=ECIES
        private int _curve;      // ECIES: 0=secp256r1, 1=secp384r1, 2=secp521r1
        private int _keySize;    // 0=1024, 1=2048 bits

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2", "ActionList3" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("AlgorithmCaption", "AlgorithmTooltip", null, 2, false, ControlType.ComboBox, new string[] { "AlgorithmList1", "AlgorithmList2", "AlgorithmList3", "AlgorithmList4" })]
        public int Algorithm
        {
            get => _algorithm;
            set { if (value != _algorithm) { _algorithm = value; OnPropertyChanged("Algorithm"); } }
        }

        [TaskPane("KeySizeCaption", "KeySizeTooltip", null, 3, false, ControlType.ComboBox, new string[] { "KeySizeList1", "KeySizeList2" })]
        public int KeySize
        {
            get => _keySize;
            set { if (value != _keySize) { _keySize = value; OnPropertyChanged("KeySize"); } }
        }

        [TaskPane("CurveCaption", "CurveTooltip", null, 4, false, ControlType.ComboBox, new string[] { "CurveList1", "CurveList2", "CurveList3" })]
        public int Curve
        {
            get => _curve;
            set { if (value != _curve) { _curve = value; OnPropertyChanged("Curve"); } }
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

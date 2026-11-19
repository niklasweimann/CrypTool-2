using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace DigitalSignatures
{
    public class DigitalSignaturesSettings : ISettings
    {
        private int _action;     // 0=generate key pair, 1=sign, 2=verify
        private int _algorithm;  // 0=RSA (PKCS#1 v1.5), 1=RSA-PSS, 2=DSA, 3=ECDSA, 4=ECNR
        private int _hash = 1;   // 0=SHA-1, 1=SHA-256, 2=SHA-384, 3=SHA-512
        private int _keySize = 1;// 0=1024, 1=2048, 2=3072 bits (RSA, DSA)
        private int _curve;      // 0=secp256r1, 1=secp384r1, 2=secp521r1, 3=secp256k1 (ECDSA, ECNR)

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2", "ActionList3" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("AlgorithmCaption", "AlgorithmTooltip", null, 2, false, ControlType.ComboBox, new string[] { "AlgorithmList1", "AlgorithmList2", "AlgorithmList3", "AlgorithmList4", "AlgorithmList5" })]
        public int Algorithm
        {
            get => _algorithm;
            set { if (value != _algorithm) { _algorithm = value; OnPropertyChanged("Algorithm"); } }
        }

        [TaskPane("HashCaption", "HashTooltip", null, 3, false, ControlType.ComboBox, new string[] { "HashList1", "HashList2", "HashList3", "HashList4" })]
        public int Hash
        {
            get => _hash;
            set { if (value != _hash) { _hash = value; OnPropertyChanged("Hash"); } }
        }

        [TaskPane("KeySizeCaption", "KeySizeTooltip", null, 4, false, ControlType.ComboBox, new string[] { "KeySizeList1", "KeySizeList2", "KeySizeList3" })]
        public int KeySize
        {
            get => _keySize;
            set { if (value != _keySize) { _keySize = value; OnPropertyChanged("KeySize"); } }
        }

        [TaskPane("CurveCaption", "CurveTooltip", null, 5, false, ControlType.ComboBox, new string[] { "CurveList1", "CurveList2", "CurveList3", "CurveList4" })]
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

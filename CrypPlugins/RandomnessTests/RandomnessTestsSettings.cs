using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace RandomnessTests
{
    public class RandomnessTestsSettings : ISettings
    {
        private int _test;                 // 0=FIPS 140-1, 1=Poker, 2=Serial, 3=Runs
        private int _pokerBlockLength = 4;
        private int _serialBlockLength = 2;

        [TaskPane("TestCaption", "TestTooltip", null, 1, false, ControlType.ComboBox, new string[] { "TestList1", "TestList2", "TestList3", "TestList4" })]
        public int Test
        {
            get => _test;
            set
            {
                if (value != _test)
                {
                    _test = value;
                    OnPropertyChanged("Test");
                }
            }
        }

        [TaskPane("PokerBlockLengthCaption", "PokerBlockLengthTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 12)]
        public int PokerBlockLength
        {
            get => _pokerBlockLength;
            set
            {
                if (value != _pokerBlockLength)
                {
                    _pokerBlockLength = value;
                    OnPropertyChanged("PokerBlockLength");
                }
            }
        }

        [TaskPane("SerialBlockLengthCaption", "SerialBlockLengthTooltip", null, 3, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 2, 12)]
        public int SerialBlockLength
        {
            get => _serialBlockLength;
            set
            {
                if (value != _serialBlockLength)
                {
                    _serialBlockLength = value;
                    OnPropertyChanged("SerialBlockLength");
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

using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace MathVisualization
{
    public class MathVisualizationSettings : ISettings
    {
        private int _visualization; // 0=RSA, 1=Shamir's secret sharing, 2=elliptic curve

        [TaskPane("VisualizationCaption", "VisualizationTooltip", null, 1, false, ControlType.ComboBox, new string[] { "VisualizationList1", "VisualizationList2", "VisualizationList3" })]
        public int Visualization
        {
            get => _visualization;
            set { if (value != _visualization) { _visualization = value; OnPropertyChanged("Visualization"); } }
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

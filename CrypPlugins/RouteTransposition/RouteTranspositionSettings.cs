using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace RouteTransposition
{
    public class RouteTranspositionSettings : ISettings
    {
        private int _action;
        private int _columns = 5;
        private int _writeRoute;
        private int _readRoute;

        [TaskPane("ActionCaption", "ActionTooltip", null, 1, false, ControlType.ComboBox, new string[] { "ActionList1", "ActionList2" })]
        public int Action
        {
            get => _action;
            set { if (value != _action) { _action = value; OnPropertyChanged("Action"); } }
        }

        [TaskPane("ColumnsCaption", "ColumnsTooltip", null, 2, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 50)]
        public int Columns
        {
            get => _columns;
            set { if (value != _columns) { _columns = value; OnPropertyChanged("Columns"); } }
        }

        [TaskPane("WriteRouteCaption", "WriteRouteTooltip", null, 3, false, ControlType.ComboBox, new string[] { "RouteList1", "RouteList2", "RouteList3", "RouteList4", "RouteList5", "RouteList6", "RouteList7", "RouteList8", "RouteList9", "RouteList10" })]
        public int WriteRoute
        {
            get => _writeRoute;
            set { if (value != _writeRoute) { _writeRoute = value; OnPropertyChanged("WriteRoute"); } }
        }

        [TaskPane("ReadRouteCaption", "ReadRouteTooltip", null, 4, false, ControlType.ComboBox, new string[] { "RouteList1", "RouteList2", "RouteList3", "RouteList4", "RouteList5", "RouteList6", "RouteList7", "RouteList8", "RouteList9", "RouteList10" })]
        public int ReadRoute
        {
            get => _readRoute;
            set { if (value != _readRoute) { _readRoute = value; OnPropertyChanged("ReadRoute"); } }
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

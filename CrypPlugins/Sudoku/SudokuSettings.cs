using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace Sudoku
{
    public class SudokuSettings : ISettings
    {
        private int _puzzle = 1;
        private int _difficulty = 1;

        [TaskPane("PuzzleCaption", "PuzzleTooltip", null, 1, false, ControlType.NumericUpDown, ValidationType.RangeInteger, 1, 100000)]
        public int Puzzle
        {
            get => _puzzle;
            set { if (value != _puzzle) { _puzzle = value; OnPropertyChanged("Puzzle"); } }
        }

        [TaskPane("DifficultyCaption", "DifficultyTooltip", null, 2, false, ControlType.ComboBox, new string[] { "DifficultyList1", "DifficultyList2", "DifficultyList3" })]
        public int Difficulty
        {
            get => _difficulty;
            set { if (value != _difficulty) { _difficulty = value; OnPropertyChanged("Difficulty"); } }
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

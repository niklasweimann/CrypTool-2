using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Sudoku
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("Sudoku.Properties.Resources", "PluginCaption", "PluginTooltip", "Sudoku/DetailedDescription/doc.xml", "Sudoku/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class Sudoku : ICrypComponent
    {
        private SudokuSettings _settings = new SudokuSettings();
        private SudokuPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (SudokuSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new SudokuPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>Starts a new game when the workspace runs; the game then goes on in the presentation.</summary>
        public void Execute()
        {
            ((SudokuPresentation)Presentation).Start(_settings.Puzzle, _settings.Difficulty);
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>Sudoku (Zudo-Ku): a generator that makes a solved grid and removes digits as long as the solution stays unique, and a solver.</summary>
    public sealed class SudokuPuzzle
    {
        public int[] Solution = new int[81];
        public int[] Givens = new int[81];

        /// <summary>A puzzle that depends only on the number (the same number gives the same puzzle); clues is the number of digits that stay.</summary>
        public static SudokuPuzzle Generate(int number, int clues)
        {
            var random = new Random(number * 7919 + 17);
            var puzzle = new SudokuPuzzle();
            // a pattern that is a valid solution, then shuffled in ways that keep it valid: rows within bands, columns within stacks, bands, stacks, digits
            int[] digits = Enumerable.Range(1, 9).OrderBy(x => random.Next()).ToArray();
            int[] rows = Shuffle(random), columns = Shuffle(random);
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    puzzle.Solution[r * 9 + c] = digits[(3 * (rows[r] % 3) + rows[r] / 3 + columns[c]) % 9];
                }
            }
            Array.Copy(puzzle.Solution, puzzle.Givens, 81);
            int left = 81;
            foreach (int cell in Enumerable.Range(0, 81).OrderBy(x => random.Next()))
            {
                if (left <= clues) { break; }
                int keep = puzzle.Givens[cell];
                puzzle.Givens[cell] = 0;
                if (CountSolutions((int[])puzzle.Givens.Clone(), 2) != 1) { puzzle.Givens[cell] = keep; } else { left--; }
            }
            return puzzle;
        }

        // rows 0..8 in an order that keeps the bands: the three bands and the rows inside each band are permuted
        private static int[] Shuffle(Random random)
        {
            var result = new List<int>();
            foreach (int band in Enumerable.Range(0, 3).OrderBy(x => random.Next()))
            {
                foreach (int inner in Enumerable.Range(0, 3).OrderBy(x => random.Next())) { result.Add(band * 3 + inner); }
            }
            return result.ToArray();
        }

        public static bool CanPlace(int[] grid, int cell, int digit)
        {
            int row = cell / 9, column = cell % 9;
            for (int i = 0; i < 9; i++)
            {
                if (grid[row * 9 + i] == digit && row * 9 + i != cell) { return false; }
                if (grid[i * 9 + column] == digit && i * 9 + column != cell) { return false; }
            }
            int boxRow = row / 3 * 3, boxColumn = column / 3 * 3;
            for (int r = boxRow; r < boxRow + 3; r++)
            {
                for (int c = boxColumn; c < boxColumn + 3; c++) { if (grid[r * 9 + c] == digit && r * 9 + c != cell) { return false; } }
            }
            return true;
        }

        /// <summary>The number of solutions of the grid, counted up to the limit (backtracking, always filling the cell with the fewest candidates).</summary>
        public static int CountSolutions(int[] grid, int limit)
        {
            int best = -1, bestCount = 10;
            for (int cell = 0; cell < 81; cell++)
            {
                if (grid[cell] != 0) { continue; }
                int count = Enumerable.Range(1, 9).Count(d => CanPlace(grid, cell, d));
                if (count < bestCount) { best = cell; bestCount = count; if (count == 0) { return 0; } }
            }
            if (best < 0) { return 1; }
            int solutions = 0;
            for (int digit = 1; digit <= 9 && solutions < limit; digit++)
            {
                if (!CanPlace(grid, best, digit)) { continue; }
                grid[best] = digit;
                solutions += CountSolutions(grid, limit - solutions);
                grid[best] = 0;
            }
            return solutions;
        }

        /// <summary>The cells whose digit occurs again in its row, column or box.</summary>
        public static IEnumerable<int> Conflicts(int[] grid) => Enumerable.Range(0, 81).Where(c => grid[c] != 0 && !CanPlace(grid, c, grid[c]));

        public static bool IsSolved(int[] grid) => grid.All(d => d != 0) && !Conflicts(grid).Any();
    }

    public class SudokuPresentation : UserControl
    {
        private static readonly Brush Fixed = new SolidColorBrush(Color.FromRgb(215, 225, 245));
        private static readonly Brush Light = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private static readonly Brush Dark = new SolidColorBrush(Color.FromRgb(245, 245, 238));
        private static readonly Brush Selected = new SolidColorBrush(Color.FromRgb(255, 235, 130));
        private static readonly Brush Wrong = new SolidColorBrush(Color.FromRgb(240, 160, 160));

        private SudokuPuzzle _puzzle;
        private int[] _grid = new int[81];
        private int _selected = -1;
        private int _number, _clues;
        private bool _check;
        private string _message = "";
        private readonly List<Button> _cells = new List<Button>();
        private readonly List<Button> _pad = new List<Button>();

        public SudokuPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public IList<Button> CellButtons => _cells;

        public IList<Button> PadButtons => _pad;

        public SudokuPuzzle Puzzle => _puzzle;

        public int[] Grid => _grid;

        public void Start(int number, int difficulty)
        {
            Action start = () =>
            {
                _number = Math.Max(1, number);
                _clues = difficulty == 0 ? 40 : difficulty == 1 ? 32 : 26;
                NewPuzzle();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void NewPuzzle()
        {
            _puzzle = SudokuPuzzle.Generate(_number, _clues);
            _grid = (int[])_puzzle.Givens.Clone();
            _selected = -1;
            _check = false;
            _message = Properties.Resources.Rules;
            Rebuild();
        }

        private void Rebuild()
        {
            _cells.Clear();
            _pad.Clear();
            var conflicts = _check ? new HashSet<int>(SudokuPuzzle.Conflicts(_grid)) : new HashSet<int>();
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Title, _number), FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = _message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            var grid = new UniformGrid { Rows = 9, Columns = 9, Width = 9 * 42, Height = 9 * 42, HorizontalAlignment = HorizontalAlignment.Left };
            for (int cell = 0; cell < 81; cell++)
            {
                int index = cell;
                int box = (cell / 27) * 3 + (cell % 9) / 3;
                bool given = _puzzle.Givens[cell] != 0;
                var button = new Button
                {
                    Content = _grid[cell] == 0 ? "" : _grid[cell].ToString(), FontSize = 16, FontWeight = given ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(1),
                    Background = conflicts.Contains(cell) ? Wrong : cell == _selected ? Selected : given ? Fixed : box % 2 == 0 ? Light : Dark,
                    IsEnabled = !given
                };
                button.Click += (sender, e) => { _selected = index; _check = false; Rebuild(); };
                _cells.Add(button);
                grid.Children.Add(button);
            }
            panel.Children.Add(grid);
            var pad = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            for (int digit = 0; digit <= 9; digit++)
            {
                int value = digit;
                var button = new Button { Content = digit == 0 ? Properties.Resources.Clear : digit.ToString(), Width = digit == 0 ? 60 : 36, Height = 32, Margin = new Thickness(2), FontSize = 14 };
                button.Click += (sender, e) => Enter(value);
                _pad.Add(button);
                pad.Children.Add(button);
            }
            panel.Children.Add(pad);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var check = new Button { Content = Properties.Resources.Check, Width = 100, Height = 30, Margin = new Thickness(2) };
            check.Click += (sender, e) => Check();
            var solution = new Button { Content = Properties.Resources.ShowSolution, Width = 120, Height = 30, Margin = new Thickness(2) };
            solution.Click += (sender, e) => { _grid = (int[])_puzzle.Solution.Clone(); _check = false; _message = Properties.Resources.Shown; Rebuild(); };
            var again = new Button { Content = Properties.Resources.NewGame, Width = 100, Height = 30, Margin = new Thickness(2) };
            again.Click += (sender, e) => { _number++; NewPuzzle(); };
            actions.Children.Add(check);
            actions.Children.Add(solution);
            actions.Children.Add(again);
            panel.Children.Add(actions);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void Enter(int digit)
        {
            if (_selected < 0 || _puzzle.Givens[_selected] != 0) { return; }
            _grid[_selected] = digit;
            _check = false;
            Rebuild();
        }

        private void Check()
        {
            _check = true;
            int conflicts = SudokuPuzzle.Conflicts(_grid).Count();
            _message = SudokuPuzzle.IsSolved(_grid) ? Properties.Resources.Solved : conflicts > 0 ? string.Format(Properties.Resources.Conflicts, conflicts) : string.Format(Properties.Resources.NoConflicts, _grid.Count(d => d == 0));
            Rebuild();
        }
    }
}

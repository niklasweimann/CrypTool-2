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

namespace NumberShark
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("NumberShark.Properties.Resources", "PluginCaption", "PluginTooltip", "NumberShark/DetailedDescription/doc.xml", "NumberShark/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class NumberShark : ICrypComponent
    {
        private NumberSharkSettings _settings = new NumberSharkSettings();
        private NumberSharkPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (NumberSharkSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new NumberSharkPresentation(); }
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
            ((NumberSharkPresentation)Presentation).Start(_settings.Size);
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

    /// <summary>
    /// The rules of the number shark game (also known as taxman): the numbers 1 to n are on the table. The player takes a number and scores it; the shark
    /// takes all numbers that divide it (its proper divisors) and scores them. A number may only be taken if the shark gets at least one number for it.
    /// When no such number is left, the shark takes the rest. The one with more points wins.
    /// </summary>
    public sealed class SharkGame
    {
        public int Size { get; }
        public bool[] Taken { get; }
        public bool[] ByPlayer { get; }
        public int PlayerScore { get; private set; }
        public int SharkScore { get; private set; }
        public bool Finished { get; private set; }

        public SharkGame(int size)
        {
            Size = size;
            Taken = new bool[size + 1];
            ByPlayer = new bool[size + 1];
            if (!ValidMoves().Any()) { Finish(); }
        }

        /// <summary>The numbers that are still free and that divide the given number.</summary>
        public List<int> FreeDivisors(int number)
        {
            var result = new List<int>();
            for (int d = 1; d < number; d++) { if (number % d == 0 && !Taken[d]) { result.Add(d); } }
            return result;
        }

        public bool IsValid(int number) => number >= 1 && number <= Size && !Taken[number] && FreeDivisors(number).Count > 0;

        public IEnumerable<int> ValidMoves() => Enumerable.Range(1, Size).Where(IsValid);

        /// <summary>The player takes the number; returns false (and changes nothing) if the move is not allowed.</summary>
        public bool Move(int number)
        {
            if (Finished || !IsValid(number)) { return false; }
            Taken[number] = true;
            ByPlayer[number] = true;
            PlayerScore += number;
            foreach (int d in FreeDivisors(number)) { Taken[d] = true; SharkScore += d; }
            if (!ValidMoves().Any()) { Finish(); }
            return true;
        }

        private void Finish()
        {
            for (int n = 1; n <= Size; n++) { if (!Taken[n]) { Taken[n] = true; SharkScore += n; } }
            Finished = true;
        }
    }

    public class NumberSharkPresentation : UserControl
    {
        private static readonly Brush Free = new SolidColorBrush(Color.FromRgb(225, 238, 255));
        private static readonly Brush Blocked = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        private static readonly Brush Mine = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush Shark = new SolidColorBrush(Color.FromRgb(240, 160, 160));

        private SharkGame _game;
        private int _size = 24;
        private string _message = "";
        private readonly List<Button> _buttons = new List<Button>();

        public NumberSharkPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        /// <summary>The number buttons, for tests.</summary>
        public IList<Button> NumberButtons => _buttons;

        public SharkGame Game => _game;

        public void Start(int size)
        {
            Action start = () =>
            {
                _size = Math.Max(4, Math.Min(100, size));
                _game = new SharkGame(_size);
                _message = Properties.Resources.Rules;
                Rebuild();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void Rebuild()
        {
            _buttons.Clear();
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Title, FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Rules, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Score, _game.PlayerScore, _game.SharkScore, _game.Taken.Skip(1).Count(t => !t)), FontSize = 14, FontWeight = FontWeights.Bold });
            var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
            for (int n = 1; n <= _game.Size; n++)
            {
                int number = n;
                var button = new Button
                {
                    Content = n.ToString(), Width = 46, Height = 34, Margin = new Thickness(2), FontSize = 13,
                    Background = _game.Taken[n] ? (_game.ByPlayer[n] ? Mine : Shark) : (_game.IsValid(n) ? Free : Blocked),
                    IsEnabled = !_game.Taken[n] && !_game.Finished
                };
                button.Click += (sender, e) => Pick(number);
                _buttons.Add(button);
                wrap.Children.Add(button);
            }
            panel.Children.Add(wrap);
            panel.Children.Add(new TextBlock { Text = _message, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            var again = new Button { Content = Properties.Resources.NewGame, Width = 120, Height = 30, HorizontalAlignment = HorizontalAlignment.Left };
            again.Click += (sender, e) => { _game = new SharkGame(_size); _message = Properties.Resources.Rules; Rebuild(); };
            panel.Children.Add(again);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void Pick(int number)
        {
            if (_game == null) { return; }
            var divisors = _game.FreeDivisors(number);
            if (!_game.Move(number))
            {
                _message = string.Format(Properties.Resources.NotAllowed, number);
            }
            else if (_game.Finished)
            {
                _message = _game.PlayerScore > _game.SharkScore ? string.Format(Properties.Resources.YouWin, _game.PlayerScore, _game.SharkScore)
                         : _game.PlayerScore == _game.SharkScore ? Properties.Resources.Draw : string.Format(Properties.Resources.SharkWins, _game.PlayerScore, _game.SharkScore);
            }
            else
            {
                _message = string.Format(Properties.Resources.Took, number, string.Join(", ", divisors));
            }
            Rebuild();
        }
    }
}

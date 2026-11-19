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

namespace DivisorGame
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("DivisorGame.Properties.Resources", "PluginCaption", "PluginTooltip", "DivisorGame/DetailedDescription/doc.xml", "DivisorGame/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class DivisorGame : ICrypComponent
    {
        private DivisorGameSettings _settings = new DivisorGameSettings();
        private DivisorGamePresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (DivisorGameSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new DivisorGamePresentation(); }
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
            ((DivisorGamePresentation)Presentation).Start(_settings.Number, _settings.ComputerStarts);
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
    /// The divisor game (Chomp on the divisors of a number): the divisors of n are on the table. A player takes a divisor and with it all divisors that are
    /// multiples of it. The player who has to take 1 loses. The first player can always win, but must find the right move.
    /// </summary>
    public sealed class DivisorPosition
    {
        public int Number { get; }
        public int[] Divisors { get; }
        private readonly Dictionary<long, bool> _winning = new Dictionary<long, bool>();

        public DivisorPosition(int number)
        {
            Number = number;
            Divisors = Enumerable.Range(1, number).Where(d => number % d == 0).ToArray();
        }

        /// <summary>The bit mask with every divisor still on the table.</summary>
        public long Full => (1L << Divisors.Length) - 1;

        public bool Has(long mask, int divisor) => (mask >> Array.IndexOf(Divisors, divisor) & 1) == 1;

        /// <summary>The position after the divisor is taken: it and its multiples are gone.</summary>
        public long Take(long mask, int divisor)
        {
            for (int i = 0; i < Divisors.Length; i++) { if (Divisors[i] % divisor == 0) { mask &= ~(1L << i); } }
            return mask;
        }

        /// <summary>The divisors (other than 1) that can be taken.</summary>
        public IEnumerable<int> Moves(long mask) => Divisors.Where(d => d > 1 && Has(mask, d));

        /// <summary>True if the player to move can force a win (the one who must take 1 loses).</summary>
        public bool IsWinning(long mask)
        {
            if (_winning.TryGetValue(mask, out bool known)) { return known; }
            bool result = Moves(mask).Any(d => !IsWinning(Take(mask, d)));
            _winning[mask] = result;
            return result;
        }

        /// <summary>A move that wins if there is one, else any move; 1 only if nothing else is left.</summary>
        public int BestMove(long mask)
        {
            foreach (int d in Moves(mask)) { if (!IsWinning(Take(mask, d))) { return d; } }
            int any = Moves(mask).LastOrDefault();
            return any == 0 ? 1 : any;
        }
    }

    public class DivisorGamePresentation : UserControl
    {
        private static readonly Brush Free = new SolidColorBrush(Color.FromRgb(225, 238, 255));
        private static readonly Brush Gone = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        private static readonly Brush Poison = new SolidColorBrush(Color.FromRgb(240, 190, 190));

        private DivisorPosition _position;
        private long _mask;
        private bool _computerStarts;
        private bool _over;
        private readonly List<string> _log = new List<string>();
        private readonly List<Button> _buttons = new List<Button>();

        public DivisorGamePresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public IList<Button> DivisorButtons => _buttons;

        public DivisorPosition Position => _position;

        public void Start(int number, bool computerStarts)
        {
            Action start = () =>
            {
                number = Math.Max(2, Math.Min(360, number));
                _position = new DivisorPosition(number);
                _computerStarts = computerStarts;
                Restart();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void Restart()
        {
            _mask = _position.Full;
            _over = false;
            _log.Clear();
            if (_computerStarts) { ComputerMove(); }
            Rebuild();
        }

        private void ComputerMove()
        {
            int d = _position.BestMove(_mask);
            if (d == 1)
            {
                _over = true;
                _log.Add(Properties.Resources.ComputerMustTakeOne);
                return;
            }
            _mask = _position.Take(_mask, d);
            _log.Add(string.Format(Properties.Resources.ComputerTakes, d));
        }

        private void Pick(int divisor)
        {
            if (_over || !_position.Has(_mask, divisor)) { return; }
            if (divisor == 1)
            {
                _over = true;
                _log.Add(Properties.Resources.YouTookOne);
            }
            else
            {
                _mask = _position.Take(_mask, divisor);
                _log.Add(string.Format(Properties.Resources.YouTake, divisor));
                ComputerMove();
            }
            Rebuild();
        }

        private void Rebuild()
        {
            _buttons.Clear();
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Title, _position.Number), FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Rules, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            foreach (int d in _position.Divisors)
            {
                int divisor = d;
                bool present = _position.Has(_mask, d);
                var button = new Button
                {
                    Content = d.ToString(), Width = 56, Height = 36, Margin = new Thickness(2), FontSize = 14,
                    Background = !present ? Gone : d == 1 ? Poison : Free, IsEnabled = present && !_over
                };
                button.Click += (sender, e) => Pick(divisor);
                _buttons.Add(button);
                wrap.Children.Add(button);
            }
            panel.Children.Add(wrap);
            foreach (string line in _log.Skip(Math.Max(0, _log.Count - 8))) { panel.Children.Add(new TextBlock { Text = line, FontSize = 12 }); }
            if (_over) { panel.Children.Add(new TextBlock { Text = _log[_log.Count - 1] == Properties.Resources.ComputerMustTakeOne ? Properties.Resources.YouWin : Properties.Resources.YouLose, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 0) }); }
            else if (!_position.Moves(_mask).Any()) { panel.Children.Add(new TextBlock { Text = Properties.Resources.OnlyOneLeft, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) }); }
            var again = new Button { Content = Properties.Resources.NewGame, Width = 120, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
            again.Click += (sender, e) => Restart();
            panel.Children.Add(again);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

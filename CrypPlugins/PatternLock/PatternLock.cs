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
using System.Diagnostics;
using System.Security.Cryptography;

namespace PatternLock
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("PatternLock.Properties.Resources", "PluginCaption", "PluginTooltip", "PatternLock/DetailedDescription/doc.xml", "PatternLock/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class PatternLock : ICrypComponent
    {
        private PatternLockSettings _settings = new PatternLockSettings();
        private PatternLockPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (PatternLockSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new PatternLockPresentation(); }
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
            ((PatternLockPresentation)Presentation).Start(_settings.ShowNumbers);
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
    /// The rules of the Android unlock pattern: the nine points of a 3x3 grid (numbered 0 to 8, row by row) are connected by a path of 4 to 9 different points.
    /// A line that goes over a point that was not visited yet takes this point with it. The pattern is stored as the SHA-1 hash of the point numbers as bytes (no salt).
    /// </summary>
    public static class PatternRules
    {
        public const int MinimumLength = 4;

        /// <summary>The point between two points on a line of the grid (a point that the line goes over), or -1 if there is none.</summary>
        public static int Between(int a, int b)
        {
            int rowDifference = Math.Abs(a / 3 - b / 3), columnDifference = Math.Abs(a % 3 - b % 3);
            if ((rowDifference == 0 || rowDifference == 2) && (columnDifference == 0 || columnDifference == 2) && rowDifference + columnDifference > 0)
            {
                return (a / 3 + b / 3) / 2 * 3 + (a % 3 + b % 3) / 2;
            }
            return -1;
        }

        /// <summary>Calls visit for every valid pattern with a length from min to max (a line never goes over an unvisited point).</summary>
        public static void Walk(int min, int max, Func<int[], bool> visit)
        {
            var path = new int[9];
            var used = new bool[9];
            for (int start = 0; start < 9; start++)
            {
                path[0] = start;
                used[start] = true;
                bool goOn = Extend(path, 1, used, min, max, visit);
                used[start] = false;
                if (!goOn) { return; }
            }
        }

        // false: the visitor wants to stop
        private static bool Extend(int[] path, int length, bool[] used, int min, int max, Func<int[], bool> visit)
        {
            if (length >= min)
            {
                var copy = new int[length];
                Array.Copy(path, copy, length);
                if (!visit(copy)) { return false; }
            }
            if (length == max) { return true; }
            for (int next = 0; next < 9; next++)
            {
                if (used[next]) { continue; }
                int between = Between(path[length - 1], next);
                if (between >= 0 && !used[between]) { continue; }
                path[length] = next;
                used[next] = true;
                bool goOn = Extend(path, length + 1, used, min, max, visit);
                used[next] = false;
                if (!goOn) { return false; }
            }
            return true;
        }

        public static long Count(int length)
        {
            long count = 0;
            Walk(length, length, p => { count++; return true; });
            return count;
        }

        public static byte[] Hash(IEnumerable<int> pattern)
        {
            using (var sha = SHA1.Create()) { return sha.ComputeHash(pattern.Select(p => (byte)p).ToArray()); }
        }

        /// <summary>Tries all patterns until one has the hash value: the attack on a stolen gesture file.</summary>
        public static int[] Crack(byte[] hash, out long tries)
        {
            long counted = 0;
            int[] found = null;
            Walk(MinimumLength, 9, p =>
            {
                counted++;
                if (Hash(p).SequenceEqual(hash)) { found = p; return false; }
                return true;
            });
            tries = counted;
            return found;
        }
    }

    public class PatternLockPresentation : UserControl
    {
        private static readonly Brush Idle = new SolidColorBrush(Color.FromRgb(225, 238, 255));
        private static readonly Brush Visited = new SolidColorBrush(Color.FromRgb(160, 200, 240));

        private readonly List<int> _pattern = new List<int>();
        private bool _showNumbers = true;
        private string _message = "";
        private string _attack = "";
        private bool _started;
        private readonly List<Button> _points = new List<Button>();

        public PatternLockPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public IList<Button> PointButtons => _points;

        public IList<int> Pattern => _pattern;

        public void Start(bool showNumbers)
        {
            Action start = () =>
            {
                _showNumbers = showNumbers;
                _started = true;
                Reset();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void Reset()
        {
            _pattern.Clear();
            _message = Properties.Resources.Rules;
            _attack = "";
            Rebuild();
        }

        private void Pick(int point)
        {
            if (_pattern.Contains(point)) { _message = Properties.Resources.Used; Rebuild(); return; }
            if (_pattern.Count > 0)
            {
                int between = PatternRules.Between(_pattern[_pattern.Count - 1], point);
                if (between >= 0 && !_pattern.Contains(between)) { _pattern.Add(between); }
            }
            _pattern.Add(point);
            _attack = "";
            _message = _pattern.Count < PatternRules.MinimumLength ? string.Format(Properties.Resources.TooShort, PatternRules.MinimumLength) : Properties.Resources.ValidPattern;
            Rebuild();
        }

        private void Crack()
        {
            if (_pattern.Count < PatternRules.MinimumLength) { return; }
            byte[] hash = PatternRules.Hash(_pattern);
            var watch = Stopwatch.StartNew();
            long tries;
            int[] found = PatternRules.Crack(hash, out tries);
            watch.Stop();
            _attack = found == null ? Properties.Resources.NotFound : string.Format(Properties.Resources.Found, string.Join(" ", found.Select(p => p + 1)), tries, watch.ElapsedMilliseconds);
            Rebuild();
        }

        private void Rebuild()
        {
            _points.Clear();
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Title, FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = _message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            var grid = new UniformGrid { Rows = 3, Columns = 3, Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Left };
            for (int p = 0; p < 9; p++)
            {
                int point = p;
                int order = _pattern.IndexOf(p);
                var button = new Button
                {
                    Content = order >= 0 ? (_showNumbers ? (order + 1).ToString() : "O") : "o", FontSize = 22, Margin = new Thickness(6),
                    Background = order >= 0 ? Visited : Idle
                };
                button.Click += (sender, e) => Pick(point);
                _points.Add(button);
                grid.Children.Add(button);
            }
            panel.Children.Add(grid);

            if (_pattern.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Sequence, string.Join(" - ", _pattern.Select(p => p + 1))), FontSize = 13, Margin = new Thickness(0, 8, 0, 0) });
            }
            if (_pattern.Count >= PatternRules.MinimumLength)
            {
                long count = PatternRules.Count(_pattern.Count);
                panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Strength, _pattern.Count, count, Math.Log(389112, 2).ToString("F1")), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.HashLine, BitConverter.ToString(PatternRules.Hash(_pattern)).Replace("-", "")), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            }
            if (_attack != "") { panel.Children.Add(new TextBlock { Text = _attack, FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) }); }
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var crack = new Button { Content = Properties.Resources.CrackButton, Width = 150, Height = 30, Margin = new Thickness(2), IsEnabled = _pattern.Count >= PatternRules.MinimumLength };
            crack.Click += (sender, e) => Crack();
            var reset = new Button { Content = Properties.Resources.Reset, Width = 100, Height = 30, Margin = new Thickness(2) };
            reset.Click += (sender, e) => Reset();
            actions.Children.Add(crack);
            actions.Children.Add(reset);
            panel.Children.Add(actions);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

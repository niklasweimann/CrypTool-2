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
using System.Security.Cryptography;

namespace RainbowTable
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("RainbowTable.Properties.Resources", "PluginCaption", "PluginTooltip", "RainbowTable/DetailedDescription/doc.xml", "RainbowTable/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class RainbowTable : ICrypComponent
    {
        private RainbowTableSettings _settings = new RainbowTableSettings();
        private RainbowTablePresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (RainbowTableSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new RainbowTablePresentation(); }
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
            ((RainbowTablePresentation)Presentation).Start(_settings.Length, _settings.Chains, _settings.ChainLength, _settings.Seed, _settings.Target);
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
    /// A rainbow table (Oechslin, 2003) for passwords of lowercase letters: a chain starts with a password, hashes it, reduces the hash value to a new password
    /// with a reduction function that is different in every column, and so on. Only the first and the last password of every chain are stored. To find the
    /// password of a hash value, the chains are followed from every possible column to the end and the end is looked up in the table.
    /// </summary>
    public sealed class ChainTable
    {
        public int Length { get; }
        public int ChainLength { get; }
        public long Space { get; }
        public List<long> Starts { get; } = new List<long>();
        public List<long> Ends { get; } = new List<long>();

        public ChainTable(int length, int chains, int chainLength, int seed)
        {
            Length = length;
            ChainLength = chainLength;
            Space = (long)Math.Pow(26, length);
            var random = new Random(seed);
            var used = new HashSet<long>();
            while (Starts.Count < Math.Min(chains, Space))
            {
                long start = (long)(random.NextDouble() * Space);
                if (used.Add(start)) { Starts.Add(start); }
            }
            foreach (long start in Starts) { Ends.Add(Walk(start, 1, chainLength)); }
        }

        public string Password(long index)
        {
            var letters = new char[Length];
            for (int i = Length - 1; i >= 0; i--) { letters[i] = (char)('a' + index % 26); index /= 26; }
            return new string(letters);
        }

        public long Index(string password) => password.Aggregate(0L, (value, c) => value * 26 + (c - 'a'));

        /// <summary>The hash function: the first 8 bytes of SHA-256, as a number.</summary>
        public static ulong Hash(string password)
        {
            using (var sha = SHA256.Create()) { return BitConverter.ToUInt64(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(password)), 0); }
        }

        /// <summary>The reduction function of a column: hash value and column number to a password.</summary>
        public long Reduce(ulong hash, int column) => (long)((hash + (ulong)column) % (ulong)Space);

        /// <summary>From the password at the index, in the columns first to last: hash, reduce.</summary>
        public long Walk(long index, int first, int last)
        {
            for (int column = first; column <= last; column++) { index = Reduce(Hash(Password(index)), column); }
            return index;
        }

        /// <summary>The distinct passwords on the chains: the passwords that the table covers.</summary>
        public int Coverage()
        {
            var seen = new HashSet<long>();
            foreach (long start in Starts)
            {
                long index = start;
                seen.Add(index);
                for (int column = 1; column <= ChainLength; column++) { index = Reduce(Hash(Password(index)), column); seen.Add(index); }
            }
            return seen.Count;
        }

        /// <summary>Looks for a password with the hash value; work counts the hash computations.</summary>
        public string Lookup(ulong hash, out long work)
        {
            work = 0;
            for (int column = ChainLength; column >= 1; column--)
            {
                long index = Reduce(hash, column);
                for (int next = column + 1; next <= ChainLength; next++) { index = Reduce(Hash(Password(index)), next); work++; }
                for (int chain = 0; chain < Ends.Count; chain++)
                {
                    if (Ends[chain] != index) { continue; }
                    long candidate = Starts[chain];
                    for (int step = 1; step <= ChainLength; step++)
                    {
                        work++;
                        if (Hash(Password(candidate)) == hash) { return Password(candidate); }
                        candidate = Reduce(Hash(Password(candidate)), step);
                    }
                }
            }
            return null;
        }
    }

    public class RainbowTablePresentation : UserControl
    {
        private ChainTable _table;
        private string _target = "";
        private int _seed;
        private string _test = "";
        private readonly List<Button> _buttons = new List<Button>();

        public RainbowTablePresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public IList<Button> ControlButtons => _buttons;

        public ChainTable Table => _table;

        public void Start(int length, int chains, int chainLength, int seed, string target)
        {
            Action start = () =>
            {
                _table = new ChainTable(Math.Max(3, Math.Min(4, length)), chains, chainLength, seed);
                _target = (target ?? "").Trim().ToLowerInvariant();
                _seed = seed;
                _test = "";
                Rebuild();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void TestRandom()
        {
            var random = new Random(_seed + 1);
            int found = 0;
            long work = 0;
            for (int i = 0; i < 100; i++)
            {
                string password = _table.Password((long)(random.NextDouble() * _table.Space));
                long w;
                string result = _table.Lookup(ChainTable.Hash(password), out w);
                work += w;
                if (result != null) { found++; }
            }
            _test = string.Format(Properties.Resources.TestResult, found, work / 100);
            Rebuild();
        }

        private void Rebuild()
        {
            _buttons.Clear();
            var panel = new StackPanel { Margin = new Thickness(10) };
            Action<string, bool> line = (text, bold) => panel.Children.Add(new TextBlock { Text = text, FontSize = bold ? 14 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, bold ? 10 : 2, 0, 2) });
            line(Properties.Resources.Title, true);
            line(Properties.Resources.Rules, false);
            int covered = _table.Coverage();
            line(string.Format(Properties.Resources.TableInfo, _table.Starts.Count, _table.ChainLength, _table.Space, covered, (100.0 * covered / _table.Space).ToString("F1")), false);
            line(Properties.Resources.ChainsTitle, true);
            for (int i = 0; i < Math.Min(6, _table.Starts.Count); i++)
            {
                string chain = _table.Password(_table.Starts[i]);
                long index = _table.Starts[i];
                for (int column = 1; column <= Math.Min(3, _table.ChainLength); column++)
                {
                    ulong hash = ChainTable.Hash(_table.Password(index));
                    index = _table.Reduce(hash, column);
                    chain += " -H-> " + hash.ToString("X16").Substring(0, 6) + " -R" + column + "-> " + _table.Password(index);
                }
                line(chain + " ... " + _table.Password(_table.Ends[i]), false);
            }
            line(Properties.Resources.LookupTitle, true);
            if (_target.Length != _table.Length || _target.Any(c => c < 'a' || c > 'z')) { line(string.Format(Properties.Resources.BadTarget, _table.Length), false); }
            else
            {
                ulong hash = ChainTable.Hash(_target);
                long work;
                string found = _table.Lookup(hash, out work);
                line(string.Format(Properties.Resources.TargetHash, _target, hash.ToString("X16")), false);
                line(found != null ? string.Format(Properties.Resources.Found, found, work) : string.Format(Properties.Resources.NotFound, work), true);
            }
            if (_test != "") { line(_test, true); }
            var test = new Button { Content = Properties.Resources.TestButton, Width = 220, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            test.Click += (sender, e) => TestRandom();
            _buttons.Add(test);
            panel.Children.Add(test);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}

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
using System.Windows.Shapes;

namespace AntColony
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("AntColony.Properties.Resources", "PluginCaption", "PluginTooltip", "AntColony/DetailedDescription/doc.xml", "AntColony/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class AntColony : ICrypComponent
    {
        private AntColonySettings _settings = new AntColonySettings();
        private AntColonyPresentation _presentation;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (AntColonySettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new AntColonyPresentation(); }
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
            ((AntColonyPresentation)Presentation).Start(_settings.Cities, _settings.Ants, _settings.Alpha, _settings.Beta, _settings.Evaporation, _settings.Seed);
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
    /// The ant system of Dorigo, Maniezzo and Colorni (1991, 1996) for the travelling salesman problem: every ant builds a tour and chooses the next city
    /// with a probability proportional to pheromone^alpha * (1/distance)^beta; then the pheromone evaporates and every ant leaves 1/length on the edges of its tour.
    /// Short tours get more pheromone and so more ants.
    /// </summary>
    public sealed class AntSystem
    {
        public int Count { get; }
        public double[] X { get; }
        public double[] Y { get; }
        public double[,] Distance { get; }
        public double[,] Pheromone { get; }
        public int[] BestTour { get; private set; }
        public double BestLength { get; private set; } = double.MaxValue;
        public int Iterations { get; private set; }
        /// <summary>The length of the tour that always goes to the nearest city (from city 0), for comparison.</summary>
        public double NearestNeighbourLength { get; }

        private readonly int _ants;
        private readonly double _alpha, _beta, _rho;
        private readonly Random _random;

        /// <summary>Cities at random places in the unit square; the same seed gives the same cities and the same run.</summary>
        public AntSystem(int cities, int ants, double alpha, double beta, double evaporation, int seed)
            : this(RandomPoints(cities, seed, 0), RandomPoints(cities, seed, 1), ants, alpha, beta, evaporation, seed)
        {
        }

        public AntSystem(double[] x, double[] y, int ants, double alpha, double beta, double evaporation, int seed)
        {
            Count = x.Length;
            X = x;
            Y = y;
            _ants = ants;
            _alpha = alpha;
            _beta = beta;
            _rho = evaporation;
            _random = new Random(seed * 31 + 5);
            Distance = new double[Count, Count];
            for (int i = 0; i < Count; i++)
            {
                for (int j = 0; j < Count; j++) { Distance[i, j] = Math.Max(1e-9, Math.Sqrt((x[i] - x[j]) * (x[i] - x[j]) + (y[i] - y[j]) * (y[i] - y[j]))); }
            }
            NearestNeighbourLength = NearestNeighbour();
            Pheromone = new double[Count, Count];
            for (int i = 0; i < Count; i++) { for (int j = 0; j < Count; j++) { Pheromone[i, j] = _ants / NearestNeighbourLength; } }
        }

        private static double[] RandomPoints(int count, int seed, int axis)
        {
            var random = new Random(seed);
            var result = new double[2][] { new double[count], new double[count] };
            for (int i = 0; i < count; i++) { result[0][i] = random.NextDouble(); result[1][i] = random.NextDouble(); }
            return result[axis];
        }

        public double Length(int[] tour)
        {
            double sum = 0;
            for (int i = 0; i < tour.Length; i++) { sum += Distance[tour[i], tour[(i + 1) % tour.Length]]; }
            return sum;
        }

        private double NearestNeighbour()
        {
            var visited = new bool[Count];
            int city = 0;
            visited[0] = true;
            double sum = 0;
            for (int step = 1; step < Count; step++)
            {
                int best = -1;
                for (int j = 0; j < Count; j++) { if (!visited[j] && (best < 0 || Distance[city, j] < Distance[city, best])) { best = j; } }
                sum += Distance[city, best];
                visited[best] = true;
                city = best;
            }
            return sum + Distance[city, 0];
        }

        /// <summary>One ant builds a tour from the start city.</summary>
        private int[] BuildTour(int start)
        {
            var tour = new int[Count];
            var visited = new bool[Count];
            tour[0] = start;
            visited[start] = true;
            for (int step = 1; step < Count; step++)
            {
                int city = tour[step - 1];
                var weight = new double[Count];
                double total = 0;
                for (int j = 0; j < Count; j++)
                {
                    if (visited[j]) { continue; }
                    weight[j] = Math.Pow(Pheromone[city, j], _alpha) * Math.Pow(1.0 / Distance[city, j], _beta);
                    total += weight[j];
                }
                double pick = _random.NextDouble() * total;
                int next = -1;
                for (int j = 0; j < Count; j++)
                {
                    if (visited[j]) { continue; }
                    next = j;   // (the last city that is still free also catches rounding errors)
                    pick -= weight[j];
                    if (pick <= 0) { break; }
                }
                tour[step] = next;
                visited[next] = true;
            }
            return tour;
        }

        /// <summary>One round: all ants walk, the pheromone evaporates, then every ant deposits pheromone.</summary>
        public void Iterate()
        {
            var tours = new List<int[]>();
            for (int a = 0; a < _ants; a++) { tours.Add(BuildTour(_random.Next(Count))); }
            for (int i = 0; i < Count; i++) { for (int j = 0; j < Count; j++) { Pheromone[i, j] *= 1 - _rho; } }
            foreach (int[] tour in tours)
            {
                double length = Length(tour);
                for (int i = 0; i < Count; i++)
                {
                    int from = tour[i], to = tour[(i + 1) % Count];
                    Pheromone[from, to] += 1 / length;
                    Pheromone[to, from] += 1 / length;
                }
                if (length < BestLength) { BestLength = length; BestTour = tour; }
            }
            Iterations++;
        }
    }

    public class AntColonyPresentation : UserControl
    {
        private const double Width = 520, Height = 380;

        private AntSystem _system;
        private int _cities, _ants, _alpha, _beta, _evaporation, _seed;
        private readonly List<Button> _buttons = new List<Button>();

        public AntColonyPresentation()
        {
            Background = Brushes.White;
            Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        }

        public IList<Button> ControlButtons => _buttons;

        public AntSystem System => _system;

        public void Start(int cities, int ants, int alpha, int beta, int evaporation, int seed)
        {
            Action start = () =>
            {
                _cities = Math.Max(4, Math.Min(40, cities));
                _ants = Math.Max(1, Math.Min(200, ants));
                _alpha = alpha; _beta = beta; _evaporation = Math.Max(1, Math.Min(99, evaporation)); _seed = seed;
                Restart();
            };
            if (Dispatcher.CheckAccess()) { start(); } else { Dispatcher.BeginInvoke(start); }
        }

        private void Restart()
        {
            _system = new AntSystem(_cities, _ants, _alpha, _beta, _evaporation / 100.0, _seed);
            Rebuild();
        }

        private void Run(int rounds)
        {
            for (int i = 0; i < rounds; i++) { _system.Iterate(); }
            Rebuild();
        }

        private static Button MakeButton(string text, Action click)
        {
            var button = new Button { Content = text, Width = 110, Height = 30, Margin = new Thickness(2) };
            button.Click += (sender, e) => click();
            return button;
        }

        private void Rebuild()
        {
            _buttons.Clear();
            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Title, FontSize = 16, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = Properties.Resources.Rules, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            string best = _system.BestTour == null ? "-" : _system.BestLength.ToString("F3");
            panel.Children.Add(new TextBlock { Text = string.Format(Properties.Resources.Status, _system.Iterations, best, _system.NearestNeighbourLength.ToString("F3")), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });

            var canvas = new Canvas { Width = Width + 40, Height = Height + 40, Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), HorizontalAlignment = HorizontalAlignment.Left };
            double maximum = 0;
            for (int i = 0; i < _system.Count; i++) { for (int j = 0; j < _system.Count; j++) { if (i != j) { maximum = Math.Max(maximum, _system.Pheromone[i, j]); } } }
            for (int i = 0; i < _system.Count; i++)
            {
                for (int j = i + 1; j < _system.Count; j++)
                {
                    double share = _system.Pheromone[i, j] / maximum;
                    if (share < 0.05) { continue; }
                    canvas.Children.Add(new Line { X1 = Px(_system.X[i]), Y1 = Py(_system.Y[i]), X2 = Px(_system.X[j]), Y2 = Py(_system.Y[j]), Stroke = new SolidColorBrush(Color.FromRgb(120, 150, 210)), StrokeThickness = 0.5 + 6 * share * share });
                }
            }
            if (_system.BestTour != null)
            {
                for (int i = 0; i < _system.Count; i++)
                {
                    int a = _system.BestTour[i], b = _system.BestTour[(i + 1) % _system.Count];
                    canvas.Children.Add(new Line { X1 = Px(_system.X[a]), Y1 = Py(_system.Y[a]), X2 = Px(_system.X[b]), Y2 = Py(_system.Y[b]), Stroke = Brushes.Red, StrokeThickness = 2 });
                }
            }
            for (int i = 0; i < _system.Count; i++)
            {
                var city = new Ellipse { Width = 14, Height = 14, Fill = Brushes.Black };
                Canvas.SetLeft(city, Px(_system.X[i]) - 7);
                Canvas.SetTop(city, Py(_system.Y[i]) - 7);
                canvas.Children.Add(city);
            }
            panel.Children.Add(canvas);

            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            _buttons.Add(MakeButton(Properties.Resources.Step, () => Run(1)));
            _buttons.Add(MakeButton(Properties.Resources.Ten, () => Run(10)));
            _buttons.Add(MakeButton(Properties.Resources.Hundred, () => Run(100)));
            _buttons.Add(MakeButton(Properties.Resources.Reset, Restart));
            foreach (var button in _buttons) { row.Children.Add(button); }
            panel.Children.Add(row);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private static double Px(double x) => 20 + x * Width;

        private static double Py(double y) => 20 + y * Height;
    }
}

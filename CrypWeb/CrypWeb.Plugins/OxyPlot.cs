// OxyPlot for the browser build: the part the AudioOutput plugin uses (a Plot with line series of DataPoints, as
// declared in XAML with xmlns:oxy="http://oxyplot.org/wpf").
//
// The plot draws itself through OnRender, which the HTML renderer writes as SVG: one polyline per series, scaled
// to the data range (or to the axes' Minimum/Maximum when given), axes drawn unless IsAxisVisible is false.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using OxyPlot.Axes;

[assembly: XmlnsDefinition("http://oxyplot.org/wpf", "OxyPlot.Wpf")]

namespace OxyPlot
{
    public struct DataPoint
    {
        public DataPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public override string ToString() => X + " " + Y;
    }
}

namespace OxyPlot.Axes
{
    public enum AxisPosition
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        All,
    }
}

namespace OxyPlot.Wpf
{
    /// <summary>An axis of a plot: where it is, whether it is drawn, optionally a fixed range.</summary>
    public abstract class Axis : DependencyObject
    {
        public AxisPosition Position { get; set; } = AxisPosition.Left;

        public bool IsAxisVisible { get; set; } = true;

        public double Minimum { get; set; } = double.NaN;

        public double Maximum { get; set; } = double.NaN;

        public string Title { get; set; }
    }

    public class LinearAxis : Axis { }

    /// <summary>The data shown by a plot; <see cref="ItemsSource"/> holds DataPoints.</summary>
    public abstract class Series : DependencyObject
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(Series), new PropertyMetadata(null));

        public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

        public string Name { get; set; }

        public string Title { get; set; }

        public Color Color { get; set; } = Color.FromRgb(0x4E, 0x9A, 0x06);

        public double StrokeThickness { get; set; } = 2;
    }

    public class LineSeries : Series { }

    /// <summary>A chart of line series; <see cref="InvalidatePlot"/> redraws it with the current data.</summary>
    [ContentProperty(nameof(Series))]
    public class Plot : FrameworkElement
    {
        public Collection<Axis> Axes { get; } = new Collection<Axis>();

        public Collection<Series> Series { get; } = new Collection<Series>();

        public string Title { get; set; }

        public void InvalidatePlot(bool updateData = true) => RenderVersion.Bump();

        protected override void OnRender(DrawingContext drawingContext)
        {
            double width = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 400 : Width);
            double height = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 200 : Height);
            const double margin = 8;

            // the data of every series, copied (plugins add points from their own thread)
            var data = new List<(Series Series, DataPoint[] Points)>();
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (Series series in Series)
            {
                DataPoint[] points = Snapshot(ItemsOf(series));
                data.Add((series, points));
                foreach (DataPoint point in points)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }
            if (minX > maxX)
            {
                return;     // no data
            }
            foreach (Axis axis in Axes)
            {
                bool horizontal = axis.Position == AxisPosition.Bottom || axis.Position == AxisPosition.Top;
                if (!double.IsNaN(axis.Minimum)) { if (horizontal) minX = axis.Minimum; else minY = axis.Minimum; }
                if (!double.IsNaN(axis.Maximum)) { if (horizontal) maxX = axis.Maximum; else maxY = axis.Maximum; }
            }
            if (maxX - minX < 1e-12) maxX = minX + 1;
            if (maxY - minY < 1e-12) { maxY += 1; minY -= 1; }

            Point ToScreen(DataPoint p) => new Point(
                margin + (p.X - minX) / (maxX - minX) * (width - 2 * margin),
                height - margin - (p.Y - minY) / (maxY - minY) * (height - 2 * margin));

            var axisPen = new Pen(Brushes.Black, 1);
            foreach (Axis axis in Axes)
            {
                if (!axis.IsAxisVisible) continue;
                if (axis.Position == AxisPosition.Bottom) drawingContext.DrawLine(axisPen, new Point(margin, height - margin), new Point(width - margin, height - margin));
                if (axis.Position == AxisPosition.Left) drawingContext.DrawLine(axisPen, new Point(margin, margin), new Point(margin, height - margin));
            }

            foreach (var (series, points) in data)
            {
                if (points.Length < 2) continue;
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(ToScreen(points[0]), false, false);
                    for (int i = 1; i < points.Length; i++)
                    {
                        context.LineTo(ToScreen(points[i]), true, false);
                    }
                }
                drawingContext.DrawGeometry(null, new Pen(new SolidColorBrush(series.Color), series.StrokeThickness), geometry);
            }
        }

        /// <summary>The items of a series: its ItemsSource, or what its binding names on this plot's DataContext.</summary>
        private IEnumerable ItemsOf(Series series)
        {
            if (series.ItemsSource != null)
            {
                return series.ItemsSource;
            }
            string path = BindingOperations.GetBindingExpression(series, OxyPlot.Wpf.Series.ItemsSourceProperty)?.ParentBinding?.Path?.Path;
            object source = DataContext;
            if (string.IsNullOrEmpty(path) || source == null)
            {
                return null;
            }
            return source.GetType().GetProperty(path)?.GetValue(source) as IEnumerable;
        }

        private static DataPoint[] Snapshot(IEnumerable items)
        {
            if (items == null)
            {
                return Array.Empty<DataPoint>();
            }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var points = new List<DataPoint>();
                    foreach (object item in items)
                    {
                        if (item is DataPoint point) points.Add(point);
                    }
                    return points.ToArray();
                }
                catch (InvalidOperationException)
                {
                    // the list was changed while it was read: read it again
                }
            }
            return Array.Empty<DataPoint>();
        }
    }
}

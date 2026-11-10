using System.Windows.Media;

namespace System.Windows.Shapes
{
    /// <summary>A drawn figure with a fill and an outline. Subclasses provide the outline as a geometry.</summary>
    public abstract class Shape : FrameworkElement
    {
        public static readonly DependencyProperty FillProperty = Dp.Register<Brush, Shape>(nameof(Fill));
        public static readonly DependencyProperty StrokeProperty = Dp.Register<Brush, Shape>(nameof(Stroke));
        public static readonly DependencyProperty StrokeThicknessProperty = Dp.Register<double, Shape>(nameof(StrokeThickness), 1.0);
        public static readonly DependencyProperty StretchProperty = Dp.Register<Stretch, Shape>(nameof(Stretch), Stretch.None);
        public static readonly DependencyProperty StrokeDashArrayProperty = Dp.Register<DoubleCollection, Shape>(nameof(StrokeDashArray));
        public static readonly DependencyProperty StrokeDashOffsetProperty = Dp.Register<double, Shape>(nameof(StrokeDashOffset));
        public static readonly DependencyProperty StrokeStartLineCapProperty = Dp.Register<PenLineCap, Shape>(nameof(StrokeStartLineCap));
        public static readonly DependencyProperty StrokeEndLineCapProperty = Dp.Register<PenLineCap, Shape>(nameof(StrokeEndLineCap));
        public static readonly DependencyProperty StrokeDashCapProperty = Dp.Register<PenLineCap, Shape>(nameof(StrokeDashCap));
        public static readonly DependencyProperty StrokeLineJoinProperty = Dp.Register<PenLineJoin, Shape>(nameof(StrokeLineJoin));
        public static readonly DependencyProperty StrokeMiterLimitProperty = Dp.Register<double, Shape>(nameof(StrokeMiterLimit), 10.0);

        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public DoubleCollection StrokeDashArray { get => (DoubleCollection)GetValue(StrokeDashArrayProperty); set => SetValue(StrokeDashArrayProperty, value); }
        public double StrokeDashOffset { get => (double)GetValue(StrokeDashOffsetProperty); set => SetValue(StrokeDashOffsetProperty, value); }
        public PenLineCap StrokeStartLineCap { get => (PenLineCap)GetValue(StrokeStartLineCapProperty); set => SetValue(StrokeStartLineCapProperty, value); }
        public PenLineCap StrokeEndLineCap { get => (PenLineCap)GetValue(StrokeEndLineCapProperty); set => SetValue(StrokeEndLineCapProperty, value); }
        public PenLineCap StrokeDashCap { get => (PenLineCap)GetValue(StrokeDashCapProperty); set => SetValue(StrokeDashCapProperty, value); }
        public PenLineJoin StrokeLineJoin { get => (PenLineJoin)GetValue(StrokeLineJoinProperty); set => SetValue(StrokeLineJoinProperty, value); }
        public double StrokeMiterLimit { get => (double)GetValue(StrokeMiterLimitProperty); set => SetValue(StrokeMiterLimitProperty, value); }

        /// <summary>The outline of the shape; custom shapes override this.</summary>
        protected abstract Geometry DefiningGeometry { get; }

        internal Geometry Outline => DefiningGeometry;

        public Geometry RenderedGeometry => DefiningGeometry;
    }

    public sealed class Rectangle : Shape
    {
        public static readonly DependencyProperty RadiusXProperty = Dp.Register<double, Rectangle>(nameof(RadiusX));
        public static readonly DependencyProperty RadiusYProperty = Dp.Register<double, Rectangle>(nameof(RadiusY));

        public double RadiusX { get => (double)GetValue(RadiusXProperty); set => SetValue(RadiusXProperty, value); }
        public double RadiusY { get => (double)GetValue(RadiusYProperty); set => SetValue(RadiusYProperty, value); }

        protected override Geometry DefiningGeometry => new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), RadiusX, RadiusY);
    }

    public sealed class Ellipse : Shape
    {
        protected override Geometry DefiningGeometry => new EllipseGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
    }

    public sealed class Line : Shape
    {
        public static readonly DependencyProperty X1Property = Dp.Register<double, Line>(nameof(X1));
        public static readonly DependencyProperty Y1Property = Dp.Register<double, Line>(nameof(Y1));
        public static readonly DependencyProperty X2Property = Dp.Register<double, Line>(nameof(X2));
        public static readonly DependencyProperty Y2Property = Dp.Register<double, Line>(nameof(Y2));

        public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
        public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
        public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
        public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

        protected override Geometry DefiningGeometry => new LineGeometry(new Point(X1, Y1), new Point(X2, Y2));
    }

    public sealed class Path : Shape
    {
        public static readonly DependencyProperty DataProperty = Dp.Register<Geometry, Path>(nameof(Data));

        public Geometry Data { get => (Geometry)GetValue(DataProperty); set => SetValue(DataProperty, value); }

        protected override Geometry DefiningGeometry => Data ?? Geometry.Empty;
    }

    public sealed class Polygon : Shape
    {
        public static readonly DependencyProperty PointsProperty = Dp.Register<PointCollection, Polygon>(nameof(Points));
        public static readonly DependencyProperty FillRuleProperty = Dp.Register<FillRule, Polygon>(nameof(FillRule));

        public Polygon() => Points = new PointCollection();

        public PointCollection Points { get => (PointCollection)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
        public FillRule FillRule { get => (FillRule)GetValue(FillRuleProperty); set => SetValue(FillRuleProperty, value); }

        protected override Geometry DefiningGeometry => PolyGeometry.Create(Points, closed: true, FillRule);
    }

    public sealed class Polyline : Shape
    {
        public static readonly DependencyProperty PointsProperty = Dp.Register<PointCollection, Polyline>(nameof(Points));
        public static readonly DependencyProperty FillRuleProperty = Dp.Register<FillRule, Polyline>(nameof(FillRule));

        public Polyline() => Points = new PointCollection();

        public PointCollection Points { get => (PointCollection)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
        public FillRule FillRule { get => (FillRule)GetValue(FillRuleProperty); set => SetValue(FillRuleProperty, value); }

        protected override Geometry DefiningGeometry => PolyGeometry.Create(Points, closed: false, FillRule);
    }

    internal static class PolyGeometry
    {
        public static Geometry Create(PointCollection points, bool closed, FillRule fillRule)
        {
            var geometry = new PathGeometry { FillRule = fillRule };
            if (points != null && points.Count > 0)
            {
                var figure = new PathFigure { StartPoint = points[0], IsClosed = closed };
                for (int i = 1; i < points.Count; i++)
                {
                    figure.Segments.Add(new LineSegment(points[i], true));
                }
                geometry.Figures.Add(figure);
            }
            return geometry;
        }
    }
}

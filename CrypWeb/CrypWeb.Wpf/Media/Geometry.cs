using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Media.Animation;

namespace System.Windows.Media
{
    /// <summary>A shape outline. Every geometry can describe itself as SVG path data, which is how it is drawn.</summary>
    public abstract class Geometry : Animatable
    {
        public Transform Transform { get; set; }

        public static Geometry Empty => new StreamGeometry();

        /// <summary>Parses the path mini language ("M 0,0 L 10,10 Z"); SVG uses the same syntax.</summary>
        public static Geometry Parse(string source) => StreamGeometry.FromPathData(source);

        /// <summary>The outline as SVG path data.</summary>
        internal abstract string ToSvgPathData();

        internal virtual FillRule EffectiveFillRule => FillRule.Nonzero;

        public virtual Rect Bounds => default;

        public bool IsEmpty() => string.IsNullOrWhiteSpace(ToSvgPathData());

        public new Geometry Clone() => (Geometry)MemberwiseClone();

        public PathGeometry GetFlattenedPathGeometry() => new PathGeometry { Figures = new PathFigureCollection { RawData = ToSvgPathData() } };

        internal static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        internal static string Format(Point point) => Format(point.X) + "," + Format(point.Y);
    }

    /// <summary>A geometry written through a <see cref="StreamGeometryContext"/> or given as path data text.</summary>
    public sealed class StreamGeometry : Geometry
    {
        private string _pathData = "";

        public FillRule FillRule { get; set; } = FillRule.EvenOdd;

        internal override FillRule EffectiveFillRule => FillRule;

        public StreamGeometryContext Open() => new StreamGeometryContext(data => { _pathData = data; RenderVersion.Bump(); });

        public void Clear() => _pathData = "";

        internal static StreamGeometry FromPathData(string source)
        {
            source = (source ?? "").Trim();
            var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
            // a leading "F0"/"F1" selects the fill rule
            if (source.Length >= 2 && source[0] == 'F' && (source[1] == '0' || source[1] == '1'))
            {
                geometry.FillRule = source[1] == '1' ? FillRule.Nonzero : FillRule.EvenOdd;
                source = source.Substring(2).TrimStart();
            }
            geometry._pathData = source;
            return geometry;
        }

        internal override string ToSvgPathData() => _pathData;
    }

    /// <summary>Records drawing commands as SVG path data.</summary>
    public sealed class StreamGeometryContext : IDisposable
    {
        private readonly StringBuilder _data = new StringBuilder();
        private readonly Action<string> _commit;
        private bool _figureClosed;
        private bool _figureOpen;

        internal StreamGeometryContext(Action<string> commit) => _commit = commit;

        public void BeginFigure(Point startPoint, bool isFilled, bool isClosed)
        {
            EndFigure();
            _data.Append('M').Append(Geometry.Format(startPoint));
            _figureOpen = true;
            _figureClosed = isClosed;
        }

        public void LineTo(Point point, bool isStroked, bool isSmoothJoin)
        {
            _data.Append('L').Append(Geometry.Format(point));
        }

        public void PolyLineTo(IList<Point> points, bool isStroked, bool isSmoothJoin)
        {
            foreach (Point point in points)
            {
                LineTo(point, isStroked, isSmoothJoin);
            }
        }

        public void BezierTo(Point point1, Point point2, Point point3, bool isStroked, bool isSmoothJoin)
        {
            _data.Append('C').Append(Geometry.Format(point1)).Append(' ').Append(Geometry.Format(point2)).Append(' ').Append(Geometry.Format(point3));
        }

        public void QuadraticBezierTo(Point point1, Point point2, bool isStroked, bool isSmoothJoin)
        {
            _data.Append('Q').Append(Geometry.Format(point1)).Append(' ').Append(Geometry.Format(point2));
        }

        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked, bool isSmoothJoin)
        {
            _data.Append(PathSegment.Arc(point, size, rotationAngle, isLargeArc, sweepDirection));
        }

        public void Close()
        {
            EndFigure();
            _commit(_data.ToString());
        }

        public void Dispose() => Close();

        private void EndFigure()
        {
            if (_figureOpen && _figureClosed)
            {
                _data.Append('Z');
            }
            _figureOpen = false;
        }
    }

    public abstract class PathSegment : Animatable
    {
        public bool IsStroked { get; set; } = true;
        public bool IsSmoothJoin { get; set; }

        internal abstract string ToSvg();

        internal static string Arc(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection)
        {
            return "A" + Geometry.Format(size.Width) + "," + Geometry.Format(size.Height) + " " + Geometry.Format(rotationAngle) + " "
                + (isLargeArc ? "1" : "0") + " " + (sweepDirection == SweepDirection.Clockwise ? "1" : "0") + " " + Geometry.Format(point);
        }

        internal static string PointList(char command, IEnumerable<Point> points)
        {
            var text = new StringBuilder().Append(command);
            foreach (Point point in points)
            {
                text.Append(Geometry.Format(point)).Append(' ');
            }
            return text.ToString();
        }
    }

    public sealed class LineSegment : PathSegment
    {
        public LineSegment() { }

        public LineSegment(Point point, bool isStroked)
        {
            Point = point;
            IsStroked = isStroked;
        }

        public Point Point { get; set; }

        internal override string ToSvg() => "L" + Geometry.Format(Point);
    }

    public sealed class PolyLineSegment : PathSegment
    {
        public PolyLineSegment() { }

        public PolyLineSegment(IEnumerable<Point> points, bool isStroked)
        {
            Points = new PointCollection(points);
            IsStroked = isStroked;
        }

        public PointCollection Points { get; set; } = new PointCollection();

        internal override string ToSvg() => Points.Count == 0 ? "" : PointList('L', Points);
    }

    public sealed class BezierSegment : PathSegment
    {
        public BezierSegment() { }

        public BezierSegment(Point point1, Point point2, Point point3, bool isStroked)
        {
            Point1 = point1;
            Point2 = point2;
            Point3 = point3;
            IsStroked = isStroked;
        }

        public Point Point1 { get; set; }
        public Point Point2 { get; set; }
        public Point Point3 { get; set; }

        internal override string ToSvg() => PointList('C', new[] { Point1, Point2, Point3 });
    }

    public sealed class PolyBezierSegment : PathSegment
    {
        public PointCollection Points { get; set; } = new PointCollection();

        internal override string ToSvg() => Points.Count == 0 ? "" : PointList('C', Points);
    }

    public sealed class QuadraticBezierSegment : PathSegment
    {
        public QuadraticBezierSegment() { }

        public QuadraticBezierSegment(Point point1, Point point2, bool isStroked)
        {
            Point1 = point1;
            Point2 = point2;
            IsStroked = isStroked;
        }

        public Point Point1 { get; set; }
        public Point Point2 { get; set; }

        internal override string ToSvg() => PointList('Q', new[] { Point1, Point2 });
    }

    public sealed class ArcSegment : PathSegment
    {
        public ArcSegment() { }

        public ArcSegment(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked)
        {
            Point = point;
            Size = size;
            RotationAngle = rotationAngle;
            IsLargeArc = isLargeArc;
            SweepDirection = sweepDirection;
            IsStroked = isStroked;
        }

        public Point Point { get; set; }
        public Size Size { get; set; }
        public double RotationAngle { get; set; }
        public bool IsLargeArc { get; set; }
        public SweepDirection SweepDirection { get; set; }

        internal override string ToSvg() => Arc(Point, Size, RotationAngle, IsLargeArc, SweepDirection);
    }

    public class PathSegmentCollection : Collection<PathSegment> { }

    [Markup.ContentProperty(nameof(Segments))]
    public sealed class PathFigure : Animatable
    {
        public PathFigure() { }

        public PathFigure(Point start, IEnumerable<PathSegment> segments, bool closed)
        {
            StartPoint = start;
            IsClosed = closed;
            foreach (PathSegment segment in segments)
            {
                Segments.Add(segment);
            }
        }

        public Point StartPoint { get; set; }
        public PathSegmentCollection Segments { get; set; } = new PathSegmentCollection();
        public bool IsClosed { get; set; }
        public bool IsFilled { get; set; } = true;

        internal string ToSvg()
        {
            var data = new StringBuilder("M").Append(Geometry.Format(StartPoint));
            foreach (PathSegment segment in Segments)
            {
                data.Append(segment.ToSvg());
            }
            if (IsClosed)
            {
                data.Append('Z');
            }
            return data.ToString();
        }
    }

    public class PathFigureCollection : Collection<PathFigure>
    {
        /// <summary>Figures given as path data text (XAML attribute <c>Figures="M ..."</c>).</summary>
        internal string RawData { get; set; }

        public static PathFigureCollection Parse(string source) => new PathFigureCollection { RawData = source };
    }

    [Markup.ContentProperty(nameof(Figures))]
    public sealed class PathGeometry : Geometry
    {
        public PathGeometry() { }

        public PathGeometry(IEnumerable<PathFigure> figures)
        {
            foreach (PathFigure figure in figures)
            {
                Figures.Add(figure);
            }
        }

        public PathFigureCollection Figures { get; set; } = new PathFigureCollection();
        public FillRule FillRule { get; set; } = FillRule.EvenOdd;

        internal override FillRule EffectiveFillRule => FillRule;

        public void Clear() => Figures.Clear();

        public void AddGeometry(Geometry geometry)
        {
            Figures.RawData = (Figures.RawData ?? "") + " " + geometry?.ToSvgPathData();
        }

        internal override string ToSvgPathData()
        {
            var data = new StringBuilder(Figures.RawData ?? "");
            foreach (PathFigure figure in Figures)
            {
                data.Append(figure.ToSvg());
            }
            return data.ToString();
        }
    }

    public sealed class EllipseGeometry : Geometry
    {
        public EllipseGeometry() { }

        public EllipseGeometry(Point center, double radiusX, double radiusY)
        {
            Center = center;
            RadiusX = radiusX;
            RadiusY = radiusY;
        }

        public EllipseGeometry(Rect rect) : this(new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2) { }

        public Point Center { get; set; }
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }

        public override Rect Bounds => new Rect(Center.X - RadiusX, Center.Y - RadiusY, 2 * RadiusX, 2 * RadiusY);

        internal override string ToSvgPathData()
        {
            string radii = Format(RadiusX) + "," + Format(RadiusY);
            return "M" + Format(Center.X - RadiusX) + "," + Format(Center.Y)
                + "a" + radii + " 0 1,0 " + Format(2 * RadiusX) + ",0"
                + "a" + radii + " 0 1,0 " + Format(-2 * RadiusX) + ",0Z";
        }
    }

    public sealed class RectangleGeometry : Geometry
    {
        public RectangleGeometry() { }

        public RectangleGeometry(Rect rect) => Rect = rect;

        public RectangleGeometry(Rect rect, double radiusX, double radiusY)
        {
            Rect = rect;
            RadiusX = radiusX;
            RadiusY = radiusY;
        }

        public Rect Rect { get; set; }
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }

        public override Rect Bounds => Rect;

        internal override string ToSvgPathData()
        {
            return "M" + Format(Rect.X) + "," + Format(Rect.Y) + "h" + Format(Rect.Width) + "v" + Format(Rect.Height) + "h" + Format(-Rect.Width) + "Z";
        }
    }

    public sealed class LineGeometry : Geometry
    {
        public LineGeometry() { }

        public LineGeometry(Point startPoint, Point endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        internal override string ToSvgPathData() => "M" + Format(StartPoint) + "L" + Format(EndPoint);
    }

    public class GeometryCollection : Collection<Geometry> { }

    [Markup.ContentProperty(nameof(Children))]
    public sealed class GeometryGroup : Geometry
    {
        public GeometryCollection Children { get; set; } = new GeometryCollection();
        public FillRule FillRule { get; set; } = FillRule.EvenOdd;

        internal override FillRule EffectiveFillRule => FillRule;

        internal override string ToSvgPathData()
        {
            var data = new StringBuilder();
            foreach (Geometry child in Children)
            {
                data.Append(child.ToSvgPathData()).Append(' ');
            }
            return data.ToString();
        }
    }

    public enum GeometryCombineMode { Union, Intersect, Xor, Exclude }

    /// <summary>Two geometries combined. Only the union is exact; the other modes draw both outlines with the even-odd rule.</summary>
    public sealed class CombinedGeometry : Geometry
    {
        public CombinedGeometry() { }

        public CombinedGeometry(GeometryCombineMode mode, Geometry geometry1, Geometry geometry2)
        {
            GeometryCombineMode = mode;
            Geometry1 = geometry1;
            Geometry2 = geometry2;
        }

        public Geometry Geometry1 { get; set; }
        public Geometry Geometry2 { get; set; }
        public GeometryCombineMode GeometryCombineMode { get; set; }

        internal override FillRule EffectiveFillRule => GeometryCombineMode == GeometryCombineMode.Union ? FillRule.Nonzero : FillRule.EvenOdd;

        internal override string ToSvgPathData() => Geometry1?.ToSvgPathData() + " " + Geometry2?.ToSvgPathData();
    }
}

using System.Collections.Generic;
using System.Text;

namespace System.Windows.Media
{
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
            _data.Append('C')
                .Append(Geometry.Format(point1))
                .Append(' ')
                .Append(Geometry.Format(point2))
                .Append(' ')
                .Append(Geometry.Format(point3));
        }

        public void QuadraticBezierTo(Point point1, Point point2, bool isStroked, bool isSmoothJoin)
        {
            _data.Append('Q').Append(Geometry.Format(point1)).Append(' ').Append(Geometry.Format(point2));
        }

        public void ArcTo(Point point,
            Size size,
            double rotationAngle,
            bool isLargeArc,
            SweepDirection sweepDirection,
            bool isStroked,
            bool isSmoothJoin)
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
}

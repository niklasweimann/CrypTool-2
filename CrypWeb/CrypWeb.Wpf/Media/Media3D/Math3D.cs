// Points, vectors and matrices of WPF 3D (System.Windows.Media.Media3D).
//
// WPF multiplies row vectors with matrices: a point p is transformed as p * M, and the translation is in the
// fourth row (OffsetX, OffsetY, OffsetZ). Combining transforms A then B is A * B. The types here follow that.
using System.Collections.ObjectModel;
using System.Globalization;

namespace System.Windows.Media.Media3D
{
    public struct Point3D
    {
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static Point3D operator +(Point3D point, Vector3D vector) => new Point3D(point.X + vector.X,
            point.Y + vector.Y,
            point.Z + vector.Z);

        public static Point3D operator -(Point3D point, Vector3D vector) => new Point3D(point.X - vector.X,
            point.Y - vector.Y,
            point.Z - vector.Z);

        public static Vector3D operator -(Point3D point1, Point3D point2) => new Vector3D(point1.X - point2.X,
            point1.Y - point2.Y,
            point1.Z - point2.Z);

        public static explicit operator Vector3D(Point3D point) => new Vector3D(point.X, point.Y, point.Z);

        public static bool operator ==(Point3D a, Point3D b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        public static bool operator !=(Point3D a, Point3D b) => !(a == b);

        public void Offset(double offsetX, double offsetY, double offsetZ)
        {
            X += offsetX;
            Y += offsetY;
            Z += offsetZ;
        }

        public override bool Equals(object obj) => obj is Point3D other && this == other;

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", X, Y, Z);

        public static Point3D Parse(string source)
        {
            double[] n = Math3DParser.Numbers(source, 3);
            return new Point3D(n[0], n[1], n[2]);
        }
    }

    /// <summary>A rotation as a unit quaternion.</summary>
    public struct Quaternion
    {
        public Quaternion(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>A rotation by <paramref name="angleInDegrees"/> about <paramref name="axisOfRotation"/>.</summary>
        public Quaternion(Vector3D axisOfRotation, double angleInDegrees)
        {
            Vector3D axis = axisOfRotation;
            axis.Normalize();
            double half = angleInDegrees * Math.PI / 360;
            double sin = Math.Sin(half);
            X = axis.X * sin;
            Y = axis.Y * sin;
            Z = axis.Z * sin;
            W = Math.Cos(half);
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }

        public static Quaternion Identity => new Quaternion(0, 0, 0, 1);

        /// <summary>The rotation matrix (for row vectors).</summary>
        internal Matrix3D ToMatrix()
        {
            double x2 = X + X, y2 = Y + Y, z2 = Z + Z;
            double xx = X * x2, xy = X * y2, xz = X * z2;
            double yy = Y * y2, yz = Y * z2, zz = Z * z2;
            double wx = W * x2, wy = W * y2, wz = W * z2;
            return new Matrix3D(
                1 - (yy + zz), xy + wz, xz - wy, 0,
                xy - wz, 1 - (xx + zz), yz + wx, 0,
                xz + wy, yz - wx, 1 - (xx + yy), 0,
                0, 0, 0, 1);
        }
    }

    public sealed class Point3DCollection : Collection<Point3D>
    {
        public Point3DCollection() { }

        public Point3DCollection(System.Collections.Generic.IEnumerable<Point3D> points)
        {
            foreach (Point3D point in points)
            {
                Add(point);
            }
        }

        public static Point3DCollection Parse(string source)
        {
            double[] n = Math3DParser.Numbers(source, -1);
            var result = new Point3DCollection();
            for (int i = 0; i + 2 < n.Length; i += 3)
            {
                result.Add(new Point3D(n[i], n[i + 1], n[i + 2]));
            }
            return result;
        }
    }

    public sealed class Vector3DCollection : Collection<Vector3D>
    {
        public static Vector3DCollection Parse(string source)
        {
            double[] n = Math3DParser.Numbers(source, -1);
            var result = new Vector3DCollection();
            for (int i = 0; i + 2 < n.Length; i += 3)
            {
                result.Add(new Vector3D(n[i], n[i + 1], n[i + 2]));
            }
            return result;
        }
    }

    internal static class Math3DParser
    {
        /// <summary>The numbers of "1,2,3" or "1 2 3"; <paramref name="count"/> -1 means any number.</summary>
        internal static double[] Numbers(string source, int count)
        {
            string[] parts = (source ?? "").Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (count >= 0 && parts.Length != count)
            {
                throw new FormatException("Expected " + count + " numbers: '" + source + "'.");
            }
            var numbers = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                numbers[i] = double.Parse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            return numbers;
        }
    }
}

namespace System.Windows.Media
{
    public sealed class Int32Collection : Collection<int>
    {
        public Int32Collection() { }

        public Int32Collection(System.Collections.Generic.IEnumerable<int> values)
        {
            foreach (int value in values)
            {
                Add(value);
            }
        }

        public static Int32Collection Parse(string source)
        {
            var result = new Int32Collection();
            foreach (string part in (source ?? "").Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(int.Parse(part, CultureInfo.InvariantCulture));
            }
            return result;
        }
    }
}

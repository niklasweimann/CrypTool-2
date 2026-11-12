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

        public static Point3D operator +(Point3D point, Vector3D vector) => new Point3D(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

        public static Point3D operator -(Point3D point, Vector3D vector) => new Point3D(point.X - vector.X, point.Y - vector.Y, point.Z - vector.Z);

        public static Vector3D operator -(Point3D point1, Point3D point2) => new Vector3D(point1.X - point2.X, point1.Y - point2.Y, point1.Z - point2.Z);

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

    public struct Vector3D
    {
        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double Length => Math.Sqrt(LengthSquared);

        public double LengthSquared => X * X + Y * Y + Z * Z;

        public void Normalize()
        {
            double length = Length;
            if (length > 0)
            {
                X /= length;
                Y /= length;
                Z /= length;
            }
        }

        public void Negate()
        {
            X = -X;
            Y = -Y;
            Z = -Z;
        }

        public static double DotProduct(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3D CrossProduct(Vector3D a, Vector3D b) =>
            new Vector3D(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static double AngleBetween(Vector3D a, Vector3D b)
        {
            double cos = DotProduct(a, b) / (a.Length * b.Length);
            return Math.Acos(Math.Clamp(cos, -1, 1)) * 180 / Math.PI;
        }

        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3D operator -(Vector3D a) => new Vector3D(-a.X, -a.Y, -a.Z);

        public static Vector3D operator *(Vector3D a, double s) => new Vector3D(a.X * s, a.Y * s, a.Z * s);

        public static Vector3D operator *(double s, Vector3D a) => a * s;

        public static Vector3D operator /(Vector3D a, double s) => new Vector3D(a.X / s, a.Y / s, a.Z / s);

        public static bool operator ==(Vector3D a, Vector3D b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        public static bool operator !=(Vector3D a, Vector3D b) => !(a == b);

        public static explicit operator Point3D(Vector3D vector) => new Point3D(vector.X, vector.Y, vector.Z);

        public override bool Equals(object obj) => obj is Vector3D other && this == other;

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", X, Y, Z);

        public static Vector3D Parse(string source)
        {
            double[] n = Math3DParser.Numbers(source, 3);
            return new Vector3D(n[0], n[1], n[2]);
        }
    }

    /// <summary>A 4x4 matrix for row vectors (see the file comment).</summary>
    public struct Matrix3D
    {
        private bool _initialized;
        private double _m11, _m12, _m13, _m14, _m21, _m22, _m23, _m24, _m31, _m32, _m33, _m34, _offsetX, _offsetY, _offsetZ, _m44;

        public Matrix3D(double m11, double m12, double m13, double m14, double m21, double m22, double m23, double m24,
            double m31, double m32, double m33, double m34, double offsetX, double offsetY, double offsetZ, double m44)
        {
            _initialized = true;
            (_m11, _m12, _m13, _m14) = (m11, m12, m13, m14);
            (_m21, _m22, _m23, _m24) = (m21, m22, m23, m24);
            (_m31, _m32, _m33, _m34) = (m31, m32, m33, m34);
            (_offsetX, _offsetY, _offsetZ, _m44) = (offsetX, offsetY, offsetZ, m44);
        }

        public static Matrix3D Identity => new Matrix3D(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

        // default(Matrix3D) is the identity, as in WPF
        private Matrix3D Value => _initialized ? this : Identity;

        public double M11 { get => Value._m11; set => Set(ref _m11, value); }
        public double M12 { get => Value._m12; set => Set(ref _m12, value); }
        public double M13 { get => Value._m13; set => Set(ref _m13, value); }
        public double M14 { get => Value._m14; set => Set(ref _m14, value); }
        public double M21 { get => Value._m21; set => Set(ref _m21, value); }
        public double M22 { get => Value._m22; set => Set(ref _m22, value); }
        public double M23 { get => Value._m23; set => Set(ref _m23, value); }
        public double M24 { get => Value._m24; set => Set(ref _m24, value); }
        public double M31 { get => Value._m31; set => Set(ref _m31, value); }
        public double M32 { get => Value._m32; set => Set(ref _m32, value); }
        public double M33 { get => Value._m33; set => Set(ref _m33, value); }
        public double M34 { get => Value._m34; set => Set(ref _m34, value); }
        public double OffsetX { get => Value._offsetX; set => Set(ref _offsetX, value); }
        public double OffsetY { get => Value._offsetY; set => Set(ref _offsetY, value); }
        public double OffsetZ { get => Value._offsetZ; set => Set(ref _offsetZ, value); }
        public double M44 { get => Value._m44; set => Set(ref _m44, value); }

        public bool IsIdentity => this == Identity;

        private void Set(ref double field, double value)
        {
            if (!_initialized)
            {
                this = Identity;
            }
            field = value;
        }

        public static Matrix3D operator *(Matrix3D left, Matrix3D right)
        {
            // fields of initialized copies: the properties would copy the whole matrix on every access
            Matrix3D a = left.Value, b = right.Value;
            return new Matrix3D(
                a._m11 * b._m11 + a._m12 * b._m21 + a._m13 * b._m31 + a._m14 * b._offsetX,
                a._m11 * b._m12 + a._m12 * b._m22 + a._m13 * b._m32 + a._m14 * b._offsetY,
                a._m11 * b._m13 + a._m12 * b._m23 + a._m13 * b._m33 + a._m14 * b._offsetZ,
                a._m11 * b._m14 + a._m12 * b._m24 + a._m13 * b._m34 + a._m14 * b._m44,
                a._m21 * b._m11 + a._m22 * b._m21 + a._m23 * b._m31 + a._m24 * b._offsetX,
                a._m21 * b._m12 + a._m22 * b._m22 + a._m23 * b._m32 + a._m24 * b._offsetY,
                a._m21 * b._m13 + a._m22 * b._m23 + a._m23 * b._m33 + a._m24 * b._offsetZ,
                a._m21 * b._m14 + a._m22 * b._m24 + a._m23 * b._m34 + a._m24 * b._m44,
                a._m31 * b._m11 + a._m32 * b._m21 + a._m33 * b._m31 + a._m34 * b._offsetX,
                a._m31 * b._m12 + a._m32 * b._m22 + a._m33 * b._m32 + a._m34 * b._offsetY,
                a._m31 * b._m13 + a._m32 * b._m23 + a._m33 * b._m33 + a._m34 * b._offsetZ,
                a._m31 * b._m14 + a._m32 * b._m24 + a._m33 * b._m34 + a._m34 * b._m44,
                a._offsetX * b._m11 + a._offsetY * b._m21 + a._offsetZ * b._m31 + a._m44 * b._offsetX,
                a._offsetX * b._m12 + a._offsetY * b._m22 + a._offsetZ * b._m32 + a._m44 * b._offsetY,
                a._offsetX * b._m13 + a._offsetY * b._m23 + a._offsetZ * b._m33 + a._m44 * b._offsetZ,
                a._offsetX * b._m14 + a._offsetY * b._m24 + a._offsetZ * b._m34 + a._m44 * b._m44);
        }

        public static bool operator ==(Matrix3D a, Matrix3D b) =>
            a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14 &&
            a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24 &&
            a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34 &&
            a.OffsetX == b.OffsetX && a.OffsetY == b.OffsetY && a.OffsetZ == b.OffsetZ && a.M44 == b.M44;

        public static bool operator !=(Matrix3D a, Matrix3D b) => !(a == b);

        public static Matrix3D Multiply(Matrix3D a, Matrix3D b) => a * b;

        public void Append(Matrix3D matrix) => this = this * matrix;

        public void Prepend(Matrix3D matrix) => this = matrix * this;

        public void Translate(Vector3D offset) => Append(TranslationMatrix(offset));

        public void Scale(Vector3D scale) => Append(new Matrix3D(scale.X, 0, 0, 0, 0, scale.Y, 0, 0, 0, 0, scale.Z, 0, 0, 0, 0, 1));

        public void Rotate(Quaternion quaternion) => Append(quaternion.ToMatrix());

        internal static Matrix3D TranslationMatrix(Vector3D offset) =>
            new Matrix3D(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, offset.X, offset.Y, offset.Z, 1);

        public Point3D Transform(Point3D p)
        {
            Matrix3D m = Value;
            double x = p.X * m._m11 + p.Y * m._m21 + p.Z * m._m31 + m._offsetX;
            double y = p.X * m._m12 + p.Y * m._m22 + p.Z * m._m32 + m._offsetY;
            double z = p.X * m._m13 + p.Y * m._m23 + p.Z * m._m33 + m._offsetZ;
            double w = p.X * m._m14 + p.Y * m._m24 + p.Z * m._m34 + m._m44;
            return w != 0 && w != 1 ? new Point3D(x / w, y / w, z / w) : new Point3D(x, y, z);
        }

        /// <summary>Transforms a direction (the translation does not apply).</summary>
        public Vector3D Transform(Vector3D v)
        {
            Matrix3D m = Value;
            return new Vector3D(v.X * m._m11 + v.Y * m._m21 + v.Z * m._m31, v.X * m._m12 + v.Y * m._m22 + v.Z * m._m32, v.X * m._m13 + v.Y * m._m23 + v.Z * m._m33);
        }

        public override bool Equals(object obj) => obj is Matrix3D other && this == other;

        public override int GetHashCode() => HashCode.Combine(M11, M22, M33, OffsetX, OffsetY, OffsetZ);
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

// Points, vectors and matrices of WPF 3D (System.Windows.Media.Media3D).
//
// WPF multiplies row vectors with matrices: a point p is transformed as p * M, and the translation is in the
// fourth row (OffsetX, OffsetY, OffsetZ). Combining transforms A then B is A * B. The types here follow that.
using System.Globalization;

namespace System.Windows.Media.Media3D
{
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
}

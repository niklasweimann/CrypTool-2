using System.Collections.ObjectModel;
using System.Windows.Media.Animation;

namespace System.Windows.Media
{
    /// <summary>Affine 2D matrix (same layout as the CSS <c>matrix(m11, m12, m21, m22, dx, dy)</c> function).</summary>
    public struct Matrix
    {
        public Matrix(double m11, double m12, double m21, double m22, double offsetX, double offsetY)
        {
            M11 = m11;
            M12 = m12;
            M21 = m21;
            M22 = m22;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public double M11 { get; set; }
        public double M12 { get; set; }
        public double M21 { get; set; }
        public double M22 { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public static Matrix Identity => new Matrix(1, 0, 0, 1, 0, 0);

        public Point Transform(Point point)
        {
            return new Point(point.X * M11 + point.Y * M21 + OffsetX, point.X * M12 + point.Y * M22 + OffsetY);
        }

        /// <summary>The matrix that applies <paramref name="first"/> and then <paramref name="second"/>.</summary>
        public static Matrix Multiply(Matrix first, Matrix second)
        {
            return new Matrix(
                first.M11 * second.M11 + first.M12 * second.M21,
                first.M11 * second.M12 + first.M12 * second.M22,
                first.M21 * second.M11 + first.M22 * second.M21,
                first.M21 * second.M12 + first.M22 * second.M22,
                first.OffsetX * second.M11 + first.OffsetY * second.M21 + second.OffsetX,
                first.OffsetX * second.M12 + first.OffsetY * second.M22 + second.OffsetY);
        }
    }

    public abstract class GeneralTransform : Animatable
    {
        public abstract Matrix Value { get; }

        public Point Transform(Point point) => Value.Transform(point);

        public Rect TransformBounds(Rect rect)
        {
            Point topLeft = Transform(new Point(rect.X, rect.Y));
            Point bottomRight = Transform(new Point(rect.X + rect.Width, rect.Y + rect.Height));
            return new Rect(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y),
                Math.Abs(bottomRight.X - topLeft.X), Math.Abs(bottomRight.Y - topLeft.Y));
        }
    }

    public abstract class Transform : GeneralTransform
    {
        public static Transform Identity => new MatrixTransform(Matrix.Identity);

        public new Transform Clone() => (Transform)MemberwiseClone();
    }

    public sealed class TranslateTransform : Transform
    {
        public static readonly DependencyProperty XProperty = Dp.Register<double, TranslateTransform>(nameof(X));
        public static readonly DependencyProperty YProperty = Dp.Register<double, TranslateTransform>(nameof(Y));

        public TranslateTransform() { }

        public TranslateTransform(double offsetX, double offsetY)
        {
            X = offsetX;
            Y = offsetY;
        }

        public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }
        public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }

        public override Matrix Value => new Matrix(1, 0, 0, 1, X, Y);
    }

    public sealed class ScaleTransform : Transform
    {
        public static readonly DependencyProperty ScaleXProperty = Dp.Register<double, ScaleTransform>(nameof(ScaleX), 1.0);
        public static readonly DependencyProperty ScaleYProperty = Dp.Register<double, ScaleTransform>(nameof(ScaleY), 1.0);
        public static readonly DependencyProperty CenterXProperty = Dp.Register<double, ScaleTransform>(nameof(CenterX));
        public static readonly DependencyProperty CenterYProperty = Dp.Register<double, ScaleTransform>(nameof(CenterY));

        public ScaleTransform() { }

        public ScaleTransform(double scaleX, double scaleY)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public ScaleTransform(double scaleX, double scaleY, double centerX, double centerY) : this(scaleX, scaleY)
        {
            CenterX = centerX;
            CenterY = centerY;
        }

        public double ScaleX { get => (double)GetValue(ScaleXProperty); set => SetValue(ScaleXProperty, value); }
        public double ScaleY { get => (double)GetValue(ScaleYProperty); set => SetValue(ScaleYProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty); set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty); set => SetValue(CenterYProperty, value); }

        public override Matrix Value => new Matrix(ScaleX, 0, 0, ScaleY, CenterX - ScaleX * CenterX, CenterY - ScaleY * CenterY);
    }

    public sealed class RotateTransform : Transform
    {
        public static readonly DependencyProperty AngleProperty = Dp.Register<double, RotateTransform>(nameof(Angle));
        public static readonly DependencyProperty CenterXProperty = Dp.Register<double, RotateTransform>(nameof(CenterX));
        public static readonly DependencyProperty CenterYProperty = Dp.Register<double, RotateTransform>(nameof(CenterY));

        public RotateTransform() { }

        public RotateTransform(double angle) => Angle = angle;

        public RotateTransform(double angle, double centerX, double centerY)
        {
            Angle = angle;
            CenterX = centerX;
            CenterY = centerY;
        }

        public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty); set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty); set => SetValue(CenterYProperty, value); }

        public override Matrix Value
        {
            get
            {
                double radians = Angle * Math.PI / 180;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                return new Matrix(cos, sin, -sin, cos,
                    CenterX - cos * CenterX + sin * CenterY,
                    CenterY - sin * CenterX - cos * CenterY);
            }
        }
    }

    public sealed class SkewTransform : Transform
    {
        public static readonly DependencyProperty AngleXProperty = Dp.Register<double, SkewTransform>(nameof(AngleX));
        public static readonly DependencyProperty AngleYProperty = Dp.Register<double, SkewTransform>(nameof(AngleY));

        public SkewTransform() { }

        public SkewTransform(double angleX, double angleY)
        {
            AngleX = angleX;
            AngleY = angleY;
        }

        public double AngleX { get => (double)GetValue(AngleXProperty); set => SetValue(AngleXProperty, value); }
        public double AngleY { get => (double)GetValue(AngleYProperty); set => SetValue(AngleYProperty, value); }
        public double CenterX { get; set; }
        public double CenterY { get; set; }

        public override Matrix Value
        {
            get
            {
                double tanX = Math.Tan(AngleX * Math.PI / 180);
                double tanY = Math.Tan(AngleY * Math.PI / 180);
                return new Matrix(1, tanY, tanX, 1, -tanX * CenterY, -tanY * CenterX);
            }
        }
    }

    public sealed class MatrixTransform : Transform
    {
        public MatrixTransform() => Matrix = Matrix.Identity;

        public MatrixTransform(Matrix matrix) => Matrix = matrix;

        public MatrixTransform(double m11, double m12, double m21, double m22, double offsetX, double offsetY)
        {
            Matrix = new Matrix(m11, m12, m21, m22, offsetX, offsetY);
        }

        public Matrix Matrix { get; set; }

        public override Matrix Value => Matrix;
    }

    public class TransformCollection : Collection<Transform>
    {
        protected override void InsertItem(int index, Transform item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    /// <summary>Several transforms applied one after the other (the first child first).</summary>
    [Markup.ContentProperty(nameof(Children))]
    public sealed class TransformGroup : Transform
    {
        public TransformCollection Children { get; set; } = new TransformCollection();

        public override Matrix Value
        {
            get
            {
                Matrix result = Matrix.Identity;
                foreach (Transform child in Children)
                {
                    result = Matrix.Multiply(result, child.Value);
                }
                return result;
            }
        }
    }

    public sealed class GeneralTransformGroup : GeneralTransform
    {
        public Collection<GeneralTransform> Children { get; } = new Collection<GeneralTransform>();

        public override Matrix Value
        {
            get
            {
                Matrix result = Matrix.Identity;
                foreach (GeneralTransform child in Children)
                {
                    result = Matrix.Multiply(result, child.Value);
                }
                return result;
            }
        }
    }
}

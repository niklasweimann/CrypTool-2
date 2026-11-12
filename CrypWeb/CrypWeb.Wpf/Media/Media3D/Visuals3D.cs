// The 3D element tree (Viewport3D > ModelVisual3D > models) and the cameras that look at it.
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace System.Windows.Media.Media3D
{
    /// <summary>A node of the 3D element tree.</summary>
    public abstract class Visual3D : Animatable
    {
        public static readonly DependencyProperty TransformProperty = Dp.Register<Transform3D, Visual3D>(nameof(Transform));

        public Transform3D Transform { get => (Transform3D)GetValue(TransformProperty); set => SetValue(TransformProperty, value); }
    }

    public sealed class Visual3DCollection : Collection<Visual3D>
    {
        protected override void InsertItem(int index, Visual3D item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    /// <summary>Shows a model (<see cref="Content"/>) and child visuals, all moved by <see cref="Visual3D.Transform"/>.</summary>
    [Markup.ContentProperty(nameof(Children))]
    public class ModelVisual3D : Visual3D
    {
        public static readonly DependencyProperty ContentProperty = Dp.Register<Model3D, ModelVisual3D>(nameof(Content));

        public Model3D Content { get => (Model3D)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public Visual3DCollection Children { get; } = new Visual3DCollection();
    }

    // ---- cameras -------------------------------------------------------------------------------------------------

    public abstract class Camera : Animatable
    {
        public static readonly DependencyProperty TransformProperty = Dp.Register<Transform3D, Camera>(nameof(Transform));

        public Transform3D Transform { get => (Transform3D)GetValue(TransformProperty); set => SetValue(TransformProperty, value); }
    }

    /// <summary>A camera at a position looking in a direction.</summary>
    public abstract class ProjectionCamera : Camera
    {
        public static readonly DependencyProperty PositionProperty = Dp.Register<Point3D, ProjectionCamera>(nameof(Position));
        public static readonly DependencyProperty LookDirectionProperty = Dp.Register<Vector3D, ProjectionCamera>(nameof(LookDirection), new Vector3D(0, 0, -1));
        public static readonly DependencyProperty UpDirectionProperty = Dp.Register<Vector3D, ProjectionCamera>(nameof(UpDirection), new Vector3D(0, 1, 0));
        public static readonly DependencyProperty NearPlaneDistanceProperty = Dp.Register<double, ProjectionCamera>(nameof(NearPlaneDistance), 0.125);
        public static readonly DependencyProperty FarPlaneDistanceProperty = Dp.Register<double, ProjectionCamera>(nameof(FarPlaneDistance), double.PositiveInfinity);

        public Point3D Position { get => (Point3D)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
        public Vector3D LookDirection { get => (Vector3D)GetValue(LookDirectionProperty); set => SetValue(LookDirectionProperty, value); }
        public Vector3D UpDirection { get => (Vector3D)GetValue(UpDirectionProperty); set => SetValue(UpDirectionProperty, value); }
        public double NearPlaneDistance { get => (double)GetValue(NearPlaneDistanceProperty); set => SetValue(NearPlaneDistanceProperty, value); }
        public double FarPlaneDistance { get => (double)GetValue(FarPlaneDistanceProperty); set => SetValue(FarPlaneDistanceProperty, value); }
    }

    /// <summary>A perspective camera; <see cref="FieldOfView"/> is the horizontal opening angle in degrees.</summary>
    public sealed class PerspectiveCamera : ProjectionCamera
    {
        public static readonly DependencyProperty FieldOfViewProperty = Dp.Register<double, PerspectiveCamera>(nameof(FieldOfView), 45.0);

        public PerspectiveCamera() { }

        public PerspectiveCamera(Point3D position, Vector3D lookDirection, Vector3D upDirection, double fieldOfView)
        {
            Position = position;
            LookDirection = lookDirection;
            UpDirection = upDirection;
            FieldOfView = fieldOfView;
        }

        public double FieldOfView { get => (double)GetValue(FieldOfViewProperty); set => SetValue(FieldOfViewProperty, value); }
    }

    /// <summary>A parallel projection; <see cref="Width"/> is the width of the visible area in scene units.</summary>
    public sealed class OrthographicCamera : ProjectionCamera
    {
        public static readonly DependencyProperty WidthProperty = Dp.Register<double, OrthographicCamera>(nameof(Width), 2.0);

        public OrthographicCamera() { }

        public OrthographicCamera(Point3D position, Vector3D lookDirection, Vector3D upDirection, double width)
        {
            Position = position;
            LookDirection = lookDirection;
            UpDirection = upDirection;
            Width = width;
        }

        public double Width { get => (double)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    }
}

namespace System.Windows.Controls
{
    /// <summary>
    /// A 3D scene in the 2D layout: the camera's view of <see cref="Children"/>, drawn as SVG by the renderer
    /// (perspective projection, painter's algorithm, flat Lambert shading).
    /// </summary>
    [Markup.ContentProperty(nameof(Children))]
    public class Viewport3D : FrameworkElement
    {
        public static readonly DependencyProperty CameraProperty = Dp.Register<Camera, Viewport3D>(nameof(Camera));

        public Camera Camera { get => (Camera)GetValue(CameraProperty); set => SetValue(CameraProperty, value); }

        public Visual3DCollection Children { get; } = new Visual3DCollection();

        public bool ClipToBounds { get; set; } = true;
    }
}

namespace System.Windows.Media.Animation
{
    /// <summary>Animates a <see cref="Point3D"/> property in a straight line.</summary>
    public class Point3DAnimation : AnimationTimeline
    {
        public Point3DAnimation() { }

        public Point3DAnimation(Point3D toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public Point3DAnimation(Point3D fromValue, Point3D toValue, Duration duration) : this(toValue, duration) => From = fromValue;

        public Point3D? From { get; set; }
        public Point3D? To { get; set; }
        public Vector3D? By { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        internal override object ValueAt(double progress, object origin)
        {
            Point3D from = From ?? (origin is Point3D o ? o : default);
            Point3D to = To ?? (By.HasValue ? from + By.Value : from);
            double t = EasingFunction?.Ease(progress) ?? progress;
            return from + (to - from) * t;
        }
    }

    /// <summary>Animates a <see cref="Vector3D"/> property in a straight line.</summary>
    public class Vector3DAnimation : AnimationTimeline
    {
        public Vector3DAnimation() { }

        public Vector3DAnimation(Vector3D toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public Vector3DAnimation(Vector3D fromValue, Vector3D toValue, Duration duration) : this(toValue, duration) => From = fromValue;

        public Vector3D? From { get; set; }
        public Vector3D? To { get; set; }
        public Vector3D? By { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        internal override object ValueAt(double progress, object origin)
        {
            Vector3D from = From ?? (origin is Vector3D o ? o : default);
            Vector3D to = To ?? (By.HasValue ? from + By.Value : from);
            double t = EasingFunction?.Ease(progress) ?? progress;
            return from + (to - from) * t;
        }
    }
}

// The scene content of WPF 3D: models (meshes and groups), materials, lights and transforms.
//
// Properties that plugins animate (transform offsets, rotation angles, light colours, ...) are dependency
// properties, so the animation scheduler can drive them and every change redraws the scene. The scene is drawn by
// the HTML renderer (Rendering/Scene3D.cs).
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;

namespace System.Windows.Media.Media3D
{
    // ---- transforms ----------------------------------------------------------------------------------------------

    /// <summary>A 3D transformation; <see cref="Value"/> is its matrix.</summary>
    public abstract class Transform3D : Animatable
    {
        public abstract Matrix3D Value { get; }

        public static Transform3D Identity { get; } = new MatrixTransform3D(Matrix3D.Identity);

        public Point3D Transform(Point3D point) => Value.Transform(point);

        public Vector3D Transform(Vector3D vector) => Value.Transform(vector);
    }

    public sealed class TranslateTransform3D : Transform3D
    {
        public static readonly DependencyProperty OffsetXProperty = Dp.Register<double, TranslateTransform3D>(nameof(OffsetX));
        public static readonly DependencyProperty OffsetYProperty = Dp.Register<double, TranslateTransform3D>(nameof(OffsetY));
        public static readonly DependencyProperty OffsetZProperty = Dp.Register<double, TranslateTransform3D>(nameof(OffsetZ));

        public TranslateTransform3D() { }

        public TranslateTransform3D(double offsetX, double offsetY, double offsetZ)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
        }

        public TranslateTransform3D(Vector3D offset) : this(offset.X, offset.Y, offset.Z) { }

        public double OffsetX { get => (double)GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
        public double OffsetY { get => (double)GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }
        public double OffsetZ { get => (double)GetValue(OffsetZProperty); set => SetValue(OffsetZProperty, value); }

        public override Matrix3D Value => Matrix3D.TranslationMatrix(new Vector3D(OffsetX, OffsetY, OffsetZ));
    }

    public sealed class ScaleTransform3D : Transform3D
    {
        public static readonly DependencyProperty ScaleXProperty = Dp.Register<double, ScaleTransform3D>(nameof(ScaleX), 1.0);
        public static readonly DependencyProperty ScaleYProperty = Dp.Register<double, ScaleTransform3D>(nameof(ScaleY), 1.0);
        public static readonly DependencyProperty ScaleZProperty = Dp.Register<double, ScaleTransform3D>(nameof(ScaleZ), 1.0);
        public static readonly DependencyProperty CenterXProperty = Dp.Register<double, ScaleTransform3D>(nameof(CenterX));
        public static readonly DependencyProperty CenterYProperty = Dp.Register<double, ScaleTransform3D>(nameof(CenterY));
        public static readonly DependencyProperty CenterZProperty = Dp.Register<double, ScaleTransform3D>(nameof(CenterZ));

        public ScaleTransform3D() { }

        public ScaleTransform3D(double scaleX, double scaleY, double scaleZ)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
        }

        public ScaleTransform3D(Vector3D scale) : this(scale.X, scale.Y, scale.Z) { }

        public ScaleTransform3D(double scaleX, double scaleY, double scaleZ, double centerX, double centerY, double centerZ)
            : this(scaleX, scaleY, scaleZ)
        {
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
        }

        public double ScaleX { get => (double)GetValue(ScaleXProperty); set => SetValue(ScaleXProperty, value); }
        public double ScaleY { get => (double)GetValue(ScaleYProperty); set => SetValue(ScaleYProperty, value); }
        public double ScaleZ { get => (double)GetValue(ScaleZProperty); set => SetValue(ScaleZProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty); set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty); set => SetValue(CenterYProperty, value); }
        public double CenterZ { get => (double)GetValue(CenterZProperty); set => SetValue(CenterZProperty, value); }

        public override Matrix3D Value
        {
            get
            {
                var center = new Vector3D(CenterX, CenterY, CenterZ);
                Matrix3D matrix = Matrix3D.TranslationMatrix(-center);
                matrix.Scale(new Vector3D(ScaleX, ScaleY, ScaleZ));
                matrix.Translate(center);
                return matrix;
            }
        }
    }

    /// <summary>A rotation; <see cref="RotateTransform3D"/> turns it into a transform.</summary>
    public abstract class Rotation3D : Animatable
    {
        internal abstract Quaternion Quaternion { get; }

        public static Rotation3D Identity { get; } = new QuaternionRotation3D(Quaternion.Identity);
    }

    /// <summary>A rotation by <see cref="Angle"/> degrees about <see cref="Axis"/> (counter-clockwise looking at the origin).</summary>
    public sealed class AxisAngleRotation3D : Rotation3D
    {
        public static readonly DependencyProperty AxisProperty = Dp.Register<Vector3D, AxisAngleRotation3D>(nameof(Axis), new Vector3D(0, 1, 0));
        public static readonly DependencyProperty AngleProperty = Dp.Register<double, AxisAngleRotation3D>(nameof(Angle));

        public AxisAngleRotation3D() { }

        public AxisAngleRotation3D(Vector3D axis, double angle)
        {
            Axis = axis;
            Angle = angle;
        }

        public Vector3D Axis { get => (Vector3D)GetValue(AxisProperty); set => SetValue(AxisProperty, value); }
        public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

        internal override Quaternion Quaternion => Axis.LengthSquared == 0 ? Quaternion.Identity : new Quaternion(Axis, Angle);
    }

    public sealed class QuaternionRotation3D : Rotation3D
    {
        public QuaternionRotation3D() { }

        public QuaternionRotation3D(Quaternion quaternion) => Quaternion = quaternion;

        internal override Quaternion Quaternion { get; }
    }

    public sealed class RotateTransform3D : Transform3D
    {
        public static readonly DependencyProperty RotationProperty = Dp.Register<Rotation3D, RotateTransform3D>(nameof(Rotation));
        public static readonly DependencyProperty CenterXProperty = Dp.Register<double, RotateTransform3D>(nameof(CenterX));
        public static readonly DependencyProperty CenterYProperty = Dp.Register<double, RotateTransform3D>(nameof(CenterY));
        public static readonly DependencyProperty CenterZProperty = Dp.Register<double, RotateTransform3D>(nameof(CenterZ));

        public RotateTransform3D() { }

        public RotateTransform3D(Rotation3D rotation) => Rotation = rotation;

        public RotateTransform3D(Rotation3D rotation, Point3D center) : this(rotation)
        {
            CenterX = center.X;
            CenterY = center.Y;
            CenterZ = center.Z;
        }

        public RotateTransform3D(Rotation3D rotation, double centerX, double centerY, double centerZ)
            : this(rotation, new Point3D(centerX, centerY, centerZ)) { }

        public Rotation3D Rotation { get => (Rotation3D)GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty); set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty); set => SetValue(CenterYProperty, value); }
        public double CenterZ { get => (double)GetValue(CenterZProperty); set => SetValue(CenterZProperty, value); }

        public override Matrix3D Value
        {
            get
            {
                var center = new Vector3D(CenterX, CenterY, CenterZ);
                Matrix3D matrix = Matrix3D.TranslationMatrix(-center);
                matrix.Append((Rotation?.Quaternion ?? Quaternion.Identity).ToMatrix());
                matrix.Translate(center);
                return matrix;
            }
        }
    }

    public sealed class MatrixTransform3D : Transform3D
    {
        public static readonly DependencyProperty MatrixProperty = Dp.Register<Matrix3D, MatrixTransform3D>(nameof(Matrix), Matrix3D.Identity);

        public MatrixTransform3D() { }

        public MatrixTransform3D(Matrix3D matrix) => Matrix = matrix;

        public Matrix3D Matrix { get => (Matrix3D)GetValue(MatrixProperty); set => SetValue(MatrixProperty, value); }

        public override Matrix3D Value => Matrix;
    }

    public sealed class Transform3DCollection : Collection<Transform3D>
    {
        protected override void InsertItem(int index, Transform3D item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }
    }

    /// <summary>Applies its children one after the other (the first child first).</summary>
    [Markup.ContentProperty(nameof(Children))]
    public sealed class Transform3DGroup : Transform3D
    {
        public Transform3DCollection Children { get; set; } = new Transform3DCollection();

        public override Matrix3D Value
        {
            get
            {
                Matrix3D matrix = Matrix3D.Identity;
                foreach (Transform3D child in Children)
                {
                    matrix.Append(child.Value);
                }
                return matrix;
            }
        }
    }

    // ---- materials -----------------------------------------------------------------------------------------------

    public abstract class Material : Animatable { }

    /// <summary>A surface lit by the scene's lights (Lambert shading).</summary>
    public sealed class DiffuseMaterial : Material
    {
        public static readonly DependencyProperty BrushProperty = Dp.Register<Brush, DiffuseMaterial>(nameof(Brush));

        public DiffuseMaterial() { }

        public DiffuseMaterial(Brush brush) => Brush = brush;

        public Brush Brush { get => (Brush)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

        public Color Color { get; set; } = Colors.White;

        public Color AmbientColor { get; set; } = Colors.White;
    }

    /// <summary>A surface that shines in its own colour, whatever the light.</summary>
    public sealed class EmissiveMaterial : Material
    {
        public static readonly DependencyProperty BrushProperty = Dp.Register<Brush, EmissiveMaterial>(nameof(Brush));

        public EmissiveMaterial() { }

        public EmissiveMaterial(Brush brush) => Brush = brush;

        public Brush Brush { get => (Brush)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

        public Color Color { get; set; } = Colors.White;
    }

    /// <summary>Highlights; drawn like a diffuse surface without its own colour contribution.</summary>
    public sealed class SpecularMaterial : Material
    {
        public SpecularMaterial() { }

        public SpecularMaterial(Brush brush, double specularPower)
        {
            Brush = brush;
            SpecularPower = specularPower;
        }

        public Brush Brush { get; set; }

        public double SpecularPower { get; set; } = 40;
    }

    public sealed class MaterialCollection : Collection<Material>
    {
        protected override void InsertItem(int index, Material item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }
    }

    /// <summary>Several materials painted on top of each other (the first at the bottom).</summary>
    [Markup.ContentProperty(nameof(Children))]
    public sealed class MaterialGroup : Material
    {
        public MaterialCollection Children { get; set; } = new MaterialCollection();
    }

    // ---- geometry and models -------------------------------------------------------------------------------------

    public abstract class Geometry3D : Animatable { }

    /// <summary>A triangle mesh: corner positions, triangles as index triples, texture coordinates per corner.</summary>
    public sealed class MeshGeometry3D : Geometry3D
    {
        public static readonly DependencyProperty PositionsProperty = Dp.Register<Point3DCollection, MeshGeometry3D>(nameof(Positions));
        public static readonly DependencyProperty TriangleIndicesProperty = Dp.Register<Int32Collection, MeshGeometry3D>(nameof(TriangleIndices));
        public static readonly DependencyProperty TextureCoordinatesProperty = Dp.Register<PointCollection, MeshGeometry3D>(nameof(TextureCoordinates));
        public static readonly DependencyProperty NormalsProperty = Dp.Register<Vector3DCollection, MeshGeometry3D>(nameof(Normals));

        public Point3DCollection Positions { get => (Point3DCollection)GetValue(PositionsProperty); set => SetValue(PositionsProperty, value); }
        public Int32Collection TriangleIndices { get => (Int32Collection)GetValue(TriangleIndicesProperty); set => SetValue(TriangleIndicesProperty, value); }
        public PointCollection TextureCoordinates { get => (PointCollection)GetValue(TextureCoordinatesProperty); set => SetValue(TextureCoordinatesProperty, value); }
        public Vector3DCollection Normals { get => (Vector3DCollection)GetValue(NormalsProperty); set => SetValue(NormalsProperty, value); }
    }

    /// <summary>Something in a 3D scene: a mesh, a group or a light.</summary>
    public abstract class Model3D : Animatable
    {
        public static readonly DependencyProperty TransformProperty = Dp.Register<Transform3D, Model3D>(nameof(Transform));

        public Transform3D Transform { get => (Transform3D)GetValue(TransformProperty); set => SetValue(TransformProperty, value); }
    }

    public sealed class Model3DCollection : Collection<Model3D>
    {
        public Model3DCollection() { }

        public Model3DCollection(System.Collections.Generic.IEnumerable<Model3D> models)
        {
            foreach (Model3D model in models)
            {
                Add(model);
            }
        }

        protected override void InsertItem(int index, Model3D item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void SetItem(int index, Model3D item)
        {
            base.SetItem(index, item);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    [Markup.ContentProperty(nameof(Children))]
    public sealed class Model3DGroup : Model3D
    {
        public static readonly DependencyProperty ChildrenProperty = Dp.Register<Model3DCollection, Model3DGroup>(nameof(Children));

        public Model3DGroup() => Children = new Model3DCollection();

        public Model3DCollection Children { get => (Model3DCollection)GetValue(ChildrenProperty); set => SetValue(ChildrenProperty, value); }
    }

    /// <summary>A mesh with a material for its front faces and optionally one for its back faces.</summary>
    public sealed class GeometryModel3D : Model3D
    {
        public static readonly DependencyProperty GeometryProperty = Dp.Register<Geometry3D, GeometryModel3D>(nameof(Geometry));
        public static readonly DependencyProperty MaterialProperty = Dp.Register<Material, GeometryModel3D>(nameof(Material));
        public static readonly DependencyProperty BackMaterialProperty = Dp.Register<Material, GeometryModel3D>(nameof(BackMaterial));

        public GeometryModel3D() { }

        public GeometryModel3D(Geometry3D geometry, Material material)
        {
            Geometry = geometry;
            Material = material;
        }

        public Geometry3D Geometry { get => (Geometry3D)GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
        public Material Material { get => (Material)GetValue(MaterialProperty); set => SetValue(MaterialProperty, value); }
        public Material BackMaterial { get => (Material)GetValue(BackMaterialProperty); set => SetValue(BackMaterialProperty, value); }
    }

    // ---- lights --------------------------------------------------------------------------------------------------

    public abstract class Light : Model3D
    {
        public static readonly DependencyProperty ColorProperty = Dp.Register<Color, Light>(nameof(Color), Colors.White);

        public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    }

    /// <summary>Light from everywhere: every surface gets it.</summary>
    public sealed class AmbientLight : Light
    {
        public AmbientLight() { }

        public AmbientLight(Color ambientColor) => Color = ambientColor;
    }

    /// <summary>Parallel light from one direction (like the sun).</summary>
    public sealed class DirectionalLight : Light
    {
        public static readonly DependencyProperty DirectionProperty = Dp.Register<Vector3D, DirectionalLight>(nameof(Direction), new Vector3D(0, 0, -1));

        public DirectionalLight() { }

        public DirectionalLight(Color diffuseColor, Vector3D direction)
        {
            Color = diffuseColor;
            Direction = direction;
        }

        public Vector3D Direction { get => (Vector3D)GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }
    }

    /// <summary>Light from a point; it fades with distance (attenuation) and ends at <see cref="Range"/>.</summary>
    public class PointLightBase : Light
    {
        public static readonly DependencyProperty PositionProperty = Dp.Register<Point3D, PointLightBase>(nameof(Position));
        public static readonly DependencyProperty RangeProperty = Dp.Register<double, PointLightBase>(nameof(Range), double.PositiveInfinity);

        public Point3D Position { get => (Point3D)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
        public double Range { get => (double)GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
        public double ConstantAttenuation { get; set; } = 1;
        public double LinearAttenuation { get; set; }
        public double QuadraticAttenuation { get; set; }
    }

    public sealed class PointLight : PointLightBase
    {
        public PointLight() { }

        public PointLight(Color diffuseColor, Point3D position)
        {
            Color = diffuseColor;
            Position = position;
        }
    }

    /// <summary>A cone of light; drawn like a point light.</summary>
    public sealed class SpotLight : PointLightBase
    {
        public Vector3D Direction { get; set; } = new Vector3D(0, 0, -1);
        public double InnerConeAngle { get; set; } = 180;
        public double OuterConeAngle { get; set; } = 90;
    }
}

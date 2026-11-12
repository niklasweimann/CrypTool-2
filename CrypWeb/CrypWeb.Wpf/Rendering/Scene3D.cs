// Turns a Viewport3D's scene into flat, shaded polygons in screen coordinates, back to front.
//
// The steps follow WPF's model:
//   1. walk the visuals and models, combining their transforms (child transform first, then the parent's);
//   2. collect the lights (ambient, directional, point) in world space;
//   3. for every triangle: transform to world space, decide front or back face (counter-clockwise as seen from the
//      camera is the front, which gets Material; the back gets BackMaterial or is not drawn), shade it with the
//      Lambert model (ambient + sum of lights * cos(angle)), project it with the camera;
//   4. sort all faces by distance (painter's algorithm: far ones are drawn first).
// Two triangles of a mesh that share an edge and lie in one plane (the faces of a cube, a label) become one quad,
// which halves the output and avoids hairline seams between the halves.
// Materials painted with a VisualBrush showing text (a TextBlock or Label) keep the text and the affine mapping from
// texture coordinates to the screen, so the renderer can draw the text on the face.
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace System.Windows.Rendering
{
    /// <summary>One flat polygon of the projected scene with what is painted on it.</summary>
    internal sealed class Face3D
    {
        public Point[] Points;

        /// <summary>Distance from the camera (for sorting).</summary>
        public double Depth;

        /// <summary>Materials from bottom to top.</summary>
        public List<FaceLayer> Layers = new List<FaceLayer>();
    }

    /// <summary>One material on a face: a colour, or text on a background (from a VisualBrush).</summary>
    internal sealed class FaceLayer
    {
        public Color Color;
        public double Opacity = 1;

        /// <summary>Picture of an ImageBrush (its address), drawn over the texture square; null otherwise.</summary>
        public ImageSource Image;

        /// <summary>Text of a VisualBrush, null for a plain colour.</summary>
        public string Text;
        public Color TextColor;
        public Color? TextBackground;
        public double FontSize = 12;
        public bool Bold;

        /// <summary>Maps texture coordinates (0..1, 0..1) to the screen: x' = M11 u + M21 v + OffsetX, ...</summary>
        public Matrix TextureToScreen;
    }

    internal sealed class Scene3D
    {
        /// <summary>A light with its values resolved to world space once per frame.</summary>
        private sealed class SceneLight
        {
            public bool Ambient;
            public bool Directional;
            public double R, G, B;                  // colour, 1 = full
            public Vector3D TowardsLight;           // directional lights
            public Point3D Position;                // point lights
            public double Range, Constant, Linear, Quadratic;
        }

        /// <summary>A mesh's corners in world space and on the screen (projected once, shared by its faces).</summary>
        private sealed class MeshFrame
        {
            public Point3D[] World;
            public Point[] Screen;
            public double[] Depth;
            public bool[] Visible;                  // false: behind the camera
        }

        private sealed class Camera3D
        {
            public Point3D Position;
            public Vector3D Right, Up, Back;     // camera axes in world space (Back points away from the view)
            public bool Perspective;
            public double Scale;                // pixels per unit at distance 1 (perspective) or per unit (orthographic)
            public double Near;
            public double CenterX, CenterY;
        }

        private readonly List<SceneLight> _lights = new List<SceneLight>();
        private readonly List<Face3D> _faces = new List<Face3D>();
        private Camera3D _camera;

        /// <summary>The faces of <paramref name="viewport"/> seen at size width x height, sorted back to front.</summary>
        internal static List<Face3D> Project(Viewport3D viewport, double width, double height)
        {
            var scene = new Scene3D();
            scene._camera = CreateCamera(viewport.Camera, width, height);
            if (scene._camera == null)
            {
                return scene._faces;
            }
            foreach (Visual3D visual in viewport.Children)
            {
                scene.CollectLights(visual, Matrix3D.Identity);
            }
            foreach (Visual3D visual in viewport.Children)
            {
                scene.AddVisual(visual, Matrix3D.Identity);
            }
            scene._faces.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            return scene._faces;
        }

        // ---- camera --------------------------------------------------------------------------------------------

        private static Camera3D CreateCamera(Camera camera, double width, double height)
        {
            if (camera is not ProjectionCamera projection)
            {
                return null;
            }
            Matrix3D transform = camera.Transform?.Value ?? Matrix3D.Identity;
            Point3D position = transform.Transform(projection.Position);
            Vector3D look = transform.Transform(projection.LookDirection);
            Vector3D up = transform.Transform(projection.UpDirection);
            if (look.LengthSquared == 0)
            {
                return null;
            }
            Vector3D back = -look;
            back.Normalize();
            Vector3D right = Vector3D.CrossProduct(up, back);
            if (right.LengthSquared == 0)
            {
                right = Vector3D.CrossProduct(new Vector3D(0, 0, 1), back);
            }
            right.Normalize();
            Vector3D trueUp = Vector3D.CrossProduct(back, right);

            var result = new Camera3D
            {
                Position = position, Right = right, Up = trueUp, Back = back,
                CenterX = width / 2, CenterY = height / 2, Near = Math.Max(projection.NearPlaneDistance, 1e-6),
            };
            if (camera is PerspectiveCamera perspective)
            {
                double halfAngle = Math.Clamp(perspective.FieldOfView, 0.01, 179.99) * Math.PI / 360;
                result.Perspective = true;
                result.Scale = (width / 2) / Math.Tan(halfAngle);
            }
            else
            {
                double viewWidth = ((OrthographicCamera)camera).Width;
                result.Scale = viewWidth > 0 ? width / viewWidth : 1;
            }
            return result;
        }

        /// <summary>Distance in front of the camera and screen position of a world point.</summary>
        private bool ToScreen(Point3D world, out Point screen, out double depth)
        {
            Vector3D relative = world - _camera.Position;
            double x = Vector3D.DotProduct(relative, _camera.Right);
            double y = Vector3D.DotProduct(relative, _camera.Up);
            depth = -Vector3D.DotProduct(relative, _camera.Back);
            if (_camera.Perspective)
            {
                if (depth < _camera.Near)
                {
                    screen = default;
                    return false;       // behind the camera: the triangle is not drawn
                }
                screen = new Point(_camera.CenterX + _camera.Scale * x / depth, _camera.CenterY - _camera.Scale * y / depth);
            }
            else
            {
                screen = new Point(_camera.CenterX + _camera.Scale * x, _camera.CenterY - _camera.Scale * y);
            }
            return true;
        }

        // ---- lights --------------------------------------------------------------------------------------------

        private void CollectLights(Visual3D visual, Matrix3D parent)
        {
            Matrix3D world = Combine(visual.Transform, parent);
            if (visual is ModelVisual3D modelVisual)
            {
                CollectLights(modelVisual.Content, world);
                foreach (Visual3D child in modelVisual.Children)
                {
                    CollectLights(child, world);
                }
            }
        }

        private void CollectLights(Model3D model, Matrix3D parent)
        {
            if (model == null)
            {
                return;
            }
            Matrix3D world = Combine(model.Transform, parent);
            if (model is Light light)
            {
                Color color = light.Color;
                var resolved = new SceneLight { R = color.R / 255.0, G = color.G / 255.0, B = color.B / 255.0 };
                switch (light)
                {
                    case AmbientLight:
                        resolved.Ambient = true;
                        break;
                    case DirectionalLight directional:
                        resolved.Directional = true;
                        resolved.TowardsLight = -world.Transform(directional.Direction);
                        resolved.TowardsLight.Normalize();
                        break;
                    case PointLightBase pointLight:
                        resolved.Position = world.Transform(pointLight.Position);
                        resolved.Range = pointLight.Range;
                        resolved.Constant = pointLight.ConstantAttenuation;
                        resolved.Linear = pointLight.LinearAttenuation;
                        resolved.Quadratic = pointLight.QuadraticAttenuation;
                        break;
                    default:
                        return;
                }
                _lights.Add(resolved);
            }
            else if (model is Model3DGroup group)
            {
                foreach (Model3D child in group.Children)
                {
                    CollectLights(child, world);
                }
            }
        }

        /// <summary>Light reaching a surface point with normal <paramref name="normal"/> (r, g, b; 1 = full).</summary>
        private (double R, double G, double B) Illumination(Point3D point, Vector3D normal)
        {
            double r = 0, g = 0, b = 0;
            foreach (SceneLight light in _lights)
            {
                double factor;
                if (light.Ambient)
                {
                    factor = 1;
                }
                else if (light.Directional)
                {
                    factor = Math.Max(0, Vector3D.DotProduct(normal, light.TowardsLight));
                }
                else
                {
                    Vector3D toLight = light.Position - point;
                    double distance = toLight.Length;
                    if (distance > light.Range || distance == 0)
                    {
                        continue;
                    }
                    toLight /= distance;
                    double attenuation = light.Constant + light.Linear * distance + light.Quadratic * distance * distance;
                    factor = Math.Max(0, Vector3D.DotProduct(normal, toLight)) / Math.Max(attenuation, 1e-6);
                }
                r += light.R * factor;
                g += light.G * factor;
                b += light.B * factor;
            }
            return (r, g, b);
        }

        // ---- models --------------------------------------------------------------------------------------------

        private void AddVisual(Visual3D visual, Matrix3D parent)
        {
            Matrix3D world = Combine(visual.Transform, parent);
            if (visual is ModelVisual3D modelVisual)
            {
                AddModel(modelVisual.Content, world);
                foreach (Visual3D child in modelVisual.Children)
                {
                    AddVisual(child, world);
                }
            }
        }

        private void AddModel(Model3D model, Matrix3D parent)
        {
            if (model == null)
            {
                return;
            }
            Matrix3D world = Combine(model.Transform, parent);
            switch (model)
            {
                case Model3DGroup group:
                    foreach (Model3D child in group.Children)
                    {
                        AddModel(child, world);
                    }
                    break;
                case GeometryModel3D geometryModel when geometryModel.Geometry is MeshGeometry3D mesh:
                    AddMesh(mesh, geometryModel.Material, geometryModel.BackMaterial, world);
                    break;
            }
        }

        private static Matrix3D Combine(Transform3D own, Matrix3D parent) => own == null ? parent : own.Value * parent;

        private void AddMesh(MeshGeometry3D mesh, Material front, Material back, Matrix3D world)
        {
            Point3DCollection positions = mesh.Positions;
            if (positions == null || positions.Count < 3 || (front == null && back == null))
            {
                return;
            }
            var frame = new MeshFrame
            {
                World = new Point3D[positions.Count],
                Screen = new Point[positions.Count],
                Depth = new double[positions.Count],
                Visible = new bool[positions.Count],
            };
            for (int i = 0; i < positions.Count; i++)
            {
                frame.World[i] = world.Transform(positions[i]);
                frame.Visible[i] = ToScreen(frame.World[i], out frame.Screen[i], out frame.Depth[i]);
            }
            Point3D[] worldPoints = frame.World;
            IList<int> indices = mesh.TriangleIndices;
            if (indices == null || indices.Count == 0)
            {
                // without indices the positions are the triangles, three at a time
                var all = new List<int>();
                for (int i = 0; i + 2 < positions.Count; i += 3)
                {
                    all.AddRange(new[] { i, i + 1, i + 2 });
                }
                indices = all;
            }
            PointCollection texture = mesh.TextureCoordinates;

            // buffers reused for every triangle (a scene has thousands of them per frame)
            var triangle = new int[3];
            var next = new int[3];
            var quad = new int[4];
            for (int t = 0; t + 2 < indices.Count; t += 3)
            {
                triangle[0] = indices[t];
                triangle[1] = indices[t + 1];
                triangle[2] = indices[t + 2];
                if (!ValidIndices(triangle, worldPoints.Length))
                {
                    continue;
                }
                Vector3D normal = Normal(worldPoints, triangle);
                if (normal.LengthSquared == 0)
                {
                    continue;
                }

                // the next triangle may complete a quad with this one (same plane, shared edge)
                int[] polygon = triangle;
                if (t + 5 < indices.Count)
                {
                    next[0] = indices[t + 3];
                    next[1] = indices[t + 4];
                    next[2] = indices[t + 5];
                    if (ValidIndices(next, worldPoints.Length) && TryMergeQuad(worldPoints, triangle, next, normal, quad))
                    {
                        polygon = quad;
                        t += 3;
                    }
                }
                AddPolygon(frame, polygon, triangle, normal, texture, front, back);
            }
        }

        private static bool ValidIndices(int[] triangle, int count) =>
            triangle[0] >= 0 && triangle[1] >= 0 && triangle[2] >= 0 && triangle[0] < count && triangle[1] < count && triangle[2] < count;

        private static Vector3D Normal(Point3D[] points, int[] triangle)
        {
            Vector3D normal = Vector3D.CrossProduct(points[triangle[1]] - points[triangle[0]], points[triangle[2]] - points[triangle[0]]);
            normal.Normalize();
            return normal;
        }

        /// <summary>Two triangles a, b sharing an edge and lying in one plane, as one quad in drawing order (written to <paramref name="quad"/>).</summary>
        private static bool TryMergeQuad(Point3D[] points, int[] a, int[] b, Vector3D normal, int[] quad)
        {
            Vector3D otherNormal = Normal(points, b);
            if (Vector3D.DotProduct(normal, otherNormal) < 0.999)
            {
                return false;
            }
            // the corner of b that is not in a, and the edge of a (going round a) that b shares
            int extra = -1, shared = 0;
            foreach (int corner in b)
            {
                if (Array.IndexOf(a, corner) < 0)
                {
                    extra = corner;
                }
                else
                {
                    shared++;
                }
            }
            if (extra < 0 || shared != 2)
            {
                return false;
            }
            for (int i = 0; i < 3; i++)
            {
                int from = a[i], to = a[(i + 1) % 3];
                if (Array.IndexOf(b, from) >= 0 && Array.IndexOf(b, to) >= 0)
                {
                    // going round a: from -> to -> opposite; the extra corner goes between from and to
                    int opposite = a[(i + 2) % 3];
                    quad[0] = from;
                    quad[1] = extra;
                    quad[2] = to;
                    quad[3] = opposite;
                    return true;
                }
            }
            return false;
        }

        private void AddPolygon(MeshFrame frame, int[] polygon, int[] triangle, Vector3D normal, PointCollection texture, Material front, Material back)
        {
            // front or back: does the face point towards the camera?
            Point3D corner = frame.World[polygon[0]];
            Vector3D towardsCamera = _camera.Perspective ? _camera.Position - corner : _camera.Back;
            bool isFront = Vector3D.DotProduct(normal, towardsCamera) > 0;
            Material material = isFront ? front : back;
            if (material == null)
            {
                return;
            }
            Vector3D litNormal = isFront ? normal : -normal;

            var face = new Face3D { Points = new Point[polygon.Length] };
            double depthSum = 0;
            var center = new Vector3D();
            for (int i = 0; i < polygon.Length; i++)
            {
                int corner_ = polygon[i];
                if (!frame.Visible[corner_])
                {
                    return;
                }
                face.Points[i] = frame.Screen[corner_];
                depthSum += frame.Depth[corner_];
                center += (Vector3D)frame.World[corner_];
            }
            face.Depth = depthSum / polygon.Length;
            var centerPoint = (Point3D)(center / polygon.Length);

            Matrix? textureToScreen = TextureMapping(frame, triangle, texture);
            (double R, double G, double B)? light = null;
            AddLayers(face, material, () => light ??= Illumination(centerPoint, litNormal), textureToScreen);
            if (face.Layers.Count > 0)
            {
                _faces.Add(face);
            }
        }

        /// <summary>The affine map from texture coordinates to the screen given by one triangle (null when degenerate).</summary>
        private static Matrix? TextureMapping(MeshFrame frame, int[] triangle, PointCollection texture)
        {
            if (texture == null || triangle[0] >= texture.Count || triangle[1] >= texture.Count || triangle[2] >= texture.Count)
            {
                return null;
            }
            Point t0 = texture[triangle[0]], t1 = texture[triangle[1]], t2 = texture[triangle[2]];
            double du1 = t1.X - t0.X, dv1 = t1.Y - t0.Y, du2 = t2.X - t0.X, dv2 = t2.Y - t0.Y;
            double determinant = du1 * dv2 - du2 * dv1;
            if (Math.Abs(determinant) < 1e-9)
            {
                return null;
            }
            if (!frame.Visible[triangle[0]] || !frame.Visible[triangle[1]] || !frame.Visible[triangle[2]])
            {
                return null;
            }
            Point s0 = frame.Screen[triangle[0]], s1 = frame.Screen[triangle[1]], s2 = frame.Screen[triangle[2]];
            double dx1 = s1.X - s0.X, dy1 = s1.Y - s0.Y, dx2 = s2.X - s0.X, dy2 = s2.Y - s0.Y;
            // screen delta = [du dv] * M  with M rows (M11 M12) and (M21 M22)
            double m11 = (dx1 * dv2 - dx2 * dv1) / determinant;
            double m12 = (dy1 * dv2 - dy2 * dv1) / determinant;
            double m21 = (dx2 * du1 - dx1 * du2) / determinant;
            double m22 = (dy2 * du1 - dy1 * du2) / determinant;
            double offsetX = s0.X - (t0.X * m11 + t0.Y * m21);
            double offsetY = s0.Y - (t0.X * m12 + t0.Y * m22);
            return new Matrix(m11, m12, m21, m22, offsetX, offsetY);
        }

        // ---- materials -----------------------------------------------------------------------------------------

        private void AddLayers(Face3D face, Material material, Func<(double R, double G, double B)> light, Matrix? textureToScreen)
        {
            switch (material)
            {
                case MaterialGroup group:
                    foreach (Material child in group.Children)
                    {
                        AddLayers(face, child, light, textureToScreen);
                    }
                    break;
                case DiffuseMaterial diffuse when diffuse.Brush != null:
                    AddBrushLayer(face, diffuse.Brush, diffuse.Color, light(), textureToScreen);
                    break;
                case EmissiveMaterial emissive when emissive.Brush != null:
                    AddBrushLayer(face, emissive.Brush, emissive.Color, (1, 1, 1), textureToScreen);
                    break;
            }
        }

        private static void AddBrushLayer(Face3D face, Brush brush, Color materialColor, (double R, double G, double B) light, Matrix? textureToScreen)
        {
            Color Lit(Color color) => Color.FromArgb(color.A,
                ToByte(color.R * materialColor.R / 255.0 * light.R),
                ToByte(color.G * materialColor.G / 255.0 * light.G),
                ToByte(color.B * materialColor.B / 255.0 * light.B));

            if (brush is VisualBrush visualBrush && TextOf(visualBrush.Visual, out string text, out Brush foreground, out Brush background, out double fontSize, out bool bold))
            {
                if (textureToScreen == null)
                {
                    if (background is SolidColorBrush solidBackground)
                    {
                        face.Layers.Add(new FaceLayer { Color = Lit(solidBackground.Color), Opacity = brush.Opacity * solidBackground.Opacity });
                    }
                    return;
                }
                face.Layers.Add(new FaceLayer
                {
                    Text = text,
                    TextColor = Lit(BrushColor(foreground, Colors.Black)),
                    TextBackground = background == null ? (Color?)null : Lit(BrushColor(background, Colors.Transparent)),
                    Opacity = brush.Opacity,
                    FontSize = fontSize,
                    Bold = bold,
                    TextureToScreen = textureToScreen.Value,
                });
                return;
            }
            if (brush is ImageBrush imageBrush && imageBrush.ImageSource != null && textureToScreen != null)
            {
                // the picture is drawn as it is (lighting would need its pixels); WPF lights it, so the lit
                // brightness is kept as an overlay of black with the missing light as opacity
                face.Layers.Add(new FaceLayer { Image = imageBrush.ImageSource, Opacity = brush.Opacity, TextureToScreen = textureToScreen.Value });
                double brightness = (light.R + light.G + light.B) / 3;
                if (brightness < 1)
                {
                    face.Layers.Add(new FaceLayer { Color = Colors.Black, Opacity = brush.Opacity * Math.Clamp(1 - brightness, 0, 1) });
                }
                return;
            }
            face.Layers.Add(new FaceLayer { Color = Lit(BrushColor(brush, Colors.Gray)), Opacity = brush.Opacity });
        }

        /// <summary>The colour a brush paints with on average (gradients: the mean of their stops).</summary>
        private static Color BrushColor(Brush brush, Color fallback)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return solid.Color;
                case GradientBrush gradient when gradient.GradientStops.Count > 0:
                    double a = 0, r = 0, g = 0, b = 0;
                    foreach (GradientStop stop in gradient.GradientStops)
                    {
                        a += stop.Color.A;
                        r += stop.Color.R;
                        g += stop.Color.G;
                        b += stop.Color.B;
                    }
                    int n = gradient.GradientStops.Count;
                    return Color.FromArgb(ToByte(a / n), ToByte(r / n), ToByte(g / n), ToByte(b / n));
                default:
                    return fallback;
            }
        }

        /// <summary>The text of a visual a VisualBrush shows (a TextBlock, or a content control with text).</summary>
        private static bool TextOf(Visual visual, out string text, out Brush foreground, out Brush background, out double fontSize, out bool bold)
        {
            text = null;
            foreground = null;
            background = null;
            fontSize = 12;
            bold = false;
            switch (visual)
            {
                case TextBlock block:
                    text = block.Text;
                    foreground = block.Foreground;
                    background = block.Background;
                    fontSize = block.FontSize;
                    bold = block.FontWeight.ToOpenTypeWeight() >= 600;
                    return text != null;
                case ContentControl control when control.Content is string content:
                    text = content;
                    foreground = control.Foreground;
                    background = control.Background;
                    fontSize = control.FontSize;
                    bold = control.FontWeight.ToOpenTypeWeight() >= 600;
                    return true;
                default:
                    return false;
            }
        }

        private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}

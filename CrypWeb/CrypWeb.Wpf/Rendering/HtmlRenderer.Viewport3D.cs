using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace System.Windows.Rendering
{
    internal sealed partial class HtmlRenderer
    {
        /// <summary>
        /// A 3D scene as a canvas. The projected faces (see <see cref="Scene3D"/>) are written as a compact list in
        /// the canvas's <c>data-scene</c> attribute and drawn by wpf.js (draw3d) back to front: a scene of a few
        /// thousand textured cubes (PRESENT) would be tens of thousands of SVG elements, too slow to update for every
        /// animation step.
        ///
        /// data-scene is JSON: { "img": [picture URLs], "f": [faces] }, every face an array starting with its kind:
        ///   [0, colour, x1, y1, x2, y2, ...]                      a filled polygon
        ///   [1, picture, opacity, a, b, c, d, e, f, x1, y1, ...]  a picture over the texture square mapped by the
        ///                                                         matrix (a..f), clipped to the polygon
        ///   [2, text, size, bold, colour, background, opacity, a, b, c, d, e, f, x1, y1, ...]  text, likewise
        /// </summary>
        private void WriteViewport3D(Viewport3D viewport, LayoutSlot slot)
        {
            double width = double.IsNaN(viewport.Width) ? 400 : viewport.Width;
            double height = double.IsNaN(viewport.Height) ? 300 : viewport.Height;

            var colours = new Dictionary<(Color, double), string>();
            string Colour(Color color, double opacity)
            {
                if (!colours.TryGetValue((color, opacity), out string json))
                {
                    colours[(color, opacity)] = json = Json(CssValues.Color(color, opacity));
                }
                return json;
            }

            var pictures = new List<string>();
            var pictureIndex = new Dictionary<string, int>();
            var faces = new StringBuilder();
            foreach (Face3D face in Scene3D.Project(viewport, width, height))
            {
                foreach (FaceLayer layer in face.Layers)
                {
                    int start = faces.Length;
                    if (layer.Image != null)
                    {
                        string address = layer.Image is Media.Imaging.DrawnBitmap ? null : layer.Image.Address;
                        if (string.IsNullOrEmpty(address))
                        {
                            continue;
                        }
                        string url = ResolveImage(viewport, address);
                        if (!pictureIndex.TryGetValue(url, out int index))
                        {
                            pictureIndex[url] = index = pictures.Count;
                            pictures.Add(url);
                        }
                        faces.Append("[1,").Append(index).Append(',');
                        AppendShort(faces, layer.Opacity, 2);
                        AppendMatrix(faces, layer.TextureToScreen);
                    }
                    else if (layer.Text != null)
                    {
                        faces.Append("[2,").Append(Json(layer.Text)).Append(',').Append((int)Math.Round(layer.FontSize)).Append(',').Append(layer.Bold ? 1 : 0)
                            .Append(',').Append(Colour(layer.TextColor, 1)).Append(',')
                            .Append(layer.TextBackground is Color background && background.A > 0 ? Json(CssValues.Color(background)) : "null")
                            .Append(',');
                        AppendShort(faces, layer.Opacity, 2);
                        AppendMatrix(faces, layer.TextureToScreen);
                    }
                    else
                    {
                        faces.Append("[0,").Append(Colour(layer.Color, layer.Opacity));
                    }
                    foreach (Point point in face.Points)
                    {
                        faces.Append(',');
                        AppendShort(faces, point.X);
                        faces.Append(',');
                        AppendShort(faces, point.Y);
                    }
                    faces.Append("],");
                }
            }
            if (faces.Length > 0)
            {
                faces.Length--;     // the last comma
            }

            var scene = new StringBuilder("{\"img\":[");
            for (int i = 0; i < pictures.Count; i++)
            {
                scene.Append(i > 0 ? "," : "").Append(Json(pictures[i]));
            }
            scene.Append("],\"f\":[").Append(faces).Append("]}");

            OpenTag("div", viewport, slot, "w3d", "");
            _html.Append("<canvas width=\"").Append(CssValues.Number(width)).Append("\" height=\"").Append(CssValues.Number(height))
                .Append("\" style=\"display:block\" data-scene=\"").Append(CssValues.Escape(scene.ToString())).Append("\"></canvas></div>");
        }

        private static void AppendMatrix(StringBuilder target, Matrix m)
        {
            foreach (double value in new[] { m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY })
            {
                target.Append(',');
                AppendShort(target, value);
            }
        }

        /// <summary>
        /// Appends a number with <paramref name="decimals"/> decimals (one for coordinates: a tenth of a pixel is
        /// below what the screen shows; two for opacities). Written by hand: a scene has some hundred thousand
        /// numbers per frame, and double formatting is slow in WebAssembly.
        /// </summary>
        private static void AppendShort(StringBuilder target, double value, int decimals = 1)
        {
            long scale = decimals == 2 ? 100 : 10;
            if (!double.IsFinite(value) || Math.Abs(value) > 1e9)
            {
                target.Append('0');
                return;
            }
            long scaled = (long)Math.Round(value * scale);
            if (scaled < 0)
            {
                target.Append('-');
                scaled = -scaled;
            }
            target.Append(scaled / scale);
            long fraction = scaled % scale;
            if (fraction != 0)
            {
                target.Append('.');
                if (decimals == 2 && fraction < 10)
                {
                    target.Append('0');
                }
                target.Append(decimals == 2 && fraction % 10 == 0 ? fraction / 10 : fraction);
            }
        }

        private static string Json(string text) => global::System.Text.Json.JsonSerializer.Serialize(text ?? "");
    }
}

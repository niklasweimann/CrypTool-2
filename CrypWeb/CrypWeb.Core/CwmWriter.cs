using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CrypTool.Web.Core;

/// <summary>
/// Writes CrypTool 2 workspace files (.cwm = gzip + "XMLSerialization" object graph) that the desktop application can open.
/// Fields the editor does not model are replayed from <see cref="VisualNode.Extra"/> (the original file), everything else is generated.
/// </summary>
public static class CwmWriter
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const string M = "WorkspaceManager.Model.";

    public static byte[] Write(Workspace ws)
    {
        var sb = new StringBuilder();
        var w = new Emitter(sb, ws);
        w.EmitAll();
        var utf8 = new UTF8Encoding(true); // like the original: UTF-8 with BOM
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
            var bytes = utf8.GetPreamble().Concat(utf8.GetBytes(sb.ToString())).ToArray();
            gz.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    /// <summary>Signature of the geometry a stored wire route is valid for (see Wire.RouteSig).</summary>
    public static string RouteSig(Wire w) => Emitter.RouteSig(w);

    sealed class Emitter
    {
        readonly StringBuilder o; readonly Workspace ws;
        int nextId;
        readonly int idModel, idWorkspace;
        readonly Dictionary<PluginNode, int> idPersistPlugin = new();
        readonly List<(int id, string name, string type, string value)> settingIds = new();

        public Emitter(StringBuilder sb, Workspace workspace)
        {
            o = sb; ws = workspace; nextId = ws.NextId();
            idModel = nextId++; idWorkspace = nextId++;
        }

        // ---------- value helpers ----------
        static string X(Dictionary<string, string> extra, string name, string dflt) => extra.TryGetValue(name, out var v) ? v : dflt;
        static string D(double d) => d.ToString("R", Inv);
        static string Pt(double x, double y) => D(x) + ";" + D(y);
        static string B(bool b) => b ? "True" : "False";
        static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

        // member name as written by the original serializer: "<X>k__BackingField" becomes "&amp;lt;X&amp;gtk__BackingField"
        static string N(string name) => name.StartsWith("<") ? "&amp;lt;" + name.Substring(1, name.IndexOf('>') - 1) + "&amp;gtk__BackingField" : name;

        void Begin(string type, int id) { o.Append("<object>\n<type>").Append(type).Append("</type>\n<id>").Append(id).Append("</id>\n<members>\n"); }
        void End() => o.Append("</members>\n</object>\n");
        void Head(string name, string type) => o.Append("<member>\n<name>").Append(N(name)).Append("</name>\n<type>").Append(Esc(type)).Append("</type>\n");
        void Tail() => o.Append("</member>\n");

        void Str(string name, string value) { Head(name, "System.String"); o.Append("<value><![CDATA[").Append(B64(value)).Append("]]></value>\n<B64Encoded/>\n"); Tail(); }
        void Prim(string name, string type, string value) { Head(name, type); o.Append("<value><![CDATA[").Append(value).Append("]]></value>\n"); Tail(); }
        void Int(string name, int v) => Prim(name, "System.Int32", v.ToString(Inv));
        void Dbl(string name, string v) => Prim(name, "System.Double", v);
        void Bool(string name, bool v) => Prim(name, "System.Boolean", B(v));
        void Pnt(string name, string v) => Prim(name, "System.Windows.Point", v);
        void Enum(string name, string type, int v) { Head(name, type); o.Append("<value>").Append(v).Append("</value>\n"); Tail(); }
        void Ref(string name, string type, int id) { Head(name, type); o.Append("<reference>").Append(id).Append("</reference>\n"); Tail(); }
        void Bytes(string name, byte[] data) { Head(name, "System.Byte[]"); o.Append("<value><![CDATA[").Append(Convert.ToBase64String(data)).Append("]]></value>\n"); Tail(); }
        void Color(string name, string v) => Prim(name, "System.Windows.Media.Color", v);
        void Refs(string name, string itemType, IEnumerable<int> ids)
        {
            Head(name, "System.Collections.Generic.List;" + itemType);
            o.Append("<list>\n");
            foreach (var id in ids) o.Append("<entry>\n<type>").Append(itemType).Append("</type>\n<reference>").Append(id).Append("</reference>\n</entry>\n");
            o.Append("</list>\n"); Tail();
        }

        /// <summary>The common base fields every model element has (ZIndex, Position, sizes, Name).</summary>
        static string SizeOut(Dictionary<string, string> extra, string which, double current)
        {
            // unchanged since load: write the original text (e.g. "0" = auto size) instead of the layout fallback value
            if (extra.TryGetValue(which, out var raw) && extra.TryGetValue("__" + which, out var comp) && double.TryParse(comp, NumberStyles.Float, Inv, out var c) && c == current) return raw;
            return D(current);
        }

        void Base(VisualNode n, string width, string height, string name = "")
        {
            Int("ZIndex", n.ZIndex);
            Pnt("Position", Pt(n.X, n.Y));
            Dbl("MinWidth", X(n.Extra, "MinWidth", "250"));
            Dbl("MinHeight", X(n.Extra, "MinHeight", "200"));
            Dbl("Width", width);
            Dbl("Height", height);
            Str("Name", name);
        }

        // ---------- the file ----------
        public void EmitAll()
        {
            o.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!--\n     XML serialized C# Objects\n     File created: ").Append(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", Inv))
             .Append("\n     File compressed: True\n     XMLSerialization created by Nils Kopal\n     mailto: kopal(AT)cryptool.org\n-->\n<objects>\n");

            // persistant plugins + settings get their own ids
            var settings = new Dictionary<PluginNode, List<int>>();
            foreach (var p in ws.Plugins)
            {
                var list = new List<int>();
                foreach (var kv in p.Settings)
                {
                    var type = p.SettingTypes.TryGetValue(kv.Key, out var t) ? t : "";
                    if (string.IsNullOrEmpty(type)) continue; // unknown type: the desktop loader could not restore it anyway
                    int id = nextId++; settingIds.Add((id, kv.Key, type, kv.Value)); list.Add(id);
                }
                settings[p] = list;
                // like the original: only components that have a settings object get a PersistantPlugin entry
                if (list.Count > 0 || p.Extra.ContainsKey("__persist")) idPersistPlugin[p] = nextId++;
            }

            Begin(M + "PersistantModel", idModel);
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Refs("PersistantPluginList", M + "PersistantPlugin", ws.Plugins.Where(p => idPersistPlugin.ContainsKey(p)).Select(p => idPersistPlugin[p]));
            End();

            Begin(M + "WorkspaceModel", idWorkspace);
            Dbl("<Zoom>k__BackingField", D(ws.Zoom));
            Refs("AllPluginModels", M + "PluginModel", ws.Plugins.Select(p => p.Id));
            Refs("AllConnectorModels", M + "ConnectorModel", ws.Connectors.Where(c => c.Plugin != null).Select(c => c.Id));
            Refs("AllConnectionModels", M + "ConnectionModel", ws.Wires.Where(x => x.From != null && x.To != null).Select(x => x.Id));
            Refs("AllImageModels", M + "ImageModel", ws.Images.Select(i => i.Id));
            Refs("AllTextModels", M + "TextModel", ws.Texts.Select(t => t.Id));
            Prim("ZIndex", "System.Int32", X(ws.Extra, "ZIndex", "0")); Pnt("Position", X(ws.Extra, "Position", "0;0")); Dbl("MinWidth", X(ws.Extra, "MinWidth", "250")); Dbl("MinHeight", X(ws.Extra, "MinHeight", "200")); Dbl("Width", X(ws.Extra, "Width", "0")); Dbl("Height", X(ws.Extra, "Height", "0")); Str("Name", X(ws.Extra, "Name", ""));
            End();

            foreach (var p in ws.Plugins) EmitPlugin(p);
            foreach (var c in ws.Connectors.Where(c => c.Plugin != null)) EmitConnector(c);
            foreach (var x in ws.Wires.Where(x => x.From != null && x.To != null)) EmitWire(x);
            foreach (var t in ws.Texts) EmitText(t);
            foreach (var i in ws.Images) EmitImage(i);
            foreach (var p in ws.Plugins.Where(p => idPersistPlugin.ContainsKey(p)))
            {
                Begin(M + "PersistantPlugin", idPersistPlugin[p]);
                Ref("<PluginModel>k__BackingField", M + "PluginModel", p.Id);
                Refs("PersistantSettingsList", M + "PersistantSetting", settings[p]);
                End();
            }
            foreach (var (id, name, type, value) in settingIds)
            {
                Begin(M + "PersistantSetting", id);
                Str("<Name>k__BackingField", name); Str("<Type>k__BackingField", type); Str("<Value>k__BackingField", value);
                End();
            }
            o.Append("</objects>\n");
        }

        void EmitPlugin(PluginNode p)
        {
            Begin(M + "PluginModel", p.Id);
            Int("imageIndex", p.ImageIndex);
            Str("PluginTypeName", p.TypeName);
            Str("PluginTypeAssemblyName", p.AssemblyName ?? "");
            Refs("InputConnectors", M + "ConnectorModel", p.Inputs.Select(c => c.Id));
            Refs("OutputConnectors", M + "ConnectorModel", p.Outputs.Select(c => c.Id));
            Bool("RepeatStart", X(p.Extra, "RepeatStart", "False") == "True");
            Bool("Startable", p.Startable);
            Bool("<Minimized>k__BackingField", p.Minimized);
            Dbl("<PercentageFinished>k__BackingField", "0");
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Enum("<ViewState>k__BackingField", M + "PluginViewState", p.ViewState);
            Str("<CopyID>k__BackingField", "");
            // default view: the desktop sizes the component itself, so keep the original 0 values; once resized the size is explicit
            string w = SizeOut(p.Extra, "Width", p.Width), h = SizeOut(p.Extra, "Height", p.Height);
            Base(p, w, h, p.Name ?? "");
            End();
        }

        static string Assembly(string typeName, string given)
        {
            if (!string.IsNullOrEmpty(given)) return given;
            if (typeName.StartsWith("System.Numerics")) return "System.Numerics";
            if (typeName.StartsWith("System.")) return "mscorlib";
            if (typeName.StartsWith("CrypTool.PluginBase")) return "CrypPluginBase";
            return "";
        }

        void EmitConnector(ConnectorPort c)
        {
            var asm = Assembly(c.DataTypeName, c.DataTypeAssembly);
            var typeName = asm == "" ? "" : c.DataTypeName; // unknown type: leave both empty, the desktop resolves it from the plugin property
            Begin(M + "ConnectorModel", c.Id);
            Str("ConnectorTypeName", typeName);
            Str("ConnectorTypeAssemblyName", asm);
            Ref("<PluginModel>k__BackingField", M + "PluginModel", c.Plugin!.Id);
            Int("Index", c.Index);
            Bool("<Outgoing>k__BackingField", c.Outgoing);
            Bool("<IControl>k__BackingField", X(c.Extra, "IControl", "False") == "True");
            Refs("InputConnections", M + "ConnectionModel", c.Wires.Where(x => x.To == c && x.From != null).Select(x => x.Id));
            Refs("OutputConnections", M + "ConnectionModel", c.Wires.Where(x => x.From == c && x.To != null).Select(x => x.Id));
            Enum("Orientation", M + "ConnectorOrientation", c.Orientation is >= 0 and <= 4 ? c.Orientation : 4);
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Bool("<IsMandatory>k__BackingField", c.IsMandatory);
            Str("<PropertyName>k__BackingField", c.PropertyName);
            Str("<ToolTip>k__BackingField", c.ToolTip ?? "");
            Base(c, SizeOut(c.Extra, "Width", c.Width), SizeOut(c.Extra, "Height", c.Height), c.Name ?? "");
            End();
        }

        void EmitWire(Wire w)
        {
            var asm = Assembly(w.From!.DataTypeName, w.From.DataTypeAssembly);
            Begin(M + "ConnectionModel", w.Id);
            Str("ConnectionTypeName", X(w.Extra, "ConnectionTypeName", asm == "" ? "" : w.From.DataTypeName));
            Str("ConnectionTypeAssemblyName", X(w.Extra, "ConnectionTypeAssemblyName", asm));
            // keep the stored route only while both components are where they were; otherwise let the desktop route it again
            bool keep = w.Points.Count > 0 && w.RouteSig != "" && w.RouteSig == RouteSig(w);
            if (keep)
            {
                Head("pointList", "System.Collections.Generic.List;System.Windows.Point");
                o.Append("<list>\n");
                foreach (var (x, y) in w.Points) o.Append("<entry>\n<type>System.Windows.Point</type>\n<value><![CDATA[").Append(Pt(x, y)).Append("]]></value>\n</entry>\n");
                o.Append("</list>\n"); Tail();
            }
            else
            {
                // what the original writes for a null list (ConnectionModel.PointList = null is a normal state)
                Head("pointList", "System.Collections.Generic.List`1[[System.Windows.Point, WindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35]]");
                o.Append("<value></value>\n"); Tail();
            }
            Ref("<From>k__BackingField", M + "ConnectorModel", w.From.Id);
            Ref("<To>k__BackingField", M + "ConnectorModel", w.To!.Id);
            Bool("<Active>k__BackingField", false);
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Bool("<IsCopy>k__BackingField", false);
            Base(w, SizeOut(w.Extra, "Width", w.Width), SizeOut(w.Extra, "Height", w.Height));
            End();
        }

        public static string RouteSig(Wire w) =>
            w.From?.Plugin == null || w.To?.Plugin == null ? "" :
            string.Join("|", new[] { w.From.Plugin, w.To.Plugin }.Select(p => string.Format(Inv, "{0:0.##};{1:0.##};{2:0.##};{3:0.##};{4}", p.X, p.Y, p.Width, p.Height, p.Minimized))) + "|" + w.From.Index + ">" + w.To.Index;

        void EmitText(TextNode t)
        {
            // keep the original rich text (formatting) as long as the text was not edited
            var data = t.OriginalData != null && t.Text == t.OriginalText ? t.OriginalData : XamlPackage(t.Text);
            Begin(M + "TextModel", t.Id);
            Bytes("data", data);
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Bool("<IsEnabled>k__BackingField", X(t.Extra, "IsEnabled", "True") != "False");
            Color("BackgroundColor", t.BackgroundColor ?? X(t.Extra, "BackgroundColor", "255;255;255;255"));
            Int("ZIndex", t.ZIndex);
            Pnt("Position", Pt(t.X, t.Y));
            Dbl("MinWidth", X(t.Extra, "MinWidth", "10")); Dbl("MinHeight", X(t.Extra, "MinHeight", "10"));
            Dbl("Width", SizeOut(t.Extra, "Width", t.Width)); Dbl("Height", SizeOut(t.Extra, "Height", t.Height));
            Str("Name", "");
            End();
        }

        void EmitImage(ImageNode i)
        {
            Begin(M + "ImageModel", i.Id);
            Bytes("data", i.Data);
            Ref("<WorkspaceModel>k__BackingField", M + "WorkspaceModel", idWorkspace);
            Int("ZIndex", i.ZIndex);
            Pnt("Position", Pt(i.X, i.Y));
            Dbl("MinWidth", X(i.Extra, "MinWidth", "10")); Dbl("MinHeight", X(i.Extra, "MinHeight", "10"));
            Dbl("Width", SizeOut(i.Extra, "Width", i.Width)); Dbl("Height", SizeOut(i.Extra, "Height", i.Height));
            Str("Name", "");
            End();
        }
    }

    /// <summary>A minimal WPF "XamlPackage" (zip with Xaml/Document.xaml) holding the plain text, one paragraph per line.</summary>
    public static byte[] XamlPackage(string text)
    {
        var ns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var section = new XElement(ns + "Section",
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            new XAttribute("TextAlignment", "Left"), new XAttribute("FontFamily", "Segoe UI"), new XAttribute("FontSize", "12"), new XAttribute("Foreground", "#FF000000"));
        foreach (var line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            section.Add(new XElement(ns + "Paragraph", new XAttribute("Margin", "0,0,0,0"), new XElement(ns + "Run", line)));
        string xaml = section.ToString(SaveOptions.DisableFormatting); // the document part has no XML declaration
        string rels = "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Type=\"http://schemas.microsoft.com/wpf/2005/10/xaml/entry\" Target=\"/Xaml/Document.xaml\" Id=\"R" + Guid.NewGuid().ToString("N")[..16] + "\" /></Relationships>";
        string types = "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xaml\" ContentType=\"application/vnd.ms-wpf.xaml+xml\" /><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" /></Types>";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string content, CompressionLevel lvl)
            {
                var e = zip.CreateEntry(name, lvl);
                using var s = e.Open(); var b = new UTF8Encoding(false).GetBytes(content); s.Write(b, 0, b.Length);
            }
            Add("Xaml/Document.xaml", xaml, CompressionLevel.Optimal);
            Add("_rels/.rels", rels, CompressionLevel.NoCompression);
            Add("[Content_Types].xml", types, CompressionLevel.NoCompression);
        }
        return ms.ToArray();
    }
}

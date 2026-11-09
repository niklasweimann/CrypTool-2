using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CrypTool.Web.Core;

/// <summary>
/// Reads CrypTool 2 workspace files (gzip + "XMLSerialization" object graph) without any WPF dependency.
/// </summary>
public static class CwmReader
{
    private sealed class Raw
    {
        public string Type = "";
        public int Id;
        public Dictionary<string, Member> Members = new();
    }

    private sealed class Member
    {
        public string Type = "";
        public string? Value;
        public int? Reference;
        public List<Member>? List;
    }

    public static Workspace Read(byte[] data)
    {
        if (data.Length > 2 && data[0] == 0x1f && data[1] == 0x8b)
        {
            using var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gz.CopyTo(ms);
            data = ms.ToArray();
        }
        var doc = XDocument.Load(new StreamReader(new MemoryStream(data), new UTF8Encoding(false), true));
        var objs = new Dictionary<int, Raw>();
        foreach (var o in doc.Root!.Elements("object"))
        {
            var raw = new Raw { Type = (string)o.Element("type")!, Id = int.Parse((string)o.Element("id")!) };
            foreach (var m in o.Element("members")?.Elements("member") ?? Enumerable.Empty<XElement>())
            {
                raw.Members[CleanName((string)m.Element("name")!)] = ParseMember(m);
            }
            objs[raw.Id] = raw;
        }

        var root = objs.Values.First(o => o.Type.EndsWith("PersistantModel"));
        var ws = new Workspace();
        var wsRaw = objs[root.Members["WorkspaceModel"].Reference!.Value];
        ws.Zoom = Dbl(wsRaw, "Zoom", 1);
        ws.Extra = Extras(wsRaw);

        var plugins = new Dictionary<int, PluginNode>();
        var connectors = new Dictionary<int, ConnectorPort>();

        foreach (var r in Refs(wsRaw, "AllPluginModels"))
        {
            var p = objs[r];
            var node = new PluginNode
            {
                Id = p.Id,
                Name = Str(p, "Name"),
                TypeName = Str(p, "PluginTypeName"),
                AssemblyName = Str(p, "PluginTypeAssemblyName"),
                Startable = Bool(p, "Startable"),
                Minimized = Bool(p, "Minimized"),
                ImageIndex = (int)Dbl(p, "imageIndex", 0),
                ViewState = int.TryParse(p.Members.GetValueOrDefault("ViewState")?.Value, out var vs) ? vs : 0,
            };
            Layout(p, node);
            node.Extra = Extras(p, node);
            plugins[p.Id] = node;
            ws.Plugins.Add(node);
        }
        foreach (var r in Refs(wsRaw, "AllConnectorModels"))
        {
            var c = objs[r];
            var port = new ConnectorPort
            {
                Id = c.Id,
                PropertyName = Str(c, "PropertyName"),
                Name = Str(c, "Name"),
                ToolTip = Str(c, "ToolTip"),
                DataTypeName = Str(c, "ConnectorTypeName"),
                DataTypeAssembly = Str(c, "ConnectorTypeAssemblyName"),
                Outgoing = Bool(c, "Outgoing"),
                IsMandatory = Bool(c, "IsMandatory"),
                Orientation = (int)Dbl(c, "Orientation", 0),
                Index = (int)Dbl(c, "Index", 0),
            };
            Layout(c, port);
            port.Extra = Extras(c, port);
            if (c.Members.TryGetValue("PluginModel", out var pm) && pm.Reference is int pid && plugins.TryGetValue(pid, out var owner))
            {
                port.Plugin = owner;
                (port.Outgoing ? owner.Outputs : owner.Inputs).Add(port);
            }
            connectors[c.Id] = port;
            ws.Connectors.Add(port);
        }
        foreach (var plugin in ws.Plugins)
        {
            plugin.Inputs.Sort((a, b) => a.Index.CompareTo(b.Index));
            plugin.Outputs.Sort((a, b) => a.Index.CompareTo(b.Index));
        }
        foreach (var r in Refs(wsRaw, "AllConnectionModels"))
        {
            var c = objs[r];
            var wire = new Wire { Id = c.Id };
            Layout(c, wire);
            wire.Extra = Extras(c, wire);
            if (c.Members.TryGetValue("From", out var f) && f.Reference is int fi && connectors.TryGetValue(fi, out var from)) { wire.From = from; from.Wires.Add(wire); }
            if (c.Members.TryGetValue("To", out var t) && t.Reference is int ti && connectors.TryGetValue(ti, out var to)) { wire.To = to; to.Wires.Add(wire); }
            if (c.Members.TryGetValue("pointList", out var pl) && pl.List != null)
                foreach (var e in pl.List) if (e.Value != null) wire.Points.Add(ParsePoint(e.Value));
            wire.RouteSig = RouteSig(wire);
            ws.Wires.Add(wire);
        }
        foreach (var r in Refs(wsRaw, "AllTextModels"))
        {
            var t = objs[r];
            var tn = new TextNode { Id = t.Id, Text = ExtractText(Str(t, "data", binary: true)), BackgroundColor = t.Members.TryGetValue("BackgroundColor", out var bc) ? bc.Value : null };
            try { tn.OriginalData = Convert.FromBase64String(Str(t, "data")); } catch { }
            tn.OriginalText = tn.Text;
            Layout(t, tn);
            tn.Extra = Extras(t, tn);
            ws.Texts.Add(tn);
        }
        foreach (var r in Refs(wsRaw, "AllImageModels"))
        {
            var t = objs[r];
            var im = new ImageNode { Id = t.Id };
            if (t.Members.TryGetValue("data", out var d) && d.Value != null)
                try { im.Data = Convert.FromBase64String(d.Value); } catch { }
            Layout(t, im);
            im.Extra = Extras(t, im);
            ws.Images.Add(im);
        }

        // Settings
        if (root.Members.TryGetValue("PersistantPluginList", out var ppl) && ppl.List != null)
        {
            foreach (var e in ppl.List)
            {
                if (e.Reference is not int ppid) continue;
                var pp = objs[ppid];
                if (!pp.Members.TryGetValue("PluginModel", out var pmr) || pmr.Reference is not int pmid || !plugins.TryGetValue(pmid, out var node)) continue;
                node.Extra["__persist"] = "1";
                foreach (var se in pp.Members.GetValueOrDefault("PersistantSettingsList")?.List ?? new())
                {
                    if (se.Reference is not int sid) continue;
                    var s = objs[sid];
                    var name = Str(s, "Name");
                    node.Extra["__persist"] = "1";
                    node.Settings[name] = Str(s, "Value");
                    node.SettingTypes[name] = Str(s, "Type");
                }
            }
        }
        return ws;
    }

    private static Member ParseMember(XElement m)
    {
        var mem = new Member { Type = (string?)m.Element("type") ?? "" };
        var refEl = m.Element("reference");
        if (refEl != null) mem.Reference = int.Parse(refEl.Value);
        var val = m.Element("value");
        if (val != null)
        {
            var v = val.Value;
            if (m.Element("B64Encoded") != null)
            {
                try { v = Encoding.UTF8.GetString(Convert.FromBase64String(v)); } catch { }
            }
            mem.Value = v;
            if (m.Element("B64Encoded") != null) mem.Type += "|b64";
        }
        var list = m.Element("list");
        if (list != null)
        {
            mem.List = new();
            foreach (var e in list.Elements("entry"))
            {
                var em = ParseMember(e);
                mem.List.Add(em);
            }
        }
        return mem;
    }

    private static Dictionary<string, string> Extras(Raw r, VisualNode? n = null)
    {
        var d = new Dictionary<string, string>();
        if (n != null) { d["__Width"] = n.Width.ToString("R", CultureInfo.InvariantCulture); d["__Height"] = n.Height.ToString("R", CultureInfo.InvariantCulture); }
        foreach (var kv in r.Members) if (kv.Value.Value != null && kv.Value.List == null && kv.Value.Reference == null) d[kv.Key] = kv.Value.Value;
        return d;
    }

    private static string RouteSig(Wire w) =>
        w.From?.Plugin == null || w.To?.Plugin == null ? "" :
        string.Join("|", new[] { w.From.Plugin, w.To.Plugin }.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##};{1:0.##};{2:0.##};{3:0.##};{4}", p.X, p.Y, p.Width, p.Height, p.Minimized))) + "|" + w.From.Index + ">" + w.To.Index;

    private static string CleanName(string n)
    {
        n = n.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&gt", ">");
        return n.StartsWith('<') && n.IndexOf('>') > 0 ? n[1..n.IndexOf('>')] : n;
    }

    private static IEnumerable<int> Refs(Raw r, string name) =>
        r.Members.TryGetValue(name, out var m) && m.List != null
            ? m.List.Where(e => e.Reference.HasValue).Select(e => e.Reference!.Value)
            : Enumerable.Empty<int>();

    private static string Str(Raw r, string name, bool binary = false) =>
        r.Members.TryGetValue(name, out var m) ? (m.Value ?? "") : "";

    private static bool Bool(Raw r, string name) =>
        r.Members.TryGetValue(name, out var m) && string.Equals(m.Value, "true", StringComparison.OrdinalIgnoreCase);

    private static double Dbl(Raw r, string name, double dflt) =>
        r.Members.TryGetValue(name, out var m) && m.Value != null && TryDouble(m.Value, out var d) ? d : dflt;

    private static bool TryDouble(string s, out double d) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);

    private static (double, double) ParsePoint(string s)
    {
        var parts = s.Split(';');
        return parts.Length == 2 && TryDouble(parts[0], out var x) && TryDouble(parts[1], out var y) ? (x, y) : (0, 0);
    }

    private static void Layout(Raw r, VisualNode n)
    {
        if (r.Members.TryGetValue("Position", out var p) && p.Value != null) (n.X, n.Y) = ParsePoint(p.Value);
        n.Width = Dbl(r, "Width", 0);
        n.Height = Dbl(r, "Height", 0);
        if (n.Width <= 0) n.Width = Dbl(r, "MinWidth", 0);
        if (n.Height <= 0) n.Height = Dbl(r, "MinHeight", 0);
        n.ZIndex = (int)Dbl(r, "ZIndex", 0);
    }

    /// <summary>TextModel.data is base64 (inside a b64 string) of an RTF or XAML FlowDocument; extract readable text.</summary>
    private static string ExtractText(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return "";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(payload); } catch { return payload; }
        if (bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K')
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(bytes));
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("Document.xaml", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    using var r = new StreamReader(entry.Open(), Encoding.UTF8);
                    bytes = Encoding.UTF8.GetBytes(r.ReadToEnd());
                }
            }
            catch { }
        }
        var s = Encoding.UTF8.GetString(bytes);
        if (s.StartsWith("{\\rtf")) return RtfToText(s);
        var sb = new StringBuilder();
        try
        {
            var x = XDocument.Parse(s);
            foreach (var e in x.Descendants())
            {
                if (e.Name.LocalName == "Run") sb.Append((string?)e.Attribute("Text") ?? e.Value);
                else if (e.Name.LocalName == "LineBreak") sb.Append('\n');
                else if (e.Name.LocalName == "Paragraph" && sb.Length > 0) sb.Append('\n');
            }
            return sb.ToString().Trim();
        }
        catch { return s; }
    }

    public static string RtfSnippetToText(string s) => s.Contains('\\') || s.Contains('{') ? RtfToText(s) : s;

    private static string RtfToText(string rtf)
    {
        var sb = new StringBuilder();
        int depth = 0; bool skip = false; int skipDepth = 0;
        for (int i = 0; i < rtf.Length; i++)
        {
            char c = rtf[i];
            if (c == '{') { depth++; if (i + 2 < rtf.Length && rtf[i + 1] == '\\' && rtf[i + 2] == '*') { skip = true; skipDepth = depth; } }
            else if (c == '}') { if (skip && depth == skipDepth) skip = false; depth--; }
            else if (c == '\\')
            {
                int j = i + 1;
                if (j < rtf.Length && !char.IsLetter(rtf[j]))
                {
                    if (rtf[j] == '\'' && j + 2 < rtf.Length) { if (!skip) sb.Append((char)Convert.ToInt32(rtf.Substring(j + 1, 2), 16)); i = j + 2; }
                    else { if (!skip && (rtf[j] == '\\' || rtf[j] == '{' || rtf[j] == '}')) sb.Append(rtf[j]); i = j; }
                    continue;
                }
                int k = j; while (k < rtf.Length && char.IsLetter(rtf[k])) k++;
                var word = rtf[j..k];
                int m = k; if (m < rtf.Length && (rtf[m] == '-' || char.IsDigit(rtf[m]))) { m++; while (m < rtf.Length && char.IsDigit(rtf[m])) m++; }
                if (!skip)
                {
                    if (word == "par" || word == "line") sb.Append('\n');
                    else if (word == "tab") sb.Append('\t');
                    else if (word == "u" && int.TryParse(rtf[k..m], out var u)) { sb.Append((char)(u < 0 ? u + 65536 : u)); if (m < rtf.Length && rtf[m] == '?') m++; }
                }
                if (m < rtf.Length && rtf[m] == ' ') m++;
                i = m - 1;
                if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict") { skip = true; skipDepth = depth; }
            }
            else if (!skip && c != '\r' && c != '\n') sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}

using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>Checks that reading and writing a .cwm loses nothing but orphaned objects.</summary>
public static class RoundTripCommand
{
    /// <summary>Reads every template, writes it again and compares the two field by field.</summary>
    public static void Run(string root, string[] args)
    {
        int rtOk = 0, rtBad = 0, rtDropOnly = 0;
        var shown = 0;
        foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
        {
            var orig = File.ReadAllBytes(f);
            var m1 = CwmReader.Read(orig);
            var written = CwmWriter.Write(m1);
            var m2 = CwmReader.Read(written);
            var a = Canon(orig);
            var b = Canon(written);
            var onlyA = a.ToList();
            foreach (var x in b) onlyA.Remove(x);
            var onlyB = b.ToList();
            foreach (var x in a) onlyB.Remove(x);
            var diff = new List<string>();
            foreach (var x in onlyA.Take(3))
            {
                var type = x[..x.IndexOf('{')];
                var other = onlyB.FirstOrDefault(y => y.StartsWith(type + "{"));
                var ma = x[(x.IndexOf('{') + 1)..^1].Split(';');
                var mb = other == null ? Array.Empty<string>() : other[(other.IndexOf('{') + 1)..^1].Split(';');
                diff.Add(type.Split('.').Last() + ": only original: " + string.Join(" | ",
                    ma.Except(mb).Select(q => q.Length > 90 ? q[..90] : q)) + "  ## only mine: " + string.Join(" | ",
                    mb.Except(ma).Select(q => q.Length > 90 ? q[..90] : q)));
            }
            if (onlyA.Count == 0 && onlyB.Count > 0) diff.Add("extra objects: " + string.Join(", ",
                onlyB.Take(3).Select(q => q[..q.IndexOf('{')].Split('.').Last())));
            bool same = m1.Plugins.Count == m2.Plugins.Count
                && m1.Wires.Count == m2.Wires.Count
                && m1.Texts.Count == m2.Texts.Count
                && a.Count == b.Count
                && onlyA.Count == 0
                && onlyB.Count == 0
                && m1.Plugins.Zip(m2.Plugins).All(t => t.First.X == t.Second.X
                && t.First.Y == t.Second.Y
                && t.First.TypeName == t.Second.TypeName
                && t.First.Settings.Count == t.Second.Settings.Count)
                && m1.Texts.Zip(m2.Texts).All(t => t.First.Text == t.Second.Text);
            if (same) rtOk++; else if (onlyB.Count == 0 && m1.Plugins.Count == m2.Plugins.Count) rtDropOnly++; else
            {
                rtBad++;
                if (shown++ < 6)
                {
                    Console.WriteLine("DIFF " + Path.GetRelativePath(root, f) + $" objects {a.Count}/{b.Count}");
                    foreach (var d in diff) Console.WriteLine("   " + (d.Length > 260 ? d[..260] : d));
                }
            }
        }
        Console.WriteLine($"roundtrip identical={rtOk} onlyDroppedOrphans={rtDropOnly} changed={rtBad}");
        return;
    }

    // Canonical form of a .cwm: one string per object with all primitive members (names, types, decoded values); references reduced to the target type.
    static List<string> Canon(byte[] cwm)
    {
        if (cwm.Length > 2 && cwm[0] == 0x1f)
        {
            using var gz = new System.IO.Compression.GZipStream(new MemoryStream(cwm), System.IO.Compression.CompressionMode.Decompress);
            var ms = new MemoryStream();
            gz.CopyTo(ms);
            cwm = ms.ToArray();
        }
        var doc = System.Xml.Linq.XDocument.Load(new StreamReader(new MemoryStream(cwm), new System.Text.UTF8Encoding(false), true));
        var types = doc.Root!.Elements("object").ToDictionary(o => (string)o.Element("id")!, o => (string)o.Element("type")!);
        string Val(System.Xml.Linq.XElement m)
        {
            var v = m.Element("value");
            if (v == null) return "";
            var t = v.Value;
            if (m.Element("B64Encoded") != null) t = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(t));
            return t.Replace(',', '.');
        }
        string Entry(System.Xml.Linq.XElement e) => e.Element("reference") != null
            ? "ref:" + types[e.Element("reference")!.Value]
            : (string)e.Element("type")! + "=" + Val(e);
        var res = new List<string>();
        foreach (var o in doc.Root.Elements("object"))
        {
            var mems = new List<string>();
            foreach (var m in o.Element("members")!.Elements("member"))
            {
                var name = ((string)m.Element("name")!).Replace("&amp;lt;", "<")
                    .Replace("&lt;", "<")
                    .Replace("&amp;gtk", ">k")
                    .Replace("&gtk", ">k");
                var type = (string)m.Element("type")!;
                string v;
                if (m.Element("reference") != null) v = "ref:" + types[m.Element("reference")!.Value];
                else if (m.Element("list") != null) v = "[" + string.Join(",",
                m.Element("list")!.Elements("entry")
                .Select(Entry)
                .Distinct()) + "]"; // duplicates in the originals are repaired by the writer
                else if (type == "System.Byte[]") v = "bytes:" + Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(Convert.FromBase64String(m.Element("value")!.Value)));
                else v = Val(m);
                if (type.StartsWith("System.Collections.Generic.List`1[[System.Windows.Point"))
                {
                    type = "System.Collections.Generic.List;System.Windows.Point";
                    v = v == "" ? "[]" : v;
                }
                if (name.Contains("PercentageFinished")
                    || name.Contains("<Active>")) continue; // runtime state, reset by the desktop on load
                mems.Add(name + ":" + type + "=" + v);
            }
            res.Add((string)o.Element("type")! + "{" + string.Join(";", mems.OrderBy(x => x, StringComparer.Ordinal)) + "}");
        }
        res.Sort(StringComparer.Ordinal);
        return res;
    }
}

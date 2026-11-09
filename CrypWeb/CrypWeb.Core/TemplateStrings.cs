using System.Xml.Linq;

namespace CrypTool.Web.Core;

/// <summary>Resolves the $key$ placeholders of a template from its sibling .xml file.</summary>
public static class TemplateStrings
{
    public static Dictionary<string, string> Load(byte[] xml, string lang = "en")
    {
        var map = new Dictionary<string, string>();
        try
        {
            var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(xml).TrimStart('﻿'));
            foreach (var l in new[] { "en", lang }) // later language wins
                foreach (var rep in doc.Descendants("replacements").Where(r => (string?)r.Attribute("lang") == l).Elements("replacement"))
                    map[(string)rep.Attribute("key")!] = (string)rep.Attribute("value")!;
        }
        catch { }
        return map;
    }

    public static string Apply(string s, Dictionary<string, string> map)
    {
        foreach (var kv in map) s = s.Replace(kv.Key, CwmReader.RtfSnippetToText(kv.Value));
        return s;
    }

    public static void Apply(Workspace ws, Dictionary<string, string> map)
    {
        if (map.Count == 0) return;
        foreach (var p in ws.Plugins) p.Name = Apply(p.Name, map);
        foreach (var t in ws.Texts) t.Text = Apply(t.Text, map);
        foreach (var p in ws.Plugins) foreach (var k in p.Settings.Keys.ToList()) p.Settings[k] = Apply(p.Settings[k], map);
    }
}

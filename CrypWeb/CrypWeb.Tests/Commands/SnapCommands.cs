using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>Writes rendered WPF presentations as standalone pages, to compare them with the originals in a browser.</summary>
public static class SnapCommands
{
    /// <summary>Runs one template briefly and writes every rendered presentation to wwwroot/_snap/.</summary>
    public static async Task Snap(string root, string[] args)
    {
        // Runs one template briefly and writes every rendered WPF presentation as a standalone page to
        // CrypWeb.App/wwwroot/_snap/, so the views can be compared with the originals in a browser at a fixed size
        // (served by the dev server; the folder is git-ignored).
        var outDir = Path.Combine(AppContext.BaseDirectory, "../../../../CrypWeb.App/wwwroot/_snap");
        System.Windows.Hosting.WpfRuntime.UnhandledException += e => Console.WriteLine("PRESENTATION ERROR " + e);
        Directory.CreateDirectory(outDir);
        var ws = CwmReader.Read(File.ReadAllBytes(Path.Combine(root, args[2])));
        var engine = new ExecutionEngine();
        var run = engine.RunAsync(ws);
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(50);
            System.Windows.Hosting.WpfRuntime.Tick();
        }
        engine.Stop();
        await Task.WhenAny(run, Task.Delay(4000));
        // optional "Element.Property=value" arguments put a view into a state first (e.g. tick a checkbox)
        var assignments = args.Skip(3).ToList();
        foreach (var node in engine.Nodes.Values)
        {
            if (node.Comp is not PluginComponent { Host: { } host }) continue;
            host.Render();      // assigns the element ids that "size:" arguments refer to
            foreach (var assignment in assignments)
            {
                if (assignment.StartsWith("size:")) SnapSize(host, assignment);
                else if (assignment.StartsWith("event:")) SnapEvent(host, assignment);
                else SnapAssign(host.Root, assignment);
            }
            var name = node.Model.TypeName.Split('.').Last();
            var page = SnapPage(name, host.Render());
            File.WriteAllText(Path.Combine(outDir, name + ".html"), page);
            Console.WriteLine("wrote _snap/" + name + ".html");
        }
        foreach (var d in System.Windows.Markup.XamlDiagnostics.Snapshot().Take(20)) Console.WriteLine($"  XAML {d.Value}x {d.Key}");
        return;
    }

    /// <summary>Writes every presentation once, plus an overview page.</summary>
    public static async Task SnapAll(string root, string[] args)
    {
        // every presentation once: runs a template whenever it contains a component type not seen yet, writes the
        // views to CrypWeb.App/wwwroot/_snap/ and an overview page _snap/index.html that shows them side by side
        var outDir = Path.Combine(AppContext.BaseDirectory, "../../../../CrypWeb.App/wwwroot/_snap");
        Directory.CreateDirectory(outDir);
        var seenTypes = new HashSet<string>();
        var written = new SortedDictionary<string, string>();   // view name -> template it came from
        foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var ws = CwmReader.Read(File.ReadAllBytes(f));
            if (ws.Plugins.All(p => seenTypes.Contains(p.TypeName))) continue;
            var engine = new ExecutionEngine { ExecuteInBackground = true };
            var run = engine.RunAsync(ws);
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(50);
                System.Windows.Hosting.WpfRuntime.Tick();
            }
            engine.Stop();
            await Task.WhenAny(run, Task.Delay(4000));
            foreach (var node in engine.Nodes.Values)
            {
                seenTypes.Add(node.Model.TypeName);
                var name = node.Model.TypeName.Split('.').Last();
                if (written.ContainsKey(name) || node.Comp is not PluginComponent { Host: { } host }) continue;
                try
                {
                    File.WriteAllText(Path.Combine(outDir, name + ".html"), SnapPage(name, host.Render()));
                    written[name] = Path.GetRelativePath(root, f);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"FAILED {name}: {e.Message}");
                }
            }
        }
        var index = new System.Text.StringBuilder("<!doctype html><meta charset=utf-8><title>All views</title><style>"
            + "body{font:12px sans-serif;margin:8px;background:#ddd}div.t{display:inline-block;margin:4px;background:#fff;vertical-align:top}"
            + "div.f{width:400px;height:300px;overflow:hidden}iframe{width:800px;height:600px;border:0;transform:scale(.5);transform-origin:0 0}"
            + "</style>");
        // reference pictures of the original views (CrypWeb/reference/<View>.png|jpg) are shown next to the rendering
        var referenceDir = Path.Combine(AppContext.BaseDirectory, "../../../../reference");
        var referenceOut = Path.Combine(outDir, "ref");
        Directory.CreateDirectory(referenceOut);
        foreach (var (name, template) in written)
        {
            string? picture = Directory.Exists(referenceDir)
                ? Directory.EnumerateFiles(referenceDir, name + ".*").FirstOrDefault(p => !p.EndsWith(".md"))
                : null;
            index.Append($"<div class=t><b>{name}</b> <small>{System.Net.WebUtility.HtmlEncode(template)}</small><div class=f><iframe loading=lazy src=\"{name}.html\"></iframe></div></div>");
            if (picture != null)
            {
                File.Copy(picture, Path.Combine(referenceOut, Path.GetFileName(picture)), overwrite: true);
                index.Append($"<div class=t><b>{name}</b> <small>original (WPF)</small><div class=f><img style=\"max-width:400px;max-height:300px\" src=\"ref/{Path.GetFileName(picture)}\"></div></div>");
            }
        }
        File.WriteAllText(Path.Combine(outDir, "index.html"), index.ToString());
        Console.WriteLine($"wrote {written.Count} views and _snap/index.html");
        return;
    }

    /// <summary>A standalone page that shows one rendered presentation with the app's own wpf.css / wpf.js.</summary>
    static string SnapPage(string title, string html)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(html);
        return $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><title>{{title}}</title>
            <base href="/">
            <link rel="stylesheet" href="css/wpf.css">
            <style>body { margin: 0; background: #ccc; } .wpfhost { width: 1200px; height: 800px; background: #fff; position: relative; overflow: hidden; }</style>
            </head><body>
            <div class="wpfhost" id="host"></div>
            <script>
            // no worker here: collect the sizes the page measures, to pass them back as "size:id=WxH" arguments
            window.snapSizes = [];
            window.ct2engine = { presEvent(node, id, kind, value) { if (kind === 'size') snapSizes.push('size:' + id + '=' + value.replace(',', 'x')); } };
            </script>
            <script src="wpf.js"></script>
            <script>ctWpf.update(document.getElementById('host'), {{json}}, '1');</script>
            </body></html>
            """;
    }

    /// <summary>Sets "Element.Property=value" on a named element of a presentation (used by the snap command).</summary>
    static void SnapAssign(System.Windows.UIElement root, string assignment)
    {
        var equals = assignment.IndexOf('=');
        var dot = assignment.IndexOf('.');
        if (equals < 0 || dot < 0 || dot > equals) return;
        var elementName = assignment[..dot];
        var propertyName = assignment[(dot + 1)..equals];
        var text = assignment[(equals + 1)..];
        if (root is not System.Windows.FrameworkElement frameworkRoot) return;
        var element = frameworkRoot.FindName(elementName);
        var property = element?.GetType().GetProperty(propertyName);
        if (property == null) return;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var value = type.IsEnum ? Enum.Parse(type, text) : Convert.ChangeType(text,
            type,
            System.Globalization.CultureInfo.InvariantCulture);
        property.SetValue(element, value);
        Console.WriteLine($"set {elementName}.{propertyName} = {value}");
    }

    /// <summary>Applies a browser-measured size "size:id=WxH" (collected by the snap page in window.snapSizes).</summary>
    static void SnapSize(System.Windows.Hosting.PresentationHost host, string argument)
    {
        var parts = argument["size:".Length..].Split('=', 'x');
        if (parts.Length != 3) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        host.ReportSize(int.Parse(parts[0]), double.Parse(parts[1], culture), double.Parse(parts[2], culture));
    }

    /// <summary>Replays a browser input event "event:id:kind:value:x:y" (as wpf.js sends it) on a presentation.</summary>
    static void SnapEvent(System.Windows.Hosting.PresentationHost host, string argument)
    {
        var parts = argument["event:".Length..].Split(':');
        if (parts.Length != 5) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        host.Dispatch(int.Parse(parts[0]), parts[1], parts[2], double.Parse(parts[3], culture), double.Parse(parts[4], culture));
        host.Render();
        Console.WriteLine($"event {parts[1]} on {parts[0]}");
    }
}

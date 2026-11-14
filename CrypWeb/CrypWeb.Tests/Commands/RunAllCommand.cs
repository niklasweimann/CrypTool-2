using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>Runs all templates and renders all presentations: the main regression check.</summary>
public static class RunAllCommand
{
    /// <summary>Runs every template (and renders every presentation) and prints a summary.</summary>
    public static async Task Run(string root, string[] args)
    {
        DebugCommands.HookTrace();   // CT2_TRACE / CT2_TRACE_FILTER (see there)
        // every template, analyzers included; plugin code runs on background threads as in the app, so a long
        // computation is stopped after the time limit instead of holding up the run
        var unsupported = new Dictionary<string, int>();
        int complete = 0;
        var errs = new Dictionary<string, int>();
        int tpls = 0, ranOk = 0, withRunnable = 0;
        var blocked = new HashSet<string>();
        var viewsOk = new HashSet<string>();
        var viewErrs = new Dictionary<string, string>();
        var viewChars = new Dictionary<string, int>();
        foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
        {
            var w = CwmReader.Read(File.ReadAllBytes(f));
            var missing = w.Plugins.Where(p => !PluginRegistry.IsSupported(p.TypeName))
                .Select(p => p.TypeName.Split('.').Last())
                .Distinct()
                .ToList();
            foreach (var m in missing) unsupported[m] = unsupported.GetValueOrDefault(m) + 1;
            if (missing.Count == 0) complete++;
            tpls++;
            Console.Error.WriteLine("RUN " + Path.GetRelativePath(root, f));     // shows where a run hangs
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var e = new ExecutionEngine { ExecuteInBackground = true };
            var t = e.RunAsync(w);
            await Task.WhenAny(t, Task.Delay(1500));
            e.Stop();
            await Task.WhenAny(t, Task.Delay(4000));
            Console.Error.WriteLine($"  took {clock.ElapsedMilliseconds} ms, {System.Diagnostics.Process.GetCurrentProcess().Threads.Count} threads");
            var bad = e.Nodes.Values.Where(n => n.State == RunState.Error && n.Supported).ToList();
            if (e.Nodes.Values.Any(n => n.Supported && n.Comp is PluginComponent)) withRunnable++;
            if (bad.Count == 0) ranOk++;
            foreach (var n in e.Nodes.Values.Where(n => n.State == RunState.Running)) blocked.Add(n.Model.TypeName.Split('.').Last());
            // presentations that run on the WPF runtime must also render
            foreach (var n in e.Nodes.Values)
            {
                if (n.Comp is not PluginComponent { Host: { } host }) continue;
                var view = n.Model.TypeName.Split('.').Last();
                try
                {
                    var html = host.Render();
                    viewsOk.Add(view);
                    viewChars[view] = html.Length;
                }
                catch (Exception ex)
                {
                    viewErrs[view] = (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message.Split('\n')[0];
                }
            }
            foreach (var n in bad)
            {
                var k = n.Model.TypeName.Split('.').Last() + ": " + (n.Logs.LastOrDefault(l => l.Level == LogLevel.Error).Message ?? "?");
                errs[k] = errs.GetValueOrDefault(k) + 1;
            }
        }
        Console.WriteLine($"templates run={tpls} withoutErrors={ranOk} usingRealPlugins={withRunnable} allComponentsSupported={complete}");
        Console.WriteLine("unsupported components (templates): " + string.Join(", ",
            unsupported.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "(" + kv.Value + ")")));
        Console.WriteLine($"presentations rendered={viewsOk.Count} failed={viewErrs.Count}: " + string.Join(" ",
            viewsOk.OrderBy(v => v).Select(v => v + "(" + viewChars[v] + ")")));
        Console.WriteLine("still executing when stopped (would block the browser worker): " + string.Join(" ", blocked.OrderBy(b => b)));
        foreach (var kv in viewErrs) Console.WriteLine($"  VIEW {kv.Key}: {kv.Value}");

        // every component with a visualization, independent of the templates (analyzers are skipped above, some
        // components are in no template): create it alone and render its presentation
        var metaFile = Path.Combine(AppContext.BaseDirectory, "../../../../CrypWeb.App/wwwroot/plugins.json");
        var withView = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metaFile)).RootElement.EnumerateArray()
            .Where(m => m.TryGetProperty("presentation", out var pr) && pr.GetBoolean() && m.GetProperty("supported").GetBoolean())
            .Select(m => m.GetProperty("type").GetString()!).ToList();
        var alone = new List<string>();
        var noHost = new List<string>();
        var aloneErrs = new List<string>();
        foreach (var type in withView)
        {
            string name = type.Split('.').Last();
            try
            {
                var comp = PluginRegistry.Create(type);
                if (comp is NativeComponent)
                {
                    alone.Add(name + "(native)");
                    continue;
                }
                if (comp is not PluginComponent { Host: { } host })
                {
                    noHost.Add(name);
                    continue;
                }
                host.Render();
                alone.Add(name);
            }
            catch (Exception ex)
            {
                aloneErrs.Add(name + ": " + (ex.InnerException ?? ex).Message.Split('\n')[0]);
            }
        }
        Console.WriteLine($"components with a visualization: {withView.Count}; rendered alone: {alone.Count}; no rendered view: {noHost.Count} [{string.Join(" ", noHost)}]; failed: {aloneErrs.Count}");
        foreach (var e in aloneErrs) Console.WriteLine("  ALONE " + e);
        foreach (var d in System.Windows.Markup.XamlDiagnostics.Snapshot().Take(40)) Console.WriteLine($"  XAML {d.Value}x {d.Key}");
        foreach (var kv in errs.OrderByDescending(k => k.Value).Take(40)) Console.WriteLine($"  {kv.Value}x {kv.Key}");
        return;
    }
}

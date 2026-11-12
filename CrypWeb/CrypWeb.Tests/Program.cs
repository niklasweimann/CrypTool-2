using System.Text.Json;
using CrypTool.Web.Core;
var root = args.Length > 0 ? args[0] : "../Templates";
CrypTool.Web.Core.TemplateFiles.Root = Path.GetFullPath(root);     // template data files (pictures, examples) are read from the template folder
if (args.Length > 1 && args[1] == "culture")
{
    var t = typeof(CrypTool.PluginBase.IPlugin).Assembly; // force load of plugins assembly
    var plugins = System.Reflection.Assembly.Load("CrypWeb.Plugins");
    var resType = plugins.GetType("CrypTool.Caesar.Properties.Resources")!;
    var prop = resType.GetProperty("PluginCaption", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
    foreach (var c in new[] { "en", "de" })
    {
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(c);
        var rm = (System.Resources.ResourceManager)resType.GetProperty("ResourceManager", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(null)!;
        Console.WriteLine($"{c}: PluginTooltip = {rm.GetString("PluginTooltip")}");
    }
    return;
}
if (args.Length > 1 && args[1] == "roundtrip")
{
    int rtOk = 0, rtBad = 0, rtDropOnly = 0; var shown = 0;
    foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
    {
        var orig = File.ReadAllBytes(f);
        var m1 = CwmReader.Read(orig);
        var written = CwmWriter.Write(m1);
        var m2 = CwmReader.Read(written);
        var a = Canon(orig); var b = Canon(written);
        var onlyA = a.ToList(); foreach (var x in b) onlyA.Remove(x);
        var onlyB = b.ToList(); foreach (var x in a) onlyB.Remove(x);
        var diff = new List<string>();
        foreach (var x in onlyA.Take(3))
        {
            var type = x[..x.IndexOf('{')]; var other = onlyB.FirstOrDefault(y => y.StartsWith(type + "{"));
            var ma = x[(x.IndexOf('{') + 1)..^1].Split(';'); var mb = other == null ? Array.Empty<string>() : other[(other.IndexOf('{') + 1)..^1].Split(';');
            diff.Add(type.Split('.').Last() + ": only original: " + string.Join(" | ", ma.Except(mb).Select(q => q.Length > 90 ? q[..90] : q)) + "  ## only mine: " + string.Join(" | ", mb.Except(ma).Select(q => q.Length > 90 ? q[..90] : q)));
        }
        if (onlyA.Count == 0 && onlyB.Count > 0) diff.Add("extra objects: " + string.Join(", ", onlyB.Take(3).Select(q => q[..q.IndexOf('{')].Split('.').Last())));
        bool same = m1.Plugins.Count == m2.Plugins.Count && m1.Wires.Count == m2.Wires.Count && m1.Texts.Count == m2.Texts.Count && a.Count == b.Count && onlyA.Count == 0 && onlyB.Count == 0
            && m1.Plugins.Zip(m2.Plugins).All(t => t.First.X == t.Second.X && t.First.Y == t.Second.Y && t.First.TypeName == t.Second.TypeName && t.First.Settings.Count == t.Second.Settings.Count)
            && m1.Texts.Zip(m2.Texts).All(t => t.First.Text == t.Second.Text);
        if (same) rtOk++; else if (onlyB.Count == 0 && m1.Plugins.Count == m2.Plugins.Count) rtDropOnly++; else { rtBad++; if (shown++ < 6) { Console.WriteLine("DIFF " + Path.GetRelativePath(root, f) + $" objects {a.Count}/{b.Count}"); foreach (var d in diff) Console.WriteLine("   " + (d.Length > 260 ? d[..260] : d)); } }
    }
    Console.WriteLine($"roundtrip identical={rtOk} onlyDroppedOrphans={rtDropOnly} changed={rtBad}");
    return;
}
if (args.Length > 1 && args[1] == "prescalls")
{
    // which presentation calls do the components make? (plugin type -> call name -> count, templates, sample)
    var stat = new Dictionary<string, Dictionary<string, (int n, int t, string sample)>>();
    var skip2 = new[] { "PrimesGenerator", "KeySearcher", "Analyzer", "Analyser" };
    foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
    {
        var w = CwmReader.Read(File.ReadAllBytes(f));
        if (w.Plugins.Any(p => skip2.Any(k => p.TypeName.Contains(k)))) continue;
        var e = new ExecutionEngine(); var t = e.RunAsync(w); await Task.WhenAny(t, Task.Delay(1500)); e.Stop(); await Task.WhenAny(t, Task.Delay(4000));
        foreach (var n in e.Nodes.Values)
        {
            if (n.Pres.Count == 0) continue;
            var key = n.Model.TypeName.Split('.').Last();
            if (!stat.TryGetValue(key, out var d)) stat[key] = d = new();
            foreach (var kv in n.Pres) { d.TryGetValue(kv.Key, out var cur); d[kv.Key] = (cur.n + 1, cur.t + 1, cur.sample ?? kv.Value[..Math.Min(90, kv.Value.Length)]); }
        }
    }
    foreach (var kv in stat.OrderByDescending(k => k.Value.Values.Sum(v => v.n)))
        Console.WriteLine($"PRES {kv.Key}: " + string.Join(" | ", kv.Value.OrderByDescending(x => x.Value.n).Take(6).Select(x => $"{x.Key}×{x.Value.n} {x.Value.sample}")));
    return;
}
if (args.Length > 1 && args[1] == "runall")
{
    // every template, analyzers included; plugin code runs on background threads as in the app, so a long
    // computation is stopped after the time limit instead of holding up the run
    var unsupported = new Dictionary<string, int>(); int complete = 0;
    var errs = new Dictionary<string, int>(); int tpls = 0, ranOk = 0, withRunnable = 0;
    var blocked = new HashSet<string>(); var viewsOk = new HashSet<string>(); var viewErrs = new Dictionary<string, string>(); var viewChars = new Dictionary<string, int>();
    foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
    {
        var w = CwmReader.Read(File.ReadAllBytes(f));
        var missing = w.Plugins.Where(p => !PluginRegistry.IsSupported(p.TypeName)).Select(p => p.TypeName.Split('.').Last()).Distinct().ToList();
        foreach (var m in missing) unsupported[m] = unsupported.GetValueOrDefault(m) + 1;
        if (missing.Count == 0) complete++;
        tpls++;
        Console.Error.WriteLine("RUN " + Path.GetRelativePath(root, f));     // shows where a run hangs
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var e = new ExecutionEngine { ExecuteInBackground = true }; var t = e.RunAsync(w); await Task.WhenAny(t, Task.Delay(1500)); e.Stop(); await Task.WhenAny(t, Task.Delay(4000));
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
            try { var html = host.Render(); viewsOk.Add(view); viewChars[view] = html.Length; }
            catch (Exception ex) { viewErrs[view] = (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message.Split('\n')[0]; }
        }
        foreach (var n in bad) { var k = n.Model.TypeName.Split('.').Last() + ": " + (n.Logs.LastOrDefault(l => l.Level == LogLevel.Error).Message ?? "?"); errs[k] = errs.GetValueOrDefault(k) + 1; }
    }
    Console.WriteLine($"templates run={tpls} withoutErrors={ranOk} usingRealPlugins={withRunnable} allComponentsSupported={complete}");
    Console.WriteLine("unsupported components (templates): " + string.Join(", ", unsupported.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "(" + kv.Value + ")")));
    Console.WriteLine($"presentations rendered={viewsOk.Count} failed={viewErrs.Count}: " + string.Join(" ", viewsOk.OrderBy(v => v).Select(v => v + "(" + viewChars[v] + ")")));
    Console.WriteLine("still executing when stopped (would block the browser worker): " + string.Join(" ", blocked.OrderBy(b => b)));
    foreach (var kv in viewErrs) Console.WriteLine($"  VIEW {kv.Key}: {kv.Value}");

    // every component with a visualization, independent of the templates (analyzers are skipped above, some
    // components are in no template): create it alone and render its presentation
    var metaFile = Path.Combine(AppContext.BaseDirectory, "../../../../CrypWeb.App/wwwroot/plugins.json");
    var withView = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metaFile)).RootElement.EnumerateArray()
        .Where(m => m.TryGetProperty("presentation", out var pr) && pr.GetBoolean() && m.GetProperty("supported").GetBoolean())
        .Select(m => m.GetProperty("type").GetString()!).ToList();
    var alone = new List<string>(); var noHost = new List<string>(); var aloneErrs = new List<string>();
    foreach (var type in withView)
    {
        string name = type.Split('.').Last();
        try
        {
            var comp = PluginRegistry.Create(type);
            if (comp is NativeComponent) { alone.Add(name + "(native)"); continue; }
            if (comp is not PluginComponent { Host: { } host }) { noHost.Add(name); continue; }
            host.Render(); alone.Add(name);
        }
        catch (Exception ex) { aloneErrs.Add(name + ": " + (ex.InnerException ?? ex).Message.Split('\n')[0]); }
    }
    Console.WriteLine($"components with a visualization: {withView.Count}; rendered alone: {alone.Count}; no WPF view (stand-in): {noHost.Count} [{string.Join(" ", noHost)}]; failed: {aloneErrs.Count}");
    foreach (var e in aloneErrs) Console.WriteLine("  ALONE " + e);
    foreach (var d in System.Windows.Markup.XamlDiagnostics.Snapshot().Take(40)) Console.WriteLine($"  XAML {d.Value}x {d.Key}");
    foreach (var kv in errs.OrderByDescending(k => k.Value).Take(40)) Console.WriteLine($"  {kv.Value}x {kv.Key}");
    return;
}
if (args.Length > 1 && args[1] == "present3d")
{
    // the PRESENT 3D animation: one "next step" on the encryption tab, then animation ticks; prints how the
    // rendered scene changes and how long rendering takes (a check for the Viewport3D renderer)
    var comp = PluginRegistry.Create("CrypTool.PRESENT.PRESENT");
    var host = ((PluginComponent)comp).Host!;
    var view = (System.Windows.FrameworkElement)host.Root;
    var tabs = FindAll<System.Windows.Controls.TabControl>(view).First();
    tabs.SelectedIndex = 4;
    var next = FindAll<System.Windows.Controls.Button>(view).First(b => b.Name == "btn_EC_Pause");
    var watch = System.Diagnostics.Stopwatch.StartNew();
    string previous = host.Render();
    Console.WriteLine($"first render {watch.ElapsedMilliseconds} ms, {previous.Length} chars");
    typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(next, null);
    int changes = 0;
    for (int i = 0; i < 100; i++)
    {
        await Task.Delay(50);
        System.Windows.Hosting.WpfRuntime.Tick();
        watch.Restart();
        string html = host.Render();
        if (html != previous) { changes++; previous = html; Console.WriteLine($"tick {i}: changed, render {watch.ElapsedMilliseconds} ms, {html.Length} chars"); }
    }
    Console.WriteLine($"changes: {changes}");
    return;
}
static IEnumerable<T> FindAll<T>(System.Windows.UIElement element) where T : System.Windows.UIElement
{
    if (element is T match) yield return match;
    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
        foreach (var found in FindAll<T>((System.Windows.UIElement)System.Windows.Media.VisualTreeHelper.GetChild(element, i))) yield return found;
}
if (args.Length > 2 && args[1] == "probe")
{
    // runs one template and prints the last value on every connector (debugging a single workspace)
    var ws0 = CwmReader.Read(File.ReadAllBytes(Path.Combine(root, args[2])));
    // CT2_TRACE=1: also print every exception thrown inside plugins (they often catch and log only the message)
    if (Environment.GetEnvironmentVariable("CT2_TRACE") == "1")
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => Console.WriteLine("THROWN " + e.Exception.GetType().Name + ": " + e.Exception.Message + " @ " + e.Exception.StackTrace?.Split('\n').FirstOrDefault());
    var eng0 = new ExecutionEngine { ExecuteInBackground = true }; var r0 = eng0.RunAsync(ws0);
    // like the browser worker: render the presentations and tick animations while the workspace runs
    for (int i = 0; i < 60; i++)
    {
        await Task.Delay(50);
        foreach (var n in eng0.Nodes.Values) if (n.Comp is PluginComponent { Host: { } host }) try { host.RenderIfChanged(); } catch (Exception e) { Console.WriteLine("RENDER " + e.Message); }
        System.Windows.Hosting.WpfRuntime.Tick();
    }
    // long-running plugins (analyzers) may ignore Stop for a while: do not wait forever
    eng0.Stop(); await Task.WhenAny(r0, Task.Delay(4000));
    foreach (var n in eng0.Nodes.Values)
    {
        if (n.Comp is PluginComponent { Host: { } h }) Console.WriteLine("VIEW " + n.Model.TypeName.Split('.').Last() + " " + h.Render().Length + " chars");
        Console.WriteLine(n.Model.TypeName.Split('.').Last() + " " + n.State + " " + string.Join(" | ", n.Logs.Select(l => l.Level + ": " + l.Message)));
        foreach (var kv in n.Last) Console.WriteLine("   " + (kv.Key.Outgoing ? "out " : "in  ") + kv.Key.PropertyName + " = " + Convert.ToString(kv.Value)?.Replace("\n", " ")[..Math.Min(80, Convert.ToString(kv.Value)?.Length ?? 0)]);
    }
    return;
}
if (args.Length > 2 && args[1] == "settings")
{
    // prints the stored settings of every component of a template (debugging)
    var stored = CwmReader.Read(File.ReadAllBytes(Path.Combine(root, args[2])));
    foreach (var plugin in stored.Plugins)
        Console.WriteLine(plugin.TypeName + ": " + string.Join(", ", plugin.Settings.Select(kv => kv.Key + "=" + kv.Value)));
    return;
}
if (args.Length > 2 && args[1] == "snap")
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
if (args.Length > 2 && args[1] == "runtype")
{
    foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
    {
        var ws0 = CwmReader.Read(File.ReadAllBytes(f));
        if (!ws0.Plugins.Any(p => p.TypeName.Contains(args[2], StringComparison.OrdinalIgnoreCase))) continue;
        var eng0 = new ExecutionEngine { ExecuteInBackground = true }; var r0 = eng0.RunAsync(ws0); await Task.Delay(2000); eng0.Stop(); await r0;
        Console.WriteLine(Path.GetRelativePath(root, f) + "  executions=" + eng0.ExecutionCount + "  slowest=" + string.Join(", ", eng0.Nodes.Values.OrderByDescending(n => n.LastMs).Take(3).Select(n => n.Model.TypeName.Split('.').Last() + ":" + n.LastMs + "ms")));
        foreach (var n in eng0.Nodes.Values.Where(n => n.State == RunState.Error || n.Logs.Any(l => l.Level == LogLevel.Error))) Console.WriteLine("   ERR " + n.Model.TypeName + ": " + string.Join(" | ", n.Logs.Select(l => l.Message)));
    }
    return;
}
if (args.Length > 1 && args[1] == "palette") { Palette(root, args[2]); return; }
int ok = 0, fail = 0;
foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
{
    try { CwmReader.Read(File.ReadAllBytes(f)); ok++; }
    catch (Exception e) { fail++; Console.WriteLine($"FAIL {f}: {e.Message}"); }
}
Console.WriteLine($"parsed ok={ok} fail={fail}");
// editor smoke test
var ed = new WorkspaceEditor();
ed.Load(CwmReader.Read(File.ReadAllBytes(Path.Combine(root, "Cryptography/Classic/AffineCipher.cwm"))));
int n0 = ed.Ws.Plugins.Count, w0 = ed.Ws.Wires.Count;
ed.Checkpoint(); ed.Selection.Add(ed.Ws.Plugins[2].Id); ed.DeleteSelection();
Check(ed.Ws.Plugins.Count == n0 - 1 && ed.Ws.Wires.Count < w0, "delete removes plugin and wires");
ed.Undo(); Check(ed.Ws.Plugins.Count == n0 && ed.Ws.Wires.Count == w0, "undo restores");
ed.Redo(); Check(ed.Ws.Plugins.Count == n0 - 1, "redo");
ed.Undo(); ed.SelectAll(); ed.Copy(); ed.Paste(); Check(ed.Ws.Plugins.Count == 2 * n0 && ed.Ws.Wires.Count == 2 * w0, "copy/paste duplicates wires");
var ws2 = WorkspaceEditor.FromJson(ed.ToJson()); Check(ws2.Plugins.Count == 2 * n0 && ws2.Wires.Count == 2 * w0, "json roundtrip");
{
    var e2 = new WorkspaceEditor(); e2.Load(new Workspace());
    var dict = e2.AddPlugin(new PaletteEntry { TypeName = "d", Outputs = { new PalettePort("OutputList", "System.String[]", "", false) } }, 0, 0);
    var t9 = e2.AddPlugin(new PaletteEntry { TypeName = "t", Inputs = { new PalettePort("DictionaryContent", "System.Array", "", false), new PalettePort("S", "System.Int32", "", false) } }, 300, 0);
    Check(e2.CanConnect(dict.Outputs[0], t9.Inputs[0]), "string[] -> Array (T9 dictionary) connects");
    Check(!e2.CanConnect(dict.Outputs[0], t9.Inputs[1]), "string[] -> Int32 is rejected");
}
foreach (var tn in new[] { "CrypTool.Caesar.Caesar", "CrypTool.Plugins.AffineCipher.AffineCipher" })
{
    var fields = SettingsSchema.Build(tn, new Dictionary<string, string>());
    Check(fields.Count > 0, $"settings schema for {tn}: " + string.Join("; ", fields.Select(f => $"{f.Caption}[{f.Control}/{f.ValueType}={f.Value}{(f.Options.Count > 0 ? " opts=" + string.Join("|", f.Options) : "")}]")));
}
{
    // a workspace built in the editor (new components, a connection, a note, a changed setting) survives save -> load
    var e3 = new WorkspaceEditor(); e3.Load(new Workspace());
    var a = e3.AddPlugin(new PaletteEntry { TypeName = "CrypTool.Caesar.Caesar", AssemblyName = "Caesar", Name = "Caesar", Inputs = { new PalettePort("InputString", "System.String", "", true), new PalettePort("ShiftKey", "System.Int32", "", false) }, Outputs = { new PalettePort("OutputString", "System.String", "", false) } }, 100, 50);
    var b = e3.AddPlugin(new PaletteEntry { TypeName = "CrypTool.Caesar.Caesar", AssemblyName = "Caesar", Name = "Caesar 2", Inputs = { new PalettePort("InputString", "System.String", "", true) } }, 400, 50);
    e3.Connect(a.Outputs[0], b.Inputs[0]);
    a.Settings["Key"] = "7"; a.SettingTypes["Key"] = "System.Int32";
    var note = e3.AddText(10, 10); note.Text = "Hello\nWorld";
    var r3 = CwmReader.Read(CwmWriter.Write(e3.Ws));
    Check(r3.Plugins.Count == 2 && r3.Wires.Count == 1 && r3.Connectors.Count == 4, "editor-built workspace round-trips through .cwm");
    Check(r3.Plugins[0].Settings.GetValueOrDefault("Key") == "7" && r3.Plugins[0].AssemblyName == "Caesar", "setting + assembly name are saved");
    Check(r3.Texts.Count == 1 && r3.Texts[0].Text == "Hello\nWorld", "edited note text survives (generated XamlPackage)");
    Check(r3.Connectors.All(c => c.DataTypeAssembly == "mscorlib"), "connector types get their assembly");
}
{
    // the managed block ciphers (used in the browser) must agree with the .NET implementation
    var rnd = new Random(7); byte[] R(int n) { var b = new byte[n]; rnd.NextBytes(b); return b; }
    bool same = true; string why = "";
    foreach (var mode in new[] { System.Security.Cryptography.CipherMode.ECB, System.Security.Cryptography.CipherMode.CBC, System.Security.Cryptography.CipherMode.CFB })
    foreach (var pad in new[] { System.Security.Cryptography.PaddingMode.PKCS7, System.Security.Cryptography.PaddingMode.Zeros, System.Security.Cryptography.PaddingMode.None, System.Security.Cryptography.PaddingMode.ANSIX923 })
    foreach (var len in new[] { 16, 32, 37 })
    {
        if (pad == System.Security.Cryptography.PaddingMode.None && len % 16 != 0 && mode != System.Security.Cryptography.CipherMode.CFB) continue;
        if (mode == System.Security.Cryptography.CipherMode.CFB && pad == System.Security.Cryptography.PaddingMode.None && len % 16 != 0) continue;
        var key = R(16); var iv = R(16); var data = R(len);
        try
        {
            using var ours = new AesCryptoServiceProvider { Mode = mode, Padding = pad };
            using var theirs = System.Security.Cryptography.Aes.Create(); theirs.Mode = mode; theirs.Padding = pad;
            var a = ours.CreateEncryptor(key, iv); var b = theirs.CreateEncryptor(key, iv);
            var ea = a.TransformFinalBlock(data, 0, data.Length); var eb = b.TransformFinalBlock(data, 0, data.Length);
            if (!ea.SequenceEqual(eb)) { same = false; why += $" enc {mode}/{pad}/{len};"; }
            var da = ours.CreateDecryptor(key, iv).TransformFinalBlock(eb, 0, eb.Length); var db = theirs.CreateDecryptor(key, iv).TransformFinalBlock(eb, 0, eb.Length);
            if (!da.SequenceEqual(db)) { same = false; why += $" dec {mode}/{pad}/{len};"; }
        }
        catch (Exception e) { same = false; why += $" {mode}/{pad}/{len}: {e.Message};"; }
    }
    Check(same, "managed AES == .NET AES (ECB/CBC/CFB x paddings)" + why);
    var kat = new AesCryptoServiceProvider { Mode = System.Security.Cryptography.CipherMode.ECB, Padding = System.Security.Cryptography.PaddingMode.None }.CreateEncryptor(Convert.FromHexString("000102030405060708090a0b0c0d0e0f"), null).TransformFinalBlock(Convert.FromHexString("00112233445566778899aabbccddeeff"), 0, 16);
    Check(Convert.ToHexString(kat).ToLower() == "69c4e0d86a7b0430d8cdb78070b4c55a", "AES-128 known answer (FIPS-197)");
}
Console.WriteLine("editor tests done");
foreach (var tpl in new[] { "Cryptography/Classic/AffineCipher.cwm", "Cryptography/Classic/Caesar.cwm", "Cryptography/Classic/Vigenere.cwm", "Codes/T9Code.cwm" })
{
    var path = Path.Combine(root, tpl); if (!File.Exists(path)) { Console.WriteLine("skip " + tpl); continue; }
    var ws = CwmReader.Read(File.ReadAllBytes(path));
    var eng = new ExecutionEngine();
    var run = eng.RunAsync(ws); await Task.Delay(2500); eng.Stop(); await run;
    foreach (var n in eng.Nodes.Values) Console.WriteLine($"  {tpl} [{n.Model.TypeName.Split('.').Last()}] {(n.Supported ? n.State.ToString() : "unsupported")} display='{n.Display?.Replace("\n", " ")[..Math.Min(50, n.Display?.Length ?? 0)]}' logs={string.Join("|", n.Logs.Select(l => l.Message))}");
}
static void Check(bool c, string m) => Console.WriteLine((c ? "PASS " : "FAIL ") + m);

static void Palette(string root, string outFile)
{
    var map = new Dictionary<string, PaletteEntry>();
    foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
    {
        var ws = CwmReader.Read(File.ReadAllBytes(f));
        var xml = Path.ChangeExtension(f, ".xml");
        var rep = File.Exists(xml) ? TemplateStrings.Load(File.ReadAllBytes(xml)) : new();
        foreach (var p in ws.Plugins)
        {
            if (string.IsNullOrEmpty(p.TypeName) || map.ContainsKey(p.TypeName)) continue;
            var short_ = p.TypeName.Split('.').Last();
            var cat = Path.GetRelativePath(root, Path.GetDirectoryName(f)!).Split(Path.DirectorySeparatorChar)[0];
            PalettePort pp(ConnectorPort c) => new(c.PropertyName, c.DataTypeName, c.ToolTip, c.IsMandatory);
            map[p.TypeName] = new PaletteEntry { TypeName = p.TypeName, AssemblyName = p.AssemblyName, Name = System.Text.RegularExpressions.Regex.Replace(short_, "(?<=[a-z0-9])(?=[A-Z])", " "),
                Category = cat, Width = Math.Max(p.Width, 170), Height = Math.Max(p.Height, 80), Inputs = p.Inputs.Select(pp).ToList(), Outputs = p.Outputs.Select(pp).ToList() };
        }
    }
    File.WriteAllText(outFile, JsonSerializer.Serialize(map.Values.OrderBy(e => e.Name).ToList()));
    Console.WriteLine($"{map.Count} palette entries");
}


// Canonical form of a .cwm: one string per object with all primitive members (names, types, decoded values); references reduced to the target type.
static List<string> Canon(byte[] cwm)
{
    if (cwm.Length > 2 && cwm[0] == 0x1f) { using var gz = new System.IO.Compression.GZipStream(new MemoryStream(cwm), System.IO.Compression.CompressionMode.Decompress); var ms = new MemoryStream(); gz.CopyTo(ms); cwm = ms.ToArray(); }
    var doc = System.Xml.Linq.XDocument.Load(new StreamReader(new MemoryStream(cwm), new System.Text.UTF8Encoding(false), true));
    var types = doc.Root!.Elements("object").ToDictionary(o => (string)o.Element("id")!, o => (string)o.Element("type")!);
    string Val(System.Xml.Linq.XElement m)
    {
        var v = m.Element("value"); if (v == null) return "";
        var t = v.Value; if (m.Element("B64Encoded") != null) t = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(t));
        return t.Replace(',', '.');
    }
    string Entry(System.Xml.Linq.XElement e) => e.Element("reference") != null ? "ref:" + types[e.Element("reference")!.Value] : (string)e.Element("type")! + "=" + Val(e);
    var res = new List<string>();
    foreach (var o in doc.Root.Elements("object"))
    {
        var mems = new List<string>();
        foreach (var m in o.Element("members")!.Elements("member"))
        {
            var name = ((string)m.Element("name")!).Replace("&amp;lt;", "<").Replace("&lt;", "<").Replace("&amp;gtk", ">k").Replace("&gtk", ">k");
            var type = (string)m.Element("type")!;
            string v;
            if (m.Element("reference") != null) v = "ref:" + types[m.Element("reference")!.Value];
            else if (m.Element("list") != null) v = "[" + string.Join(",", m.Element("list")!.Elements("entry").Select(Entry).Distinct()) + "]"; // duplicates in the originals are repaired by the writer
            else if (type == "System.Byte[]") v = "bytes:" + Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(Convert.FromBase64String(m.Element("value")!.Value)));
            else v = Val(m);
            if (type.StartsWith("System.Collections.Generic.List`1[[System.Windows.Point")) { type = "System.Collections.Generic.List;System.Windows.Point"; v = v == "" ? "[]" : v; }
            if (name.Contains("PercentageFinished") || name.Contains("<Active>")) continue; // runtime state, reset by the desktop on load
            mems.Add(name + ":" + type + "=" + v);
        }
        res.Add((string)o.Element("type")! + "{" + string.Join(";", mems.OrderBy(x => x, StringComparer.Ordinal)) + "}");
    }
    res.Sort(StringComparer.Ordinal);
    return res;
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
    var value = type.IsEnum ? Enum.Parse(type, text) : Convert.ChangeType(text, type, System.Globalization.CultureInfo.InvariantCulture);
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

using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>Small debugging commands (see the usage text in Program.cs).</summary>
public static class DebugCommands
{
    /// <summary>Prints the English and German plugin texts of the Caesar plugin (checks the satellite resources).</summary>
    public static void Culture(string root, string[] args)
    {
        var t = typeof(CrypTool.PluginBase.IPlugin).Assembly; // force load of plugins assembly
        var plugins = System.Reflection.Assembly.Load("CrypWeb.Plugins");
        var resType = plugins.GetType("CrypTool.Caesar.Properties.Resources")!;
        var prop = resType.GetProperty("PluginCaption",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        foreach (var c in new[] { "en", "de" })
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(c);
            var rm = (System.Resources.ResourceManager)resType.GetProperty("ResourceManager",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(null)!;
            Console.WriteLine($"{c}: PluginTooltip = {rm.GetString("PluginTooltip")}");
        }
        return;
    }

    /// <summary>A binding on a gradient stop inside a data template: the stop takes the data context of its element.</summary>
    public static void BindTest(string root, string[] args)
    {
        // a binding on a gradient stop inside a data template: the stop takes the data context of its element
        var control = (System.Windows.Controls.UserControl)System.Windows.Markup.XamlReader.Parse(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
          <UserControl.Resources><DataTemplate x:Key='template'><StackPanel><Rectangle Width='10' Height='10' Stroke='Beige' RadiusX='5' RadiusY='5'>
            <Rectangle.BitmapEffect><DropShadowBitmapEffect /></Rectangle.BitmapEffect>
            <Rectangle.Fill><RadialGradientBrush>
            <GradientStop Color='{Binding Path=A}' Offset='0'/><GradientStop Color='{Binding Path=B}' Offset='1'/>
          </RadialGradientBrush></Rectangle.Fill></Rectangle></StackPanel></DataTemplate></UserControl.Resources>
          <ItemsControl x:Name='bars' ItemTemplate='{StaticResource template}'/></UserControl>");
        var list = (System.Windows.Controls.ItemsControl)control.FindName("bars");
        list.ItemsSource = new[] { new BindItem() };
        var host = new System.Windows.Hosting.PresentationHost(control);
        string html = host.Render();
        int i = html.IndexOf("radial-gradient");
        Console.WriteLine(i < 0 ? "no gradient: " + html : html.Substring(i, Math.Min(120, html.Length - i)));
        return;
    }

    /// <summary>Control-template triggers: hover, pressed, a data trigger and a TabItem's selection.</summary>
    public static void TriggerTest(string root, string[] args)
    {
        // control-template triggers: IsMouseOver / IsPressed (reported by the browser as hover / press events), a
        // data trigger on the control's data context, and a TabItem's computed IsSelected
        var control = (System.Windows.Controls.UserControl)System.Windows.Markup.XamlReader.Parse(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
          <UserControl.Resources><ControlTemplate x:Key='t' TargetType='Button'>
            <Border x:Name='border' Background='#111111' BorderBrush='Red'><ContentPresenter/></Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='border' Property='Background' Value='#222222'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='border' Property='Background' Value='#333333'/></Trigger>
              <DataTrigger Binding='{Binding Flag}' Value='True'><Setter TargetName='border' Property='BorderThickness' Value='0,7,0,0'/></DataTrigger>
            </ControlTemplate.Triggers></ControlTemplate></UserControl.Resources>
          <StackPanel><Button x:Name='b' Template='{StaticResource t}' Content='x'/></StackPanel></UserControl>");
        control.DataContext = new TriggerItem();
        var host = new System.Windows.Hosting.PresentationHost(control);
        string html = host.Render();
        int id = int.Parse(System.Text.RegularExpressions.Regex.Match(html, "data-e=\"(\\d+)\" data-h=\"1\" data-p=\"1\"").Groups[1].Value);
        string Look(string h) => string.Join(" ",
            System.Text.RegularExpressions.Regex.Matches(h, "rgb\\((17|34|51),[^)]*\\)|border-width:[^;]*")
            .Select(m => m.Value)
            .Distinct());
        Console.WriteLine("idle:    " + Look(html));
        host.Dispatch(id, "hover", "1");
        Console.WriteLine("hover:   " + Look(host.Render()));
        host.Dispatch(id, "press", "1");
        Console.WriteLine("pressed: " + Look(host.Render()));
        host.Dispatch(id, "press", "0");
        host.Dispatch(id, "hover", "0");
        Console.WriteLine("left:    " + Look(host.Render()));
        return;
    }

    /// <summary>The PRESENT 3D animation: prints how the rendered scene changes and how long rendering takes.</summary>
    public static async Task Present3D(string root, string[] args)
    {
        // the PRESENT 3D animation: one "next step" on the encryption tab, then animation ticks; prints how the
        // rendered scene changes and how long rendering takes (a check for the Viewport3D renderer)
        var comp = PluginRegistry.Create("CrypTool.PRESENT.PRESENT")!;
        var host = ((PluginComponent)comp).Host!;
        var view = (System.Windows.FrameworkElement)host.Root;
        var tabs = FindAll<System.Windows.Controls.TabControl>(view).First();
        tabs.SelectedIndex = 4;
        var next = FindAll<System.Windows.Controls.Button>(view).First(b => b.Name == "btn_EC_Pause");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        string previous = host.Render();
        Console.WriteLine($"first render {watch.ElapsedMilliseconds} ms, {previous.Length} chars");
        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(next, null);
        int changes = 0;
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(50);
            System.Windows.Hosting.WpfRuntime.Tick();
            watch.Restart();
            string html = host.Render();
            if (html != previous)
            {
                changes++;
                previous = html;
                Console.WriteLine($"tick {i}: changed, render {watch.ElapsedMilliseconds} ms, {html.Length} chars");
            }
        }
        Console.WriteLine($"changes: {changes}");
        return;
    }

    /// <summary>Runs one template and prints the last value on every connector (debugging a single workspace).</summary>
    public static async Task Probe(string root, string[] args)
    {
        // runs one template and prints the last value on every connector (debugging a single workspace)
        var ws0 = CwmReader.Read(File.ReadAllBytes(Path.Combine(root, args[2])));
        // CT2_TRACE=1: also print every exception thrown inside plugins (they often catch and log only the message)
        if (Environment.GetEnvironmentVariable("CT2_TRACE") == "1")
            AppDomain.CurrentDomain.FirstChanceException += (_, e) => Console.WriteLine("THROWN " + e.Exception.GetType().Name + ": " + e.Exception.Message + " @ " + e.Exception.StackTrace?.Split('\n').FirstOrDefault());
        var eng0 = new ExecutionEngine { ExecuteInBackground = true };
        var r0 = eng0.RunAsync(ws0);
        // like the browser worker: render the presentations and tick animations while the workspace runs
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(50);
            foreach (var n in eng0.Nodes.Values) if (n.Comp is PluginComponent { Host: { } host }) try
            {
                host.RenderIfChanged();
            }
            catch (Exception e)
            {
                Console.WriteLine("RENDER " + e.Message);
            }
            System.Windows.Hosting.WpfRuntime.Tick();
        }
        // long-running plugins (analyzers) may ignore Stop for a while: do not wait forever
        eng0.Stop();
        await Task.WhenAny(r0, Task.Delay(4000));
        foreach (var n in eng0.Nodes.Values)
        {
            if (n.Comp is PluginComponent { Host: { } h }) Console.WriteLine("VIEW " + n.Model.TypeName.Split('.').Last() + " " + h.Render().Length + " chars");
            Console.WriteLine(n.Model.TypeName.Split('.').Last() + " " + n.State + " " + string.Join(" | ", n.Logs.Select(l => l.Level + ": " + l.Message)));
            foreach (var kv in n.Last) Console.WriteLine("   " + (kv.Key.Outgoing
                ? "out "
                : "in  ") + kv.Key.PropertyName + " = " + Convert.ToString(kv.Value)?.Replace("\n", " ")[..Math.Min(80, Convert.ToString(kv.Value)?.Length ?? 0)]);
        }
        return;
    }

    /// <summary>Prints the stored settings of every component of a template.</summary>
    public static void Settings(string root, string[] args)
    {
        // prints the stored settings of every component of a template (debugging)
        var stored = CwmReader.Read(File.ReadAllBytes(Path.Combine(root, args[2])));
        foreach (var plugin in stored.Plugins)
            Console.WriteLine(plugin.TypeName + ": " + string.Join(", ", plugin.Settings.Select(kv => kv.Key + "=" + kv.Value)));
        return;
    }

    /// <summary>Runs every template that contains the given component type and prints timings and errors.</summary>
    public static async Task RunType(string root, string[] args)
    {
        foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
        {
            var ws0 = CwmReader.Read(File.ReadAllBytes(f));
            if (!ws0.Plugins.Any(p => p.TypeName.Contains(args[2], StringComparison.OrdinalIgnoreCase))) continue;
            var eng0 = new ExecutionEngine { ExecuteInBackground = true };
            var r0 = eng0.RunAsync(ws0);
            await Task.Delay(2000);
            eng0.Stop();
            await r0;
            Console.WriteLine(Path.GetRelativePath(root, f) + "  executions=" + eng0.ExecutionCount + "  slowest=" + string.Join(", ",
                eng0.Nodes.Values.OrderByDescending(n => n.LastMs)
                .Take(3)
                .Select(n => n.Model.TypeName.Split('.').Last() + ":" + n.LastMs + "ms")));
            foreach (var n in eng0.Nodes.Values.Where(n => n.State == RunState.Error
                || n.Logs.Any(l => l.Level == LogLevel.Error))) Console.WriteLine("   ERR " + n.Model.TypeName + ": " + string.Join(" | ", n.Logs.Select(l => l.Message)));
        }
        return;
    }

    static IEnumerable<T> FindAll<T>(System.Windows.UIElement element) where T : System.Windows.UIElement
    {
        if (element is T match) yield return match;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var found in FindAll<T>((System.Windows.UIElement)System.Windows.Media.VisualTreeHelper.GetChild(element, i))) yield return found;
    }

    /// <summary>Writes the components found in the templates as palette entries (JSON).</summary>
    public static void Palette(string root, string outFile)
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

    /// <summary>Item for the triggertest command.</summary>
    public sealed class TriggerItem
    {
        public bool Flag => true;
    }

    /// <summary>Item for the bindtest command.</summary>
    public sealed class BindItem
    {
        public System.Windows.Media.Color A => System.Windows.Media.Colors.Blue;
        public System.Windows.Media.Color B => System.Windows.Media.Colors.Red;
    }
}

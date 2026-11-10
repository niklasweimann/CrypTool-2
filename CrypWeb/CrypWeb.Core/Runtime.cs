using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text;
using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;

namespace CrypTool.Web.Core;

public enum RunState { Idle, Running, Done, Error }
public enum LogLevel { Debug, Info, Warning, Error }

/// <summary>Uniform wrapper over a real ICrypComponent or a native web component.</summary>
public abstract class RunComponent
{
    public event Action<string>? OutputChanged;
    public event Action<LogLevel, string>? Log;
    public event Action<double>? Progress;
    /// <summary>Calls the plugin code makes on its (browser-less) presentation: method name or "Property=" and the arguments.</summary>
    public event Action<string, object?[]>? Presentation;
    protected void RaisePresentation(string name, object?[] args) => Presentation?.Invoke(name, args);
    protected void RaiseOutput(string prop) => OutputChanged?.Invoke(prop);
    protected void RaiseLog(LogLevel l, string m) => Log?.Invoke(l, m);
    protected void RaiseProgress(double p) => Progress?.Invoke(p);
    public abstract Type? TypeOf(string prop);
    public abstract void Set(string prop, object? value);
    public abstract object? Get(string prop);
    public virtual void ApplySettings(IReadOnlyDictionary<string, string> settings) { }
    public virtual void PreExecution() { }
    public abstract void Execute();
    public virtual void PostExecution() { }
    public virtual void Stop() { }
    /// <summary>Text shown inside the node (inputs show their value, outputs the received value).</summary>
    public virtual string? Display => null;
}

public sealed class PluginComponent : RunComponent
{
    readonly ICrypComponent plugin;
    readonly Dictionary<string, PropertyInfo> props = new(StringComparer.Ordinal);
    public PluginComponent(ICrypComponent p)
    {
        plugin = p;
        foreach (var pr in p.GetType().GetProperties()) props[pr.Name] = pr;
        p.PropertyChanged += (s, e) => { if (e.PropertyName != null) RaiseOutput(e.PropertyName); };
        p.OnGuiLogNotificationOccured += (s, e) => RaiseLog(e.NotificationLevel switch { NotificationLevel.Error => LogLevel.Error, NotificationLevel.Warning => LogLevel.Warning, NotificationLevel.Debug => LogLevel.Debug, _ => LogLevel.Info }, e.Message);
        p.OnPluginProgressChanged += (s, e) => { if (e.Max > 0) RaiseProgress(e.Value / e.Max); };
        try { p.Initialize(); } catch { }
        try { if (p.Presentation is System.Windows.DependencyObject d) d.BridgeSink = (n, a) => RaisePresentation(n, a); } catch { }
        // a presentation compiled from its original XAML/code-behind is drawn as HTML by the WPF runtime
        try { if (p.Presentation is System.Windows.UIElement view && System.Windows.Hosting.PresentationHost.HasContent(view)) Host = new System.Windows.Hosting.PresentationHost(view) { ReportsHidden = BlockingPresentations.Contains(p.GetType().Name) }; } catch { }
    }
    /// <summary>
    /// Plugins whose Execute() loops while their presentation is visible (until the user has stepped through it).
    /// On the desktop that loop runs on a worker thread; here it would block the only thread, so these plugins are
    /// told that their presentation is hidden. Found with `CrypWeb.Tests ... runall` ("still executing").
    /// </summary>
    public static readonly HashSet<string> BlockingPresentations = new(StringComparer.Ordinal) { "DESVisualization", "Keccak" };

    /// <summary>Draws the plugin's presentation when it is a real element tree (null for generated stand-ins).</summary>
    public System.Windows.Hosting.PresentationHost? Host { get; }
    public override Type? TypeOf(string prop) => props.TryGetValue(prop, out var p) ? p.PropertyType : null;
    public override void Set(string prop, object? value) { if (props.TryGetValue(prop, out var p) && p.CanWrite) p.SetValue(plugin, value); }
    public override object? Get(string prop) => props.TryGetValue(prop, out var p) && p.CanRead ? p.GetValue(plugin) : null;
    public override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        var s = plugin.Settings; if (s == null) return;
        foreach (var kv in settings)
        {
            var pi = s.GetType().GetProperty(kv.Key);
            if (pi == null || !pi.CanWrite) continue;
            try { pi.SetValue(s, DataConvert.FromString(kv.Value, pi.PropertyType)); } catch { }
        }
    }
    public override void PreExecution() => plugin.PreExecution();
    public override void Execute() => plugin.Execute();
    public override void PostExecution() => plugin.PostExecution();
    public override void Stop() => plugin.Stop();
}

/// <summary>Browser-native replacements for components whose original implementation is WPF-only.</summary>
public sealed class NativeComponent : RunComponent
{
    public enum Kind { TextInput, TextOutput, NumberInput, BooleanInput, BooleanOutput, PictureOutput }
    public Kind Which { get; }
    readonly Dictionary<string, object?> v = new();
    public string Text { get; set; } = "";
    public NativeComponent(Kind k) { Which = k; }

    public static NativeComponent? TryCreate(string typeName) => typeName.Split('.').Last().ToLowerInvariant() switch
    {
        "textinput" => new NativeComponent(Kind.TextInput),
        "textoutput" => new NativeComponent(Kind.TextOutput),
        "numberinput" => new NativeComponent(Kind.NumberInput),
        "booleaninput" => new NativeComponent(Kind.BooleanInput),
        "booleanoutput" => new NativeComponent(Kind.BooleanOutput),
        "pictureoutput" => new NativeComponent(Kind.PictureOutput),
        _ => null
    };

    /// <summary>PictureOutput: the received image as a data: URI, and a counter that changes with every new picture.</summary>
    public string? ImageUri { get; private set; }
    public int ImageVersion { get; private set; }

    public override Type? TypeOf(string prop) => Which switch
    {
        Kind.PictureOutput => prop == "PictureStream" ? typeof(ICrypToolStream) : typeof(byte[]),
        Kind.TextInput => typeof(string), Kind.TextOutput => typeof(string),
        Kind.NumberInput => typeof(BigInteger), _ => typeof(bool)
    };
    static string? Mime(byte[] b) =>
        b.Length > 3 && b[0] == 0x89 && b[1] == 0x50 ? "image/png" : b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8 ? "image/jpeg" :
        b.Length > 3 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' ? "image/gif" : b.Length > 2 && b[0] == 'B' && b[1] == 'M' ? "image/bmp" :
        b.Length > 11 && b[0] == 'R' && b[1] == 'I' && b[8] == 'W' ? "image/webp" : null;

    public override void Set(string prop, object? value)
    {
        if (Which == Kind.PictureOutput)
        {
            var bytes = DataConvert.To(value, typeof(byte[])) as byte[];
            var mime = bytes == null ? null : Mime(bytes);
            if (bytes != null && mime != null) { ImageUri = "data:" + mime + ";base64," + Convert.ToBase64String(bytes); ImageVersion++; }
            else if (bytes != null) RaiseLog(LogLevel.Warning, "The data is not a picture the browser can show (PNG, JPEG, GIF, BMP, WebP).");
            return;
        }
        v[prop] = value; if (Which is Kind.TextOutput or Kind.BooleanOutput && value != null) { Text = value.ToString() ?? ""; if (value is bool b) Text = b ? "True" : "False"; } }
    public override object? Get(string prop) => v.GetValueOrDefault(prop);
    public override string? Display => Text;
    public override void ApplySettings(IReadOnlyDictionary<string, string> s)
    {
        if (Which == Kind.TextInput && s.TryGetValue("Text", out var t)) Text = t;
        if (Which == Kind.NumberInput && s.TryGetValue("Number", out var n)) Text = n;
        if (Which == Kind.BooleanInput && s.TryGetValue("Value", out var b)) Text = b == "1" || b.Equals("true", StringComparison.OrdinalIgnoreCase) ? "True" : "False";
    }
    string OutName => Which switch { Kind.TextInput => "TextOutput", Kind.NumberInput => "NumberOutput", _ => "Output" };
    public override void Execute()
    {
        switch (Which)
        {
            case Kind.TextInput: v[OutName] = Text; RaiseOutput(OutName); break;
            case Kind.NumberInput: v[OutName] = BigInteger.TryParse(Text.Trim(), out var n) ? n : BigInteger.Zero; RaiseOutput(OutName); break;
            case Kind.BooleanInput: v[OutName] = Text == "True"; RaiseOutput(OutName); break;
        }
        RaiseProgress(1);
    }
}

public static class DataConvert
{
    public static object? FromString(string s, Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t == typeof(string)) return s;
        if (t.IsEnum) return int.TryParse(s, out var i) ? Enum.ToObject(t, i) : Enum.Parse(t, s, true);
        if (t == typeof(bool)) return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (t == typeof(BigInteger)) return BigInteger.Parse(s);
        if (t == typeof(double)) return double.Parse(s.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        if (t == typeof(float)) return float.Parse(s.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ChangeType(s, t, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Converts a value flowing along a wire to the type expected by the target property.</summary>
    public static object? To(object? v, Type? target)
    {
        if (v == null || target == null || target.IsInstanceOfType(v)) return v;
        if (v is ICrypToolStream cs)
        {
            using var r = cs.CreateReader(); var buf = new byte[cs.Length]; r.Read(buf, 0, buf.Length);
            return To(buf, target);
        }
        if (target == typeof(string)) return v switch { byte[] b => Encoding.UTF8.GetString(b), _ => v.ToString() };
        if (target == typeof(byte[])) return v is string s ? Encoding.UTF8.GetBytes(s) : v;
        if (target.IsAssignableFrom(typeof(CStreamWriter))) { var w = new CStreamWriter(); w.Write(v is string s2 ? Encoding.UTF8.GetBytes(s2) : (byte[])v); w.Close(); return w; }
        if (target == typeof(BigInteger)) return v is double d ? new BigInteger(d) : BigInteger.Parse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!);
        if (v is BigInteger bi) { if (target == typeof(int)) return (int)bi; if (target == typeof(long)) return (long)bi; if (target == typeof(double)) return (double)bi; if (target == typeof(string)) return bi.ToString(); }
        try { return Convert.ChangeType(v, target, System.Globalization.CultureInfo.InvariantCulture); } catch { return v; }
    }
}

public static class PluginRegistry
{
    static Dictionary<string, Type>? map;
    public static void Reset() => map = null;
    static void Build()
    {
        map = new(StringComparer.OrdinalIgnoreCase);
        try { System.Reflection.Assembly.Load("CrypWeb.Plugins"); } catch { }
        foreach (var n in IsolatedAssemblies.Names) { try { System.Reflection.Assembly.Load(n); } catch { } }
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types; try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; } catch { continue; }
            foreach (var t in types)
                if (t.FullName != null && !t.IsAbstract && typeof(ICrypComponent).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null) map[t.FullName] = t;
        }
    }
    public static Type? FindType(string typeName) { if (map == null) Build(); return map!.GetValueOrDefault(typeName); }
    public static bool IsSupported(string typeName) { if (map == null) Build(); return NativeComponent.TryCreate(typeName) != null || map!.ContainsKey(typeName); }
    public static RunComponent? Create(string typeName)
    {
        if (map == null) Build();
        if (map!.TryGetValue(typeName, out var t)) return new PluginComponent((ICrypComponent)Activator.CreateInstance(t)!);
        return NativeComponent.TryCreate(typeName);
    }
}

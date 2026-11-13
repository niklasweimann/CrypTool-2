using System.Reflection;
using CrypTool.PluginBase;

namespace CrypTool.Web.Core;

/// <summary>Runs an original CrypTool 2 plugin (<see cref="ICrypComponent"/>) and exposes its properties by name.</summary>
public sealed class PluginComponent : RunComponent
{
    readonly ICrypComponent plugin;
    readonly Dictionary<string, PropertyInfo> properties = new(StringComparer.Ordinal);

    public PluginComponent(ICrypComponent plugin)
    {
        this.plugin = plugin;
        foreach (var property in plugin.GetType().GetProperties()) properties[property.Name] = property;

        plugin.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) RaiseOutput(e.PropertyName);
        };
        plugin.OnGuiLogNotificationOccured += (_, e) => RaiseLog(ToLogLevel(e.NotificationLevel), e.Message);
        plugin.OnPluginProgressChanged += (_, e) =>
        {
            if (e.Max > 0) RaiseProgress(e.Value / e.Max);
        };

        try
        {
            plugin.Initialize();
        }
        catch (Exception) { /* a plugin that fails to initialize reports the problem when it runs */ }

        // a presentation compiled from its original XAML/code-behind is drawn as HTML by the WPF runtime
        try
        {
            if (plugin.Presentation is System.Windows.UIElement view && System.Windows.Hosting.PresentationHost.HasContent(view))
            {
                Host = new System.Windows.Hosting.PresentationHost(view)
                {
                    ReportsHidden = BlockingPresentations.Contains(plugin.GetType().Name),
                };
            }
        }
        catch (Exception) { /* no presentation: the component shows its data instead */ }
    }

    static LogLevel ToLogLevel(NotificationLevel level) => level switch
    {
        NotificationLevel.Error => LogLevel.Error,
        NotificationLevel.Warning => LogLevel.Warning,
        NotificationLevel.Debug => LogLevel.Debug,
        _ => LogLevel.Info,
    };

    /// <summary>
    /// Plugins to tell that their presentation is hidden. Some plugins (DES, AES, Keccak, ...) wait in Execute()
    /// until the user has stepped through their presentation; the browser app runs Execute on a background thread
    /// (ExecutionEngine.ExecuteInBackground), so that works as on the desktop and the list is empty. A host that
    /// runs plugin code on its UI thread would list them here (`CrypWeb.Tests ... runall` reports them as
    /// "still executing").
    /// </summary>
    public static readonly HashSet<string> BlockingPresentations = new(StringComparer.Ordinal);

    /// <summary>Draws the plugin's presentation when it is a real element tree (null for generated stand-ins).</summary>
    public System.Windows.Hosting.PresentationHost? Host { get; }

    public override Type? TypeOf(string property) => properties.TryGetValue(property, out var p) ? p.PropertyType : null;

    public override void Set(string property, object? value)
    {
        if (properties.TryGetValue(property, out var p) && p.CanWrite) p.SetValue(plugin, value);
    }

    public override object? Get(string property) =>
        properties.TryGetValue(property, out var p) && p.CanRead ? p.GetValue(plugin) : null;

    public override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        var pluginSettings = plugin.Settings;
        if (pluginSettings == null) return;

        WatchSettings(pluginSettings);
        applyingSettings = true;
        try
        {
            foreach (var (name, text) in settings)
            {
                var property = pluginSettings.GetType().GetProperty(name);
                if (property == null || !property.CanWrite) continue;

                // file names of templates refer to files of the virtual file system
                string value = property.PropertyType == typeof(string) ? TemplateFiles.Resolve(text) : text;
                try
                {
                    property.SetValue(pluginSettings, DataConvert.FromString(value, property.PropertyType));
                }
                catch (Exception) { /* a stored value that does not fit (any more): keep the plugin's default */ }
            }
        }
        finally
        {
            applyingSettings = false;
        }
    }

    // Settings the plugin changed itself (e.g. its presentation: Enigma's rotors are swapped by drag and drop).
    // The app keeps the workspace's settings, so these are reported back (TakeChangedSettings).
    readonly Dictionary<string, (string Value, string Type)> changedSettings = new(StringComparer.Ordinal);
    bool applyingSettings;
    bool watchingSettings;

    void WatchSettings(ISettings settings)
    {
        if (watchingSettings) return;
        watchingSettings = true;
        settings.PropertyChanged += (_, e) =>
        {
            if (applyingSettings || e.PropertyName == null) return; // changes we made ourselves are no news
            var property = settings.GetType().GetProperty(e.PropertyName);
            if (property == null || !property.CanWrite || !property.CanRead) return;
            try
            {
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var text = DataConvert.ToSettingString(property.GetValue(settings));
                lock (changedSettings) changedSettings[e.PropertyName] = (text, type.FullName ?? "");
            }
            catch (Exception) { /* a setting that cannot be read is not reported */ }
        };
    }

    public override IReadOnlyDictionary<string, (string Value, string Type)>? TakeChangedSettings()
    {
        lock (changedSettings)
        {
            if (changedSettings.Count == 0) return null;
            var taken = new Dictionary<string, (string Value, string Type)>(changedSettings);
            changedSettings.Clear();
            return taken;
        }
    }

    public override void PreExecution()
    {
        Host?.RequestSizes(); // the presentation gets its sizes again at the start of every run
        plugin.PreExecution();
    }

    public override void Execute() => plugin.Execute();
    public override void PostExecution() => plugin.PostExecution();
    public override void Stop() => plugin.Stop();
}

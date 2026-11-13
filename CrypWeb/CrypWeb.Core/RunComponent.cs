namespace CrypTool.Web.Core;

/// <summary>What a component is doing in the current run.</summary>
public enum RunState { Idle, Running, Done, Error }

/// <summary>Severity of a log message (mirrors the notification levels of the plugin base).</summary>
public enum LogLevel { Debug, Info, Warning, Error }

/// <summary>Uniform wrapper over a real ICrypComponent (<see cref="PluginComponent"/>) or a browser-native component (<see cref="NativeComponent"/>).</summary>
public abstract class RunComponent
{
    /// <summary>An output property got a new value.</summary>
    public event Action<string>? OutputChanged;

    public event Action<LogLevel, string>? Log;

    /// <summary>Progress between 0 and 1.</summary>
    public event Action<double>? Progress;

    protected void RaiseOutput(string property) => OutputChanged?.Invoke(property);
    protected void RaiseLog(LogLevel level, string message) => Log?.Invoke(level, message);
    protected void RaiseProgress(double progress) => Progress?.Invoke(progress);

    /// <summary>The .NET type of an input/output property (used to convert values flowing along wires).</summary>
    public abstract Type? TypeOf(string property);

    public abstract void Set(string property, object? value);
    public abstract object? Get(string property);

    public virtual void ApplySettings(IReadOnlyDictionary<string, string> settings) { }

    /// <summary>Settings the component changed by itself since the last call (name → stored text and .NET type).</summary>
    public virtual IReadOnlyDictionary<string, (string Value, string Type)>? TakeChangedSettings() => null;

    public virtual void PreExecution() { }
    public abstract void Execute();
    public virtual void PostExecution() { }
    public virtual void Stop() { }

    /// <summary>Text shown inside the node (inputs show their value, outputs the received value).</summary>
    public virtual string? Display => null;
}

using CrypTool.Web.Core;

namespace CrypWeb.App;

/// <summary>UI-side mirror of the execution state that the engine worker reports.</summary>
public sealed class NodeView
{
    public RunState State { get; set; }
    public double Progress { get; set; }
    public string? Display { get; set; }
    public string? LastError { get; set; }
    public long Ms { get; set; }
    public string? Image { get; set; }
    /// <summary>The plugin's WPF presentation rendered as HTML (null until it has drawn something).</summary>
    public string? Html { get; set; }
    /// <summary>Latest value seen on each connector: (name, isOutput) -> text.</summary>
    public Dictionary<(string Name, bool Out), string> Io { get; } = new();
}

public sealed class RunView
{
    public bool Running { get; set; }
    public int ExecutionCount { get; set; }
    public HashSet<int> ActiveWires { get; } = new();
    public Dictionary<int, NodeView> Nodes { get; } = new();
    public void Reset() { Running = false; ExecutionCount = 0; ActiveWires.Clear(); Nodes.Clear(); }
}

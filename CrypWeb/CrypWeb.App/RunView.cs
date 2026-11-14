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

    /// <summary>Executions in the current run and their total duration (ms).</summary>
    public int Runs { get; set; }

    public long TotalMs { get; set; }
    public string? Image { get; set; }

    /// <summary>The plugin's WPF presentation rendered as HTML (null until it has drawn something).</summary>
    public string? Html { get; set; }

    /// <summary>Latest value seen on each connector: (name, isOutput) -> text.</summary>
    public Dictionary<(string Name, bool Out), string> Io { get; } = new();
}

/// <summary>What the canvas shows about a running (or prepared) workspace: per component state, progress and results, and the wires that carry data.</summary>
public sealed class RunView
{
    public bool Running { get; set; }
    public int ExecutionCount { get; set; }
    public HashSet<int> ActiveWires { get; } = new();
    public Dictionary<int, NodeView> Nodes { get; } = new();

    /// <summary>Step mode: no component starts until the next step.</summary>
    public bool Paused { get; set; }

    /// <summary>Step mode: the components that the next step may start.</summary>
    public HashSet<int> Ready { get; } = new();

    /// <summary>The component started last.</summary>
    public int? LastStarted { get; set; }

    public void Reset()
    {
        Running = false;
        Paused = false;
        Ready.Clear();
        LastStarted = null;
        ExecutionCount = 0;
        ActiveWires.Clear();
        Nodes.Clear();
    }
}

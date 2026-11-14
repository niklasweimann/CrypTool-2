namespace CrypTool.Web.Core;

/// <summary>A component of the running workspace: its model, its runtime component and the per-run state.</summary>
public sealed class RunNode
{
    public PluginNode Model { get; init; } = null!;

    /// <summary>The plugin or native component; null when the plugin is unknown (not supported in the browser).</summary>
    public RunComponent? Comp { get; set; }

    public RunState State { get; set; } = RunState.Idle;
    public double Progress { get; set; }
    public bool Supported => Comp != null;

    /// <summary>True until a component without (mandatory) inputs has been started in this run.</summary>
    public bool FirstRun = true;

    /// <summary>Duration of the last Execute in milliseconds.</summary>
    public long LastMs;

    /// <summary>Values waiting at each input (one entry per received value).</summary>
    public readonly Dictionary<ConnectorPort, Queue<object?>> Queues = new();

    /// <summary>The last value seen at each connector: a component can run again with an old value at an input that gets no new one.</summary>
    public readonly Dictionary<ConnectorPort, object?> Last = new();

    public readonly List<(LogLevel Level, string Message)> Logs = new();
    public string? Display => Comp?.Display;
    public string? ImageUri => (Comp as NativeComponent)?.ImageUri;
    public int ImageVersion => (Comp as NativeComponent)?.ImageVersion ?? 0;
}

/// <summary>
/// Re-implementation of the CT2 workspace execution semantics: queued inputs, mandatory inputs, back-pressure on
/// unconsumed outputs. The scheduling loop runs on the caller's thread (the UI thread in the app); with
/// <see cref="ExecuteInBackground"/> each component's Execute runs on a thread of its own, several at once, as in CT2.
/// </summary>
public sealed class ExecutionEngine
{
    static ExecutionEngine()
    {
        // plugins use code pages that .NET (Core) only knows after this (iso-8859-x, windows-125x)
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public Dictionary<int, RunNode> Nodes { get; } = new();
    public HashSet<int> ActiveWires { get; } = new();
    public bool Running { get; private set; }

    /// <summary>Pause between executions in ms (global setting "sleep time").</summary>
    public int SleepMs { get; set; } = 1;
    public event Action? Changed;
    public event Action<RunNode, LogLevel, string>? Log;
    int executionCount;
    public int ExecutionCount { get => executionCount; private set => executionCount = value; }
    CancellationTokenSource? cts;

    /// <summary>
    /// Run plugin code (Execute) on a thread-pool thread, so the calling (UI) thread stays free to draw presentations
    /// and handle input while a component computes, as on the desktop. The browser app sets this; tests do not.
    /// </summary>
    public bool ExecuteInBackground { get; set; }

    /// <summary>Lock for the engine state that components change from their own thread (outputs, wires).</summary>
    public object Gate { get; } = new();

    /// <summary>Stops the run: the components are told at once (a running Execute sees its stop flag), the loop ends.</summary>
    public void Stop()
    {
        cts?.Cancel();
        foreach (var n in Nodes.Values.ToList())
        {
            try
            {
                n.Comp?.Stop();
            }
            catch (Exception) { /* the component is stopped anyway; one that fails to stop must not keep the others running */ }
        }
    }

    /// <summary>Re-runs a source component (e.g. after its text was edited while running).</summary>
    public void Retrigger(int pluginId)
    {
        if (Nodes.TryGetValue(pluginId, out var node) && node.Model.Inputs.Count == 0) node.FirstRun = true;
    }

    /// <summary>
    /// Creates the components of a workspace (with their presentations) without running it, like CT2 does when a
    /// workspace is opened. Components that already exist for the same plugin id and type are kept, so a
    /// presentation keeps its state between runs; their settings are applied again.
    /// </summary>
    public void Prepare(Workspace ws)
    {
        if (Running) return;
        var wanted = ws.Plugins.Select(p => p.Id).ToHashSet();
        foreach (var gone in Nodes.Keys.Where(id => !wanted.Contains(id)).ToList()) Nodes.Remove(gone);
        foreach (var p in ws.Plugins)
        {
            if (Nodes.TryGetValue(p.Id, out var existing) && existing.Model.TypeName == p.TypeName && existing.Comp != null)
            {
                var kept = new RunNode { Model = p, Comp = existing.Comp };
                Nodes[p.Id] = kept;
                try
                {
                    kept.Comp.ApplySettings(p.Settings);
                }
                catch (Exception e)
                {
                    kept.Logs.Add((LogLevel.Warning, e.Message));
                }
                continue;
            }
            Nodes[p.Id] = CreateNode(p);
        }
        // the event handlers of a kept component still point to its previous node
        foreach (var n in Nodes.Values) WireEvents(n);
        Changed?.Invoke();
    }

    RunNode CreateNode(PluginNode p)
    {
        var rn = new RunNode { Model = p };
        try
        {
            rn.Comp = PluginRegistry.Create(p.TypeName);
            rn.Comp?.ApplySettings(p.Settings);
        }
        catch (Exception e)
        {
            rn.State = RunState.Error;
            rn.Logs.Add((LogLevel.Error, e.Message));
        }
        return rn;
    }

    // Component events are routed to the node that currently owns the component (nodes are replaced on Prepare).
    readonly Dictionary<RunComponent, RunNode> owners = new();

    void WireEvents(RunNode rn)
    {
        if (rn.Comp == null) return;
        bool wired = owners.ContainsKey(rn.Comp);
        owners[rn.Comp] = rn;
        if (wired) return;
        var comp = rn.Comp;
        comp.OutputChanged += prop => OnOutput(owners[comp], prop);
        comp.Log += (l, m) =>
        {
            var node = owners[comp];
            node.Logs.Add((l, m));
            if (node.Logs.Count > 200) node.Logs.RemoveAt(0);
            Log?.Invoke(node, l, m);
        };
        comp.Progress += d => owners[comp].Progress = d;
    }

    /// <summary>Runs the workspace until <see cref="Stop"/> is called (the task ends when all components have been told to stop).</summary>
    public async Task RunAsync(Workspace ws)
    {
        if (Running) return;
        Prepare(ws);
        cts = new CancellationTokenSource();
        Running = true;
        ActiveWires.Clear();
        ExecutionCount = 0;
        ResetNodesForRun();
        ConnectControls();
        PreExecuteAll();
        Changed?.Invoke();

        // components executing right now (background mode): as in CT2, other components keep running meanwhile,
        // e.g. while the DES visualization waits for the user, the components after it process its result
        var executing = new Dictionary<RunNode, Task>();
        try
        {
            await SchedulingLoopAsync(cts.Token, executing);
        }
        finally
        {
            await FinishRunAsync(executing);
        }
    }

    /// <summary>A fresh run: per-run state only; component, presentation and logs stay.</summary>
    void ResetNodesForRun()
    {
        foreach (var node in Nodes.Values)
        {
            if (node.Comp != null && node.State != RunState.Error) node.State = RunState.Idle;
            node.FirstRun = true;
            node.Progress = 0;
            node.Queues.Clear();
            node.Last.Clear();
            foreach (var input in node.Model.Inputs) node.Queues[input] = new();
        }
    }

    /// <summary>
    /// Control connections (IControl), as the CT2 workspace sets them up when a run starts: the master component
    /// (e.g. KeySearcher, TranspositionAnalyser) gets the control object of the slave (e.g. AES, Transposition, a
    /// cost function) through its ControlMaster property and drives the slave itself.
    /// </summary>
    void ConnectControls()
    {
        foreach (var node in Nodes.Values.Where(n => n.Comp != null))
        {
            foreach (var master in node.Model.Outputs.Where(o => o.IsControl))
            {
                foreach (var wire in master.Wires)
                {
                    if (wire.To?.Plugin == null || !Nodes.TryGetValue(wire.To.Plugin.Id, out var slave) || slave.Comp == null)
                    {
                        continue;
                    }
                    try
                    {
                        node.Comp!.Set(master.PropertyName, slave.Comp.Get(wire.To.PropertyName));
                    }
                    catch (Exception e)
                    {
                        Fail(node, $"Control connection '{master.PropertyName}': {e.Message}");
                    }
                }
            }
        }
    }

    void PreExecuteAll()
    {
        foreach (var node in Nodes.Values.Where(n => n.Comp != null))
        {
            try
            {
                node.Comp!.PreExecution();
            }
            catch (Exception e)
            {
                Fail(node, "PreExecution: " + e.Message + TraceSuffix(e));
            }
        }
    }

    /// <summary>With the environment variable CT2_TRACE=1 error messages include the stack trace.</summary>
    static string TraceSuffix(Exception e) => Environment.GetEnvironmentVariable("CT2_TRACE") == "1" ? "\n" + e : "";

    /// <summary>
    /// Fires every component that is ready (see <see cref="TryFire"/>), again and again, until cancelled.
    /// When nothing happened for a while the components are shown as done.
    /// </summary>
    async Task SchedulingLoopAsync(CancellationToken ct, Dictionary<RunNode, Task> executing)
    {
        const int IdleTicksUntilDone = 5;
        int idle = 0;
        while (!ct.IsCancellationRequested)
        {
            bool progressed = false;
            foreach (var node in Nodes.Values.ToList())
            {
                if (ct.IsCancellationRequested) break;
                if (node.Comp == null || node.State == RunState.Error) continue;
                lock (executing) if (executing.ContainsKey(node)) continue;

                bool fire;
                lock (Gate) fire = TryFire(node);
                if (!fire) continue;

                progressed = true;
                if (node.State == RunState.Error)
                {
                    Changed?.Invoke();
                    continue;
                }

                if (ExecuteInBackground) StartInBackground(node, executing);
                else
                {
                    await RunNodeAsync(node);
                    Changed?.Invoke();
                    await Task.Yield();
                }
            }

            bool busy;
            lock (executing) busy = executing.Count > 0;
            if (progressed || busy)
            {
                idle = 0;
                await Task.Delay(Math.Max(1, progressed ? SleepMs : 20), CancellationToken.None);
            }
            else
            {
                idle++;
                await Task.Delay(idle > 20 ? 100 : 20, CancellationToken.None);
            }
            if (idle == IdleTicksUntilDone) MarkRunningNodesDone();
        }
    }

    void StartInBackground(RunNode node, Dictionary<RunNode, Task> executing)
    {
        var execution = RunNodeAsync(node).ContinueWith(_ =>
        {
            lock (executing) executing.Remove(node);
            Changed?.Invoke();
        }, TaskScheduler.Default);
        lock (executing) executing[node] = execution;
        Changed?.Invoke();
    }

    void MarkRunningNodesDone()
    {
        foreach (var node in Nodes.Values.Where(n => n.State == RunState.Running)) node.State = RunState.Done;
        lock (Gate) ActiveWires.Clear();
        Changed?.Invoke();
    }

    /// <summary>Waits briefly for components that are still executing, then stops and post-executes all of them.</summary>
    async Task FinishRunAsync(Dictionary<RunNode, Task> executing)
    {
        // components that are still executing were told to stop (Stop); give them a moment to return, as CT2 does
        Task[] pending;
        lock (executing) pending = executing.Values.ToArray();
        if (pending.Length > 0) await Task.WhenAny(Task.WhenAll(pending), Task.Delay(3000));

        foreach (var node in Nodes.Values.Where(n => n.Comp != null))
        {
            try
            {
                node.Comp!.Stop();
            }
            catch (Exception) { /* see Stop() */ }
            try
            {
                node.Comp!.PostExecution();
            }
            catch (Exception e)
            {
                Fail(node, "PostExecution: " + e.Message);
            }
        }
        Running = false;
        ActiveWires.Clear();
        Changed?.Invoke();
    }

    void Fail(RunNode node, string message)
    {
        node.State = RunState.Error;
        node.Logs.Add((LogLevel.Error, message));
        Log?.Invoke(node, LogLevel.Error, message);
    }

    /// <summary>
    /// CT2's rule for starting a component: sources start once; others start when all connected or mandatory inputs
    /// have a value and at least one input has a new one, and no consumer still has an unconsumed value (back-pressure).
    /// On start the queued inputs are handed to the component.
    /// </summary>
    bool TryFire(RunNode n)
    {
        var p = n.Model;
        // as in CT2 (PluginModel.HasOnlyOptionalUnconnectedInputs) a connected control input counts as connected,
        // so a slave that is driven by its master does not start by itself; for data, control connectors are ignored
        bool onlyOptionalUnconnected = p.Inputs.All(c => !c.IsMandatory && c.Wires.Count == 0);
        var dataInputs = p.Inputs.Where(c => !c.IsControl).ToList();
        if (n.FirstRun && (p.Inputs.Count == 0 || onlyOptionalUnconnected))
        {
            n.FirstRun = false;
            return true;
        }
        bool blocked = false;
        bool atLeastOneNew = false;
        foreach (var input in dataInputs)
        {
            if (!input.IsMandatory && input.Wires.Count == 0) continue;
            var queue = n.Queues[input];
            if (queue.Count == 0 && !n.Last.ContainsKey(input)) blocked = true; // never received a value
            else if (queue.Count > 0) atLeastOneNew = true;
        }
        foreach (var output in p.Outputs.Where(o => !o.IsControl))
        {
            foreach (var wire in output.Wires)
            {
                bool consumerHasPending = wire.To != null
                    && Nodes.TryGetValue(wire.To.Plugin!.Id, out var target)
                    && target.Queues.TryGetValue(wire.To, out var targetQueue)
                    && targetQueue.Count > 0;
                if (consumerHasPending) blocked = true;
            }
        }
        if (blocked || !atLeastOneNew) return false;
        foreach (var c in p.Inputs)
        {
            var q = n.Queues[c];
            if (q.Count == 0) continue;
            var val = q.Dequeue();
            try
            {
                n.Comp!.Set(c.PropertyName, DataConvert.To(val, n.Comp.TypeOf(c.PropertyName)));
            }
            catch (Exception e)
            {
                Fail(n, $"Setting '{c.PropertyName}': {e.Message}");
                return true;
            }
            lock (Gate)
            {
                n.Last[c] = val;
                foreach (var w in c.Wires) ActiveWires.Remove(w.Id);
            }
        }
        return true;
    }

    /// <summary>
    /// Runs plugin code on a thread of its own, as CT2 does. Plugins block in Execute (network receivers wait for
    /// packets, visualizations wait for the user), which must not use up the thread pool: the runtime's own work
    /// (timers, task continuations) runs there, and the browser runtime cannot always add threads in time.
    /// </summary>
    static Task OnOwnThread(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Executes one component (on a background thread when <see cref="ExecuteInBackground"/>).</summary>
    async Task RunNodeAsync(RunNode n)
    {
        n.State = RunState.Running;
        n.Progress = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var comp = n.Comp!;
            if (ExecuteInBackground) await OnOwnThread(comp.Execute);
            else comp.Execute();
            n.LastMs = sw.ElapsedMilliseconds;
            Interlocked.Increment(ref executionCount);
            n.Progress = 1;
            if (n.State == RunState.Running) n.State = RunState.Done;
        }
        catch (Exception e)
        {
            Fail(n, (e.InnerException?.Message ?? e.Message) + TraceSuffix(e));
        }
    }

    void OnOutput(RunNode n, string prop)
    {
        // outside a run (prepared or stopped workspace) nothing flows along the wires, as in CT2
        if (!Running) return;
        var port = n.Model.Outputs.FirstOrDefault(o => o.PropertyName == prop);
        if (port == null || port.IsControl) return;
        object? data;
        try
        {
            data = n.Comp!.Get(prop);
        }
        catch (Exception)
        {
            return;
        } // a property that cannot be read produces no output
        if (data == null) return;
        // components report outputs from their own thread while the UI thread reads the state: under the gate
        lock (Gate)
        {
            n.Last[port] = data;
            foreach (var wire in port.Wires)
            {
                if (wire.To?.Plugin == null || !Nodes.TryGetValue(wire.To.Plugin.Id, out var target)) continue;
                target.Queues[wire.To].Enqueue(data);
                ActiveWires.Add(wire.Id);
            }
        }
    }
}

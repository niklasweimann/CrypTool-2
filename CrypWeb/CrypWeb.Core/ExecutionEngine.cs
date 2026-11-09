namespace CrypTool.Web.Core;

public sealed class RunNode
{
    public PluginNode Model { get; init; } = null!;
    public RunComponent? Comp { get; set; }
    public RunState State { get; set; } = RunState.Idle;
    public double Progress { get; set; }
    public bool Supported => Comp != null;
    public bool FirstRun = true;
    public long LastMs;
    /// <summary>Latest value per presentation call (JSON), and the names changed since the last report.</summary>
    public readonly Dictionary<string, string> Pres = new();
    public readonly HashSet<string> PresDirty = new();
    public readonly Dictionary<ConnectorPort, Queue<object?>> Queues = new();
    public readonly Dictionary<ConnectorPort, object?> Last = new();
    public readonly List<(LogLevel Level, string Message)> Logs = new();
    public string? Display => Comp?.Display;
    public string? ImageUri => (Comp as NativeComponent)?.ImageUri;
    public int ImageVersion => (Comp as NativeComponent)?.ImageVersion ?? 0;
}

/// <summary>
/// Single-threaded cooperative re-implementation of the CT2 workspace execution semantics
/// (queued inputs, mandatory inputs, back-pressure on unconsumed outputs).
/// </summary>
public sealed class ExecutionEngine
{
    public Dictionary<int, RunNode> Nodes { get; } = new();
    public HashSet<int> ActiveWires { get; } = new();
    public bool Running { get; private set; }
    /// <summary>Pause between executions in ms (global setting "sleep time").</summary>
    public int SleepMs { get; set; } = 1;
    public event Action? Changed;
    public event Action<RunNode, LogLevel, string>? Log;
    public int ExecutionCount { get; private set; }
    CancellationTokenSource? cts;

    public void Stop() { cts?.Cancel(); }

    /// <summary>Re-runs a source component (e.g. after its text was edited while running).</summary>
    public void Retrigger(int pluginId) { if (Nodes.TryGetValue(pluginId, out var n) && n.Model.Inputs.Count == 0) n.FirstRun = true; }

    public async Task RunAsync(Workspace ws)
    {
        if (Running) return;
        cts = new CancellationTokenSource(); var ct = cts.Token;
        Running = true; Nodes.Clear(); ActiveWires.Clear(); ExecutionCount = 0;
        foreach (var p in ws.Plugins)
        {
            var rn = new RunNode { Model = p };
            try
            {
                rn.Comp = PluginRegistry.Create(p.TypeName);
                if (rn.Comp != null)
                {
                    rn.Comp.ApplySettings(p.Settings);
                    rn.Comp.OutputChanged += prop => OnOutput(rn, prop);
                    rn.Comp.Log += (l, m) => { rn.Logs.Add((l, m)); if (rn.Logs.Count > 200) rn.Logs.RemoveAt(0); Log?.Invoke(rn, l, m); };
                    rn.Comp.Progress += d => rn.Progress = d;
                    rn.Comp.Presentation += (name, args) => RecordPresentation(rn, name, args);
                }
            }
            catch (Exception e) { rn.State = RunState.Error; rn.Logs.Add((LogLevel.Error, e.Message)); }
            foreach (var c in p.Inputs) rn.Queues[c] = new();
            Nodes[p.Id] = rn;
        }
        foreach (var n in Nodes.Values.Where(n => n.Comp != null))
        {
            try { n.Comp!.PreExecution(); } catch (Exception e) { Fail(n, "PreExecution: " + e.Message + (Environment.GetEnvironmentVariable("CT2_TRACE") == "1" ? "\n" + e : "")); }
        }
        Changed?.Invoke();
        try
        {
            int idle = 0;
            while (!ct.IsCancellationRequested)
            {
                bool progressed = false;
                foreach (var n in Nodes.Values.ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    if (n.Comp == null || n.State == RunState.Error) continue;
                    if (TryFire(n)) { progressed = true; Changed?.Invoke(); await Task.Yield(); }
                }
                if (progressed) { idle = 0; await Task.Delay(Math.Max(1, SleepMs), CancellationToken.None); }
                else { idle++; await Task.Delay(idle > 20 ? 100 : 20, CancellationToken.None); }
                if (idle == 5) { foreach (var n in Nodes.Values) if (n.State == RunState.Running) n.State = RunState.Done; ActiveWires.Clear(); Changed?.Invoke(); }
            }
        }
        finally
        {
            foreach (var n in Nodes.Values.Where(n => n.Comp != null))
            {
                try { n.Comp!.Stop(); } catch { }
                try { n.Comp!.PostExecution(); } catch (Exception e) { Fail(n, "PostExecution: " + e.Message); }
            }
            Running = false; ActiveWires.Clear(); Changed?.Invoke();
        }
    }

    /// <summary>Turns whatever a plugin passed to its presentation into JSON-friendly data (grids, lists, numbers, text).</summary>
    static object? ToPlain(object? o, int depth = 0)
    {
        if (o == null) return null;
        if (depth > 5) return o.ToString();
        switch (o)
        {
            case string: case bool: return o;
            case char c: return c.ToString();
            case double d: return double.IsFinite(d) ? d : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case float f: return float.IsFinite(f) ? f : f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case byte or sbyte or short or ushort or int or uint or long or ulong or decimal: return o;
            case System.Numerics.BigInteger bi: return bi.ToString();
            case Enum e: return e.ToString();
            case byte[] bytes: return "0x" + Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 96))) + (bytes.Length > 96 ? "…" : "");
            case Array a when a.Rank == 2:
                {
                    var rows = new List<object?>();
                    for (int i = 0; i < Math.Min(a.GetLength(0), 200); i++)
                    {
                        var row = new List<object?>();
                        for (int j = 0; j < Math.Min(a.GetLength(1), 60); j++) row.Add(ToPlain(a.GetValue(i, j), depth + 1));
                        rows.Add(row);
                    }
                    return rows;
                }
            case System.Collections.IDictionary dict:
                {
                    var r = new Dictionary<string, object?>(); int n = 0;
                    foreach (System.Collections.DictionaryEntry kv in dict) { if (n++ >= 100) break; r[kv.Key?.ToString() ?? ""] = ToPlain(kv.Value, depth + 1); }
                    return r;
                }
            case System.Collections.IEnumerable en:
                {
                    var l = new List<object?>(); int n = 0;
                    foreach (var x in en) { if (n++ >= 500) break; l.Add(ToPlain(x, depth + 1)); }
                    return l;
                }
            default: return o.ToString();
        }
    }

    void RecordPresentation(RunNode n, string name, object?[] args)
    {
        string json;
        try { json = System.Text.Json.JsonSerializer.Serialize(ToPlain(args.Length == 1 ? args[0] : args)); }
        catch { json = System.Text.Json.JsonSerializer.Serialize(string.Join(", ", args.Select(x => x?.ToString()))); }
        if (json.Length > 60000) json = System.Text.Json.JsonSerializer.Serialize("(too large to show: " + json.Length + " characters)");
        lock (n.Pres) { n.Pres[name] = json; n.PresDirty.Add(name); }
    }

    void Fail(RunNode n, string msg) { n.State = RunState.Error; n.Logs.Add((LogLevel.Error, msg)); Log?.Invoke(n, LogLevel.Error, msg); }

    bool TryFire(RunNode n)
    {
        var p = n.Model;
        bool onlyOptionalUnconnected = p.Inputs.All(c => !c.IsMandatory && c.Wires.Count == 0);
        if (n.FirstRun && (p.Inputs.Count == 0 || onlyOptionalUnconnected))
        {
            n.FirstRun = false;
            Run(n); return true;
        }
        bool blocked = false, atLeastOneNew = false;
        foreach (var c in p.Inputs)
        {
            if (c.IsMandatory || c.Wires.Count > 0)
            {
                var q = n.Queues[c];
                if (q.Count == 0 && !n.Last.ContainsKey(c)) { blocked = true; continue; }
                if (q.Count > 0) atLeastOneNew = true;
            }
        }
        foreach (var o in p.Outputs)
            foreach (var w in o.Wires)
                if (w.To != null && Nodes.TryGetValue(w.To.Plugin!.Id, out var tn) && tn.Queues.TryGetValue(w.To, out var tq) && tq.Count > 0) blocked = true;
        if (blocked || !atLeastOneNew) return false;
        foreach (var c in p.Inputs)
        {
            var q = n.Queues[c];
            if (q.Count == 0) continue;
            var val = q.Dequeue();
            try { n.Comp!.Set(c.PropertyName, DataConvert.To(val, n.Comp.TypeOf(c.PropertyName))); }
            catch (Exception e) { Fail(n, $"Setting '{c.PropertyName}': {e.Message}"); return true; }
            n.Last[c] = val;
            foreach (var w in c.Wires) ActiveWires.Remove(w.Id);
        }
        Run(n); return true;
    }

    void Run(RunNode n)
    {
        n.State = RunState.Running; n.Progress = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { n.Comp!.Execute(); n.LastMs = sw.ElapsedMilliseconds; ExecutionCount++; n.Progress = 1; if (n.State == RunState.Running) n.State = RunState.Done; }
        catch (Exception e) { Fail(n, (e.InnerException?.Message ?? e.Message) + (Environment.GetEnvironmentVariable("CT2_TRACE") == "1" ? "\n" + e : "")); }
    }

    void OnOutput(RunNode n, string prop)
    {
        var port = n.Model.Outputs.FirstOrDefault(o => o.PropertyName == prop);
        if (port == null) return;
        object? data; try { data = n.Comp!.Get(prop); } catch { return; }
        if (data == null) return;
        n.Last[port] = data;
        foreach (var w in port.Wires)
            if (w.To?.Plugin != null && Nodes.TryGetValue(w.To.Plugin.Id, out var tn))
            { tn.Queues[w.To].Enqueue(data); ActiveWires.Add(w.Id); }
    }
}

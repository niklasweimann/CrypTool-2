using System.Text.Json;
using CrypTool.Web.Core;
using CrypWeb.App.Engine;
using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>Running a workspace: start/stop and messages coming back from the engine.</summary>
public partial class Home
{
    private async Task Play()
    {
        if (engine.Running && engine.Paused && ReferenceEquals(runDoc, active))
        {
            // Play while paused continues the run
            engine.Paused = false;
            EngineHost.Resume();
            return;
        }

        await StartRun(false);
    }

    /// <summary>Step mode: pauses the run; it continues with Play or one component at a time with Step.</summary>
    private void PauseRun()
    {
        if (!engine.Running || !ReferenceEquals(runDoc, active)) return;
        engine.Paused = true;
        EngineHost.Pause();
    }

    /// <summary>Starts the next ready component (F10); without a run the workspace starts paused.</summary>
    private async Task StepRun()
    {
        if (active.Kind != DocKind.Workspace) return;
        if (!engine.Running)
        {
            await StartRun(true);
        }

        if (!ReferenceEquals(runDoc, active)) return;
        engine.Paused = true;
        EngineHost.Step();
    }

    private async Task StartRun(bool paused)
    {
        if (engine.Running || active.Kind != DocKind.Workspace) return;
        if (runDoc != null && runDoc.Engine.Running) StopRun();
        CommitEdit();
        engine.Reset();
        engine.Running = true;
        runDoc = active;
        // the worker reuses the components it prepared for this workspace (presentations keep their state)
        previewDoc = active;
        previewSignature = PreviewSignature(active);
        AddLog(Loc["log.workspace"], LogLevel.Info, Loc["log.started"]);
        EngineHost.SetCulture(Loc.Lang);
        engine.Paused = paused;
        EngineHost.Start(ed.ToJson(), active.Key, settings.GuiUpdateMs, settings.SleepMs, paused);
        StartRunLimit();
        await Task.CompletedTask;
    }

    private Timer? runLimitTimer;

    /// <summary>Stops the run after the time limit of the settings (0 = no limit).</summary>
    private void StartRunLimit()
    {
        ClearRunLimit();
        if (settings.RunLimitMinutes <= 0) return;
        var minutes = settings.RunLimitMinutes;
        runLimitTimer = new Timer(_ => _ = InvokeAsync(() =>
        {
            if (runDoc == null || !runDoc.Engine.Running) return;
            AddLog(Loc["log.workspace"], LogLevel.Warning, Loc.T("log.timeLimit", minutes), 0, runDoc);
            StopRun();
            StateHasChanged();
        }), null, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan);
    }

    private void ClearRunLimit()
    {
        runLimitTimer?.Dispose();
        runLimitTimer = null;
    }

    private void StopRun()
    {
        ClearRunLimit();
        var d = runDoc;
        if (d == null || !d.Engine.Running) return;
        d.Engine.Running = false;
        d.Engine.Paused = false;
        d.Engine.Ready.Clear();
        d.Engine.ActiveWires.Clear();
        foreach (var node in d.Engine.Nodes.Values.Where(n => n.State == RunState.Running))
            node.State = RunState.Idle;
        AddLog(Loc["log.workspace"], LogLevel.Info, Loc["log.stopped"], 0, d);
        runDoc = null;
        // the components are told to stop; one that ignores it keeps computing in the background, the page stays usable
        EngineHost.Stop();
    }

    /// <summary>
    ///     Stores settings that a component changed itself (e.g. Enigma's rotors, swapped by drag and drop in its
    ///     presentation) in the workspace, so they are saved and used by the next run, as in CT2.
    /// </summary>
    private void ApplyChangedSettings(Doc d, int pluginId, JsonElement changed)
    {
        var plugin = d.Ed.Ws.Plugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin == null) return;
        foreach (var setting in changed.EnumerateArray())
        {
            var name = setting.GetProperty("name").GetString()!;
            plugin.Settings[name] = setting.GetProperty("value").GetString() ?? "";
            if (!plugin.SettingTypes.ContainsKey(name)) plugin.SettingTypes[name] = setting.GetProperty("type").GetString() ?? "System.String";
        }

        d.Ed.MarkDirty();
        // the worker already has these values: no need to give it the workspace again
        if (ReferenceEquals(d, previewDoc)) previewSignature = PreviewSignature(d);
    }

    // ---- messages from the engine ----
    // The engine posts JSON objects: {"type":"state"|"log"|"stopped", ...} (see EngineHost).

    [JSInvokable]
    public void OnWorkerMessage(string json)
    {
        // messages belong to the running workspace, or else to the one the engine prepared
        var target = runDoc ?? previewDoc;
        if (target == null) return;

        using var message = JsonDocument.Parse(json);
        var root = message.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "state":
                HandleStateMessage(target, root);
                break;
            case "log":
                HandleLogMessage(target, root);
                break;
            case "stopped":
                HandleStoppedMessage(target);
                break;
        }

        StateHasChanged();
    }

    private static string? StringOrNull(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>Progress snapshot: which wires carry data and, per component, state, progress, display texts and results.</summary>
    private void HandleStateMessage(Doc target, JsonElement state)
    {
        var engine = target.Engine;
        if (!engine.Running && !ReferenceEquals(target, previewDoc)) return;

        engine.ExecutionCount = state.GetProperty("count").GetInt32();
        engine.Paused = state.GetProperty("paused").GetBoolean();
        engine.Ready.Clear();
        foreach (var ready in state.GetProperty("ready").EnumerateArray())
            engine.Ready.Add(ready.GetInt32());
        engine.LastStarted = state.GetProperty("last").ValueKind == JsonValueKind.Number ? state.GetProperty("last").GetInt32() : null;
        engine.ActiveWires.Clear();
        foreach (var wire in state.GetProperty("wires").EnumerateArray())
            engine.ActiveWires.Add(wire.GetInt32());

        foreach (var node in state.GetProperty("nodes").EnumerateArray())
        {
            var id = node.GetProperty("id").GetInt32();
            if (!engine.Nodes.TryGetValue(id, out var view))
                engine.Nodes[id] = view = new NodeView();

            view.State = Enum.Parse<RunState>(node.GetProperty("state").GetString()!);
            view.Progress = node.GetProperty("progress").GetDouble();
            view.Ms = node.GetProperty("ms").GetInt64();
            view.Runs = node.GetProperty("runs").GetInt32();
            view.TotalMs = node.GetProperty("total").GetInt64();
            view.Display = StringOrNull(node, "display");
            view.LastError = StringOrNull(node, "err");
            if (StringOrNull(node, "image") is { } image) view.Image = image;
            if (StringOrNull(node, "html") is { } html) view.Html = html;

            if (node.TryGetProperty("io", out var io) && io.ValueKind == JsonValueKind.Array)
                foreach (var connector in io.EnumerateArray())
                {
                    var key = (connector.GetProperty("name").GetString()!, connector.GetProperty("out").GetBoolean());
                    view.Io[key] = connector.GetProperty("value").GetString() ?? "";
                }

            if (node.TryGetProperty("settings", out var changed) && changed.ValueKind == JsonValueKind.Array)
                ApplyChangedSettings(target, id, changed);
        }
    }

    private void HandleLogMessage(Doc target, JsonElement log)
    {
        var id = log.GetProperty("id").GetInt32();
        var plugin = target.Ed.Ws.Plugins.FirstOrDefault(p => p.Id == id);
        Enum.TryParse<LogLevel>(log.GetProperty("level").GetString(), out var level);
        var source = plugin == null ? Loc["log.engine"] : Title(plugin, Meta(plugin));
        AddLog(source, level, log.GetProperty("message").GetString() ?? "", id, target);
    }

    private void HandleStoppedMessage(Doc target)
    {
        // after the user pressed Stop the log already says so
        if (target.Engine.Running)
            AddLog(Loc["log.workspace"], LogLevel.Info, Loc["log.finished"], 0, target);
        target.Engine.Running = false;
        target.Engine.Paused = false;
        target.Engine.Ready.Clear();
        target.Engine.ActiveWires.Clear();
        if (ReferenceEquals(runDoc, target))
        {
            runDoc = null;
            ClearRunLimit();
        }
    }
}

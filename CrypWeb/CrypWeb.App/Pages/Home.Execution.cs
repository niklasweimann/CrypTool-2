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
        if (engine.Running || active.Kind != DocKind.Workspace) return;
        if (runDoc != null && runDoc.Engine.Running) StopRun();
        CommitEdit();
        logOpen = true;
        engine.Reset();
        engine.Running = true;
        runDoc = active;
        // the worker reuses the components it prepared for this workspace (presentations keep their state)
        previewDoc = active;
        previewSignature = PreviewSignature(active);
        AddLog(Loc["log.workspace"], LogLevel.Info, Loc["log.started"]);
        EngineHost.SetCulture(Loc.Lang);
        EngineHost.Start(ed.ToJson(), active.Key, settings.GuiUpdateMs, settings.SleepMs);
        await Task.CompletedTask;
    }

    private void StopRun()
    {
        var d = runDoc;
        if (d == null || !d.Engine.Running) return;
        d.Engine.Running = false;
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
        target.Engine.ActiveWires.Clear();
        if (ReferenceEquals(runDoc, target)) runDoc = null;
    }
}

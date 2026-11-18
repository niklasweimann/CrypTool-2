using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

/// <summary>
///     Command palette (Ctrl/Cmd+K): type to find a command, a component or a template; Enter runs the command, places
///     the component where the mouse pointer was (or in the middle of the view) or opens the template.
/// </summary>
public partial class Home
{
    private const int PaletteMaxItems = 60;

    private bool paletteOpen;
    private string paletteQuery = "";
    private int paletteIndex;

    /// <summary>World position for a component chosen in the palette (the pointer position when it was opened).</summary>
    private (double x, double y)? paletteAt;

    private sealed record PaletteItem(string Kind, string Label, string Detail, string? Icon, Func<Task> Run);

    private async Task OpenCommandPalette()
    {
        if (paletteOpen)
        {
            ClosePalette();
            return;
        }

        paletteAt = null;
        try
        {
            var pointer = await Js.InvokeAsync<double[]>("ct2.lastPointer");
            var insideCanvas = pointer[0] >= rl && pointer[0] <= rl + rw && pointer[1] >= rt && pointer[1] <= rt + rh;
            if (insideCanvas && active.Kind == DocKind.Workspace) paletteAt = ToWorld(pointer[0], pointer[1]);
        }
        catch (JSException)
        {
            /* no pointer position: the component goes to the middle of the view */
        }

        paletteOpen = true;
        paletteQuery = "";
        paletteIndex = 0;
        ctx = Ctx.None;
        StateHasChanged();
        await Task.Yield();
        try
        {
            await Js.InvokeVoidAsync("ct2.focus", ".cmdp input");
        }
        catch (JSException)
        {
            /* the palette was closed again */
        }
    }

    private void ClosePalette()
    {
        paletteOpen = false;
        paletteQuery = "";
    }

    private Func<Task> Cmd(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    /// <summary>The commands of the palette (the ribbon's functions and the View switches).</summary>
    private IEnumerable<PaletteItem> PaletteCommands()
    {
        var command = Loc["cmd.kind"];
        var ws = active.Kind == DocKind.Workspace;
        if (ws && (!engine.Running || engine.Paused)) yield return new(command, Loc[engine.Paused ? "exec.resume" : "exec.play"], "", "play", Play);
        if (ws && engine.Running && !engine.Paused) yield return new(command, Loc["exec.pause"], "", "pause", Cmd(PauseRun));
        if (ws && (!engine.Running || engine.Paused)) yield return new(command, Loc["exec.step"], "F10", "step", StepRun);
        if (engine.Running) yield return new(command, Loc["exec.stop"], "", "stop", Cmd(StopRun));
        yield return new(command, Loc["file.new"], "", "new", Cmd(NewDoc));
        if (ws) yield return new(command, Loc["file.save"], "Ctrl+S", "save", Cmd(Save));
        yield return new(command, Loc["doc.start"], "", "star", Cmd(() => ShowSingleton(DocKind.Start)));
        yield return new(command, Loc["doc.settings"], "", "settings", Cmd(() => ShowSingleton(DocKind.Settings)));
        yield return new(command, Loc["links.title"], "", "link", Cmd(OpenLinkManager));
        if (ws)
        {
            yield return new(command, Loc["view.fit"], "", "fit", FitAsync);
            yield return new(command, "100%", "", null, Cmd(Zoom100));
            yield return new(command, Loc["ws.undo"], "Ctrl+Z", "undo", Cmd(Undo));
            yield return new(command, Loc["ws.redo"], "Ctrl+Y", "redo", Cmd(Redo));
            yield return new(command, Loc["ws.addText"], "", "note", Cmd(AddNote));
            yield return new(command, Loc["view.export"], "", "camera", ExportImage);
        }

        yield return new(command, Loc["log.title"] + (logOpen ? " ✓" : ""), "", "log", Cmd(() => logOpen = !logOpen));
        yield return new(command, Loc["view.minimap"] + (showMinimap ? " ✓" : ""), "", "map", Cmd(() => showMinimap = !showMinimap));
        yield return new(command, Loc["view.timings"] + (showTimings ? " ✓" : ""), "", "timer", Cmd(() => showTimings = !showTimings));
        yield return new(command, Loc["view.snap"] + (snap ? " ✓" : ""), "", null, Cmd(() => snap = !snap));
        yield return new(command, Loc["offline.make"], "", "offline", Cmd(MakeAvailableOffline));
        yield return new(command, Loc["cmd.errorReport"], "", null, async () => await Js.InvokeVoidAsync("ctCrash.open"));
    }

    /// <summary>The entries that match the query: every word of it must occur; names that start with it come first.</summary>
    private List<PaletteItem> PaletteItems()
    {
        var words = paletteQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var components = active.Kind == DocKind.Workspace
            ? VisibleMeta.Where(m => m.Supported).OrderBy(m => m.NameFor(Loc.Lang)).Select(m =>
                new PaletteItem(Loc["cmd.component"], m.NameFor(Loc.Lang), CatLabel(m.Category), m.Icon, Cmd(() => AddFromPalette(m.ToPalette()))))
            : Enumerable.Empty<PaletteItem>();
        var templateItems = VisibleTemplates.Select(t =>
            new PaletteItem(Loc["cmd.template"], Path.GetFileNameWithoutExtension(t), Path.GetDirectoryName(t)?.Replace("/", " › ") ?? "",
                templateIcons.Contains(t) ? TemplateIcon(t) : null, () => OpenTemplate(t)));
        var all = PaletteCommands().Concat(components).Concat(templateItems);
        if (words.Length == 0) return PaletteCommands().Take(PaletteMaxItems).ToList();

        bool Matches(PaletteItem item)
        {
            var text = item.Label + " " + item.Detail + " " + item.Kind;
            return words.All(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        }

        return all.Where(Matches)
            .OrderBy(item => item.Label.StartsWith(paletteQuery.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Take(PaletteMaxItems)
            .ToList();
    }

    private void AddFromPalette(PaletteEntry entry)
    {
        if (paletteAt is { } at) AddAt(entry, at.x - 50, at.y - 40);
        else AddAtCenter(entry);
    }

    /// <summary>Click on an entry: looked up by position at the time of the click (the handler is reused across renders).</summary>
    private async Task RunPaletteIndex(int index)
    {
        var items = PaletteItems();
        if (index < items.Count) await RunPaletteItem(items[index]);
    }

    private async Task RunPaletteItem(PaletteItem item)
    {
        ClosePalette();
        try
        {
            await item.Run();
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        StateHasChanged();
    }

    private async Task PaletteKey(KeyboardEventArgs e)
    {
        var items = PaletteItems();
        switch (e.Key)
        {
            case "Escape":
                ClosePalette();
                break;
            case "ArrowDown":
                paletteIndex = items.Count == 0 ? 0 : (paletteIndex + 1) % items.Count;
                await ScrollPaletteSelection();
                break;
            case "ArrowUp":
                paletteIndex = items.Count == 0 ? 0 : (paletteIndex - 1 + items.Count) % items.Count;
                await ScrollPaletteSelection();
                break;
            case "Enter":
                if (items.Count > 0) await RunPaletteItem(items[Math.Clamp(paletteIndex, 0, items.Count - 1)]);
                break;
        }
    }

    private void PaletteInput(ChangeEventArgs e)
    {
        paletteQuery = e.Value?.ToString() ?? "";
        paletteIndex = 0;
    }

    private async Task ScrollPaletteSelection()
    {
        StateHasChanged();
        await Task.Yield();
        try
        {
            await Js.InvokeVoidAsync("ct2.scrollIntoView", ".cmdp .ci-item.on");
        }
        catch (JSException)
        {
            /* closed meanwhile */
        }
    }
}

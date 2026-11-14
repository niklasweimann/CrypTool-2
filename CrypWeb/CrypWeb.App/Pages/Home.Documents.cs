using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>Document tabs and the editing commands (undo, copy, add component, ...).</summary>
public partial class Home
{
    // ---- document tabs ----

    /// <summary>Makes <paramref name="doc" /> the visible tab and re-reads the canvas size (it differs per tab kind).</summary>
    private async Task Activate(Doc doc)
    {
        active = doc;
        editing = null;
        error = null;
        await Task.Delay(20); // let Blazor render the new tab before measuring
        await MeasureCanvasAsync();
    }

    private void NewDoc()
    {
        var doc = new Doc();
        docs.Add(doc);
        active = doc;
    }

    /// <summary>Shows the start page / settings tab, creating it on first use.</summary>
    private void ShowSingleton(DocKind kind)
    {
        var doc = docs.FirstOrDefault(d => d.Kind == kind);
        if (doc == null)
        {
            doc = new Doc { Kind = kind };
            docs.Add(doc);
        }

        active = doc;
    }

    private async Task CloseDoc(Doc doc)
    {
        var hasContent = doc.Ed.Ws.Plugins.Count + doc.Ed.Ws.Texts.Count > 0;
        var needsConfirm = doc.Kind == DocKind.Workspace && doc.Ed.Dirty && hasContent;
        if (needsConfirm && !await Js.InvokeAsync<bool>("confirm", Loc.T("doc.confirmClose", DocTitle(doc))))
            return;

        if (runDoc == doc)
        {
            // StopRun works on the active document
            var keep = active;
            active = doc;
            StopRun();
            active = keep;
        }

        var index = docs.IndexOf(doc);
        docs.Remove(doc);
        ForgetHandlers(); // they may hold elements of the closed document
        if (docs.Count == 0) docs.Add(new Doc());
        if (active == doc) active = docs[Math.Min(index, docs.Count - 1)];
    }

    /// <summary>Tooltip of a tab: the file name, or the title of documents without a file.</summary>
    private string TabTip(Doc doc)
    {
        return doc.Kind == DocKind.Workspace && doc.Name != null ? doc.Name : DocTitle(doc).TrimEnd('*', ' ');
    }

    private async Task CloseAll()
    {
        foreach (var doc in docs.ToList()) await CloseDoc(doc);
    }

    private async Task CloseOthers(Doc keep)
    {
        foreach (var doc in docs.ToList().Where(d => d != keep)) await CloseDoc(doc);
        await Activate(keep);
    }

    private async void CopyText(string text)
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSException)
        {
            /* clipboard not available (insecure context or denied) */
        }
    }

    /// <summary>Hides or shows the ribbon (F12).</summary>
    private async void ToggleMenu()
    {
        hideMenu = !hideMenu;
        await RemeasureSoon();
    }

    /// <summary>Distraction free: only the tabs and the canvas (F11).</summary>
    private async void ToggleFocus()
    {
        focusMode = !focusMode;
        await RemeasureSoon();
    }

    private async Task RemeasureSoon()
    {
        StateHasChanged();
        await Task.Delay(30); // the canvas changes its size once Blazor has rendered
        await MeasureCanvasAsync();
    }

    private string DocTitle(Doc doc)
    {
        var title = doc.Kind switch
        {
            DocKind.Start => Loc["doc.start"],
            DocKind.Settings => Loc["doc.settings"],
            _ => doc.Name == null ? Loc["doc.untitled"] : Path.GetFileNameWithoutExtension(doc.Name)
        };
        var unsaved = doc.Kind == DocKind.Workspace && doc.Ed.Dirty;
        return unsaved ? title + " *" : title;
    }

    private void New()
    {
        NewDoc();
    }

    /// <summary>Saves a CrypTool 2 compatible .cwm file (opens in the desktop version).</summary>
    private async void Save()
    {
        if (active.Kind != DocKind.Workspace) return;
        var name = current == null ? "workspace" : Path.GetFileNameWithoutExtension(current);
        try
        {
            var bytes = CwmWriter.Write(ed.Ws);
            await Js.InvokeVoidAsync("ct2.downloadBytes", name + ".cwm", Convert.ToBase64String(bytes));
            _ = AddRecent(name + ".cwm", "file", bytes);
        }
        catch (Exception e)
        {
            AddLog("Workspace", LogLevel.Error, "Could not save: " + e.Message);
        }
    }

    // ---- editing commands (each pushes an undo checkpoint before it changes the workspace) ----

    private void Undo()
    {
        ed.Undo();
    }

    private void Redo()
    {
        ed.Redo();
    }

    private void CopySel()
    {
        ed.Copy();
    }

    private void DeleteSel()
    {
        if (!HasSelection) return;
        ed.Checkpoint();
        ed.DeleteSelection();
    }

    private void CutSel()
    {
        ed.Copy();
        DeleteSel();
    }

    private void PasteClip()
    {
        ed.Checkpoint();
        ed.Paste();
    }

    private void AddNote()
    {
        ed.Checkpoint();
        var (x, y) = Center();
        var note = ed.AddText(x - 110, y - 40);
        SelectOnly(note.Id);
    }

    private void AddAtCenter(PaletteEntry entry)
    {
        var (x, y) = Center();
        AddAt(entry, x - 60, y - 40);
    }

    /// <summary>Adds a component at the given world position, in the view chosen in the settings.</summary>
    private void AddAt(PaletteEntry entry, double x, double y)
    {
        ed.Checkpoint();
        var plugin = ed.AddPlugin(entry, Snap(x), Snap(y));
        if (settings.DefaultView == 2) plugin.ViewState = 7;
        else if (settings.DefaultView == 1) SetView(plugin, 1);
        SelectOnly(plugin.Id);
    }

    private void SelectOnly(int id)
    {
        ed.Selection.Clear();
        ed.Selection.Add(id);
    }

    /// <summary>The world coordinates of the middle of the visible canvas.</summary>
    private (double, double) Center()
    {
        return ((rw / 2 - panX) / zoom, (rh / 2 - panY) / zoom);
    }

    /// <summary>A palette entry was dropped onto the canvas.</summary>
    private void Drop(DragEventArgs e)
    {
        if (dragEntry == null) return;
        var (wx, wy) = ToWorld(e.ClientX, e.ClientY);
        AddAt(dragEntry, wx, wy);
        dragEntry = null;
    }
}

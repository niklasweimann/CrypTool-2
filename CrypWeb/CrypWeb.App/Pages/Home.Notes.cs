using System.Net;
using CrypTool.Web.Core;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

/// <summary>
///     Notes with rich text, as in CT2: a note shows its formatted text (NoteXaml turns the stored XAML into HTML) and
///     is edited in place with a toolbar (font, clipboard, undo, bold/italic/underline, size, lists, alignment,
///     indentation). The editor is a contenteditable element run by wwwroot/noteeditor.js; when it closes, it sends
///     the text as blocks of styled runs, which become the note's XAML again.
/// </summary>
public partial class Home
{
    /// <summary>The note being edited, or null.</summary>
    private TextNode? editingNote;

    /// <summary>Fonts offered in the note editor (the ones CT2's templates use, and common web fonts).</summary>
    private static readonly string[] NoteFonts =
    {
        "Segoe UI", "Arial", "Calibri", "Cambria", "Consolas", "Courier New", "Georgia", "Lucida Sans Unicode", "Tahoma",
        "Times New Roman", "Verdana",
    };

    /// <summary>The note's text as HTML: its rich text, or its plain text with the default formatting.</summary>
    private static string NoteHtml(TextNode t) =>
        NoteXaml.ToHtml(t.Xaml) ?? NoteXaml.ToHtml(NoteXaml.FromText(t.Text)) ?? WebUtility.HtmlEncode(t.Text);

    /// <summary>A new note in the middle of the view, opened in the editor (as in CT2).</summary>
    private async Task AddNote()
    {
        await EnsureWorkspace();
        ed.Checkpoint();
        var (x, y) = Center();
        var note = ed.AddText(x - 110, y - 40);
        SelectOnly(note.Id);
        EditNote(note, true);
    }

    /// <summary>Opens the editor on a note; a new note has its placeholder text selected, to be typed over.</summary>
    private void EditNote(TextNode note, bool isNew = false)
    {
        if (editingNote != null) return;
        editing = null;
        editingNote = note;
        _ = StartNoteEditor(note, isNew);
    }

    private async Task StartNoteEditor(TextNode note, bool selectAll)
    {
        await Task.Delay(20); // let Blazor render the editor element
        try
        {
            await Js.InvokeVoidAsync("ctNotes.start", NoteHtml(note), self, note.Id, selectAll);
        }
        catch (JSException)
        {
            editingNote = null;
            StateHasChanged();
        }
    }

    /// <summary>The editor closed with changes: its content as JSON blocks (see NoteXaml.Block).</summary>
    [JSInvokable]
    public void OnNoteEdited(int id, string json)
    {
        var note = editingNote;
        editingNote = null;
        if (note == null || note.Id != id) return;
        var blocks = NoteXaml.ParseBlocks(json);
        if (blocks != null)
        {
            var xaml = NoteXaml.FromBlocks(blocks);
            var text = NoteXaml.PlainText(blocks);
            if (xaml != (note.Xaml ?? NoteXaml.FromText(note.Text)) || text != note.Text)
            {
                ed.Checkpoint();
                note.Xaml = xaml;
                note.Text = text;
            }
        }

        StateHasChanged();
    }

    /// <summary>Closes the editor without keeping its changes (another tab or file is shown).</summary>
    private void CloseNoteEditor()
    {
        if (editingNote == null) return;
        editingNote = null;
        _ = Js.InvokeVoidAsync("ctNotes.abort").AsTask().ContinueWith(_ => { }, TaskScheduler.Default);
    }

    /// <summary>The editor closed without keeping the changes (Escape).</summary>
    [JSInvokable]
    public void OnNoteEditCancelled()
    {
        editingNote = null;
        StateHasChanged();
    }
}

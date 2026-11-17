using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

/// <summary>In-place editing of component names and note texts.</summary>
public partial class Home
{
    /// <summary>Shows a text box over the component title.</summary>
    private void EditName(PluginNode p)
    {
        editing = p;
        editText = Title(p, Meta(p));
        editX = p.X * zoom + panX;
        editY = (p.Y + Size(p).h + 2) * zoom + panY;
        editW = Math.Max(120, (Size(p).w - 24) * zoom);
        FocusEdit();
    }

    /// <summary>Shows a text box over a note.</summary>
    private void EditText(TextNode t)
    {
        editing = t;
        editText = t.Text;
        editX = t.X * zoom + panX;
        editY = t.Y * zoom + panY;
        editW = Math.Max(160, t.Width * zoom);
        editH = Math.Max(60, t.Height * zoom);
        FocusEdit();
    }

    /// <summary>Moves the keyboard focus into the inline text box once Blazor has rendered it.</summary>
    private async void FocusEdit()
    {
        await Task.Delay(30);
        try
        {
            await Js.InvokeVoidAsync("eval",
                "(()=>{const i=document.getElementById('inline-edit'); if(i){i.focus(); i.select();}})()");
        }
        catch (JSException)
        {
            /* the text box is already gone */
        }
    }

    /// <summary>
    ///     Keeps the typed text. No re-render: the text box already shows it, and a render per key stroke would
    ///     give the handlers of the text box new ids while the next key's events are already on their way (see
    ///     <see cref="skipRender"/>), so those events (and the typed text) would be lost.
    /// </summary>
    private void EditInput(ChangeEventArgs e)
    {
        editText = e.Value?.ToString() ?? "";
        skipRender = true;
    }

    private void EditKey(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") editing = null;
        else if (e.Key == "Enter" && (editing is not TextNode || e.CtrlKey || e.MetaKey)) CommitEdit();
        else skipRender = true; // an ordinary key: nothing visible changed
    }

    private void CommitEdit()
    {
        if (editing == null) return;
        ed.Checkpoint();
        if (editing is PluginNode plugin) plugin.Name = editText;
        else if (editing is TextNode note) note.Text = editText;
        editing = null;
    }

    private void ToggleMin(PluginNode p)
    {
        ed.Checkpoint();
        p.Minimized = !p.Minimized;
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CrypWeb.App.Pages;

// Stable event handlers for elements in loops.
//
// Blazor gives an event handler a new id whenever its delegate is not equal to the one of the previous render. A lambda
// that captures a loop variable (e => NodeDown(e, p)) is a new delegate on every render. With WasmEnableThreads the
// renderer runs on a worker thread and the browser updates the DOM a little later, so an event that arrives in between
// still carries the old id and is lost ("There is no event handler associated with this event"): clicks, double clicks
// and drags that do nothing. These helpers return the delegate of the first render for the same element (owner) and
// lambda (its compiled method), so the id stays the same. The owner must identify everything the lambda uses.
public partial class Home
{
    private readonly Dictionary<(object Owner, MethodInfo Site), Delegate> stableHandlers = new();

    private T Stable<T>(object owner, T handler) where T : Delegate
    {
        var key = (owner, handler.Method);
        if (stableHandlers.TryGetValue(key, out var existing)) return (T)existing;
        stableHandlers[key] = handler;
        return handler;
    }

    private Action On(object owner, Action handler) => Stable(owner, handler);

    private Func<Task> On(object owner, Func<Task> handler) => Stable(owner, handler);

    private Action<MouseEventArgs> OnMouse(object owner, Action<MouseEventArgs> handler) => Stable(owner, handler);

    private Action<PointerEventArgs> OnPointer(object owner, Action<PointerEventArgs> handler) => Stable(owner, handler);

    private Action<ChangeEventArgs> OnChange(object owner, Action<ChangeEventArgs> handler) => Stable(owner, handler);

    /// <summary>Forgets the handlers of a closed document's elements.</summary>
    private void ForgetHandlers()
    {
        stableHandlers.Clear();
    }
}

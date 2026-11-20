using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace CrypWeb.App.Components;

/// <summary>
///     Event handlers for elements in loops that keep the same id from one render to the next.
///     Blazor gives a handler a new id whenever its delegate is not equal to the one of the previous render, and a lambda
///     that captures a loop variable is a new delegate every time. An event that arrives while the browser still shows the
///     old DOM then carries an id nobody knows any more ("There is no event handler associated with this event"). This
///     returns the delegate of the first render for the same owner and lambda (its compiled method).
///     The owner must identify everything the lambda uses (the loop item, or a tuple of items): the lambda of the first
///     render stays in use.
/// </summary>
public sealed class StableHandlers
{
    private readonly Dictionary<(object Owner, MethodInfo Site), Delegate> known = new();

    private T Of<T>(object owner, T handler) where T : Delegate
    {
        var key = (owner, handler.Method);
        if (known.TryGetValue(key, out var existing)) return (T)existing;
        known[key] = handler;
        return handler;
    }

    public Action Of(object owner, Action handler) => Of<Action>(owner, handler);

    public Func<Task> Of(object owner, Func<Task> handler) => Of<Func<Task>>(owner, handler);

    public Action<ChangeEventArgs> Of(object owner, Action<ChangeEventArgs> handler) => Of<Action<ChangeEventArgs>>(owner, handler);

    public Func<ChangeEventArgs, Task> Of(object owner, Func<ChangeEventArgs, Task> handler) => Of<Func<ChangeEventArgs, Task>>(owner, handler);

    /// <summary>Forgets all handlers (the list the elements belong to was replaced).</summary>
    public void Clear() => known.Clear();
}

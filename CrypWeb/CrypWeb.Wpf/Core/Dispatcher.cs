namespace System.Windows.Threading
{
    public enum DispatcherPriority
    {
        Invalid = -1, Inactive = 0, SystemIdle = 1, ApplicationIdle = 2, ContextIdle = 3, Background = 4,
        Input = 5, Loaded = 6, Render = 7, DataBind = 8, Normal = 9, Send = 10,
    }

    /// <summary>There is no separate UI thread in the browser worker: everything is invoked inline.</summary>
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();

        /// <summary>The thread the runtime runs on (there is only one in the browser worker).</summary>
        public System.Threading.Thread Thread { get; } = System.Threading.Thread.CurrentThread;

        public bool CheckAccess() => true;

        public void VerifyAccess() { }

        public void Invoke(Action action) => GuardedContext.Run(action);

        public void Invoke(DispatcherPriority priority, Action action) => action();

        public void Invoke(Action action, DispatcherPriority priority) => action();

        public T Invoke<T>(Func<T> function) => function();

        public object Invoke(DispatcherPriority priority, Delegate method, params object[] args) => Call(method, args);

        public object Invoke(Delegate method, params object[] args) => Call(method, args);

        public object Invoke(Delegate method, DispatcherPriority priority, params object[] args) => Call(method, args);

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method, params object[] args)
        {
            return new DispatcherOperation(Call(method, args));
        }

        public DispatcherOperation BeginInvoke(Delegate method, params object[] args)
        {
            return new DispatcherOperation(Call(method, args));
        }

        public DispatcherOperation BeginInvoke(Delegate method, DispatcherPriority priority, params object[] args)
        {
            return new DispatcherOperation(Call(method, args));
        }

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Action action)
        {
            action();
            return new DispatcherOperation(null);
        }

        public DispatcherOperation InvokeAsync(Action action)
        {
            action();
            return new DispatcherOperation(null);
        }

        /// <summary>Callers pass null or too many/few arguments; the delegate gets exactly as many as it declares.</summary>
        private static object Call(Delegate method, object[] args)
        {
            int count = method.Method.GetParameters().Length;
            object[] exact = new object[count];
            if (args != null)
            {
                Array.Copy(args, exact, Math.Min(count, args.Length));
            }
            // async delegates continue on the guarded context: their exceptions are reported, not fatal
            return GuardedContext.Run(() => method.DynamicInvoke(exact));
        }
    }

    /// <summary>Result of an (already completed) dispatcher call.</summary>
    public sealed class DispatcherOperation
    {
        internal DispatcherOperation(object result) => Result = result;

        public object Result { get; }

        public DispatcherOperationStatus Status => DispatcherOperationStatus.Completed;

        public DispatcherOperationStatus Wait() => DispatcherOperationStatus.Completed;
    }

    public enum DispatcherOperationStatus { Pending, Aborted, Completed, Executing }

    public delegate object DispatcherOperationCallback(object arg);
}

namespace System.Windows
{
    public class Application
    {
        /// <summary>There is no desktop application object; this stand-in has no window.</summary>
        public static Application Current { get; } = new Application();

        public Window MainWindow { get; set; }

        public ResourceDictionary Resources { get; set; } = new ResourceDictionary();

        public object FindResource(object key) => Resources[key];

        public object TryFindResource(object key) => Resources[key];

        public Threading.Dispatcher Dispatcher => Threading.Dispatcher.CurrentDispatcher;
    }
}

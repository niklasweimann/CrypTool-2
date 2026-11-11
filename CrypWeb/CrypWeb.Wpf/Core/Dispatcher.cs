using System.Threading;

namespace System.Windows.Threading
{
    public enum DispatcherPriority
    {
        Invalid = -1, Inactive = 0, SystemIdle = 1, ApplicationIdle = 2, ContextIdle = 3, Background = 4,
        Input = 5, Loaded = 6, Render = 7, DataBind = 8, Normal = 9, Send = 10,
    }

    /// <summary>
    /// The UI thread of the presentations. As in WPF, presentation objects belong to one thread: the browser's main
    /// thread, which draws them and handles input. Plugin code runs on background threads and reaches its
    /// presentation through Invoke (waits for the UI thread) or BeginInvoke (queued).
    ///
    /// The host announces the UI thread with <see cref="AttachUiThread"/>. Without it (tests, single-threaded
    /// hosts) every call runs inline on the calling thread.
    /// </summary>
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();

        private static SynchronizationContext _uiContext;
        private static Thread _uiThread = Thread.CurrentThread;

        /// <summary>Makes the calling thread the UI thread; work for it is posted to <paramref name="uiContext"/>.</summary>
        public static void AttachUiThread(SynchronizationContext uiContext)
        {
            _uiContext = uiContext;
            _uiThread = Thread.CurrentThread;
        }

        /// <summary>The UI thread.</summary>
        public Thread Thread => _uiThread;

        /// <summary>True on the UI thread (or when there is no separate UI thread).</summary>
        public bool CheckAccess() => _uiContext == null || Thread.CurrentThread == _uiThread;

        public void VerifyAccess() { }

        public void Invoke(Action action) => Send(() => { action(); return null; });

        public void Invoke(DispatcherPriority priority, Action action) => Invoke(action);

        public void Invoke(Action action, DispatcherPriority priority) => Invoke(action);

        public T Invoke<T>(Func<T> function) => (T)Send(() => function());

        public object Invoke(DispatcherPriority priority, Delegate method, params object[] args) => Send(() => Call(method, args));

        public object Invoke(Delegate method, params object[] args) => Send(() => Call(method, args));

        public object Invoke(Delegate method, DispatcherPriority priority, params object[] args) => Send(() => Call(method, args));

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method, params object[] args) => Post(() => Call(method, args));

        public DispatcherOperation BeginInvoke(Delegate method, params object[] args) => Post(() => Call(method, args));

        public DispatcherOperation BeginInvoke(Delegate method, DispatcherPriority priority, params object[] args) => Post(() => Call(method, args));

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Action action) => Post(() => { action(); return null; });

        public DispatcherOperation InvokeAsync(Action action) => Post(() => { action(); return null; });

        /// <summary>Runs <paramref name="work"/> on the UI thread and waits for its result.</summary>
        private object Send(Func<object> work)
        {
            if (CheckAccess())
            {
                return GuardedContext.Run(work);
            }
            object result = null;
            Exception failure = null;
            using var done = new ManualResetEventSlim(false);
            _uiContext.Post(_ =>
            {
                try { result = GuardedContext.Run(work); }
                catch (Exception exception) { failure = exception; }
                finally { done.Set(); }
            }, null);
            done.Wait();
            if (failure != null)
            {
                throw new System.Reflection.TargetInvocationException(failure);
            }
            return result;
        }

        /// <summary>Runs <paramref name="work"/> on the UI thread without waiting (inline when already there).</summary>
        private DispatcherOperation Post(Func<object> work)
        {
            if (CheckAccess())
            {
                return new DispatcherOperation(GuardedContext.Run(work));
            }
            var operation = new DispatcherOperation(null) { Status = DispatcherOperationStatus.Pending };
            _uiContext.Post(_ =>
            {
                try { operation.Result = GuardedContext.Run(work); }
                catch (Exception exception) { GuardedContext.Report(exception); }
                finally { operation.Complete(); }
            }, null);
            return operation;
        }

        /// <summary>Work for the UI thread from code that has no dispatcher at hand (async continuations).</summary>
        internal static bool TryPostToUi(SendOrPostCallback callback, object state)
        {
            if (_uiContext == null)
            {
                return false;
            }
            _uiContext.Post(callback, state);
            return true;
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
            return method.DynamicInvoke(exact);
        }
    }

    /// <summary>A dispatcher call; completed at once on the UI thread, pending while queued from another thread.</summary>
    public sealed class DispatcherOperation
    {
        private readonly ManualResetEventSlim _done = new ManualResetEventSlim(true);

        internal DispatcherOperation(object result) => Result = result;

        public object Result { get; internal set; }

        public DispatcherOperationStatus Status
        {
            get => _done.IsSet ? DispatcherOperationStatus.Completed : DispatcherOperationStatus.Pending;
            internal set
            {
                if (value == DispatcherOperationStatus.Pending) _done.Reset();
                else _done.Set();
            }
        }

        internal void Complete() => _done.Set();

        /// <summary>Waits until the operation ran (only meaningful off the UI thread; on it, it already ran).</summary>
        public DispatcherOperationStatus Wait()
        {
            _done.Wait();
            return Status;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.Run(() => _done.Wait());
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

        /// <summary>
        /// CT2's main window. Plugins use its content as the scope of a drag operation (AllowDrop, DragOver handlers
        /// for the drag adorner), so it exists with an (empty, never drawn) panel as content.
        /// </summary>
        public Window MainWindow { get; set; } = new Window { Content = new Controls.Grid() };

        public ResourceDictionary Resources { get; set; } = new ResourceDictionary();

        public object FindResource(object key) => Resources[key];

        public object TryFindResource(object key) => Resources[key];

        public Threading.Dispatcher Dispatcher => Threading.Dispatcher.CurrentDispatcher;
    }
}

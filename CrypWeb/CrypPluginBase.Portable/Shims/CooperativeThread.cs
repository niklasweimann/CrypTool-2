using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace CrypWeb.Compat
{
    /// <summary>
    /// Stand-in for <see cref="System.Threading.Thread"/> in plugin code.
    ///
    /// The browser worker runs .NET without threads (WasmEnableThreads=false), so a real
    /// <c>new Thread(...).Start()</c> throws PlatformNotSupportedException. Plugins mostly start threads
    /// to spread a computation over several cores and then wait for them (Join, polling IsAlive, a
    /// counter or a TaskCompletionSource). For all of these patterns it is correct to simply run the
    /// thread body to completion inside <see cref="Start()"/>: when the caller begins to wait, the work is
    /// already done.
    ///
    /// The plugin projects map the name <c>Thread</c> to this class with a global using alias
    /// (CrypWeb.Plugins/ThreadAlias.cs). An alias wins over <c>using System.Threading;</c>, so the plugin
    /// sources stay unchanged. Static members that plugins use (Sleep, CurrentThread, ...) are forwarded.
    /// </summary>
    public sealed class CooperativeThread
    {
        // The thread bodies that are running right now, innermost last; CurrentThread reports the innermost one.
        private static readonly Stack<CooperativeThread> running = new();

        // Represents the worker's one real thread (CurrentThread outside of any cooperative thread body).
        private static readonly CooperativeThread mainThread = new CooperativeThread() { Name = "main", state = ThreadState.Running };

        private readonly ThreadStart start;
        private readonly ParameterizedThreadStart parameterizedStart;
        private ThreadState state = ThreadState.Unstarted;

        private CooperativeThread() { }

        public CooperativeThread(ThreadStart start) => this.start = start ?? throw new ArgumentNullException(nameof(start));

        public CooperativeThread(ThreadStart start, int maxStackSize) : this(start) { }

        public CooperativeThread(ParameterizedThreadStart start) => parameterizedStart = start ?? throw new ArgumentNullException(nameof(start));

        public CooperativeThread(ParameterizedThreadStart start, int maxStackSize) : this(start) { }

        public string Name { get; set; }

        public bool IsBackground { get; set; }

        public ThreadPriority Priority { get; set; } = ThreadPriority.Normal;

        public ThreadState ThreadState => state;

        public bool IsAlive => state == ThreadState.Running;

        public int ManagedThreadId => Environment.CurrentManagedThreadId;

        /// <summary>Culture of the (single) real thread; plugins set it to localize their messages.</summary>
        public CultureInfo CurrentCulture
        {
            get => CultureInfo.CurrentCulture;
            set => CultureInfo.CurrentCulture = value;
        }

        public CultureInfo CurrentUICulture
        {
            get => CultureInfo.CurrentUICulture;
            set => CultureInfo.CurrentUICulture = value;
        }

        public static CooperativeThread CurrentThread => running.Count > 0 ? running.Peek() : mainThread;

        /// <summary>
        /// Plugins compare <c>Thread.CurrentThread</c> with <c>Dispatcher.Thread</c> (a real thread) to find out
        /// whether they are on the UI thread. Everything runs on the one real thread, so a cooperative thread
        /// "is" that thread.
        /// Careful: the compiler also picks these operators for <c>thread == null</c> / <c>thread != null</c>
        /// (null converts to System.Threading.Thread), so null must compare like a reference comparison.
        /// </summary>
        public static bool operator ==(CooperativeThread cooperative, System.Threading.Thread real)
        {
            if (cooperative is null || real is null)
                return cooperative is null && real is null;
            return real.ManagedThreadId == Environment.CurrentManagedThreadId;
        }

        public static bool operator !=(CooperativeThread cooperative, System.Threading.Thread real) => !(cooperative == real);

        public static bool operator ==(System.Threading.Thread real, CooperativeThread cooperative) => cooperative == real;

        public static bool operator !=(System.Threading.Thread real, CooperativeThread cooperative) => !(cooperative == real);

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public void Start() => Run(null);

        public void Start(object parameter) => Run(parameter);

        /// <summary>
        /// Runs the thread body to completion. An exception ends the "thread" like on a real thread but is
        /// only logged: on Windows it would take the whole application down, here it would stop the
        /// plugin that called Start, which is not what the plugin expects.
        /// </summary>
        private void Run(object parameter)
        {
            if (state != ThreadState.Unstarted)
                throw new ThreadStateException("Thread has already been started.");

            state = ThreadState.Running;
            running.Push(this);
            try
            {
                if (start != null)
                    start();
                else
                    parameterizedStart(parameter);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unhandled exception in thread '{Name}': {ex}");
            }
            finally
            {
                running.Pop();
                state = ThreadState.Stopped;
            }
        }

        // The body has already finished when Start returned, so waiting never blocks.
        public void Join() { }

        public bool Join(int millisecondsTimeout) => !IsAlive;

        public bool Join(TimeSpan timeout) => !IsAlive;

        // Aborting or interrupting cannot stop code that is running on the only thread; plugins also
        // check their own stop flags, so these are no-ops.
        public void Abort() { }

        public void Abort(object stateInfo) { }

        public void Interrupt() { }

        public void SetApartmentState(ApartmentState apartmentState) { }

        public static void Sleep(int millisecondsTimeout) => System.Threading.Thread.Sleep(millisecondsTimeout);

        public static void Sleep(TimeSpan timeout) => System.Threading.Thread.Sleep(timeout);

        public static bool Yield() => true;

        public static void SpinWait(int iterations) => System.Threading.Thread.SpinWait(iterations);

        public static void MemoryBarrier() => Interlocked.MemoryBarrier();
    }
}

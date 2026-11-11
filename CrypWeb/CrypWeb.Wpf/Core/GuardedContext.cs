using System.Threading;

namespace System.Windows.Threading
{
    /// <summary>
    /// Synchronization context for presentation code. Code-behind often starts <c>async void</c> work from dispatcher
    /// calls and event handlers; an exception in such work is posted to the current context and would otherwise
    /// terminate the whole runtime. Here it is reported through <see cref="UnhandledException"/> instead.
    /// </summary>
    public sealed class GuardedContext : SynchronizationContext
    {
        private static readonly GuardedContext Instance = new GuardedContext();

        /// <summary>Raised for exceptions that presentation code did not handle.</summary>
        public static event Action<Exception> UnhandledException;

        /// <summary>Runs <paramref name="action"/> with this context current, so that its async continuations are guarded.</summary>
        public static void Run(Action action)
        {
            SynchronizationContext previous = Current;
            SetSynchronizationContext(Instance);
            try
            {
                action();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public static T Run<T>(Func<T> function)
        {
            T result = default;
            Run(() => { result = function(); });
            return result;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            base.Post(_ => Invoke(d, state), null);
        }

        public override void Send(SendOrPostCallback d, object state) => Invoke(d, state);

        public override SynchronizationContext CreateCopy() => this;

        private void Invoke(SendOrPostCallback d, object state)
        {
            SynchronizationContext previous = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            catch (Exception exception)
            {
                UnhandledException?.Invoke(exception);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}

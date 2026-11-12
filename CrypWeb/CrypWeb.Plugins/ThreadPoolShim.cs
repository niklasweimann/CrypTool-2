// The thread pool as plugins see it in the browser build.
//
// In CT2 every plugin's Execute runs on a thread of its own, so a plugin may resize the .NET thread pool for its
// workers (EnigmaAnalyzerLib's Runnables calls ThreadPool.SetMaxThreads(threads, threads)). In CrypWeb the engine
// runs Execute itself on the thread pool, and the pool is shared by the whole app: capping it to the number of
// analysis threads leaves no thread for the queued workers while Execute waits for them, and everything stops.
//
// This type is declared in the global namespace, so plugin code that writes "ThreadPool" (with "using
// System.Threading;") gets it instead of System.Threading.ThreadPool - the same technique as ManagedCrypto.cs.
// Queuing work is passed on unchanged; changing the pool size is ignored.
using System;
using System.Threading;

public static class ThreadPool
{
    public static bool QueueUserWorkItem(WaitCallback callBack) =>
        System.Threading.ThreadPool.QueueUserWorkItem(callBack);

    public static bool QueueUserWorkItem(WaitCallback callBack, object state) =>
        System.Threading.ThreadPool.QueueUserWorkItem(callBack, state);

    public static bool QueueUserWorkItem<TState>(Action<TState> callBack, TState state, bool preferLocal) =>
        System.Threading.ThreadPool.QueueUserWorkItem(callBack, state, preferLocal);

    public static bool UnsafeQueueUserWorkItem(WaitCallback callBack, object state) =>
        System.Threading.ThreadPool.UnsafeQueueUserWorkItem(callBack, state);

    /// <summary>Ignored: the pool belongs to the whole app (see above). Reports success like the original.</summary>
    public static bool SetMaxThreads(int workerThreads, int completionPortThreads) => true;

    /// <summary>Ignored, see <see cref="SetMaxThreads"/>.</summary>
    public static bool SetMinThreads(int workerThreads, int completionPortThreads) => true;

    public static void GetMaxThreads(out int workerThreads, out int completionPortThreads) =>
        System.Threading.ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);

    public static void GetMinThreads(out int workerThreads, out int completionPortThreads) =>
        System.Threading.ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);

    public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads) =>
        System.Threading.ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
}

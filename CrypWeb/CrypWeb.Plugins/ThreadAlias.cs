// The browser worker has no threads. Plugin code that writes "Thread" gets the cooperative stand-in that
// runs the thread body inside Start() (see CrypPluginBase.Portable/Shims/CooperativeThread.cs).
// Also compiled into every isolated plugin assembly (tools-gen-plugins.py, ISO_CSPROJ).
global using Thread = CrypWeb.Compat.CooperativeThread;

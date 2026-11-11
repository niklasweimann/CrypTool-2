// Page side of the engine, which runs in the app itself (CrypWeb.App/Engine/EngineHost.cs): plugin code on
// background threads of the multi-threaded .NET runtime, presentations on the UI thread.
window.ct2engine = {
  started: false,

  /** Called once the app is running: mounts the plugins' data files and starts the presentation clock. */
  async start() {
    if (this.started) return;
    this.started = true;
    try { await window.ctVfs.mount(globalThis.getDotnetRuntime(0)); }
    catch (error) { console.warn('Virtual file system not available:', error); }
    // animations and timers of the presentations advance on this tick
    setInterval(() => { DotNet.invokeMethodAsync('CrypWeb.App', 'Tick').catch(() => { }); }, 50);
  },

  /** Input on a drawn presentation (wpf.js): element number, kind (click/text/range/select/toggle/size/drag) and value. */
  presEvent(id, el, kind, value, x, y) {
    DotNet.invokeMethodAsync('CrypWeb.App', 'PresEvent', id, el, kind, String(value ?? ''), x || 0, y || 0).catch(() => { });
  },
};

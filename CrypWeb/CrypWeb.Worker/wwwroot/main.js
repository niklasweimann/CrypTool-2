// Web Worker entry: boots the .NET runtime and bridges messages to the engine.
import { dotnet } from './_framework/dotnet.js';

const runtime = await dotnet.withDiagnosticTracing(false).withConfig({ loadAllSatelliteResources: true }).create();
const { setModuleImports, getAssemblyExports, getConfig } = runtime;
setModuleImports('worker', { post: (json) => self.postMessage(json) });
// Virtual file system: register the server's /vfs/ directory as lazy files (downloaded on first read, synchronously, which is fine inside a worker).
try {
  const M = runtime.Module;
  const base = new URL('../vfs/', import.meta.url);
  const manifest = await (await fetch(new URL('manifest.json', base))).json();
  const created = new Set();
  for (const e of manifest) {
    const i = e.p.lastIndexOf('/'); const dir = e.p.substring(0, i) || '/'; const name = e.p.substring(i + 1);
    if (!created.has(dir)) { M.FS_createPath('/', dir.substring(1), true, true); created.add(dir); }
    M.FS_createLazyFile(dir, name, new URL(e.p.substring(1), base).href, true, false);
  }
  self.postMessage(JSON.stringify({ type: 'log', id: 0, level: 'Debug', message: 'Virtual file system: ' + manifest.length + ' files mapped to /vfs/' }));
} catch (err) { self.postMessage(JSON.stringify({ type: 'log', id: 0, level: 'Warning', message: 'Virtual file system not available: ' + err })); }
const config = getConfig();
await runtime.runMain();
const api = (await getAssemblyExports(config.mainAssemblyName)).WorkerApi;
self.postMessage('{"type":"ready"}');
// Presentations: animations and timers of the WPF runtime advance on this tick.
setInterval(() => { try { api.Tick(); } catch (err) { } }, 50);
self.onmessage = (e) => {
  const m = e.data;
  try {
    if (m.cmd === 'start') api.Start(m.json, m.doc || '', m.lang || 'en', m.guiMs || 80, m.sleepMs || 1);
    else if (m.cmd === 'open') api.Open(m.json, m.doc || '', m.lang || 'en');
    else if (m.cmd === 'stop') api.Stop();
    else if (m.cmd === 'text') api.SetText(m.id, m.text);
    else if (m.cmd === 'pres') api.PresEvent(m.id, m.el, m.kind, m.value ?? '', m.x || 0, m.y || 0);
    else if (m.cmd === 'setting') api.SetSetting(m.id, m.name, m.value);
    else if (m.cmd === 'schema') self.postMessage(JSON.stringify({ type: 'schema', req: m.req, json: api.Schema(m.type, m.values) }));
  } catch (err) { self.postMessage(JSON.stringify({ type: 'log', id: 0, level: 'Error', message: String(err) })); }
};

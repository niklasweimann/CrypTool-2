// Main-thread side of the engine worker. Stop = terminate the worker (works even if a plugin never returns).
window.ct2engine = {
  worker: null, ref: null, ready: null,
  create(ref) { this.ref = ref; },
  _spawn() {
    const w = new Worker('worker/main.js', { type: 'module' });
    this.ready = new Promise(res => { w.onmessage = e => {
      if (e.data === '{"type":"ready"}' || (typeof e.data === 'string' && e.data.includes('"type":"ready"'))) { res(); return; }
      this.ref.invokeMethodAsync('OnWorkerMessage', e.data);
    }; });
    w.onerror = e => this.ref.invokeMethodAsync('OnWorkerMessage', JSON.stringify({ type: 'log', id: 0, level: 'Error', message: 'Worker error: ' + (e.message || e) }));
    this.worker = w;
  },
  prewarm() { if (!this.worker && this.ref) this._spawn(); },
  async start(json) {
    if (!this.worker) this._spawn();
    await this.ready;
    this.worker.postMessage({ cmd: 'start', json, lang: window.ct2lang || 'en' });
  },
  text(id, text) { if (this.worker) this.worker.postMessage({ cmd: 'text', id, text }); },
  // Settings forms are computed by a second, long-lived worker so they work while the run worker is busy.
  info: null, infoReady: null, reqId: 0, pending: {},
  _spawnInfo() {
    const w = new Worker('worker/main.js', { type: 'module' });
    this.infoReady = new Promise(res => { w.onmessage = e => {
      const d = typeof e.data === 'string' ? e.data : '';
      if (d.includes('"type":"ready"')) { res(); return; }
      if (d.includes('"type":"schema"')) { const m = JSON.parse(d); const r = this.pending[m.req]; delete this.pending[m.req]; if (r) r(m.json); }
    }; });
    this.info = w;
  },
  async schema(type, valuesJson) {
    if (!this.info) this._spawnInfo();
    await this.infoReady;
    return new Promise(res => { const req = ++this.reqId; this.pending[req] = res; this.info.postMessage({ cmd: 'schema', req, type, values: valuesJson }); });
  },
  setting(id, name, value) { if (this.worker) this.worker.postMessage({ cmd: 'setting', id, name, value }); },
  // Hard stop: kill the runtime, a fresh one is booted on the next start.
  stop() { if (this.worker) { this.worker.terminate(); this.worker = null; this.ready = null; } }
};

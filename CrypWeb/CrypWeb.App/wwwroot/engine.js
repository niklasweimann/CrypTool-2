// Main-thread side of the engine worker. Stop = terminate the worker (works even if a plugin never returns).
window.ct2engine = {
  worker: null, ref: null, ready: null,
  create(ref) { this.ref = ref; },
  _spawn() {
    const w = new Worker('worker/main.js', { type: 'module' });
    this.ready = new Promise(res => { w.onmessage = e => {
      if (e.data === '{"type":"ready"}' || (typeof e.data === 'string' && e.data.includes('"type":"ready"'))) { res(); return; }
      if (typeof e.data === 'string' && e.data.includes('"type":"stopped"')) this._stopped?.();
      this.ref.invokeMethodAsync('OnWorkerMessage', e.data);
    }; });
    w.onerror = e => this.ref.invokeMethodAsync('OnWorkerMessage', JSON.stringify({ type: 'log', id: 0, level: 'Error', message: 'Worker error: ' + (e.message || e) }));
    this.worker = w;
  },
  prewarm() { if (!this.worker && this.ref) this._spawn(); },
  // Creates the components of a workspace without running it, so presentations show (and work) before Play.
  async open(json, doc) {
    if (!this.worker) this._spawn();
    await this.ready;
    this.worker.postMessage({ cmd: 'open', json, doc, lang: window.ct2lang || 'en' });
  },
  async start(json, guiMs, sleepMs, doc) {
    if (!this.worker) this._spawn();
    await this.ready;
    this.worker.postMessage({ cmd: 'start', json, doc, lang: window.ct2lang || 'en', guiMs, sleepMs });
  },
  // input on a drawn presentation (wpf.js): element number, kind (click/text/range/select/toggle) and value
  presEvent(id, el, kind, value, x, y) { if (this.worker) this.worker.postMessage({ cmd: 'pres', id, el, kind, value, x, y }); },
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
  // Stop: ask the engine to stop, so components and presentations stay alive (as in CT2). A plugin that never
  // returns control keeps the worker busy; then the runtime is killed and a fresh one is booted on the next start.
  // Resolves with true when the worker had to be killed.
  stop() {
    const w = this.worker;
    if (!w) return Promise.resolve(false);
    return new Promise(res => {
      const kill = setTimeout(() => {
        this._stopped = null;
        if (this.worker === w) { w.terminate(); this.worker = null; this.ready = null; }
        res(true);
      }, 1500);
      this._stopped = () => { clearTimeout(kill); this._stopped = null; res(false); };
      w.postMessage({ cmd: 'stop' });
    });
  }
};

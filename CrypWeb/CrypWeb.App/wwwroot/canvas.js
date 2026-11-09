window.ct2 = {
  store: {
    get(k) { try { return localStorage.getItem(k); } catch { return null; } },
    set(k, v) { try { localStorage.setItem(k, v); return true; } catch { return false; } },
    remove(k) { try { localStorage.removeItem(k); } catch { } }
  },
  rect(sel) { const r = document.querySelector(sel).getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; },
  init(ref, sel) {
    const notify = () => ref.invokeMethodAsync('OnRect', ...ct2.rect(sel));
    window.addEventListener('resize', notify); setTimeout(notify, 0);
    document.addEventListener('keydown', e => {
      const t = e.target; if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA')) return;
      const mod = e.ctrlKey || e.metaKey, k = e.key.toLowerCase();
      const handled = k === 'delete' || k === 'backspace' || k === 'escape' || k.startsWith('arrow') || (mod && 'zyxcvaswhr'.includes(k) && k.length === 1);
      if (!handled) return;
      e.preventDefault();
      ref.invokeMethodAsync('OnKey', e.key, mod, e.shiftKey);
    });
    const sp = d => e => { if (e.code === 'Space' && !(e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) { if (d) e.preventDefault(); ref.invokeMethodAsync('OnSpace', d); } };
    document.addEventListener('keydown', e => { if (!e.repeat) sp(true)(e); }); document.addEventListener('keyup', sp(false));
    document.addEventListener('contextmenu', e => { if (e.target.closest && e.target.closest('main')) e.preventDefault(); });
  },
  downloadBytes(name, b64) {
    const bin = atob(b64); const a = new Uint8Array(bin.length); for (let i = 0; i < bin.length; i++) a[i] = bin.charCodeAt(i);
    const l = document.createElement('a'); l.href = URL.createObjectURL(new Blob([a], { type: 'application/octet-stream' })); l.download = name; l.click();
  },
  download(name, text) {
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([text], { type: 'application/json' })); a.download = name; a.click();
  }
};

window.ct2 = window.ct2 || {};
window.ct2.imageSize = (uri) => new Promise(res => { const i = new Image(); i.onload = () => res([i.naturalWidth, i.naturalHeight]); i.onerror = () => res([200, 150]); i.src = uri; });

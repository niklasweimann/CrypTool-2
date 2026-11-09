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

// Renders the workspace (content bounds given in world coordinates) to a PNG and downloads it.
window.ct2.exportPng = async (sel, name, x, y, w, h) => {
  const svg = document.querySelector(sel); if (!svg) return false;
  const root = svg.firstElementChild; const clone = svg.cloneNode(true);
  const src = [svg, ...svg.querySelectorAll('*')], dst = [clone, ...clone.querySelectorAll('*')];
  const props = ['fill', 'stroke', 'stroke-width', 'stroke-dasharray', 'stroke-linejoin', 'font-size', 'font-weight', 'font-family', 'text-anchor', 'opacity', 'paint-order', 'fill-opacity', 'background-color', 'color', 'border', 'white-space', 'overflow', 'display'];
  src.forEach((s, i) => { const cs = getComputedStyle(s), st = dst[i].style; for (const p of props) st.setProperty(p, cs.getPropertyValue(p)); });
  clone.querySelectorAll('.bub, .ring').forEach(e => e.remove());
  clone.querySelectorAll('textarea').forEach(t => { const d = document.createElement('div'); d.style.cssText = 'white-space:pre-wrap;font:12px monospace;padding:3px;background:#fff;border:1px solid #bbb;height:100%;box-sizing:border-box;'; d.textContent = t.value || t.textContent; t.replaceWith(d); });
  clone.firstElementChild.setAttribute('transform', 'translate(' + (-x) + ' ' + (-y) + ')');
  const W = Math.ceil(w + 40), H = Math.ceil(h + 40);
  clone.setAttribute('width', W); clone.setAttribute('height', H); clone.setAttribute('viewBox', '0 0 ' + W + ' ' + H); clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  clone.style.background = 'linear-gradient(180deg,#c9ddf8,#a9c8f2)';
  const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect'); bg.setAttribute('width', W); bg.setAttribute('height', H); bg.setAttribute('fill', '#b9d2f4'); clone.insertBefore(bg, clone.firstChild);
  const xml = new XMLSerializer().serializeToString(clone);
  const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(xml);
  await new Promise((res, rej) => { img.onload = res; img.onerror = rej; });
  const c = document.createElement('canvas'); c.width = W * 2; c.height = H * 2; const g = c.getContext('2d'); g.scale(2, 2); g.drawImage(img, 0, 0);
  const blob = await new Promise(r => c.toBlob(r, 'image/png'));
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = name; a.click(); return true;
};

window.ct2.clampCtx = () => { const m = document.querySelector('.ctx'); if (!m) return; const r = m.getBoundingClientRect(); const dx = r.right - innerWidth + 4, dy = r.bottom - innerHeight + 4; if (dx > 0) m.style.left = (r.left - dx) + 'px'; if (dy > 0) m.style.top = Math.max(0, r.top - dy) + 'px'; };

// Scales the content of generic XAML visualizations (Viewbox / design-size roots) to the space of their host.
window.ctXamlFit = function (host) {
  if (!host) return;
  const fit = () => {
    const availW = host.clientWidth || host.parentElement?.clientWidth || 600;
    const nv = host.closest('.nview'); const availH = nv ? nv.clientHeight - 8 : Math.max(200, window.innerHeight * 0.65);
    host.querySelectorAll('.vb').forEach(vb => {
      const inner = vb.firstElementChild; if (!inner) return;
      let w = parseFloat(vb.dataset.w), h = parseFloat(vb.dataset.h);
      inner.style.transform = 'none'; inner.style.width = w > 0 ? w + 'px' : ''; inner.style.height = h > 0 ? h + 'px' : '';
      if (!(w > 0)) w = inner.scrollWidth;
      inner.style.width = w + 'px';
      if (!(h > 0)) h = inner.scrollHeight;
      if (!(w > 0 && h > 0)) return;
      inner.style.height = h + 'px';
      const pw = vb.parentElement ? Math.min(availW, vb.parentElement.clientWidth || availW) : availW;
      const s = Math.min(pw / w, availH / h);
      inner.style.transformOrigin = '0 0'; inner.style.transform = `scale(${s})`;
      vb.style.width = (w * s) + 'px'; vb.style.height = (h * s) + 'px';
    });
    const xr = host.querySelector(':scope > .xr');
    if (xr && xr.style.width) {
      const w = parseFloat(xr.style.width), h = parseFloat(xr.style.height);
      xr.style.transform = 'none'; const s = Math.min(1e6, availW / w, availH / h);
      xr.style.transformOrigin = '0 0'; xr.style.transform = `scale(${s})`;
      host.style.height = (h * s) + 'px';
    }
  };
  fit();
  if (!host._ro) { host._ro = new ResizeObserver(() => { if (!host._busy) { host._busy = true; requestAnimationFrame(() => { fit(); host._busy = false; }); } }); host._ro.observe(host.closest('.nview') || host.parentElement || host); }
};

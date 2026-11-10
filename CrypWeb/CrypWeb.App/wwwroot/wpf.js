// Browser side of the WPF runtime (CrypWeb.Wpf): shows the HTML a presentation renders in the engine worker,
// finishes the layout that needs measuring (view boxes, shape sizes) and reports input back to the worker.
window.ctWpf = (() => {
  // ---- DOM update -------------------------------------------------------------------------------------------
  // The worker sends the complete HTML on every change. Replacing the DOM would lose focus, caret and scroll
  // positions, so the new tree is merged into the existing one.

  function syncAttributes(current, next) {
    for (const attribute of [...current.attributes]) {
      if (!next.hasAttribute(attribute.name) && !attribute.name.startsWith('data-ct-')) current.removeAttribute(attribute.name);
    }
    for (const attribute of next.attributes) {
      if (attribute.name === 'style' && current.dataset.ctLayout) continue;   // re-applied by layout()
      if (current.getAttribute(attribute.name) !== attribute.value) current.setAttribute(attribute.name, attribute.value);
    }
    if (current.dataset.ctLayout) {
      current.dataset.ctStyle = next.getAttribute('style') || '';
    }
  }

  function syncFormValue(current, next) {
    const focused = document.activeElement === current;
    if (current.tagName === 'INPUT' && (current.type === 'checkbox' || current.type === 'radio')) {
      current.checked = next.hasAttribute('checked');
    } else if (current.tagName === 'INPUT' || current.tagName === 'SELECT') {
      if (!focused) current.value = next.tagName === 'SELECT' ? (next.querySelector('option[selected]')?.value ?? '') : (next.getAttribute('value') ?? '');
    } else if (current.tagName === 'TEXTAREA') {
      if (!focused) current.value = next.textContent;
    }
  }

  function merge(current, next) {
    syncAttributes(current, next);
    if (current.tagName === 'TEXTAREA') { syncFormValue(current, next); return; }

    const currentChildren = [...current.childNodes];
    const nextChildren = [...next.childNodes];
    for (let i = 0; i < nextChildren.length; i++) {
      const from = currentChildren[i];
      const to = nextChildren[i];
      if (!from) {
        current.appendChild(to);
      } else if (from.nodeType !== to.nodeType || from.nodeName !== to.nodeName) {
        current.replaceChild(to, from);
      } else if (from.nodeType === Node.TEXT_NODE) {
        if (from.nodeValue !== to.nodeValue) from.nodeValue = to.nodeValue;
      } else {
        merge(from, to);
      }
    }
    for (let i = nextChildren.length; i < currentChildren.length; i++) currentChildren[i].remove();
    syncFormValue(current, next);
  }

  // ---- layout that needs measuring ----------------------------------------------------------------------------

  // Viewbox: scale the child (at its natural size) to the box the view box got from its parent.
  function fitViewbox(box) {
    const inner = box.firstElementChild;
    if (!inner) return;
    inner.style.transform = 'none';
    const width = parseFloat(box.dataset.w) || inner.offsetWidth;
    const height = parseFloat(box.dataset.h) || inner.offsetHeight;
    if (!(width > 0 && height > 0)) return;

    // an unconstrained side (no size from the parent) follows from the other one
    box.style.width = box.dataset.ctStyleWidth || '';
    box.style.height = box.dataset.ctStyleHeight || '';
    let availableWidth = box.clientWidth, availableHeight = box.clientHeight;
    const stretch = box.dataset.stretch || 'Uniform';
    let scaleX = availableWidth > 0 ? availableWidth / width : 0;
    let scaleY = availableHeight > 0 ? availableHeight / height : 0;
    if (stretch === 'None') { scaleX = scaleY = 1; }
    else if (!scaleX && !scaleY) { scaleX = scaleY = 1; }
    else if (!scaleX) scaleX = scaleY;
    else if (!scaleY) scaleY = scaleX;
    else if (stretch === 'Uniform') scaleX = scaleY = Math.min(scaleX, scaleY);
    else if (stretch === 'UniformToFill') scaleX = scaleY = Math.max(scaleX, scaleY);

    if (!availableWidth) { box.style.width = (width * scaleX) + 'px'; availableWidth = width * scaleX; }
    if (!availableHeight) { box.style.height = (height * scaleY) + 'px'; availableHeight = height * scaleY; }
    const left = (availableWidth - width * scaleX) / 2, top = (availableHeight - height * scaleY) / 2;
    inner.style.transform = `translate(${left}px,${top}px) scale(${scaleX},${scaleY})`;
  }

  // Shapes are SVG; their size is the extent of the figure (or, with Stretch, the figure is scaled to the box).
  function sizeShape(svg) {
    const figure = svg.querySelector('path');
    if (!figure) return;
    let box;
    try { box = figure.getBBox(); } catch { return; }
    const stroke = parseFloat(figure.getAttribute('stroke-width')) || 0;
    const stretch = svg.dataset.shape || 'None';
    const style = svg.dataset.ctStyle ?? svg.getAttribute('style') ?? '';
    svg.dataset.ctLayout = '1';
    svg.dataset.ctStyle = style;
    const explicitWidth = /(^|;)width:/.test(style), explicitHeight = /(^|;)height:/.test(style);

    let extra = '';
    if (stretch === 'None') {
      svg.removeAttribute('viewBox');
      if (!explicitWidth) extra += `width:${Math.max(0, box.x + box.width + stroke / 2)}px;`;
      if (!explicitHeight) extra += `height:${Math.max(0, box.y + box.height + stroke / 2)}px;`;
    } else if (box.width > 0 || box.height > 0) {
      svg.setAttribute('viewBox', `${box.x - stroke / 2} ${box.y - stroke / 2} ${Math.max(box.width + stroke, 0.01)} ${Math.max(box.height + stroke, 0.01)}`);
      svg.setAttribute('preserveAspectRatio', stretch === 'Fill' ? 'none' : stretch === 'UniformToFill' ? 'xMidYMid slice' : 'xMidYMid meet');
      if (!explicitWidth) extra += 'width:100%;';
      if (!explicitHeight) extra += 'height:100%;';
    }
    svg.setAttribute('style', style + extra);
  }

  // A presentation with a fixed design size (Width and Height on its root) is scaled to the box it is shown in.
  function fitRoot(host) {
    const frame = host.firstElementChild, root = frame && frame.firstElementChild;
    if (!root) return;
    const width = parseFloat(root.style.width), height = parseFloat(root.style.height);
    frame.style.cssText = '';
    if (!(width > 0 && height > 0) || !host.clientWidth || !host.clientHeight) return;
    const scale = Math.min(host.clientWidth / width, host.clientHeight / height);
    const left = (host.clientWidth - width * scale) / 2, top = (host.clientHeight - height * scale) / 2;
    frame.style.cssText = `width:${width}px;height:${height}px;transform-origin:0 0;transform:translate(${left}px,${top}px) scale(${scale});`;
    host.style.overflow = 'hidden';
  }

  function layout(host) {
    fitRoot(host);
    host.querySelectorAll('svg.wsh').forEach(sizeShape);
    // inner view boxes first: their size is input for the outer ones
    [...host.querySelectorAll('.wvb')].reverse().forEach(box => {
      if (box.dataset.ctStyleWidth === undefined) {
        box.dataset.ctStyleWidth = box.style.width; box.dataset.ctStyleHeight = box.style.height;
      }
      fitViewbox(box);
    });
  }

  // ---- input ------------------------------------------------------------------------------------------------

  function send(host, element, kind, value, event) {
    const id = parseInt(host.dataset.node), target = parseInt(element.dataset.e);
    if (!window.ct2engine || !(id > 0) || !(target > 0)) return;
    let x = 0, y = 0;
    if (event && event.clientX !== undefined) {
      // position in the element's own (unscaled) coordinates
      const rect = element.getBoundingClientRect();
      const scaleX = element.offsetWidth ? rect.width / element.offsetWidth : 1, scaleY = element.offsetHeight ? rect.height / element.offsetHeight : 1;
      x = (event.clientX - rect.left) / (scaleX || 1); y = (event.clientY - rect.top) / (scaleY || 1);
    }
    window.ct2engine.presEvent(id, target, kind, String(value ?? ''), x, y);
  }

  function attach(host) {
    if (host.dataset.ctAttached) return;
    host.dataset.ctAttached = '1';

    host.addEventListener('click', event => {
      const row = event.target.closest('[data-i]');
      const list = row && row.closest('.sel[data-e]');
      if (list && host.contains(list)) send(host, list, 'select', row.dataset.i, event);

      const element = event.target.closest('[data-e]');
      if (!element || !host.contains(element) || element.dataset.k === 'text' || element.dataset.k === 'range' || element.dataset.k === 'select') return;
      // the worker decides the new state of check boxes and radio buttons and sends it back
      if (event.target.matches('input[type=checkbox],input[type=radio]')) event.preventDefault();
      send(host, element, element.dataset.k === 'toggle' ? 'toggle' : 'click', event.detail || 1, event);
    });

    host.addEventListener('input', event => {
      const element = event.target.closest('[data-e]');
      if (!element || !host.contains(element)) return;
      if (element.dataset.k === 'text' || element.dataset.k === 'range') send(host, element, element.dataset.k, element.value);
    });
    host.addEventListener('change', event => {
      const element = event.target.closest('[data-e]');
      if (element && host.contains(element) && element.dataset.k === 'select') send(host, element, 'select', element.value);
    });

    new ResizeObserver(() => {
      if (host.dataset.ctBusy) return;
      host.dataset.ctBusy = '1';
      requestAnimationFrame(() => { layout(host); delete host.dataset.ctBusy; });
    }).observe(host);
  }

  return {
    /** Shows `html` (one rendered presentation) in `host` and wires up input for workspace component `nodeId`. */
    update(host, html, nodeId) {
      if (!host) return;
      host.dataset.node = nodeId;
      attach(host);
      const template = document.createElement('template');
      template.innerHTML = html;
      const next = template.content.firstElementChild;
      if (!next) { host.replaceChildren(); return; }
      if (host.firstElementChild && host.firstElementChild.tagName === next.tagName) {
        next.setAttribute('style', host.firstElementChild.getAttribute('style') || '');   // set by fitRoot
        merge(host.firstElementChild, next);
      }
      else host.replaceChildren(next);
      layout(host);
    },
  };
})();

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
    const figure = svg.querySelector(':scope > path, :scope > text');
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

  // Tells the runtime the measured size of the elements that asked for it (ActualWidth/ActualHeight, SizeChanged).
  // Children are reported before their parents, so a parent's handler already sees the sizes of its children.
  function reportSizes(host) {
    const id = parseInt(host.dataset.node);
    if (!window.ct2engine || !(id > 0)) return;
    // a new size generation (the worker asks for all sizes again): forget what was reported
    const generation = host.firstElementChild?.dataset.gen ?? '';
    if (host.dataset.ctGen !== generation) {
      host.dataset.ctGen = generation;
      host.querySelectorAll('[data-ct-size]').forEach(element => delete element.dataset.ctSize);
    }
    for (const element of [...host.querySelectorAll('[data-s]')].reverse()) {
      const size = element.offsetWidth + ',' + element.offsetHeight;
      if (element.dataset.ctSize === size) continue;
      element.dataset.ctSize = size;
      window.ct2engine.presEvent(id, parseInt(element.dataset.s), 'size', size, 0, 0);
    }
  }

  // ---- 3D scenes (Viewport3D) --------------------------------------------------------------------------------

  // pictures used as textures, loaded once per address; a picture that arrives later redraws its scenes
  const textures = new Map();
  function texture(url, canvas) {
    let entry = textures.get(url);
    if (!entry) {
      entry = { image: new Image(), waiting: new Set() };
      entry.image.onload = () => { entry.waiting.forEach(c => { c._ctScene = null; draw3d(c); }); entry.waiting.clear(); };
      entry.image.src = url;
      textures.set(url, entry);
    }
    if (!entry.image.complete) entry.waiting.add(canvas);
    return entry.image;
  }

  function polygonPath(ctx, face, from) {
    ctx.beginPath();
    ctx.moveTo(face[from], face[from + 1]);
    for (let i = from + 2; i + 1 < face.length; i += 2) ctx.lineTo(face[i], face[i + 1]);
    ctx.closePath();
  }

  /**
   * Draws the faces of a projected 3D scene (see HtmlRenderer.Viewport3D.cs for the format), back to front.
   * Polygons get a hairline stroke in their own colour, which closes the seams between neighbouring faces.
   */
  function draw3d(canvas) {
    const text = canvas.dataset.scene;
    if (!text || canvas._ctScene === text) return;
    canvas._ctScene = text;
    const scene = JSON.parse(text);
    const ctx = canvas.getContext('2d');
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.lineJoin = 'round';
    ctx.lineWidth = 0.5;
    for (const face of scene.f) {
      if (face[0] === 0) {
        polygonPath(ctx, face, 2);
        ctx.fillStyle = ctx.strokeStyle = face[1];
        ctx.fill();
        ctx.stroke();
        continue;
      }
      const isText = face[0] === 2;
      const m = isText ? 7 : 3;            // index of the texture matrix
      ctx.save();
      polygonPath(ctx, face, m + 6);
      ctx.clip();
      ctx.globalAlpha = face[m - 1];
      ctx.transform(face[m], face[m + 1], face[m + 2], face[m + 3], face[m + 4], face[m + 5]);
      if (!isText) {
        const image = texture(scene.img[face[1]], canvas);
        if (image.complete && image.naturalWidth) ctx.drawImage(image, 0, 0, 1, 1);
      } else {
        // the text box of its natural size, stretched over the texture square (as a WPF VisualBrush does)
        const size = face[2];
        const boxWidth = Math.max(1, face[1].length) * size * 0.6, boxHeight = size * 1.33;
        ctx.scale(1 / boxWidth, 1 / boxHeight);
        if (face[5]) { ctx.fillStyle = face[5]; ctx.fillRect(0, 0, boxWidth, boxHeight); }
        ctx.font = (face[3] ? 'bold ' : '') + size + 'px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = face[4];
        ctx.fillText(face[1], boxWidth / 2, boxHeight / 2);
      }
      ctx.restore();
    }
  }

  function layout(host) {
    host.querySelectorAll('.w3d canvas').forEach(draw3d);
    fitRoot(host);
    host.querySelectorAll('svg.wsh').forEach(sizeShape);
    // inner view boxes first: their size is input for the outer ones
    [...host.querySelectorAll('.wvb')].reverse().forEach(box => {
      if (box.dataset.ctStyleWidth === undefined) {
        box.dataset.ctStyleWidth = box.style.width; box.dataset.ctStyleHeight = box.style.height;
      }
      fitViewbox(box);
    });
    reportSizes(host);
  }

  // ---- input ------------------------------------------------------------------------------------------------

  /**
   * How much the element is scaled on screen (view boxes, transforms of ancestors). A WPF Canvas often has no size
   * of its own (its children overflow it), so the scale is measured on the nearest element that has a size.
   */
  function screenScale(element) {
    let measured = element;
    while (measured && !(measured.offsetWidth && measured.offsetHeight)) measured = measured.parentElement;
    if (!measured) return { x: 1, y: 1 };
    const rect = measured.getBoundingClientRect();
    return { x: rect.width / measured.offsetWidth || 1, y: rect.height / measured.offsetHeight || 1 };
  }

  /** A point on the screen in the element's own (unscaled) coordinates; `rect` defaults to the current box. */
  function localPoint(element, clientX, clientY, rect = element.getBoundingClientRect(), scale = screenScale(element)) {
    return { x: (clientX - rect.left) / scale.x, y: (clientY - rect.top) / scale.y };
  }

  function send(host, element, kind, value, event) {
    const id = parseInt(host.dataset.node), target = parseInt(element.dataset.e);
    if (!window.ct2engine || !(id > 0) || !(target > 0)) return;
    let x = 0, y = 0;
    if (event && event.clientX !== undefined) ({ x, y } = localPoint(element, event.clientX, event.clientY));
    window.ct2engine.presEvent(id, target, kind, String(value ?? ''), x, y);
  }

  // ---- drag and drop ------------------------------------------------------------------------------------------
  // WPF's DoDragDrop blocks until the mouse is released; the worker cannot. So the browser performs the whole
  // gesture (the element follows the pointer) and reports it once at the end: source, drop target and positions.
  // The worker replays it so that DoDragDrop delivers the drop (CrypWeb.Wpf/Input/DragDrop.cs).

  function attachDrag(host) {
    let drag = null;

    host.addEventListener('pointerdown', event => {
      if (event.button !== 0) return;
      const source = event.target.closest('[data-d]');
      if (!source || !host.contains(source)) return;
      const rect = source.getBoundingClientRect(), scale = screenScale(source);
      drag = {
        source, rect, scale, clientX: event.clientX, clientY: event.clientY, moved: false,
        start: localPoint(source, event.clientX, event.clientY, rect, scale),
      };
    });

    window.addEventListener('pointermove', event => {
      if (!drag) return;
      const dx = event.clientX - drag.clientX, dy = event.clientY - drag.clientY;
      if (!drag.moved && Math.hypot(dx, dy) < 5) return;
      drag.moved = true;
      drag.source.classList.add('wdragging');
      // translate is in the element's own coordinates: undo the scaling of its ancestors
      drag.source.style.translate = `${dx / drag.scale.x}px ${dy / drag.scale.y}px`;
    });

    window.addEventListener('pointerup', event => {
      const finished = drag;
      drag = null;
      if (!finished || !finished.moved) return;
      finished.source.classList.remove('wdragging');
      finished.source.style.translate = '';
      host.dataset.ctSkipClick = '1';     // the click event that follows belongs to the drag

      // the element under the pointer that the worker knows (the dragged element itself does not count)
      const target = document.elementsFromPoint(event.clientX, event.clientY)
        .map(element => element.closest('[data-e]'))
        .find(element => element && host.contains(element) && !finished.source.contains(element));
      const move = localPoint(finished.source, event.clientX, event.clientY, finished.rect, finished.scale);
      const drop = target ? localPoint(target, event.clientX, event.clientY) : { x: 0, y: 0 };
      const targetId = target ? target.dataset.e : '0';
      const id = parseInt(host.dataset.node);
      if (!window.ct2engine || !(id > 0)) return;
      const value = [targetId, move.x, move.y, drop.x, drop.y].join(';');
      window.ct2engine.presEvent(id, parseInt(finished.source.dataset.e), 'drag', value, finished.start.x, finished.start.y);
    });
  }

  function attach(host) {
    if (host.dataset.ctAttached) return;
    host.dataset.ctAttached = '1';

    attachDrag(host);

    host.addEventListener('click', event => {
      if (host.dataset.ctSkipClick) { delete host.dataset.ctSkipClick; return; }
      const row = event.target.closest('[data-i]');
      const list = row && row.closest('.sel[data-e]');
      if (list && host.contains(list)) send(host, list, 'select', row.dataset.i, event);

      // a click on the text of a check box label is followed by the browser's own click on the box: count it once
      const label = event.target.closest('label');
      if (label && label.querySelector('input') && !event.target.matches('input')) return;

      const element = event.target.closest('[data-e]');
      if (!element || !host.contains(element) || element.dataset.k === 'text' || element.dataset.k === 'range' || element.dataset.k === 'select') return;
      // the worker decides the new state of check boxes and radio buttons and sends it back
      if (event.target.matches('input[type=checkbox],input[type=radio]')) event.preventDefault();
      send(host, element, element.dataset.k === 'toggle' ? 'toggle' : 'click', event.detail || 1, event);
    });

    host.addEventListener('dblclick', event => {
      const row = event.target.closest('[data-i]');
      const list = row && row.closest('.sel[data-e]');
      if (list && host.contains(list)) send(host, list, 'activate', row.dataset.i, event);
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

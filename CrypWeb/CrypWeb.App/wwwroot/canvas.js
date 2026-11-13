// Browser-side helpers of the workspace canvas (Home.razor): keyboard shortcuts, canvas size, downloads, PNG export.
// The Blazor component calls these through `ct2.*` (IJSRuntime) and receives OnRect / OnKey / OnSpace callbacks.

window.ct2 = {
    /** localStorage that never throws (private windows, blocked storage). */
    store: {
        get(key) {
            try {
                return localStorage.getItem(key);
            } catch {
                return null;
            }
        },
        set(key, value) {
            try {
                localStorage.setItem(key, value);
                return true;
            } catch {
                return false;
            }
        },
        remove(key) {
            try {
                localStorage.removeItem(key);
            } catch { /* storage unavailable: nothing to remove */
            }
        },
    },

    /** Position and size of the element in screen pixels: [left, top, width, height]. */
    rect(selector) {
        const r = document.querySelector(selector).getBoundingClientRect();
        return [r.left, r.top, r.width, r.height];
    },

    /**
     * Connects the page to the Blazor component `ref`: reports the canvas rectangle (OnRect), the shortcut keys (OnKey)
     * and the state of the space bar (OnSpace, used for "hold space to pan").
     */
    init(ref, canvasSelector) {
        const notifyRect = () => ref.invokeMethodAsync('OnRect', ...ct2.rect(canvasSelector));
        window.addEventListener('resize', notifyRect);
        setTimeout(notifyRect, 0);

        const isTextField = el => el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA');

        // Ctrl/Cmd+Z Y X C V A S W H R, Delete, Backspace, Escape and the arrow keys are handled by the component.
        document.addEventListener('keydown', e => {
            if (isTextField(e.target)) return;
            const modifier = e.ctrlKey || e.metaKey;
            const key = e.key.toLowerCase();
            const isShortcut = modifier && key.length === 1 && 'zyxcvaswhr'.includes(key);
            const handled = key === 'delete' || key === 'backspace' || key === 'escape' || key.startsWith('arrow') || isShortcut;
            if (!handled) return;
            e.preventDefault();
            ref.invokeMethodAsync('OnKey', e.key, modifier, e.shiftKey);
        });

        const spaceHandler = down => e => {
            if (e.code !== 'Space' || isTextField(e.target)) return;
            if (down) e.preventDefault(); // do not scroll the page
            ref.invokeMethodAsync('OnSpace', down);
        };
        document.addEventListener('keydown', e => {
            if (!e.repeat) spaceHandler(true)(e);
        });
        document.addEventListener('keyup', spaceHandler(false));

        // the canvas has its own context menu
        document.addEventListener('contextmenu', e => {
            if (e.target.closest && e.target.closest('main')) e.preventDefault();
        });
    },

    /** Starts a download of binary data given as base64. */
    downloadBytes(name, base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        saveBlob(new Blob([bytes], {type: 'application/octet-stream'}), name);
    },

    /** Starts a download of a text (JSON) file. */
    download(name, text) {
        saveBlob(new Blob([text], {type: 'application/json'}), name);
    },
};

function saveBlob(blob, name) {
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = name;
    link.click();
}

/** Natural size of an image given as URL/data URI: [width, height] ([200, 150] when it cannot be loaded). */
window.ct2.imageSize = uri => new Promise(resolve => {
    const image = new Image();
    image.onload = () => resolve([image.naturalWidth, image.naturalHeight]);
    image.onerror = () => resolve([200, 150]);
    image.src = uri;
});

// CSS properties that are copied into the exported SVG (the exported image has no access to the page's style sheets).
const EXPORT_STYLE_PROPERTIES = [
    'fill', 'stroke', 'stroke-width', 'stroke-dasharray', 'stroke-linejoin', 'font-size', 'font-weight', 'font-family',
    'text-anchor', 'opacity', 'paint-order', 'fill-opacity', 'background-color', 'color', 'border', 'white-space',
    'overflow', 'display',
];
const EXPORT_TEXTAREA_STYLE =
    'white-space:pre-wrap;font:12px monospace;padding:3px;background:#fff;border:1px solid #bbb;height:100%;box-sizing:border-box;';
const EXPORT_MARGIN = 40; // total extra space around the content, in workspace units
const EXPORT_SCALE = 2;   // the PNG has twice the resolution

/**
 * Renders the workspace to a PNG and downloads it. The content bounds (x, y, w, h) are given in workspace
 * coordinates; the SVG's pan/zoom transform is replaced so the content fills the image.
 * Returns false when the SVG element does not exist.
 */
window.ct2.exportPng = async (selector, fileName, x, y, w, h) => {
    const svg = document.querySelector(selector);
    if (!svg) return false;

    // Work on a copy with the computed styles inlined, without temporary overlays (bubbles, rings).
    const clone = svg.cloneNode(true);
    const originals = [svg, ...svg.querySelectorAll('*')];
    const copies = [clone, ...clone.querySelectorAll('*')];
    originals.forEach((original, i) => {
        const computed = getComputedStyle(original);
        for (const property of EXPORT_STYLE_PROPERTIES) copies[i].style.setProperty(property, computed.getPropertyValue(property));
    });
    clone.querySelectorAll('.bub, .ring').forEach(e => e.remove());
    clone.querySelectorAll('textarea').forEach(textarea => {
        const replacement = document.createElement('div');
        replacement.style.cssText = EXPORT_TEXTAREA_STYLE;
        replacement.textContent = textarea.value || textarea.textContent;
        textarea.replaceWith(replacement);
    });

    const width = Math.ceil(w + EXPORT_MARGIN);
    const height = Math.ceil(h + EXPORT_MARGIN);
    clone.firstElementChild.setAttribute('transform', 'translate(' + (-x) + ' ' + (-y) + ')');
    clone.setAttribute('width', width);
    clone.setAttribute('height', height);
    clone.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    clone.style.background = 'linear-gradient(180deg,#c9ddf8,#a9c8f2)';
    const background = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    background.setAttribute('width', width);
    background.setAttribute('height', height);
    background.setAttribute('fill', '#b9d2f4');
    clone.insertBefore(background, clone.firstChild);

    // SVG -> image -> canvas -> PNG
    const image = new Image();
    image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(clone));
    await new Promise((resolve, reject) => {
        image.onload = resolve;
        image.onerror = reject;
    });
    const canvas = document.createElement('canvas');
    canvas.width = width * EXPORT_SCALE;
    canvas.height = height * EXPORT_SCALE;
    const context = canvas.getContext('2d');
    context.scale(EXPORT_SCALE, EXPORT_SCALE);
    context.drawImage(image, 0, 0);
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    saveBlob(blob, fileName);
    return true;
};

/** Moves the context menu back into the window when it would extend past the right or bottom edge. */
window.ct2.clampCtx = () => {
    const menu = document.querySelector('.ctx');
    if (!menu) return;
    const rect = menu.getBoundingClientRect();
    const overflowX = rect.right - innerWidth + 4;
    const overflowY = rect.bottom - innerHeight + 4;
    if (overflowX > 0) menu.style.left = (rect.left - overflowX) + 'px';
    if (overflowY > 0) menu.style.top = Math.max(0, rect.top - overflowY) + 'px';
};

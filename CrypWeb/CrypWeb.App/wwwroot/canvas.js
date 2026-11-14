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
        // The canvas moves and changes size when panels open, tabs change, fonts load or the layout settles:
        // keep the rectangle used to convert pointer positions up to date (a stale one puts dropped components,
        // new components and the selection rectangle at the wrong place).
        const canvas = document.querySelector(canvasSelector);
        const keepRectCurrent = () => {
            const r = canvas.getBoundingClientRect();
            if (r.width > 0 && r.height > 0) notifyRect();
        };
        new ResizeObserver(keepRectCurrent).observe(canvas);
        new ResizeObserver(keepRectCurrent).observe(document.body);
        if (document.fonts && document.fonts.ready) document.fonts.ready.then(keepRectCurrent);
        // position changes without a size change (e.g. the tab bar grows): check when a gesture starts
        document.addEventListener('pointerdown', keepRectCurrent, true);

        const isTextField = el => el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA');

        // Ctrl/Cmd+Z Y X C V A S W H R, Delete, Backspace, Escape and the arrow keys are handled by the component.
        document.addEventListener('keydown', e => {
            if (isTextField(e.target)) return;
            // text selected in an output box (or elsewhere): let the browser copy it
            const modifier0 = e.ctrlKey || e.metaKey;
            if (modifier0 && e.key.toLowerCase() === 'c' && String(window.getSelection()).length > 0) return;
            const modifier = e.ctrlKey || e.metaKey;
            const key = e.key.toLowerCase();
            const isShortcut = modifier && key.length === 1 && 'zyxcvaswhr'.includes(key);
            const handled = key === 'f11' || key === 'f12' || key === 'delete' || key === 'backspace' || key === 'escape' || key.startsWith('arrow') || isShortcut;
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

        // Pointer moves are sent to .NET one at a time, newest position only. A trackpad delivers up to 120 moves per
        // second; if each one were queued as a Blazor event (and each re-rendered the canvas) the view would lag behind.
        const main = document.querySelector('main');
        let pendingMove = null, sending = false;
        const sendMoves = async () => {
            sending = true;
            while (pendingMove) {
                const m = pendingMove;
                pendingMove = null;
                try {
                    await ref.invokeMethodAsync('OnPointerMove', m.clientX, m.clientY, m.buttons, m.altKey);
                } catch (e) { /* component is gone */ }
            }
            sending = false;
        };
        main.addEventListener('pointermove', e => {
            pendingMove = {clientX: e.clientX, clientY: e.clientY, buttons: e.buttons, altKey: e.altKey};
            if (!sending) sendMoves();
        });

        for (const type of ['pointerup', 'pointercancel']) {
            window.addEventListener(type, () => ref.invokeMethodAsync('OnPointerRelease'));
        }

        // many document tabs: the mouse wheel (or a vertical trackpad swipe) scrolls the tab bar sideways
        document.addEventListener('wheel', e => {
            const tabs = e.target.closest && e.target.closest('.doctabs');
            if (!tabs || Math.abs(e.deltaX) >= Math.abs(e.deltaY)) return;
            tabs.scrollLeft += e.deltaY;
            e.preventDefault();
        }, {passive: false});

        // no text selection (it looks like rendering artefacts) while a drag, pan or wire gesture is running
        document.addEventListener('selectstart', e => {
            if (document.querySelector('main.tracking')) e.preventDefault();
        });
        document.addEventListener('pointerdown', e => {
            if (e.target.closest && e.target.closest('main') && !e.target.closest('.nview')) window.getSelection()?.removeAllRanges();
        });

        // the canvas has its own context menu
        document.addEventListener('contextmenu', e => {
            // the views of the components (input/output boxes) keep the native menu for copy and paste
            if (e.target.closest && e.target.closest('main') && !e.target.closest('.nview')) e.preventDefault();
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

// Document tab bar: arrows at the edges show that more tabs are hidden on that side; a click scrolls one bar width.
// State lives in data attributes of .doctabbar (Blazor does not touch them) and is read by the CSS.
(() => {
    let queued = false;
    const update = () => {
        queued = false;
        const tabs = document.querySelector('.doctabs');
        if (!tabs) return;
        const bar = tabs.parentElement;
        bar.dataset.l = String(tabs.scrollLeft > 1);
        bar.dataset.r = String(tabs.scrollLeft + tabs.clientWidth < tabs.scrollWidth - 1);
    };
    const schedule = () => {
        if (!queued) {
            queued = true;
            requestAnimationFrame(update);
        }
    };
    document.addEventListener('scroll', e => {
        if (e.target.classList && e.target.classList.contains('doctabs')) schedule();
    }, true);
    window.addEventListener('resize', schedule);
    new MutationObserver(schedule).observe(document.documentElement, {childList: true, subtree: true});
    document.addEventListener('click', e => {
        const arrow = e.target.closest && e.target.closest('.tabarrow');
        if (!arrow) return;
        const tabs = arrow.parentElement.querySelector('.doctabs');
        tabs.scrollBy({left: (arrow.classList.contains('l') ? -1 : 1) * tabs.clientWidth, behavior: 'smooth'});
    });
})();

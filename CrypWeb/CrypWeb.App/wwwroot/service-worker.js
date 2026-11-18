// Service worker of CrypTool 2 Web Edition: makes the app usable offline and installable (PWA).
//
// Strategy
// - Files whose name carries a content hash (_framework/*.<hash>.wasm|js|dat, as written by the .NET build) never
//   change: cache first.
// - Everything else (index.html, plugins.json, templates, data files of the virtual file system, ...): network
//   first, so a new deployment is picked up at once; the cached copy is used when the network is not available.
// - Every successful same-origin GET is stored, so whatever was used once (the runtime, opened templates, the
//   language statistics a plugin read) also works offline. "Make available offline" (View tab) fetches all
//   templates and data files through this worker in advance (message "precache").
//
// Data files of the plugins (vfs/) are read with synchronous requests, which do not pass through a service worker;
// vfs.js puts them into this cache itself and reads them from here when the browser is offline.
//
// Cached responses keep their headers, including Cross-Origin-Opener-Policy / -Embedder-Policy, which the
// multi-threaded .NET runtime needs (SharedArrayBuffer).

const CACHE = 'ct2web-v1';
const SHELL = ['./', 'manifest.webmanifest', 'favicon.ico', 'favicon.png', 'icon-192.png', 'icon-512.png',
    'icon-maskable-512.png', 'apple-touch-icon.png', 'logo.png', 'crashreport.js',
    'offline.js', 'offline.json', 'noteeditor.js'];
const HASHED = /\/_framework\/.+\.[a-z0-9]{8,}\.(wasm|js|dat|pdb|json)$/i;

self.addEventListener('install', event => {
    self.skipWaiting();
    event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(SHELL)).catch(() => { /* offline install: filled later */ }));
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        for (const key of await caches.keys()) {
            if (key !== CACHE && key.startsWith('ct2web-')) await caches.delete(key);
        }
        await self.clients.claim();
    })());
});

async function store(request, response) {
    if (response.ok && response.type === 'basic' && response.status === 200) {
        const cache = await caches.open(CACHE);
        await cache.put(request, response.clone());
    }
    return response;
}

async function cacheFirst(request) {
    const cached = await caches.match(request);
    return cached || store(request, await fetch(request));
}

/**
 * The cached copy. A page with another query (deep links ?t=...) is the same page: the start page is used, as it was
 * served for "/" (with the cross-origin isolation headers; a development server sends them only for the page routes).
 */
async function fromCache(request) {
    if (request.mode !== 'navigate') return caches.match(request);
    return await caches.match(request) || await caches.match('./') || caches.match(request, {ignoreSearch: true});
}

/** Waiting longer than this for the network (a server that does not answer) uses the cached copy, if there is one. */
const NETWORK_TIMEOUT_MS = 5000;

async function networkFirst(request) {
    const network = fetch(request).then(response => store(request, response));
    const timeout = new Promise(resolve => setTimeout(resolve, NETWORK_TIMEOUT_MS, 'timeout'));
    try {
        const first = await Promise.race([network, timeout]);
        if (first !== 'timeout') return first;
        const cached = await fromCache(request);
        return cached || await network;
    } catch (error) {
        // offline and never loaded: answer "not available" instead of failing the request with a network error
        return await fromCache(request) || new Response('Not available offline', {status: 504, statusText: 'Offline'});
    }
}

self.addEventListener('fetch', event => {
    const request = event.request;
    if (request.method !== 'GET') return;
    const url = new URL(request.url);
    if (url.origin !== self.location.origin) return;
    // the debugger proxy and the hot reload channel of the development server must not be cached
    if (url.pathname.includes('/_framework/debug') || url.pathname.includes('/_blazor')) return;
    event.respondWith(HASHED.test(url.pathname) ? cacheFirst(request) : networkFirst(request));
});

// {type: 'precache', urls: [...], sizes?: [...]}: stores the files that are not cached yet. Sent by the offline dialog
// (offline.js, with a port for progress messages {done, failed, total, bytes}) and by the page after every start (the
// runtime's files; on the first visit the page loads them before this worker controls it, so they would be missing
// offline). {type: 'cancel-precache', id} stops the download that was started with that id after the files in progress.
const cancelledDownloads = new Set();

self.addEventListener('message', event => {
    const data = event.data || {};
    if (data.type === 'cancel-precache') {
        cancelledDownloads.add(data.id);
        return;
    }
    if (data.type !== 'precache') return;
    const port = event.ports[0] || {postMessage() { /* nobody listens */ }};
    const cancelled = () => data.id !== undefined && cancelledDownloads.has(data.id);
    event.waitUntil((async () => {
        const cache = await caches.open(CACHE);
        const total = data.urls.length;
        let done = 0, failed = 0, bytes = 0;
        const queue = data.urls.map((url, i) => ({url, size: data.sizes ? data.sizes[i] : 0}));
        const worker = async () => {
            while (queue.length && !cancelled()) {
                const {url, size} = queue.shift();
                try {
                    if (!(await cache.match(url))) {
                        const response = await fetch(url);
                        if (!response.ok) throw new Error(String(response.status));
                        await cache.put(url, response);
                    }
                    bytes += size;
                } catch {
                    failed++;
                }
                done++;
                if (done % 10 === 0 || done === total) port.postMessage({done, failed, total, bytes});
            }
        };
        await Promise.all([worker(), worker(), worker(), worker()]);
        port.postMessage({done, failed, total, bytes, finished: true, cancelled: cancelled()});
        cancelledDownloads.delete(data.id);
    })());
});

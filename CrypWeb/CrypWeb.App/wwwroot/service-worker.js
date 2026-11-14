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
const SHELL = ['./', 'manifest.webmanifest', 'favicon.png', 'icon-192.png', 'icon-512.png', 'logo.png'];
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
        const cached = await fromCache(request);
        if (cached) return cached;
        throw error;
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

// "Make available offline": {type: 'precache', urls: [...]}; answers with progress messages on the given port.
self.addEventListener('message', event => {
    const data = event.data || {};
    if (data.type !== 'precache' || !event.ports[0]) return;
    const port = event.ports[0];
    event.waitUntil((async () => {
        const cache = await caches.open(CACHE);
        let done = 0, failed = 0;
        const queue = [...data.urls];
        const worker = async () => {
            while (queue.length) {
                const url = queue.shift();
                try {
                    if (!(await cache.match(url))) {
                        const response = await fetch(url);
                        if (!response.ok) throw new Error(String(response.status));
                        await cache.put(url, response);
                    }
                } catch {
                    failed++;
                }
                done++;
                if (done % 20 === 0 || done === data.urls.length) port.postMessage({done, failed, total: data.urls.length});
            }
        };
        await Promise.all([worker(), worker(), worker(), worker()]);
        port.postMessage({done, failed, total: data.urls.length, finished: true});
    })());
});

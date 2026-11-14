// Virtual file system for the plugins: the data files that CT2 reads from disk (language statistics, a few plugin
// files) are listed in vfs/manifest.json and appear in the .NET runtime's file system under the same paths
// (/LanguageStatistics/..., /CrypPlugins/...). A file is downloaded the first time it is opened.
//
// The runtime is multi-threaded: plugins read files on background threads, and the file system lives on the main
// thread, which serves those reads. Emscripten's own lazy files refuse to work there (they want a web worker), so
// the download is done here with a synchronous request in the old "x-user-defined" text mode, the one way a browser
// still allows synchronous binary downloads on the main thread. Each file is loaded once.
//
// Offline: synchronous requests do not pass through the service worker (service-worker.js). So every downloaded file is
// also put into the app's cache here, and when the browser is offline the cached files are read into memory in the
// background, where a file is taken from instead of the network.
window.ctVfs = (() => {
    /** Same cache as in service-worker.js. */
    const CACHE = 'ct2web-v1';

    /** Files read from the cache while offline: url -> bytes. */
    const inMemory = new Map();

    function remember(url, bytes) {
        if (!('caches' in window)) return;
        caches.open(CACHE).then(cache => cache.put(url, new Response(bytes))).catch(() => { /* storage full or unavailable */ });
    }

    /** URLs of all files of the manifest. */
    let allUrls = [];

    /** Reads every cached file of the manifest into memory (offline only; the files are needed synchronously). */
    let warming = false;
    async function warmFromCache(urls) {
        if (warming || !('caches' in window)) return;
        warming = true;
        const cache = await caches.open(CACHE);
        for (const url of urls) {
            if (inMemory.has(url)) continue;
            const response = await cache.match(url);
            if (response) inMemory.set(url, new Uint8Array(await response.arrayBuffer()));
        }
        warming = false;
    }

    /** Downloads `url` synchronously and returns its bytes. */
    function downloadSync(url) {
        const request = new XMLHttpRequest();
        request.open('GET', url, false);
        request.overrideMimeType('text/plain; charset=x-user-defined');
        request.send(null);
        if (request.status !== 200 && request.status !== 0) throw new Error('Could not load ' + url + ': ' + request.status);
        const text = request.responseText;
        const bytes = new Uint8Array(text.length);
        for (let i = 0; i < text.length; i++) bytes[i] = text.charCodeAt(i) & 0xff;
        return bytes;
    }

    /** Creates the file node at `path`; its content is downloaded from `url` on first use. */
    function createLazyFile(FS, path, url, size) {
        const slash = path.lastIndexOf('/');
        const folder = path.substring(0, slash) || '/';
        FS.mkdirTree(folder);
        const node = FS.createFile(folder, path.substring(slash + 1), {}, true, false);
        let loaded = false;

        function ensureLoaded() {
            if (loaded) return;
            let bytes = inMemory.get(url);
            if (bytes) inMemory.delete(url);
            else {
                try {
                    bytes = downloadSync(url);
                } catch (e) {
                    // no connection to the server: read the cached files into memory for the next attempt
                    warmFromCache(allUrls);
                    throw new Error('Offline: ' + path + ' could not be loaded (' + e.message + '). If it was made available'
                        + ' offline (View tab), run the workspace again in a moment.');
                }
                remember(url, bytes);
            }
            // the browser drops a UTF-8 byte order mark when it decodes the text; put it back
            if (bytes.length === size - 3) {
                const withMark = new Uint8Array(size);
                withMark.set([0xef, 0xbb, 0xbf]);
                withMark.set(bytes, 3);
                bytes = withMark;
            }
            loaded = true;
            node.contents = bytes;
            size = bytes.length;
        }

        // before the download the size comes from the manifest (stat must not trigger a download)
        Object.defineProperty(node, 'usedBytes', {
            get: () => size, set: value => {
                size = value;
            }, configurable: true
        });

        const operations = {};
        for (const [name, operation] of Object.entries(node.stream_ops)) {
            operations[name] = (...args) => {
                ensureLoaded();
                return operation(...args);
            };
        }
        node.stream_ops = operations;
    }

    return {
        /** Registers every file of the manifest in the runtime's file system. */
        async mount(runtime) {
            const FS = runtime.Module.FS;
            const base = new URL('vfs/', document.baseURI);
            const manifest = await (await fetch(new URL('manifest.json', base))).json();
            const urls = [];
            for (const entry of manifest) {
                const url = new URL(entry.p.substring(1), base).href;
                urls.push(url);
                createLazyFile(FS, entry.p, url, entry.s);
            }
            allUrls = urls;
            if (!navigator.onLine) warmFromCache(urls);
            window.addEventListener('offline', () => warmFromCache(urls));
            return manifest.length;
        },
    };
})();

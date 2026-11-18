// Offline data of CrypTool 2 Web Edition: what the offline dialog (Components/OfflineModal.razor) shows and does.
//
// offline.json (written by tools-gen-offline.py) lists the downloadable files in groups with their sizes: the
// templates, the pictures (component icons, presentations) and the language data, one group per language. The files
// are kept in the service worker's cache (service-worker.js), the same cache that also holds whatever was used once;
// the service worker downloads them (message "precache"), so a download goes on while the dialog is closed.
//
// The app itself (runtime, scripts, texts) is not part of the groups: the service worker stores it after every start.
window.ctOffline = (() => {
    /** Same cache as in service-worker.js and vfs.js. */
    const CACHE = 'ct2web-v1';

    /** The running download: {id, ref}. */
    let running = null;

    let groupsPromise = null;

    /** The groups of offline.json: [{id, files: [[path, size], ...]}]. */
    function groups() {
        groupsPromise ??= fetch('offline.json').then(r => r.json()).then(d => d.groups).catch(() => {
            groupsPromise = null;
            return [];
        });
        return groupsPromise;
    }

    /**
     * The address under which the app requests a file, which is the key in the cache. The app asks for templates with
     * every special character escaped (Uri.EscapeDataString: also ' ( ) !); everything else (pictures, data files) is
     * requested the way the browser writes an address (spaces escaped, ' ( ) kept).
     */
    function addressOf(path) {
        if (path.startsWith('templates/')) {
            const strict = segment => encodeURIComponent(segment).replace(/[!'()*]/g, c => '%' + c.charCodeAt(0).toString(16).toUpperCase());
            path = path.split('/').map(strict).join('/');
        }
        return new URL(path, document.baseURI).href;
    }

    async function cachedAddresses() {
        if (!('caches' in window)) return new Set();
        const cache = await caches.open(CACHE);
        return new Set((await cache.keys()).map(request => request.url));
    }

    async function serviceWorker() {
        const registration = navigator.serviceWorker && await navigator.serviceWorker.ready;
        return registration && registration.active;
    }

    return {
        /**
         * The state of every group: {available, groups: [{id, files, bytes, cachedFiles, cachedBytes}], usedBytes,
         * quotaBytes}. available is false when the browser has no service worker (e.g. a private window).
         */
        async status() {
            const available = !!navigator.serviceWorker && 'caches' in window;
            const cached = await cachedAddresses();
            const result = [];
            for (const group of await groups()) {
                let bytes = 0, cachedFiles = 0, cachedBytes = 0;
                for (const [path, size] of group.files) {
                    bytes += size;
                    if (cached.has(addressOf(path))) {
                        cachedFiles++;
                        cachedBytes += size;
                    }
                }
                result.push({id: group.id, files: group.files.length, bytes, cachedFiles, cachedBytes});
            }
            let usedBytes = 0, quotaBytes = 0;
            try {
                const estimate = await navigator.storage.estimate();
                usedBytes = estimate.usage || 0;
                quotaBytes = estimate.quota || 0;
            } catch { /* no storage estimate in this browser */ }
            return {available, groups: result, usedBytes, quotaBytes};
        },

        /**
         * Downloads the files of the groups (ids) that are not cached yet; with fresh, all of them again from the
         * server. Progress goes to ref.OnOfflineProgress(done, failed, total, bytes, totalBytes, finished, cancelled).
         * Returns false when there is no service worker.
         */
        async download(ids, fresh, ref) {
            const worker = await serviceWorker();
            if (!worker) return false;
            const cache = await caches.open(CACHE);
            const cached = fresh ? new Set() : await cachedAddresses();
            const urls = [], sizes = [];
            for (const group of await groups()) {
                if (!ids.includes(group.id)) continue;
                for (const [path, size] of group.files) {
                    const address = addressOf(path);
                    if (fresh) await cache.delete(address);
                    else if (cached.has(address)) continue;
                    urls.push(address);
                    sizes.push(size);
                }
            }
            const totalBytes = sizes.reduce((a, b) => a + b, 0);
            const id = Date.now() + Math.random();
            running = {id, ref};
            const channel = new MessageChannel();
            channel.port1.onmessage = m => {
                const d = m.data;
                if (d.finished && running && running.id === id) running = null;
                ref.invokeMethodAsync('OnOfflineProgress', d.done, d.failed, d.total, d.bytes, totalBytes, !!d.finished, !!d.cancelled)
                    .catch(() => { /* the page is gone */ });
            };
            worker.postMessage({type: 'precache', id, urls, sizes}, [channel.port2]);
            return true;
        },

        /** Stops the running download (the files already stored stay). */
        async cancel() {
            const worker = await serviceWorker();
            if (worker && running) worker.postMessage({type: 'cancel-precache', id: running.id});
        },

        /** Removes the files of the groups (ids) from the cache. Files the app uses right now stay in memory. */
        async remove(ids) {
            if (!('caches' in window)) return;
            const cache = await caches.open(CACHE);
            for (const group of await groups()) {
                if (!ids.includes(group.id)) continue;
                for (const [path] of group.files) await cache.delete(addressOf(path));
            }
        },
    };
})();

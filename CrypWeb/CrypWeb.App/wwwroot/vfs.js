// Virtual file system for the plugins: the data files that CT2 reads from disk (language statistics, a few plugin
// files) are listed in vfs/manifest.json and appear in the .NET runtime's file system under the same paths
// (/LanguageStatistics/..., /CrypPlugins/...). A file is downloaded the first time it is opened.
//
// The runtime is multi-threaded: plugins read files on background threads, and the file system lives on the main
// thread, which serves those reads. Emscripten's own lazy files refuse to work there (they want a web worker), so
// the download is done here with a synchronous request in the old "x-user-defined" text mode, the one way a browser
// still allows synchronous binary downloads on the main thread. Each file is loaded once.
window.ctVfs = (() => {
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
      let bytes = downloadSync(url);
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
    Object.defineProperty(node, 'usedBytes', { get: () => size, set: value => { size = value; }, configurable: true });

    const operations = {};
    for (const [name, operation] of Object.entries(node.stream_ops)) {
      operations[name] = (...args) => { ensureLoaded(); return operation(...args); };
    }
    node.stream_ops = operations;
  }

  return {
    /** Registers every file of the manifest in the runtime's file system. */
    async mount(runtime) {
      const FS = runtime.Module.FS;
      const base = new URL('vfs/', document.baseURI);
      const manifest = await (await fetch(new URL('manifest.json', base))).json();
      for (const entry of manifest) {
        createLazyFile(FS, entry.p, new URL(entry.p.substring(1), base).href, entry.s);
      }
      return manifest.length;
    },
  };
})();

// Error report of CrypTool 2 Web Edition.
//
// From the first moment on, this script keeps the most recent console messages and every uncaught error. When the
// app crashes (an unhandled .NET exception shows Blazor's error bar, the .NET runtime aborts, a script error is not
// caught), it opens a window with a report: what happened, the browser and its capabilities, the state of the app and
// the recent messages. The report can be copied or downloaded and attached to a bug report. Nothing is sent anywhere.
//
// The window is plain HTML and script, so it also works when the .NET runtime is no longer running.
// It can be opened at any time with ctCrash.open() (command palette: "Error report").
window.ctCrash = (() => {
    const MAX_MESSAGES = 300;
    const MAX_ERRORS = 50;
    const started = new Date();

    /** Recent console messages: {time, level, text}. */
    const messages = [];
    /** Errors: {time, kind, text}. */
    const errors = [];

    /** Readable text of a logged value (errors with their stack). */
    function describe(value) {
        if (value instanceof Error) return value.stack && value.stack.includes(value.message) ? value.stack : value.name + ': ' + value.message + '\n' + (value.stack || '');
        if (typeof value === 'string') return value;
        try {
            return JSON.stringify(value);
        } catch (e) {
            return String(value);
        }
    }

    function remember(list, max, entry) {
        list.push(entry);
        if (list.length > max) list.splice(0, list.length - max);
    }

    // ---- recording --------------------------------------------------------------------------------------------

    for (const level of ['log', 'info', 'warn', 'error', 'debug']) {
        const original = console[level].bind(console);
        console[level] = (...args) => {
            try {
                remember(messages, MAX_MESSAGES, {time: new Date(), level, text: args.map(describe).join(' ')});
            } catch (e) { /* never let the recording break the logging */ }
            original(...args);
        };
    }

    window.addEventListener('error', event => {
        const where = event.filename ? ' (' + event.filename + ':' + event.lineno + ':' + event.colno + ')' : '';
        addError('Script error', (event.error ? describe(event.error) : event.message) + where);
    });
    window.addEventListener('unhandledrejection', event => addError('Unhandled promise rejection', describe(event.reason)));

    /**
     * Blazor drops an event that arrives for a handler of an earlier render (fast pointer moves, a click while the
     * page re-renders) and reports it as an unhandled rejection. Nothing breaks, so the report only counts them.
     */
    const DROPPED_EVENT = /There is no event handler associated with this event/;
    let droppedEvents = 0;

    function addError(kind, text) {
        if (DROPPED_EVENT.test(text)) {
            droppedEvents++;
            return;
        }
        remember(errors, MAX_ERRORS, {time: new Date(), kind, text: String(text)});
    }

    // ---- crash detection --------------------------------------------------------------------------------------

    /** Called when the app can no longer be used: records the cause and shows the report (once). */
    let shown = false;

    /**
     * Called when the app can no longer be used: shows the report (once). text is the cause, or a function that
     * returns it once the runtime had a moment to log the rest of its messages (stack traces come in several lines).
     */
    function crashed(kind, text) {
        if (shown) {
            if (typeof text === 'string' && text) addError(kind, text);
            return;
        }
        shown = true;
        setTimeout(() => {
            const cause = typeof text === 'function' ? text() : text;
            if (cause) addError(kind, cause);
            open(kind);
        }, 300);
    }

    /**
     * The error messages logged just before: .NET writes an exception line by line (message, then one line per stack
     * frame), so the messages of the last two seconds before the latest one are taken together.
     */
    function recentErrorLines() {
        const lines = messages.filter(m => m.level === 'error');
        if (lines.length === 0) return '';
        const last = lines[lines.length - 1].time;
        return lines.filter(m => last - m.time <= 2000).map(m => m.text).join('\n');
    }

    // Blazor shows its error bar after an unhandled .NET exception: show the report instead.
    function watchBlazorErrorBar() {
        const bar = document.getElementById('blazor-error-ui');
        if (!bar) return;
        new MutationObserver(() => {
            if (bar.style.display && bar.style.display !== 'none') {
                bar.style.display = 'none';
                crashed('Unhandled .NET exception', recentErrorLines);
            }
        }).observe(bar, {attributes: true, attributeFilter: ['style']});
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchBlazorErrorBar);
    else watchBlazorErrorBar();

    // ---- the report -------------------------------------------------------------------------------------------

    const texts = {
        en: {
            title: 'Something went wrong',
            intro: 'CrypTool 2 Web Edition ran into an error. The report below contains the details needed to find the cause '
                + '(browser, app state, error messages). It stays on this computer: copy or download it and attach it to your bug report.',
            manual: 'Error report',
            manualIntro: 'The current state of the app and its recent messages. Copy or download the report and attach it to your bug report.',
            copy: 'Copy report',
            copied: 'Copied',
            download: 'Download',
            reload: 'Reload app',
            close: 'Close',
            unsaved: 'Reloading discards changes that were not saved.',
        },
        de: {
            title: 'Etwas ist schiefgelaufen',
            intro: 'In CrypTool 2 Web Edition ist ein Fehler aufgetreten. Der Bericht unten enthält die Angaben, mit denen sich die Ursache '
                + 'finden lässt (Browser, Zustand der App, Fehlermeldungen). Er bleibt auf diesem Rechner: Kopieren oder laden Sie ihn herunter '
                + 'und hängen Sie ihn an Ihre Fehlermeldung an.',
            manual: 'Fehlerbericht',
            manualIntro: 'Der aktuelle Zustand der App und ihre letzten Meldungen. Kopieren oder laden Sie den Bericht herunter und hängen Sie ihn an Ihre Fehlermeldung an.',
            copy: 'Bericht kopieren',
            copied: 'Kopiert',
            download: 'Herunterladen',
            reload: 'App neu laden',
            close: 'Schließen',
            unsaved: 'Beim Neuladen gehen nicht gespeicherte Änderungen verloren.',
        },
    };

    function language() {
        let code = null;
        try {
            code = localStorage.getItem('ct2.lang');
        } catch (e) { /* storage not available */ }
        code = (code || navigator.language || 'en').substring(0, 2).toLowerCase();
        return texts[code] ? code : 'en';
    }

    function time(date) {
        return date.toISOString().replace('T', ' ').replace('Z', '');
    }

    /** Version of the app: the content hashes of the framework files change with every build. */
    function buildId() {
        const script = [...document.scripts].map(s => s.src).find(src => /blazor\.webassembly/.test(src)) || '';
        const match = script.match(/blazor\.webassembly\.([a-z0-9]+)\.js/i);
        return match ? match[1] : 'unknown';
    }

    function text(selector) {
        const element = document.querySelector(selector);
        // innerText keeps the parts of the status bar apart; the close button of a tab is not part of its name
        return element ? element.innerText.replace(/[✕×]/g, '').split('\n').map(s => s.trim()).filter(Boolean).join(' · ') : '-';
    }

    /** The whole report as plain text. */
    function report(kind) {
        const lines = [];
        const add = (label, value) => lines.push(label.padEnd(24) + value);
        lines.push('CrypTool 2 Web Edition - error report');
        lines.push('='.repeat(40));
        add('Reason', kind || 'opened by the user');
        add('Time', time(new Date()));
        add('App started', time(started) + ' (' + Math.round((Date.now() - started) / 1000) + ' s before)');
        add('Build', buildId());
        add('Address', location.origin + location.pathname + location.search);
        lines.push('');
        lines.push('Browser');
        lines.push('-'.repeat(40));
        add('User agent', navigator.userAgent);
        add('Platform', (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || '-');
        add('Language', navigator.language + ' (app: ' + language() + ')');
        add('Window', innerWidth + 'x' + innerHeight + ' @' + devicePixelRatio + 'x, screen ' + screen.width + 'x' + screen.height);
        add('CPU cores', navigator.hardwareConcurrency || '-');
        add('Device memory', navigator.deviceMemory ? navigator.deviceMemory + ' GB' : '-');
        if (performance.memory) {
            add('JS heap', Math.round(performance.memory.usedJSHeapSize / 1048576) + ' of ' + Math.round(performance.memory.jsHeapSizeLimit / 1048576) + ' MB');
        }
        add('Cross-origin isolated', String(window.crossOriginIsolated));
        add('SharedArrayBuffer', String(typeof SharedArrayBuffer !== 'undefined'));
        add('Online', String(navigator.onLine));
        add('Service worker', navigator.serviceWorker && navigator.serviceWorker.controller ? 'active' : 'not active');
        lines.push('');
        lines.push('App');
        lines.push('-'.repeat(40));
        add('Open tab', text('.doctab.on'));
        add('Status', text('.status'));
        add('Dropped UI events', droppedEvents + ' (harmless: event for an outdated handler, see crashreport.js)');
        lines.push('');
        lines.push('Errors (' + errors.length + ')');
        lines.push('-'.repeat(40));
        if (errors.length === 0) lines.push('none');
        for (const e of errors) lines.push('[' + time(e.time) + '] ' + e.kind + ':\n' + e.text + '\n');
        lines.push('');
        lines.push('Recent messages (' + messages.length + ', oldest first)');
        lines.push('-'.repeat(40));
        for (const m of messages) lines.push('[' + time(m.time) + '] ' + m.level.toUpperCase().padEnd(5) + ' ' + m.text);
        return lines.join('\n');
    }

    function element(tag, className, content) {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (content !== undefined) e.textContent = content;
        return e;
    }

    /** Shows the report window. kind: what happened (empty when the user opened it). */
    function open(kind) {
        document.querySelector('.crash-back')?.remove();
        const t = texts[language()];
        const body = report(kind);

        const back = element('div', 'crash-back');
        const dialog = element('div', 'crash-dialog');
        dialog.setAttribute('role', 'alertdialog');
        dialog.setAttribute('aria-modal', 'true');
        const head = element('div', 'crash-head' + (kind ? '' : ' manual'));
        head.append(element('div', 'crash-title', kind ? t.title : t.manual));
        if (kind) head.append(element('div', 'crash-kind', kind));
        const intro = element('p', 'crash-intro', kind ? t.intro : t.manualIntro);
        const box = element('textarea', 'crash-report');
        box.readOnly = true;
        box.spellcheck = false;
        box.value = body;

        const buttons = element('div', 'crash-buttons');
        const copy = element('button', 'crash-primary', t.copy);
        copy.onclick = async () => {
            try {
                await navigator.clipboard.writeText(box.value);
            } catch (e) {
                box.select();
                document.execCommand('copy');
            }
            copy.textContent = t.copied;
            setTimeout(() => copy.textContent = t.copy, 1500);
        };
        const download = element('button', '', t.download);
        download.onclick = () => {
            const link = document.createElement('a');
            link.href = URL.createObjectURL(new Blob([box.value], {type: 'text/plain'}));
            link.download = 'cryptool-web-error-' + time(new Date()).replace(/[: ]/g, '-').replace(/\..*/, '') + '.txt';
            link.click();
            setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        };
        const reload = element('button', '', t.reload);
        reload.title = t.unsaved;
        reload.onclick = () => location.reload();
        const close = element('button', '', t.close);
        close.onclick = () => back.remove();
        buttons.append(copy, download, element('span', 'crash-gap'), reload, close);

        dialog.append(head, intro, box, buttons);
        back.append(dialog);
        document.body.append(back);
        copy.focus();
    }

    return {
        open: () => open(''),
        /** The .NET runtime stopped (Mono abort or exit): see Blazor.start in index.html. */
        runtimeAborted: reason => crashed('.NET runtime aborted', describe(reason)),
        report,
    };
})();

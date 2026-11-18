// Rich text editor of the notes (Pages/Home.Notes.cs): a contenteditable element (#note-editor) in the HTML layer
// above the canvas and a toolbar (.ne-tools) with the commands of CT2's note editor.
//
// The browser does the editing (document.execCommand). When the editor closes (a click outside it, Ctrl/Cmd+Enter or
// "Done"; Escape discards), its content is described as blocks of styled runs, read from the computed styles, so
// it does not matter which markup the browser produced (<b>, <span style>, <font>, <div> or <p>):
//   [{runs: [{text, bold, italic, underline, size, family, color, break}], align, indent, list}]
// The page turns them into the note's XAML (CrypWeb.Core/NoteXaml.cs).
window.ctNotes = (() => {
    const MIN_SIZE = 6, MAX_SIZE = 72;

    /** The running edit: {editor, ref, id}. */
    let current = null;

    function editorElement() {
        return document.getElementById('note-editor');
    }

    // ---- start and end ----------------------------------------------------------------------------------------

    function start(html, ref, id, selectAll) {
        const editor = editorElement();
        if (!editor) return;
        current = {editor, ref, id};
        editor.innerHTML = html;
        document.execCommand('defaultParagraphSeparator', false, 'p');
        document.execCommand('styleWithCSS', false, true);
        editor.focus();
        const range = document.createRange();
        range.selectNodeContents(editor);
        if (!selectAll) range.collapse(false);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        updateFont();
    }

    /** Ends the edit; with keep, the content is sent to the page. */
    function finish(keep) {
        if (!current) return;
        const {editor, ref, id} = current;
        current = null;
        const call = keep
            ? ref.invokeMethodAsync('OnNoteEdited', id, JSON.stringify(describe(editor)))
            : ref.invokeMethodAsync('OnNoteEditCancelled');
        call.catch(() => { /* the page is gone */ });
    }

    // a click outside the editor and its toolbar ends the edit
    document.addEventListener('pointerdown', event => {
        if (!current || !document.body.contains(current.editor)) return;
        if (event.target.closest && (event.target.closest('#note-editor') || event.target.closest('.ne-tools'))) return;
        finish(true);
    }, true);

    document.addEventListener('keydown', event => {
        if (!current || event.target !== current.editor) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            finish(false);
        } else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            finish(true);
        }
    });

    // pasted content becomes plain text in the current formatting (no foreign styles or markup)
    document.addEventListener('paste', event => {
        if (!current || !current.editor.contains(event.target)) return;
        event.preventDefault();
        document.execCommand('insertText', false, event.clipboardData.getData('text/plain'));
    });

    // ---- toolbar ----------------------------------------------------------------------------------------------

    // mousedown, not click: the button must not take the focus (and the selection) from the text
    document.addEventListener('mousedown', event => {
        const button = event.target.closest && event.target.closest('.ne-tools [data-cmd]');
        if (!button || !current) return;
        event.preventDefault();
        run(button.dataset.cmd);
    });

    document.addEventListener('change', event => {
        if (!current || !event.target.matches('.ne-tools [data-font]')) return;
        current.editor.focus();
        restoreSelection();
        document.execCommand('fontName', false, event.target.value);
    });

    // the font list takes the focus: remember the selection to restore it
    let savedRange = null;
    document.addEventListener('selectionchange', () => {
        if (!current) return;
        const selection = window.getSelection();
        if (selection.rangeCount && current.editor.contains(selection.anchorNode)) {
            savedRange = selection.getRangeAt(0).cloneRange();
            updateFont();
        }
    });

    function restoreSelection() {
        if (!savedRange) return;
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(savedRange);
    }

    /** Shows the font of the text at the cursor in the font list. */
    function updateFont() {
        const select = document.querySelector('.ne-tools [data-font]');
        const selection = window.getSelection();
        if (!select || !selection.rangeCount) return;
        let node = selection.anchorNode;
        if (node && node.nodeType === Node.TEXT_NODE) node = node.parentElement;
        if (!node || !current || !current.editor.contains(node)) return;
        const family = firstFamily(getComputedStyle(node).fontFamily);
        if ([...select.options].some(o => o.value === family)) select.value = family;
    }

    async function run(command) {
        const editor = current.editor;
        editor.focus();
        switch (command) {
            case 'done':
                finish(true);
                return;
            case 'paste':
                try {
                    const text = await navigator.clipboard.readText();
                    document.execCommand('insertText', false, text);
                } catch {
                    /* the browser does not let the page read the clipboard: Ctrl/Cmd+V works */
                }
                return;
            case 'grow':
                resize(+2);
                return;
            case 'shrink':
                resize(-2);
                return;
            default:
                document.execCommand(command, false, null);
        }
    }

    /**
     * Makes the selected text larger or smaller by delta pixels, each part from its own size, and keeps exactly the
     * same text selected, so that the button can be pressed again and again. (execCommand('fontSize') knows only
     * seven sizes and, once the text has a size in CSS, rearranges the markup and the selection.)
     */
    function resize(delta) {
        const editor = current.editor;
        const selection = window.getSelection();
        if (!selection.rangeCount || selection.isCollapsed) return;
        const range = selection.getRangeAt(0);
        if (!editor.contains(range.commonAncestorContainer)) return;

        // the selected text nodes; the ones at the edges are split, so that only the selected part changes
        let {startContainer, startOffset, endContainer, endOffset} = range;
        if (endContainer.nodeType === Node.TEXT_NODE && endOffset < endContainer.length) endContainer.splitText(endOffset);
        if (startContainer.nodeType === Node.TEXT_NODE && startOffset > 0) {
            const rest = startContainer.splitText(startOffset);
            if (endContainer === startContainer) endContainer = rest;
            startContainer = rest;
            startOffset = 0;
        }
        const selected = new Range();
        selected.setStart(startContainer, startOffset);
        if (endContainer.nodeType === Node.TEXT_NODE) selected.setEnd(endContainer, endContainer.length);
        else selected.setEnd(endContainer, endOffset);
        const root = selected.commonAncestorContainer.nodeType === Node.TEXT_NODE
            ? selected.commonAncestorContainer.parentNode : selected.commonAncestorContainer;
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        const texts = [];
        for (let node = walker.currentNode.nodeType === Node.TEXT_NODE ? walker.currentNode : walker.nextNode(); node; node = walker.nextNode()) {
            if (node.length > 0 && selected.intersectsNode(node)) texts.push(node);
        }
        if (texts.length === 0) return;

        for (const text of texts) {
            const parent = text.parentElement;
            const size = Math.min(MAX_SIZE, Math.max(MIN_SIZE, Math.round(parseFloat(getComputedStyle(parent).fontSize) + delta)));
            if (parent.tagName === 'SPAN' && parent.childNodes.length === 1 && parent !== editor) {
                parent.style.fontSize = size + 'px'; // the text has a span of its own: change that
            } else {
                const span = document.createElement('span');
                span.style.fontSize = size + 'px';
                parent.insertBefore(span, text);
                span.appendChild(text);
            }
        }

        const again = new Range();
        again.setStart(texts[0], 0);
        again.setEnd(texts[texts.length - 1], texts[texts.length - 1].length);
        selection.removeAllRanges();
        selection.addRange(again);
    }

    // ---- content -> blocks ------------------------------------------------------------------------------------

    function firstFamily(fontFamily) {
        return (fontFamily || '').split(',')[0].trim().replace(/^["']|["']$/g, '');
    }

    function hexColor(rgb) {
        const m = (rgb || '').match(/\d+(\.\d+)?/g);
        if (!m || m.length < 3) return null;
        return '#' + m.slice(0, 3).map(v => Math.round(+v).toString(16).padStart(2, '0')).join('');
    }

    const BLOCKS = new Set(['P', 'DIV', 'LI', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'BLOCKQUOTE', 'PRE']);

    /** The editor's content as blocks (see the top of this file). */
    function describe(editor) {
        // the editor is shown zoomed (canvas zoom): measure in its own, unzoomed units
        const box = editor.getBoundingClientRect();
        const scale = box.width / editor.offsetWidth || 1;
        const contentLeft = editor.clientLeft + (parseFloat(getComputedStyle(editor).paddingLeft) || 0);
        const blocks = [];
        let block = null;

        const startBlock = (element, list) => {
            const style = getComputedStyle(element);
            const align = {center: 'center', right: 'right', end: 'right', justify: 'justify'}[style.textAlign] || null;
            const offset = (element.getBoundingClientRect().left - box.left) / scale - contentLeft;
            const indent = list || element === editor ? 0 : Math.max(0, Math.round(offset));
            block = {runs: [], align, indent, list};
            blocks.push(block);
        };

        const underlined = element => {
            for (let e = element; e && e !== editor.parentElement; e = e.parentElement) {
                if (getComputedStyle(e).textDecorationLine.includes('underline')) return true;
            }
            return false;
        };

        const addText = (node, list) => {
            const text = node.nodeValue.replace(/ /g, ' ');
            if (!text) return;
            if (!block) startBlock(node.parentElement, list);
            const element = node.parentElement;
            const style = getComputedStyle(element);
            const run = {
                text,
                bold: (parseInt(style.fontWeight) || 400) >= 600,
                italic: style.fontStyle === 'italic' || style.fontStyle.startsWith('oblique'),
                underline: underlined(element),
                size: Math.round(parseFloat(style.fontSize) * 100) / 100,
                family: firstFamily(style.fontFamily),
                color: hexColor(style.color),
                break: false,
            };
            const last = block.runs[block.runs.length - 1];
            const same = last && !last.break && ['bold', 'italic', 'underline', 'size', 'family', 'color'].every(k => last[k] === run[k]);
            if (same) last.text += text;
            else block.runs.push(run);
        };

        const walk = (node, list) => {
            for (const child of node.childNodes) {
                if (child.nodeType === Node.TEXT_NODE) {
                    addText(child, list);
                } else if (child.nodeType === Node.ELEMENT_NODE) {
                    const tag = child.tagName;
                    if (tag === 'BR') {
                        // the browser's placeholder at the end of a line is not a line break of its own
                        const isLast = !child.nextSibling || (child.nextSibling.nodeType === Node.TEXT_NODE && !child.nextSibling.nodeValue);
                        if (!block) startBlock(child.parentElement, list);
                        else if (!isLast) block.runs.push({text: '', break: true});
                    } else if (tag === 'UL' || tag === 'OL') {
                        block = null;
                        walk(child, tag.toLowerCase());
                        block = null;
                    } else if (BLOCKS.has(tag)) {
                        const nested = [...child.children].some(c => BLOCKS.has(c.tagName) || c.tagName === 'UL' || c.tagName === 'OL');
                        if (nested) {
                            block = null;
                            walk(child, list);
                            block = null;
                        } else {
                            startBlock(child, tag === 'LI' ? list : null);
                            walk(child, list);
                            block = null;
                        }
                    } else {
                        walk(child, list);
                    }
                }
            }
        };
        walk(editor, null);
        return blocks;
    }

    return {
        start,
        /** Ends the edit without sending anything (the page closed the editor itself). */
        abort() {
            current = null;
        },
        describe,
    };
})();

// ============================================================================
// playground-editor.js — reusable syntax-highlighting code editor
//
// Powers the V10 Playground split editors (SSW + SoundCSS). Each editor owns its
// own DOM (line-number gutter + highlight overlay + transparent <textarea>) so
// Blazor never diffs it — Blazor only renders an empty mount <div> and calls the
// `window.playgroundEditor` API below. Pure client-side, no dependencies.
// ============================================================================
(function () {
    "use strict";

    const editors = {};

    // --- token tables --------------------------------------------------------
    const SSW_KEYWORDS = [
        "tempo", "time", "track", "block", "phrase", "pattern", "voice", "melody",
        "sing", "speak", "effect", "instrument", "layer", "gain", "humanize", "play",
        "vocal", "curve", "transition", "crescendo", "decrescendo", "articulation",
        "swing", "staccato", "legato", "accent", "double", "reinforce", "brighten",
        "rest", "over", "bars", "for", "up", "down", "strum", "rhythm", "wordbank",
    ];
    const SOUNDCSS_PROPS = [
        "style", "persona", "pitch", "speed", "timbre", "vibrato", "accent",
        "breath", "emotion", "gender", "age", "energy",
        // phoneme-level props kept working alongside word rules:
        "burst", "noise", "brightness", "formant1", "formant2", "formant3",
        "smoothness", "nasal", "openness", "harmonic1", "harmonic2", "harmonic3",
        "noise-fricative", "noise-plosive", "transient", "harmonic-rolloff",
        "formant-q", "noise-band", "smoothing",
    ];

    function escapeHtml(text) {
        return text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    // Highlight one physical line. Order matters: comments/strings first so their
    // inner text isn't re-tokenized.
    function highlightLine(line, language) {
        // Comments (// ...)
        const commentIdx = line.indexOf("//");
        if (commentIdx >= 0) {
            return highlightLine(line.slice(0, commentIdx), language) +
                `<span class="tok-comment">${escapeHtml(line.slice(commentIdx))}</span>`;
        }

        const parts = [];
        // Split on strings, keeping the quotes.
        const stringRe = /"[^"]*"/g;
        let last = 0;
        let m;
        while ((m = stringRe.exec(line)) !== null) {
            if (m.index > last) parts.push(highlightCode(line.slice(last, m.index), language));
            parts.push(`<span class="tok-string">${escapeHtml(m[0])}</span>`);
            last = m.index + m[0].length;
        }
        if (last < line.length) parts.push(highlightCode(line.slice(last), language));
        return parts.join("");
    }

    // Highlight a run of code that contains no strings/comments.
    function highlightCode(code, language) {
        const keywords = language === "soundcss" ? SOUNDCSS_PROPS : SSW_KEYWORDS;
        return escapeHtml(code).replace(/[A-Za-z][\w-]*|[+-]?\d+(?:\.\d+)?|[{}:;]/g, (token) => {
            if (/^[{}:;]$/.test(token)) return `<span class="tok-punct">${token}</span>`;
            if (/^[+-]?\d/.test(token)) return `<span class="tok-number">${token}</span>`;
            const lower = token.toLowerCase();
            if (keywords.includes(lower)) {
                const cls = language === "soundcss" ? "tok-prop" : "tok-keyword";
                return `<span class="${cls}">${token}</span>`;
            }
            // Note tokens like C4, F#3, durations q/e/h/w in SSW.
            if (language !== "soundcss" && /^[A-Ga-g][#b]?\d?$/.test(token))
                return `<span class="tok-note">${token}</span>`;
            return token;
        });
    }

    function render(ed) {
        const value = ed.textarea.value;
        const lines = value.split("\n");

        // Gutter line numbers.
        ed.gutter.textContent = lines.map((_, i) => i + 1).join("\n");

        // Highlight overlay (trailing newline keeps the last line height stable).
        ed.code.innerHTML = lines.map((l) => highlightLine(l, ed.language)).join("\n") + "\n";
    }

    function syncScroll(ed) {
        ed.highlight.scrollTop = ed.textarea.scrollTop;
        ed.highlight.scrollLeft = ed.textarea.scrollLeft;
        ed.gutter.scrollTop = ed.textarea.scrollTop;
    }

    // Debounced history push for undo/redo.
    function pushHistory(ed) {
        const value = ed.textarea.value;
        if (ed.undoStack.length && ed.undoStack[ed.undoStack.length - 1] === value) return;
        ed.undoStack.push(value);
        if (ed.undoStack.length > 200) ed.undoStack.shift();
        ed.redoStack.length = 0;
    }

    function handleTab(ed, e) {
        if (e.key !== "Tab") return;
        e.preventDefault();
        const ta = ed.textarea;
        const start = ta.selectionStart;
        const end = ta.selectionEnd;
        ta.value = ta.value.slice(0, start) + "    " + ta.value.slice(end);
        ta.selectionStart = ta.selectionEnd = start + 4;
        pushHistory(ed);
        render(ed);
    }

    function handleEscape(ed, e) {
        if (e.key !== "Escape") return;
        e.preventDefault();
        const selector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), ' +
            'textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])';
        const focusable = Array.from(document.querySelectorAll(selector))
            .filter((element) => element.offsetParent !== null);
        const currentIndex = focusable.indexOf(ed.textarea);
        const next = currentIndex >= 0 ? focusable[currentIndex + 1] : null;
        if (next) next.focus();
        else ed.textarea.blur();
    }

    // Auto-indent: on Enter, copy the leading whitespace of the current line.
    function handleEnter(ed, e) {
        if (e.key !== "Enter") return;
        const ta = ed.textarea;
        const start = ta.selectionStart;
        const lineStart = ta.value.lastIndexOf("\n", start - 1) + 1;
        const indentMatch = /^[ \t]*/.exec(ta.value.slice(lineStart, start));
        const indent = indentMatch ? indentMatch[0] : "";
        if (!indent) return;
        e.preventDefault();
        const insert = "\n" + indent;
        ta.value = ta.value.slice(0, start) + insert + ta.value.slice(ta.selectionEnd);
        ta.selectionStart = ta.selectionEnd = start + insert.length;
        pushHistory(ed);
        render(ed);
    }

    const api = {
        init(mountId, opts) {
            const mount = document.getElementById(mountId);
            if (!mount) return;
            opts = opts || {};

            mount.classList.add("pge");
            mount.dataset.theme = opts.theme || "dark";
            mount.innerHTML =
                '<div class="pge-gutter" aria-hidden="true"></div>' +
                '<div class="pge-scroll">' +
                '<pre class="pge-highlight" aria-hidden="true"><code></code></pre>' +
                '<textarea class="pge-input" spellcheck="false" autocapitalize="off" ' +
                'autocomplete="off" autocorrect="off" wrap="off"></textarea>' +
                "</div>";

            const ed = {
                mount,
                language: opts.language || "ssw",
                gutter: mount.querySelector(".pge-gutter"),
                highlight: mount.querySelector(".pge-highlight"),
                code: mount.querySelector(".pge-highlight code"),
                textarea: mount.querySelector(".pge-input"),
                undoStack: [],
                redoStack: [],
                historyTimer: null,
            };
            editors[mountId] = ed;

            if (opts.placeholder) ed.textarea.placeholder = opts.placeholder;
            ed.textarea.setAttribute("aria-label", opts.ariaLabel || "Code editor");
            ed.textarea.setAttribute("aria-keyshortcuts", "Escape");
            ed.textarea.setAttribute("title", "Press Escape to leave the editor.");

            ed.textarea.addEventListener("input", () => {
                render(ed);
                clearTimeout(ed.historyTimer);
                ed.historyTimer = setTimeout(() => pushHistory(ed), 300);
            });
            ed.textarea.addEventListener("scroll", () => syncScroll(ed));
            ed.textarea.addEventListener("keydown", (e) => {
                handleEscape(ed, e);
                handleTab(ed, e);
                handleEnter(ed, e);
            });

            ed.undoStack.push(ed.textarea.value);
            render(ed);
        },

        setValue(id, text) {
            const ed = editors[id];
            if (!ed) return;
            ed.textarea.value = text || "";
            ed.undoStack = [ed.textarea.value];
            ed.redoStack = [];
            render(ed);
            syncScroll(ed);
        },

        getValue(id) {
            const ed = editors[id];
            return ed ? ed.textarea.value : "";
        },

        clear(id) { this.setValue(id, ""); },

        undo(id) {
            const ed = editors[id];
            if (!ed || ed.undoStack.length <= 1) return;
            ed.redoStack.push(ed.undoStack.pop());
            ed.textarea.value = ed.undoStack[ed.undoStack.length - 1];
            render(ed);
        },

        redo(id) {
            const ed = editors[id];
            if (!ed || !ed.redoStack.length) return;
            const value = ed.redoStack.pop();
            ed.undoStack.push(value);
            ed.textarea.value = value;
            render(ed);
        },

        async copy(id) {
            const ed = editors[id];
            if (!ed) return false;
            try {
                await navigator.clipboard.writeText(ed.textarea.value);
                return true;
            } catch {
                return false;
            }
        },

        setTheme(id, theme) {
            const ed = editors[id];
            if (ed) ed.mount.dataset.theme = theme;
        },

        toggleTheme(id) {
            const ed = editors[id];
            if (!ed) return "dark";
            const next = ed.mount.dataset.theme === "dark" ? "light" : "dark";
            ed.mount.dataset.theme = next;
            return next;
        },
    };

    // Simple horizontal resizer between two panes sharing a flex row.
    api.initSplit = function (dividerId, leftId, rightId) {
        const divider = document.getElementById(dividerId);
        const left = document.getElementById(leftId);
        const right = document.getElementById(rightId);
        if (!divider || !left || !right) return;

        let dragging = false;
        const onMove = (clientX) => {
            const row = divider.parentElement;
            const rect = row.getBoundingClientRect();
            let ratio = (clientX - rect.left) / rect.width;
            ratio = Math.min(0.8, Math.max(0.2, ratio));
            left.style.flex = `0 0 ${ratio * 100}%`;
            right.style.flex = `1 1 auto`;
        };
        divider.addEventListener("mousedown", (e) => { dragging = true; e.preventDefault(); });
        window.addEventListener("mousemove", (e) => { if (dragging) onMove(e.clientX); });
        window.addEventListener("mouseup", () => { dragging = false; });
        divider.addEventListener("touchmove", (e) => {
            if (e.touches.length) onMove(e.touches[0].clientX);
        }, { passive: true });
    };

    // Smoothly scroll an element into view (used by "Style in Studio").
    api.scrollTo = function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    window.playgroundEditor = api;
})();

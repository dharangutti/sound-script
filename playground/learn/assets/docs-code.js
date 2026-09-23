(function (global) {
    'use strict';
    var labels = { csharp: 'C#', cs: 'C#', soundscript: 'SoundScript', bash: 'Bash', sh: 'Shell', shell: 'Shell',
        powershell: 'PowerShell', pwsh: 'PowerShell', cmd: 'Command Prompt', json: 'JSON', xml: 'XML',
        javascript: 'JavaScript', js: 'JavaScript', html: 'HTML', css: 'CSS', text: 'Text', 'console-output': 'Output' };
    function fallback(text, document) {
        var active = document.activeElement;
        var selection = document.getSelection();
        var ranges = [];
        for (var i = 0; selection && i < selection.rangeCount; i++) ranges.push(selection.getRangeAt(i).cloneRange());
        var area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.setAttribute('aria-label', 'Code to copy');
        area.style.cssText = 'position:fixed;left:-9999px;top:0;';
        document.body.appendChild(area);
        try { area.select(); if (!document.execCommand('copy')) throw new Error('Clipboard unavailable'); }
        finally {
            area.remove();
            if (active && active.focus) active.focus({ preventScroll: true });
            if (selection) { selection.removeAllRanges(); ranges.forEach(function (range) { selection.addRange(range); }); }
        }
    }
    async function copy(text, document) {
        if (global.isSecureContext && global.navigator.clipboard) {
            try { await global.navigator.clipboard.writeText(text); return; } catch (_) { /* Try local fallback. */ }
        }
        fallback(text, document);
    }
    function enhance(content) {
        var document = content.ownerDocument;
        content.querySelectorAll('pre > code').forEach(function (code) {
            var pre = code.parentElement;
            if (pre.parentElement.classList.contains('code-block')) return;
            var language = (code.className.match(/(?:^|\s)language-([^\s]+)/) || [])[1] || 'text';
            language = language.toLowerCase();
            var label = labels[language] || language.slice(0, 40);
            var kind = language === 'console-output' ? 'output' : /^(bash|sh|shell|powershell|pwsh|cmd)$/.test(language) ? 'terminal' : 'source';
            var shell = document.createElement('div');
            shell.className = 'code-block code-' + kind;
            var toolbar = document.createElement('div');
            toolbar.className = 'code-toolbar';
            var title = document.createElement('span');
            title.textContent = label;
            var button = document.createElement('button');
            button.type = 'button';
            button.textContent = 'Copy';
            button.setAttribute('aria-label', 'Copy ' + label + (kind === 'terminal' ? ' command' : kind === 'output' ? ' text' : ' code'));
            var status = document.createElement('span');
            status.className = 'code-status';
            status.setAttribute('role', 'status');
            status.setAttribute('aria-live', 'polite');
            toolbar.append(title, button);
            pre.replaceWith(shell);
            shell.append(toolbar, pre, status);
            var timer;
            button.addEventListener('click', async function () {
                clearTimeout(timer);
                try {
                    await copy(code.textContent, document);
                    button.textContent = 'Copied!';
                    status.textContent = 'Copied to clipboard';
                } catch (_) {
                    button.textContent = 'Copy failed';
                    status.textContent = 'Copy failed — select code and copy manually.';
                }
                timer = setTimeout(function () { button.textContent = 'Copy'; status.textContent = ''; }, 2500);
            });
        });
    }
    // Match the deterministic heading slugs used by local link validation.
    function headings(content) {
        var counts = new Map();
        content.querySelectorAll('h1,h2,h3,h4,h5,h6').forEach(function (heading) {
            var slug = heading.textContent.toLowerCase().replace(/[^\p{L}\p{N}_\s-]/gu, '').trim().replace(/\s/g, '-');
            var n = counts.get(slug) || 0;
            counts.set(slug, n + 1);
            heading.id = slug + (n ? '-' + n : '');
        });
    }
    function render(markdown, marked) {
        var renderer = new marked.Renderer();
        renderer.code = function (source, info) {
            var pre = global.document.createElement('pre');
            var code = global.document.createElement('code');
            // Marked removes the fence separator newline from token text. Restore
            // it without trimming intentional trailing blank lines (its default
            // renderer trims one). DOM text assignment safely escapes all code.
            code.textContent = source + '\n';
            if (info) code.className = 'language-' + info.trim().split(/\s/)[0];
            pre.appendChild(code);
            return pre.outerHTML;
        };
        return marked.parse(markdown, { renderer: renderer, mangle: false, headerIds: true });
    }
    global.SoundScriptDocs = { enhance: enhance, headings: headings, render: render };
})(window);

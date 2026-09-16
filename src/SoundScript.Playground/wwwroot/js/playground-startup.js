// A proxy error page can fail SRI before fetch exposes its HTTP status. Retry
// a bounded number of times, always with the original expected integrity.
window.SoundScriptStartup = (() => {
    async function fetchVerified(uri, integrity) {
        for (let attempt = 0; attempt < 3; attempt++) {
            try {
                const response = await fetch(uri, {
                    integrity,
                    cache: attempt === 0 ? 'default' : 'reload',
                    credentials: 'same-origin',
                    signal: AbortSignal.timeout(30000)
                });
                if (response.ok) return response;
                if (response.status !== 408 && response.status !== 429 && response.status < 500)
                    throw Object.assign(new Error(`HTTP ${response.status} loading ${uri}`), { permanent: true });
                throw new Error(`HTTP ${response.status} loading ${uri}`);
            } catch (error) {
                if (error.permanent || attempt === 2) throw error;
                await new Promise(resolve => setTimeout(resolve, 300 * (attempt + 1)));
            }
        }
    }
    function loadBootResource(type, name, uri, integrity) {
        // JS modules require a URI, not a Response. Let Blazor load them normally.
        if (type === 'dotnetjs' || name.endsWith('.js') || !integrity) return null;
        return fetchVerified(uri, integrity).catch(error => {
            // Some runtime download failures don't settle Blazor.start promptly.
            showFailure(error);
            throw error;
        });
    }
    function showFailure(error) {
        console.error('Playground startup failed after verified resource retries.', error);
        const message = document.querySelector('.boot-screen p');
        if (message) {
            message.setAttribute('role', 'alert');
            message.textContent = 'Playground could not load its verified application files. The server or network may be unavailable. Reload to try again; if this continues, report the failing _framework URL and HTTP status from your browser console.';
        }
        const spinner = document.querySelector('.boot-spinner');
        if (spinner) spinner.hidden = true;
        const panel = document.getElementById('blazor-error-ui');
        if (panel) panel.style.display = 'block';
    }
    async function start() {
        try { await Blazor.start({ loadBootResource }); }
        catch (error) {
            showFailure(error);
        }
    }
    return { loadBootResource, start };
})();
window.SoundScriptStartup.start();

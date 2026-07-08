// Shared, mobile-reliable file download helper.
//
// Mobile browsers (particularly iOS Safari and some Android WebViews) often
// ignore the `download` attribute on `data:` URLs and navigate to / preview
// the content instead of saving a file. Blob + URL.createObjectURL is the
// reliable path, so every download() in the playground delegates here.
window.SoundScriptDownload = (function () {
    function fromBase64(base64, filename, mimeType) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);

        const link = document.createElement('a');
        link.href = url;
        link.download = filename || 'download';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Give the browser time to start the download before revoking the URL.
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    return { fromBase64 };
})();

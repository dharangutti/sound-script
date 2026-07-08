// Plain-text file downloads for SoundScript V6 .ss export.

window.SoundScriptText = (function () {
    function download(base64, filename) {
        window.SoundScriptDownload.fromBase64(base64, filename || 'soundscript.ss', 'text/plain;charset=utf-8');
    }

    return {
        download
    };
})();

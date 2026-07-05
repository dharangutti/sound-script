// Plain-text file downloads for SoundScript V6 .ss export.

window.SoundScriptText = (function () {
    function download(base64, filename) {
        const link = document.createElement('a');
        link.href = 'data:text/plain;charset=utf-8;base64,' + base64;
        link.download = filename || 'soundscript.ss';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    return {
        download
    };
})();

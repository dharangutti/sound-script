using Microsoft.JSInterop;

namespace SoundScript.Playground.Services;

public sealed class MidiPlaybackService(IJSRuntime js)
{
    private bool _initialized;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await js.InvokeVoidAsync("SoundScriptMidi.init");
        _initialized = true;
    }

    public async Task PlayAsync(byte[] midiBytes)
    {
        await EnsureInitializedAsync();
        await js.InvokeVoidAsync("SoundScriptMidi.play", midiBytes);
    }

    public async Task StopAsync()
    {
        if (!_initialized)
            return;

        await js.InvokeVoidAsync("SoundScriptMidi.stop");
    }
}

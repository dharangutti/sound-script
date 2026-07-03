using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Playground.Services;

namespace SoundScript.Playground.Pages;

public partial class Playground
{
  [Inject] private MidiPlaybackService MidiPlayback { get; set; } = null!;
  [Inject] private IJSRuntime Js { get; set; } = null!;

  private const string DefaultScript =
      """
      tempo 120

      melody {
          C4 q
          E4 q
          G4 q
          C5 q
      }
      """;

  private string ScriptText { get; set; } = DefaultScript;
  private string? ErrorMessage { get; set; }
  private string? StatusMessage { get; set; }
  private byte[]? MidiBytes { get; set; }
  private bool IsRunning { get; set; }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
      await MidiPlayback.EnsureInitializedAsync();
  }

  private void LoadMelodyExample()
  {
    ScriptText =
        """
        melody {
            bpm 120
            C4 E4 G4 | C5
        }
        """;
    ClearState();
  }

  private void LoadChordsExample()
  {
    ScriptText =
        """
        tempo 120
        instrument piano

        melody {
            Cmaj q
            Dm q
            G7 q
            Fmaj7 h
        }
        """;
    ClearState();
  }

  private void LoadMultitrackExample()
  {
    ScriptText =
        """
        tempo 120

        track melody {
            instrument flute
            C5 q
            D5 q
            E5 h
        }

        track bass {
            instrument bass
            C2 h
            G2 h
        }
        """;
    ClearState();
  }

  private async Task RunAsync()
  {
    try
    {
      IsRunning = true;
      ErrorMessage = null;
      StatusMessage = "Compiling…";
      MidiBytes = null;
      StateHasChanged();

      await MidiPlayback.StopAsync();

      var tokens = new Tokenizer(ScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();
      var interpreted = Interpreter.Interpret(program);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
      StatusMessage = $"Compiled {noteCount} note(s) across {interpreted.Tracks.Count} track(s) at {interpreted.Tempo} BPM.";
      StateHasChanged();

      await MidiPlayback.PlayAsync(MidiBytes);
      StatusMessage = $"Playing — {noteCount} note(s) at {interpreted.Tempo} BPM.";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      MidiBytes = null;
    }
    finally
    {
      IsRunning = false;
      StateHasChanged();
    }
  }

  private async Task StopAsync()
  {
    await MidiPlayback.StopAsync();
    StatusMessage = "Stopped.";
    StateHasChanged();
  }

  private async Task DownloadAsync()
  {
    if (MidiBytes is null)
      return;

    var base64 = Convert.ToBase64String(MidiBytes);
    await Js.InvokeVoidAsync("SoundScriptMidi.download", base64, "soundscript.mid");
  }

  private void ClearState()
  {
    ErrorMessage = null;
    StatusMessage = null;
    MidiBytes = null;
  }
}

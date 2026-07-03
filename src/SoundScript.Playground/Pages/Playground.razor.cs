using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoundScript.Midi;
using SoundScript.Parser;

namespace SoundScript.Playground.Pages;

public partial class Playground
{
  [Inject] private IJSRuntime Js { get; set; } = null!;

  private const string DefaultScript =
      """
      tempo 120

      melody {
          mf
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

  private void LoadMelodyExample()
  {
    ScriptText =
        """
        melody {
            tempo 120
            C4 q E4 q G4 q | C5 h
        }
        """;
    ClearState();
  }

  private void LoadArticulationsExample()
  {
    ScriptText =
        """
        tempo 120
        instrument piano

        melody {
            staccato C4 q
            D4 q legato
            accent E4 q
            C5 h
        }
        """;
    ClearState();
  }

  private void LoadDynamicsExample()
  {
    ScriptText =
        """
        tempo 96
        instrument piano

        melody {
            p
            C4 q D4 q
            mf
            E4 q F4 q
            f
            G4 h
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

  private void LoadIntelligenceExample()
  {
    ScriptText =
        """
        tempo 110
        instrument violin

        sequence phrasea {
            C5 q
            D5 q
            E5 h
        }

        sequence phraseb {
            G5 q
            A5 q
            B5 h
        }

        melody {
            p
            C4 q D4 q E4 q
            f
            F4 q G4 q A4 q
            play phrasea
            play phraseb
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

  private void LoadPlaybackExample()
  {
    ScriptText =
        """
        tempo 120
        instrument piano

        melody {
            mf
            staccato C4 q
            legato E4 q
            accent G4 q
            Cmaj q
            f
            C5 h
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
      StatusMessage = null;
      MidiBytes = null;

      var tokens = new Tokenizer(ScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();
      var interpreted = Interpreter.Interpret(program);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);

      await Js.InvokeVoidAsync("startPlayback", MidiBytes);

      StatusMessage = $"Playing — {noteCount} note(s) across {interpreted.Tracks.Count} track(s) at {interpreted.Tempo} BPM.";
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
    await Js.InvokeVoidAsync("SoundScriptMidi.stop");
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

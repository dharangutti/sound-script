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

      block intro {
          phrase {
              mf
              C4 q E4 q G4 q
          }
      }

      track melody {
          layer piano
          layer cello
          gain 0.9
          play intro
      }
      """;

  private string ScriptText { get; set; } = DefaultScript;
  private string? ErrorMessage { get; set; }
  private string? StatusMessage { get; set; }
  private byte[]? MidiBytes { get; set; }
  private bool IsRunning { get; set; }

  private void LoadV2ShowcaseExample()
  {
    ScriptText =
        """
        tempo 120

        pattern arp { up }

        block verse {
            phrase {
                mf
                play arp Cmaj q
            }
        }

        track melody {
            layer piano
            layer cello
            gain 0.9
            humanize 0.02
            play verse
            f
            C5 h
        }

        track harmony {
            instrument piano
            double octave
            Cmaj drop2 h
        }
        """;
    ClearState();
  }

  private void LoadBlocksExample()
  {
    ScriptText =
        """
        block verse { mf C4 q E4 q G4 q }
        block chorus { f C5 q G4 q E4 q }

        track melody {
            instrument piano
            play verse
            play chorus
        }
        """;
    ClearState();
  }

  private void LoadMetadataExample()
  {
    ScriptText =
        """
        tempo 120
        track piano {
            instrument piano
            gain 0.85
            humanize 0.02
            mf
            C4 q E4 q G4 q
        }
        """;
    ClearState();
  }

  private void LoadTempoExample()
  {
    ScriptText =
        """
        time 4/4
        tempo 120 → 160 over 4 bars
        track melody {
            instrument piano
            C4 w
        }
        """;
    ClearState();
  }

  private void LoadLayersExample()
  {
    ScriptText =
        """
        tempo 120
        track piano {
            layer piano
            layer cello
            mf
            Cmaj h
        }
        """;
    ClearState();
  }

  private void LoadHumanizeExample()
  {
    ScriptText =
        """
        tempo 120
        track piano {
            humanize 0.03
            mf
            C4 q D4 q E4 q F4 q
        }
        """;
    ClearState();
  }

  private void LoadAdvancedChordsExample()
  {
    ScriptText =
        """
        tempo 96
        track melody {
            instrument piano
            Cmaj drop2 h
            Cmaj inv1 q
            Cmaj spread q
        }
        """;
    ClearState();
  }

  private void LoadPhrasesExample()
  {
    ScriptText =
        """
        tempo 108
        track melody {
            instrument violin
            phrase {
                curve soft
                transition smooth
                mf
                C4 q E4 q G4 q
            }
            phrase {
                f
                C5 q G4 q E4 q
            }
        }
        """;
    ClearState();
  }

  private void LoadPatternsExample()
  {
    ScriptText =
        """
        tempo 120
        pattern arp { up }
        pattern strumPat { strum }
        pattern rhythm8 { rhythm e e q }

        track melody {
            instrument guitar
            play arp Cmaj q
            play strumPat Cmaj q
            play rhythm8 Dm h
        }
        """;
    ClearState();
  }

  private void LoadOrchestrationExample()
  {
    ScriptText =
        """
        tempo 88
        track harmony {
            instrument piano
            double octave
            reinforce bass
            brighten top
            Cmaj w
        }
        """;
    ClearState();
  }

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
        block phrasea { C5 q D5 q E5 h }
        track melody {
            phrase {
                transition smooth
                mf
                play phrasea
                C4 q
            }
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
            C5 q D5 q E5 h
        }
        track bass {
            instrument bass
            C2 h G2 h
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

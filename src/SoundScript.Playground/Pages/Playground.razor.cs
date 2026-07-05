using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoundScript.Compose;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using SoundScript.Timbre;
using SoundScript.Voice;

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
  private string ComposeText { get; set; } = "Twinkle twinkle little star";
  private string? ErrorMessage { get; set; }
  private string? StatusMessage { get; set; }
  private List<string> WarningMessages { get; set; } = [];
  private byte[]? MidiBytes { get; set; }
  private byte[]? WavBytes { get; set; }
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

  private void LoadV3PhrasesExample()
  {
    ScriptText =
        """
        tempo 108
        pattern arp { up }

        block verse {
            phrase {
                curve gentle
                transition sharp
                crescendo
                articulation legato
                swing 0.67
                mf
                play arp Cmaj q
            }
        }

        track melody {
            instrument violin
            phrase {
                curve swell
                transition expressive
                mf
                C4 q E4 q G4 q
            }
            play verse
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

  private void LoadVoiceExample()
  {
    ScriptText =
        """
        tempo 100

        track accompaniment {
            instrument piano
            Cmaj h Fmaj h
            Cmaj h Gmaj h
        }

        voice lead {
            vocal choir
            mf
            sing "Twinkle twinkle little star" C4 q C4 q G4 q G4 q A4 q A4 q G4 h
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

  private async Task ComposeFromTextAsync()
  {
    try
    {
      IsRunning = true;
      ClearState();

      if (string.IsNullOrWhiteSpace(ComposeText))
      {
        ErrorMessage = "Nothing to compose: the text is empty.";
        return;
      }

      var interpreted = PhonemeComposer.ComposeProgram(ComposeText);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      await Js.InvokeVoidAsync("startPlayback", MidiBytes);

      var syllableCount = PhonemeComposer.SplitSyllables(ComposeText).Count;
      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
      StatusMessage = $"Composed {syllableCount} syllable(s) into {noteCount} note(s) at {interpreted.Tempo} BPM.";
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

  private async Task ComposeWithProsodyAsync()
  {
    try
    {
      IsRunning = true;
      ClearState();

      if (string.IsNullOrWhiteSpace(ComposeText))
      {
        ErrorMessage = "Nothing to compose: the text is empty.";
        return;
      }

      var interpreted = ProsodyComposer.ComposeProgram(ComposeText);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      await Js.InvokeVoidAsync("startPlayback", MidiBytes);

      var syllableCount = WordTokenizer.Tokenize(ComposeText).Sum(w => w.Syllables.Count);
      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
      StatusMessage = $"Composed {syllableCount} syllable(s) into {noteCount} note(s) at {interpreted.Tempo} BPM (word-level prosody).";
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

  private async Task RenderAudioAsync()
  {
    try
    {
      IsRunning = true;
      ClearState();

      if (string.IsNullOrWhiteSpace(ComposeText))
      {
        ErrorMessage = "Nothing to render: the text is empty.";
        return;
      }

      var interpreted = PhonemeComposer.ComposeProgram(ComposeText);
      using var midiStream = new MemoryStream();
      MidiGenerator.Write(interpreted, midiStream);
      MidiBytes = midiStream.ToArray();

      var options = new OfflineRenderer.RenderOptions { SourceText = ComposeText };
      WavBytes = OfflineRenderer.RenderToWavBytes(MidiBytes, OfflineRenderer.DefaultStylesheet, options);

      await Js.InvokeVoidAsync("SoundScriptMidi.stop");
      await Js.InvokeVoidAsync("SoundScriptVoice.stop");
      var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);

      var syllableCount = PhonemeComposer.SplitSyllables(ComposeText).Count;
      StatusMessage =
          $"Rendered {syllableCount} syllable(s) offline to {duration:F1}s of audio at {interpreted.Tempo} BPM (SoundCSS timbre).";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      WavBytes = null;
    }
    finally
    {
      IsRunning = false;
      StateHasChanged();
    }
  }

  private async Task RenderAudioWithProsodyAsync()
  {
    try
    {
      IsRunning = true;
      ClearState();

      if (string.IsNullOrWhiteSpace(ComposeText))
      {
        ErrorMessage = "Nothing to render: the text is empty.";
        return;
      }

      var interpreted = ProsodyComposer.ComposeProgram(ComposeText);
      using var midiStream = new MemoryStream();
      MidiGenerator.Write(interpreted, midiStream);
      MidiBytes = midiStream.ToArray();

      // ProsodyComposer emits its track as "prosody" rather than PhonemeComposer's
      // "phonemes", so the timeline builder is told which track to align phonemes
      // against explicitly instead of relying on the default.
      var options = new OfflineRenderer.RenderOptions { SourceText = ComposeText, PreferredTrackName = "prosody" };
      WavBytes = OfflineRenderer.RenderToWavBytes(MidiBytes, OfflineRenderer.DefaultStylesheet, options);

      await Js.InvokeVoidAsync("SoundScriptMidi.stop");
      await Js.InvokeVoidAsync("SoundScriptVoice.stop");
      var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);

      var syllableCount = WordTokenizer.Tokenize(ComposeText).Sum(w => w.Syllables.Count);
      StatusMessage =
          $"Rendered {syllableCount} syllable(s) offline to {duration:F1}s of audio at {interpreted.Tempo} BPM (SoundCSS timbre, word-level prosody).";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      WavBytes = null;
    }
    finally
    {
      IsRunning = false;
      StateHasChanged();
    }
  }

  private async Task RunAsync()
  {
    try
    {
      IsRunning = true;
      ErrorMessage = null;
      StatusMessage = null;
      WarningMessages = [];
      MidiBytes = null;

      if (SourceDiagnostics.ContainsImport(ScriptText))
        WarningMessages.Add("Imports are not supported in the browser playground. Use the CLI (ProgramLoader) for multi-file projects.");

      var tokens = new Tokenizer(ScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();
      var interpreted = Interpreter.Interpret(program, "playground.ss");
      VocalInterpreter.Apply(program, interpreted);

      foreach (var warning in interpreted.Warnings)
      {
        if (!WarningMessages.Contains(warning))
          WarningMessages.Add(warning);
      }

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);

      await Js.InvokeVoidAsync("startPlayback", MidiBytes);

      var status = $"Playing — {noteCount} note(s) across {interpreted.Tracks.Count} track(s)";
      if (interpreted.VocalTracks.Count > 0)
      {
        status += $", {interpreted.VocalTracks.Sum(v => v.Syllables.Count)} sung syllable(s)";

        var speechWords = VocalSpeechTimeline.Build(interpreted);
        var speechSupported = await Js.InvokeAsync<bool>("SoundScriptVoice.speak", speechWords);
        if (!speechSupported)
          WarningMessages.Add("This browser has no speech synthesis — lyrics play as melody only. The downloaded MIDI still contains the lyric events.");
      }

      StatusMessage = $"{status} at {interpreted.Tempo} BPM.";
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
    await Js.InvokeVoidAsync("SoundScriptVoice.stop");
    await Js.InvokeVoidAsync("SoundScriptAudio.stop");
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

  private async Task DownloadWavAsync()
  {
    if (WavBytes is null)
      return;

    var base64 = Convert.ToBase64String(WavBytes);
    await Js.InvokeVoidAsync("SoundScriptAudio.download", base64, "soundscript.wav");
  }

  private void ClearState()
  {
    ErrorMessage = null;
    StatusMessage = null;
    WarningMessages = [];
    MidiBytes = null;
    WavBytes = null;
  }
}

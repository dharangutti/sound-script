using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoundScript.Compose;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using SoundScript.Timbre;
using SoundScript.Voice;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Prosody;

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
  private string SelectedExampleKey { get; set; } = "v2-showcase";
  private string WaveScriptText { get; set; } = "";
  private string SelectedWaveExampleKey { get; set; } = "wave-effects";
  private string ComposeText { get; set; } = "Twinkle twinkle little star";
  private string? ErrorMessage { get; set; }
  private string? StatusMessage { get; set; }
  private List<string> WarningMessages { get; set; } = [];
  private byte[]? MidiBytes { get; set; }
  private byte[]? WavBytes { get; set; }
  private byte[]? WaveOutputBytes { get; set; }
  private string? SsText { get; set; }
  private bool IsRunning { get; set; }

  // UNDER DEVELOPMENT — v3: true only when the most recent Run actually took
  // the SoundScript.Wave rail (the parsed AST contained an EffectNode or
  // SpeakNode). Output-pane UI that's specific to that rail must gate on
  // this rather than rendering unconditionally — see the wave safeguards
  // doc's grammar-isolation rule.
  private bool UsedWaveBackend { get; set; }

  private PlaygroundPresetInfo? ActiveMainPreset { get; set; }
  private PlaygroundPresetInfo? ActiveWavePreset { get; set; }

  protected override void OnInitialized()
  {
    LoadSelectedExample();
    LoadSelectedWaveExample(syncMainEditor: false);
  }

  private void RefreshMainPresetInfo() =>
      ActiveMainPreset = PlaygroundPresetCatalog.TryGet(SelectedExampleKey);

  private void RefreshWavePresetInfo() =>
      ActiveWavePreset = PlaygroundPresetCatalog.TryGet(SelectedWaveExampleKey);

  private async Task CopyCliLineAsync(string line)
  {
    try
    {
      await Js.InvokeVoidAsync("navigator.clipboard.writeText", line);
      StatusMessage = "Copied CLI command to clipboard.";
    }
    catch
    {
      WarningMessages.Add("Couldn't copy to clipboard — your browser blocked clipboard access.");
    }
  }

  // UNDER DEVELOPMENT — v3: memoizes SoundScript.Wave renders by the exact
  // script text. Safe because the wave backend is fully deterministic and
  // every seed/effect parameter (speak's seed=, humanize's seed=, effect
  // delay/filter params) is textually part of ScriptText — there is no
  // hidden input that could make two identical keys render differently.
  private static readonly WaveRenderOptions PlaygroundWaveOptions = new() { SkipMissingSamples = true };

  private readonly Dictionary<string, byte[]> _waveRenderCache = new();

  private void LoadSelectedExample()
  {
    switch (SelectedExampleKey)
    {
      case "v2-showcase": LoadV2ShowcaseExample(); break;
      case "v2-blocks": LoadBlocksExample(); break;
      case "v2-metadata": LoadMetadataExample(); break;
      case "v2-tempo": LoadTempoExample(); break;
      case "v2-layers": LoadLayersExample(); break;
      case "v2-humanize": LoadHumanizeExample(); break;
      case "v2-chords": LoadAdvancedChordsExample(); break;
      case "v2-phrases": LoadPhrasesExample(); break;
      case "v2-phrases-v3": LoadV3PhrasesExample(); break;
      case "v2-patterns": LoadPatternsExample(); break;
      case "v2-orchestration": LoadOrchestrationExample(); break;
      case "v2-voice": LoadVoiceExample(); break;
      case "core-melody": LoadMelodyExample(); break;
      case "core-articulations": LoadArticulationsExample(); break;
      case "core-dynamics": LoadDynamicsExample(); break;
      case "core-chords": LoadChordsExample(); break;
      case "core-intelligence": LoadIntelligenceExample(); break;
      case "core-multitrack": LoadMultitrackExample(); break;
      case "core-playback": LoadPlaybackExample(); break;
      case "showcase-jingle-bells": LoadJingleBellsExample(); break;
      case "showcase-jingle-bells-wave": LoadJingleBellsWaveExample(); break;
      case "wave-effects": LoadWaveEffectsExample(); break;
      case "wave-speak": LoadWaveSpeakExample(); break;
      case "wave-humanize": LoadWaveHumanizeExample(); break;
      case "wave-effects-combined": LoadWaveEffectsCombinedExample(); break;
      case "wave-full-song": LoadWaveFullSongExample(); break;
      case "speech-only-wave": LoadSpeechOnlyWaveExample(); break;
      case "wave-vocal-stem": LoadWaveVocalStemExample(); break;
      case "jingle-bells-vocal": LoadJingleBellsVocalExample(); break;
    }

    if (PlaygroundPresetCatalog.IsWaveExampleKey(SelectedExampleKey))
    {
      ScriptText = WaveScriptText;
      SelectedWaveExampleKey = SelectedExampleKey;
      RefreshWavePresetInfo();
    }

    RefreshMainPresetInfo();
    ClearState();
    StateHasChanged();
  }

  private void LoadSelectedWaveExample(bool syncMainEditor = true)
  {
    switch (SelectedWaveExampleKey)
    {
      case "wave-effects": LoadWaveEffectsExample(); break;
      case "wave-speak": LoadWaveSpeakExample(); break;
      case "wave-humanize": LoadWaveHumanizeExample(); break;
      case "wave-effects-combined": LoadWaveEffectsCombinedExample(); break;
      case "wave-full-song": LoadWaveFullSongExample(); break;
      case "speech-only-wave": LoadSpeechOnlyWaveExample(); break;
      case "wave-vocal-stem": LoadWaveVocalStemExample(); break;
      case "jingle-bells-vocal": LoadJingleBellsVocalExample(); break;
      case "showcase-jingle-bells-wave": LoadJingleBellsWaveExample(); break;
    }

    if (syncMainEditor)
    {
      ScriptText = WaveScriptText;
      SelectedExampleKey = SelectedWaveExampleKey;
      RefreshMainPresetInfo();
    }

    WaveOutputBytes = null;
    ClearState();
    RefreshWavePresetInfo();
    StateHasChanged();
  }

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

  // A full-arrangement showcase: a genuinely melodious "Jingle Bells" that
  // weaves together most of the MIDI-rail surface — tempo automation, time
  // signature, reusable blocks + play, phrases (curve/transition/crescendo),
  // staccato articulation, numeric/dotted durations, chords with orchestration
  // (double octave + reinforce bass), a strum pattern, layers, gain, named-form
  // humanize (a V7 directive that stays on the MIDI rail), multi-track melody +
  // harmony + bass, and a sung voice track with lyrics aligned to the melody.
  private void LoadJingleBellsExample()
  {
    ScriptText =
        """
        tempo 132 -> 132 over 8 bars
        tempo 132 -> 112 over 4 bars
        time 4/4

        pattern strumPat { strum }

        block hook {
            staccato E4 q staccato E4 q E4 h
            staccato E4 q staccato E4 q E4 h
            staccato E4 q G4 q C4:1.5 D4 e
            E4 h rest h
        }

        block funline {
            F4 q F4 q F4 q F4 q
            F4 q E4 q E4 q E4 q
            E4 q G4 q G4 q F4 q
            D4 q C4 for 3
        }

        track melody {
            layer flute
            layer piano
            gain 0.9
            humanize timing=0.012 velocity=0.06
            phrase {
                curve soft
                transition smooth
                crescendo
                mf
                play hook
            }
            phrase {
                curve swell
                transition expressive
                f
                play funline
            }
            phrase {
                curve fade
                decrescendo
                mf
                play hook
            }
        }

        track harmony {
            instrument piano
            double octave
            reinforce bass
            gain 0.6
            p
            Cmaj w Cmaj w Cmaj w Cmaj w
            Fmaj w Cmaj w Cmaj w
            G7 q Cmaj for 3
            Cmaj w Cmaj w Cmaj w
            play strumPat Cmaj w
        }

        track bass {
            instrument bass
            gain 0.75
            mf
            C2 w | C2 w | C2 w | C2 w |
            F2 w | C2 w | C2 w |
            G2 h C2 h |
            C2 w | C2 w | C2 w | C2 w |
        }

        voice lead {
            vocal choir
            mf
            sing "Jingle bells jingle bells jingle all the way" E4 q E4 q E4 h E4 q E4 q E4 h E4 q G4 q C4:1.5 D4 e E4 h
            rest h
            sing "Oh what fun it is to ride in a one horse open sleigh" F4 q F4 q F4 q F4 q F4 q E4 q E4 q E4 q E4 q G4 q G4 q F4 q D4 q C4 for 3
            sing "Jingle bells jingle bells jingle all the way" E4 q E4 q E4 h E4 q E4 q E4 h E4 q G4 q C4:1.5 D4 e E4 h
        }
        """;
    ClearState();
  }

  // The Wave-rail (.ssw) counterpart of LoadJingleBellsExample — a full
  // four-part "Jingle Bells" that now renders directly to WAV with every part
  // audible. It exercises the multi-part audibility fixes end to end:
  //   - melody notes live inside phrase { } blocks (the phrase bodies are now
  //     entered, not skipped), with `play hook`/`play funline` resolved there;
  //   - the choir line is a real voice { sing "..." } block, rendered on its
  //     explicit per-syllable pitches (phoneme-shaped tones baked into the WAV);
  //   - the harmony ends on `play strumPat Cmaj w`, a strummed chord.
  // Plus the wave-native surface: named-form humanize with an explicit seed=,
  // and the wave-only master effect chain (a light delay for sleigh-bell
  // shimmer, then a gentle lowpass). Fully deterministic — every seed/effect
  // parameter is textual.
  //
  // Still deferred on this rail (captured but not shaping the audio): per-track
  // instrument/layer timbre, gain balancing, phrase curve/transition/crescendo
  // shaping, vocal choir coloring, and general pattern arpeggio *sequencing*
  // (only the strum-a-chord form of `play <pattern> <chord>` is honored). The
  // parts are all audible; they aren't yet timbrally distinct or mix-balanced.
  private void LoadJingleBellsWaveExample()
  {
    WaveScriptText =
        """
        tempo 132
        time 4/4

        pattern strumPat { strum }

        block hook {
            E4 q E4 q E4 h
            E4 q E4 q E4 h
            E4 q G4 q C4:1.5 D4 e
            E4 h rest h
        }

        block funline {
            F4 q F4 q F4 q F4 q
            F4 q E4 q E4 q E4 q
            E4 q G4 q G4 q F4 q
            D4 q C4 for 3
        }

        track melody {
            humanize timing=0.012 velocity=0.06 seed=7
            phrase {
                curve soft
                transition smooth
                mf
                play hook
            }
            phrase {
                curve swell
                f
                play funline
            }
            phrase {
                curve fade
                mf
                play hook
            }
        }

        track harmony {
            p
            Cmaj w Cmaj w Cmaj w Cmaj w
            Fmaj w Fmaj w G7 w Cmaj w
            Cmaj w Cmaj w G7 w
            play strumPat Cmaj w
        }

        track bass {
            mf
            C2 w C2 w C2 w C2 w
            F2 w F2 w G2 w C2 w
            C2 w C2 w G2 w C2 w
        }

        voice lead {
            vocal choir
            mf
            sing "Jingle bells jingle bells jingle all the way" E4 q E4 q E4 h E4 q E4 q E4 h E4 q G4 q C4:1.5 D4 e E4 h
            rest h
            sing "Oh what fun it is to ride in a one horse open sleigh" F4 q F4 q F4 q F4 q F4 q E4 q E4 q E4 q E4 q G4 q G4 q F4 q D4 q C4 for 3
            sing "Jingle bells jingle bells jingle all the way" E4 q E4 q E4 h E4 q E4 q E4 h E4 q G4 q C4:1.5 D4 e E4 h
        }

        effect delay time=0.18 feedback=0.25 mix=0.2
        effect filter type=lowpass cutoff=3200
        """;
  }

  // UNDER DEVELOPMENT — v3: wave-only examples. These use grammar
  // (effect/speak, humanize's named form) that the MIDI backend rejects with
  // a clear error — Run routes them through SoundScript.Wave automatically
  // because their AST contains an EffectNode/SpeakNode (see RunAsync).
  private void LoadWaveEffectsExample()
  {
    WaveScriptText =
        """
        tempo 100
        track melody {
            mf
            C4 q E4 q G4 q C5 h
        }
        effect delay time=0.25 feedback=0.35 mix=0.3
        effect filter type=lowpass cutoff=2200
        """;
  }

  private void LoadWaveSpeakExample()
  {
    WaveScriptText =
        """
        tempo 100
        speak "hello world" seed=7
        """;
  }

  private void LoadWaveHumanizeExample()
  {
    WaveScriptText =
        """
        tempo 120
        track melody {
            humanize timing=0.02 velocity=0.1 seed=42
            mf
            C4 q D4 q E4 q F4 q
        }
        speak "played by hand" seed=42
        """;
  }

  private void LoadWaveEffectsCombinedExample()
  {
    WaveScriptText =
        """
        tempo 110

        track lead {
            humanize timing=0.01 velocity=0.05 seed=42
            mf
            C4 q
            E4 q
            G4 h
        }

        speak "hello from soundscript wave" voice=default seed=7

        effect delay time=0.2 feedback=0.4 mix=0.35
        effect filter type=lowpass cutoff=3000
        """;
  }

  private void LoadWaveFullSongExample()
  {
    WaveScriptText =
        """
        tempo 132
        time 4/4

        block hook {
            E4 q E4 q E4 h
            E4 q E4 q E4 h
            E4 q G4 q C4 q D4 q
            E4 w
        }

        pattern strumPat {
            strum
        }

        track melody {
            instrument violin
            mf
            phrase {
                curve soft
                transition smooth
                play hook
            }
            tempo 132 → 112 over 4 bars
            phrase {
                transition abrupt
                f
                E4 q G4 q C4:1.5 D4 e
                E4 w
            }
        }

        track harmony {
            instrument piano
            p
            Cmaj w Cmaj w
            Cmaj w Gmaj w
            play strumPat Cmaj w
            Fmaj w Cmaj w
        }

        track bass {
            instrument bass
            mf
            C2 w C2 w
            C2 w G2 w
            C2 w
            F2 w C2 w
        }

        voice choirline {
            vocal choir
            mf
            sing "Jingle bells jingle bells" E4 q E4 q E4 h E4 q E4 q E4 h
            sing "Jingle all the way" E4 q G4 q C4 q D4 q E4 w
        }
        """;
  }

  private void LoadSpeechOnlyWaveExample()
  {
    WaveScriptText =
        """
        tempo 100
        time 4/4

        voice narrator {
            mf
            sing "Hello from SoundScript Wave" C4 q D4 q E4 q F4 q
            sing "Speech without a MIDI step" G4 q F4 q E4 w
        }

        speak "This line uses prosody tones in the WAV" seed=7

        track pad {
            p
            Cmaj w Gmaj w
        }
        """;
  }

  private void LoadWaveVocalStemExample()
  {
    WaveScriptText =
        """
        tempo 120
        time 4/4

        track pad {
            p
            Cmaj w Gmaj w
        }

        speak "Hello from my vocal stem" seed=7
        """;
  }

  private void LoadJingleBellsVocalExample()
  {
    WaveScriptText =
        """
        tempo 132
        time 4/4

        block hook {
            E4 q E4 q E4 h
            E4 q E4 q E4 h
            E4 q G4 q C4 q D4 q
            E4 w
        }

        pattern strumPat {
            strum
        }

        track melody {
            instrument violin
            mf
            phrase {
                curve soft
                transition smooth
                play hook
            }
        }

        track harmony {
            instrument piano
            p
            Cmaj w Cmaj w
            Cmaj w Gmaj w
            play strumPat Cmaj w
            Fmaj w Cmaj w
        }

        track bass {
            instrument bass
            mf
            C2 w C2 w
            C2 w G2 w
            C2 w
            F2 w C2 w
        }

        speak "Jingle bells jingle bells" seed=7 gain=1.1
        speak "Jingle all the way" seed=8 gain=1.1

        effect delay time=0.18 feedback=0.25 mix=0.2
        effect filter type=lowpass cutoff=3200
        """;
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

      var ast = PhonemeComposer.BuildAst(ComposeText);
      SsText = SsPrinter.Print(ast);
      var interpreted = Interpreter.Interpret(ast);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      await TryPlayAsync(() => Js.InvokeVoidAsync("startPlayback", MidiBytes).AsTask());

      var syllableCount = PhonemeComposer.SplitSyllables(ComposeText).Count;
      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
      StatusMessage = $"Composed {syllableCount} syllable(s) into {noteCount} note(s) at {interpreted.Tempo} BPM.";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      MidiBytes = null;
      SsText = null;
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

      var ast = ProsodyComposer.BuildAst(ComposeText);
      SsText = SsPrinter.Print(ast);
      var interpreted = Interpreter.Interpret(ast);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      MidiBytes = stream.ToArray();

      await TryPlayAsync(() => Js.InvokeVoidAsync("startPlayback", MidiBytes).AsTask());

      var syllableCount = WordTokenizer.Tokenize(ComposeText).Sum(w => w.Syllables.Count);
      var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
      StatusMessage = $"Composed {syllableCount} syllable(s) into {noteCount} note(s) at {interpreted.Tempo} BPM (word-level prosody).";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      MidiBytes = null;
      SsText = null;
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

      var syllableCount = PhonemeComposer.SplitSyllables(ComposeText).Count;
      try
      {
        await Js.InvokeVoidAsync("SoundScriptMidi.stop");
        await Js.InvokeVoidAsync("SoundScriptVoice.stop");
        var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) offline to {duration:F1}s of audio at {interpreted.Tempo} BPM (SoundCSS timbre).";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) offline at {interpreted.Tempo} BPM (SoundCSS timbre). Playback failed on this device, but you can still download the WAV.";
      }
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

      var syllableCount = WordTokenizer.Tokenize(ComposeText).Sum(w => w.Syllables.Count);
      try
      {
        await Js.InvokeVoidAsync("SoundScriptMidi.stop");
        await Js.InvokeVoidAsync("SoundScriptVoice.stop");
        var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) offline to {duration:F1}s of audio at {interpreted.Tempo} BPM (SoundCSS timbre, word-level prosody).";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) offline at {interpreted.Tempo} BPM (SoundCSS timbre, word-level prosody). Playback failed on this device, but you can still download the WAV.";
      }
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

  private async Task RenderWaveFromTextAsync()
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

      var ast = PhonemeComposer.BuildAst(ComposeText);
      WavBytes = WaveRenderer.RenderStereoToBytes(ast, PlaygroundWaveOptions);
      UsedWaveBackend = true;

      var syllableCount = PhonemeComposer.SplitSyllables(ComposeText).Count;
      var noteCount = AstToNoteEventAdapter.Convert(ast).Values.Sum(t => t.Count);
      var tempo = Interpreter.Interpret(ast).Tempo;

      try
      {
        await Js.InvokeVoidAsync("SoundScriptMidi.stop");
        await Js.InvokeVoidAsync("SoundScriptVoice.stop");
        var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) into {noteCount} note(s) via SoundScript.Wave to {duration:F1}s of audio at {tempo} BPM (no MIDI step).";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) via SoundScript.Wave at {tempo} BPM (no MIDI step). Playback failed on this device, but you can still download the WAV.";
      }
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

  private async Task RenderWaveWithProsodyAsync()
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

      var ast = ProsodyComposer.BuildAst(ComposeText);
      WavBytes = WaveRenderer.RenderStereoToBytes(ast, PlaygroundWaveOptions);
      UsedWaveBackend = true;

      var syllableCount = WordTokenizer.Tokenize(ComposeText).Sum(w => w.Syllables.Count);
      var noteCount = AstToNoteEventAdapter.Convert(ast).Values.Sum(t => t.Count);
      var tempo = Interpreter.Interpret(ast).Tempo;

      try
      {
        await Js.InvokeVoidAsync("SoundScriptMidi.stop");
        await Js.InvokeVoidAsync("SoundScriptVoice.stop");
        var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) into {noteCount} note(s) via SoundScript.Wave to {duration:F1}s of audio at {tempo} BPM (word-level prosody, no MIDI step).";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage =
            $"Rendered {syllableCount} syllable(s) via SoundScript.Wave at {tempo} BPM (word-level prosody, no MIDI step). Playback failed on this device, but you can still download the WAV.";
      }
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
      WavBytes = null;
      UsedWaveBackend = false;

      if (SourceDiagnostics.ContainsImport(ScriptText))
        WarningMessages.Add("Imports are not supported in the browser playground. Use the CLI (ProgramLoader) for multi-file projects.");

      var tokens = new Tokenizer(ScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();

      // Grammar-isolation rule (safeguards doc): the wave rail only kicks in
      // when the AST actually contains a wave-only directive. Every other
      // .ss script takes the unchanged MIDI path below — Interpreter.Interpret
      // would otherwise throw NotSupportedException on EffectNode/SpeakNode.
      if (ContainsWaveOnlyDirectives(program.Statements))
      {
        await RunWaveProgramAsync(program);
        return;
      }

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

      var status = $"Playing — {noteCount} note(s) across {interpreted.Tracks.Count} track(s)";
      if (interpreted.VocalTracks.Count > 0)
        status += $", {interpreted.VocalTracks.Sum(v => v.Syllables.Count)} sung syllable(s)";

      // Playback is decoupled from compilation: MidiBytes is already computed
      // above, so a playback failure (common on mobile — AudioContext autoplay
      // restrictions, no speech synthesis, WebAudio quirks) must not wipe the
      // bytes the user can still download. Warn instead of erroring, and never
      // rethrow — the outer catch is for real compile/parse failures only.
      try
      {
        await Js.InvokeVoidAsync("startPlayback", MidiBytes);

        if (interpreted.VocalTracks.Count > 0)
        {
          var speechWords = VocalSpeechTimeline.Build(interpreted);
          var speechSupported = await Js.InvokeAsync<bool>("SoundScriptVoice.speak", speechWords);
          if (!speechSupported)
            WarningMessages.Add("This browser has no speech synthesis — lyrics play as melody only. The downloaded MIDI still contains the lyric events.");
        }

        StatusMessage = $"{status} at {interpreted.Tempo} BPM.";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage = $"Compiled {noteCount} note(s) across {interpreted.Tracks.Count} track(s) at {interpreted.Tempo} BPM. Playback failed on this device, but you can still download the MIDI.";
      }
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      MidiBytes = null;
      WavBytes = null;
    }
    finally
    {
      IsRunning = false;
      StateHasChanged();
    }
  }

  // UNDER DEVELOPMENT — v3: renders and plays a program through
  // SoundScript.Wave (AST -> WAV, no MIDI step) instead of the MIDI rail.
  // Reuses the exact same WAV playback bridge (SoundScriptAudio/
  // startWavPlayback) that the Timbre "Render Audio" buttons use below, so
  // mobile Safari/Chrome's audio-unlock handling isn't duplicated or
  // bypassed — the click that invoked Run is the unlock gesture.
  private async Task RunWaveProgramAsync(ProgramNode program)
  {
    // Deterministic by construction: every seed/effect parameter that could
    // affect output (speak's seed=, humanize's seed=, effect delay/filter
    // params) is textual, inside ScriptText — so keying the cache on the
    // exact script text can't produce a stale hit for a changed seed.
    if (!_waveRenderCache.TryGetValue(ScriptText, out var wav))
    {
      wav = WaveRenderer.RenderStereoToBytes(program, PlaygroundWaveOptions);
      _waveRenderCache[ScriptText] = wav;
    }

    WavBytes = wav;
    UsedWaveBackend = true;

    // Playback-only speech overlay: `speak "..."` still renders as deterministic
    // prosody tones baked into the WAV above (untouched — WAV bytes are
    // identical), but we ALSO trigger real browser speech synthesis in parallel
    // so the phrase is actually spoken aloud. Invoked AFTER startWavPlayback so
    // the Run click stays the audio-unlock gesture on mobile Safari/Chrome.
    var speechWords = WaveSpeechTimeline.Build(program);

    // Decoupled from the render above: WavBytes is already set, so a playback
    // failure on this device must not cost the user the downloadable WAV. Warn
    // and swallow — do not rethrow into RunAsync's outer catch, which nulls bytes.
    try
    {
      await Js.InvokeVoidAsync("SoundScriptMidi.stop");
      await Js.InvokeVoidAsync("SoundScriptVoice.stop");
      var duration = await Js.InvokeAsync<double>("startWavPlayback", WavBytes);

      var status = $"Playing — rendered {duration:F1}s of audio via SoundScript.Wave (deterministic, no MIDI step)";

      if (speechWords.Count > 0)
      {
        status += $", speaking {speechWords.Count} phrase(s)";

        var speechSupported = await Js.InvokeAsync<bool>("SoundScriptVoice.speak", speechWords);
        if (!speechSupported)
          WarningMessages.Add("This browser has no speech synthesis — 'speak' text plays as prosody tones only.");
      }

      StatusMessage = $"{status}.";
    }
    catch (Exception)
    {
      WarningMessages.Add("Playback failed on this device, but you can still download the file.");
      StatusMessage = "Rendered audio via SoundScript.Wave (deterministic, no MIDI step). Playback failed on this device, but you can still download the WAV.";
    }
  }

  private async Task RunWaveAsync()
  {
    try
    {
      IsRunning = true;
      ErrorMessage = null;
      StatusMessage = null;
      WarningMessages = [];
      WaveOutputBytes = null;

      if (string.IsNullOrWhiteSpace(WaveScriptText))
      {
        ErrorMessage = "Nothing to render: the script is empty.";
        return;
      }

      var tokens = new Tokenizer(WaveScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();

      if (!_waveRenderCache.TryGetValue(WaveScriptText, out var wav))
      {
        wav = WaveRenderer.RenderStereoToBytes(program, PlaygroundWaveOptions);
        _waveRenderCache[WaveScriptText] = wav;
      }

      WaveOutputBytes = wav;

      var speechWords = WaveSpeechTimeline.Build(program);

      try
      {
        await Js.InvokeVoidAsync("SoundScriptMidi.stop");
        await Js.InvokeVoidAsync("SoundScriptVoice.stop");
        var duration = await Js.InvokeAsync<double>("startWavPlayback", WaveOutputBytes);

        var status = $"Playing — rendered {duration:F1}s of audio via SoundScript.Wave (deterministic, no MIDI step)";

        if (speechWords.Count > 0)
        {
          status += $", speaking {speechWords.Count} phrase(s)";

          var speechSupported = await Js.InvokeAsync<bool>("SoundScriptVoice.speak", speechWords);
          if (!speechSupported)
            WarningMessages.Add("This browser has no speech synthesis — 'speak' text plays as prosody tones only.");
        }

        StatusMessage = $"{status}.";
      }
      catch (Exception)
      {
        WarningMessages.Add("Playback failed on this device, but you can still download the file.");
        StatusMessage = "Rendered audio via SoundScript.Wave (deterministic, no MIDI step). Playback failed on this device, but you can still download the WAV.";
      }
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      StatusMessage = null;
      WaveOutputBytes = null;
    }
    finally
    {
      IsRunning = false;
      StateHasChanged();
    }
  }

  // UNDER DEVELOPMENT — v3: grammar-isolation check (safeguards doc) —
  // recurses into every statement container the parser actually nests
  // speak/effect under, so a directive buried in a block/sequence/loop body
  // isn't missed and silently falls through to the MIDI path, where
  // Interpreter.Interpret would throw NotSupportedException.
  private static bool ContainsWaveOnlyDirectives(IReadOnlyList<AstNode> statements)
  {
    foreach (var statement in statements)
    {
      switch (statement)
      {
        case EffectNode:
        case SpeakNode speak when string.IsNullOrWhiteSpace(speak.SamplePath):
        case SampleNode:
          return true;
        case TrackNode track:
          if (ContainsWaveOnlyDirectives(track.Body)) return true;
          break;
        case MelodyNode melody:
          if (ContainsWaveOnlyDirectives(melody.Body)) return true;
          break;
        case LoopNode loop:
          if (ContainsWaveOnlyDirectives(loop.Body)) return true;
          break;
        case BlockNode block:
          if (ContainsWaveOnlyDirectives(block.Body)) return true;
          break;
        case SequenceNode sequence:
          if (ContainsWaveOnlyDirectives(sequence.Body)) return true;
          break;
        case PhraseNode phrase:
          if (ContainsWaveOnlyDirectives(phrase.Body)) return true;
          break;
      }
    }

    return false;
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

  private async Task DownloadSsAsync()
  {
    if (SsText is null)
      return;

    var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(SsText));
    await Js.InvokeVoidAsync("SoundScriptText.download", base64, "soundscript.ss");
  }

  private async Task DownloadWaveOutputAsync()
  {
    if (WaveOutputBytes is null)
      return;

    var base64 = Convert.ToBase64String(WaveOutputBytes);
    await Js.InvokeVoidAsync("SoundScriptAudio.download", base64, "soundscript-wave.wav");
  }

  // Playback wrapper: runs the given JS audio call and, on failure (common on
  // mobile — autoplay restrictions, missing speech synthesis, WebAudio quirks),
  // warns instead of surfacing an error and NEVER nulls the already-computed
  // bytes. The user keeps the file they just compiled and can still download it.
  private async Task TryPlayAsync(Func<Task> play)
  {
    try
    {
      await play();
    }
    catch (Exception)
    {
      WarningMessages.Add("Playback failed on this device, but you can still download the file.");
    }
  }

  // Always-available download for whatever is currently in the editor, whether
  // or not Run/Compose has been clicked or succeeded. Compiles fresh from
  // ScriptText with no playback step, so mobile users get a file even when audio
  // fails. Routes wave-only grammar through SoundScript.Wave (WAV), everything
  // else through the MIDI rail — the same split RunAsync uses.
  private async Task DownloadCurrentScriptAsync()
  {
    if (string.IsNullOrWhiteSpace(ScriptText))
    {
      ErrorMessage = "Nothing to download: the script is empty.";
      return;
    }

    try
    {
      ErrorMessage = null;

      var tokens = new Tokenizer(ScriptText).Tokenize();
      var program = new SoundScript.Parser.Parser(tokens).Parse();

      if (ContainsWaveOnlyDirectives(program.Statements))
      {
        var wav = WaveRenderer.RenderStereoToBytes(program, PlaygroundWaveOptions);
        var wavBase64 = Convert.ToBase64String(wav);
        await Js.InvokeVoidAsync("SoundScriptAudio.download", wavBase64, "soundscript.wav");
        StatusMessage = "Downloaded current script as WAV.";
        return;
      }

      var interpreted = Interpreter.Interpret(program, "playground.ss");
      VocalInterpreter.Apply(program, interpreted);

      using var stream = new MemoryStream();
      MidiGenerator.Write(interpreted, stream);
      var midiBase64 = Convert.ToBase64String(stream.ToArray());
      await Js.InvokeVoidAsync("SoundScriptMidi.download", midiBase64, "soundscript.mid");
      StatusMessage = "Downloaded current script as MIDI.";
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  private void ClearEditor()
  {
    ScriptText = "";
    ClearState();
  }

  private void ResetEditor()
  {
    ScriptText = DefaultScript;
    ClearState();
  }

  private async Task CopyScriptAsync()
  {
    try
    {
      await Js.InvokeVoidAsync("navigator.clipboard.writeText", ScriptText);
      StatusMessage = "Copied to clipboard.";
    }
    catch (Exception)
    {
      WarningMessages.Add("Couldn't copy to clipboard — your browser blocked clipboard access.");
    }
  }

  private void ClearState()
  {
    ErrorMessage = null;
    StatusMessage = null;
    WarningMessages = [];
    MidiBytes = null;
    WavBytes = null;
    SsText = null;
    UsedWaveBackend = false;
  }
}

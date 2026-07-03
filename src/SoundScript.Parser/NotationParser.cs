using SoundScript.Core;
using SoundScript.Core.Notation;

namespace SoundScript.Parser;

/// <summary>
/// Notation validation and parsing helpers for pitch, accidentals, and durations.
/// </summary>
public static class NotationParser
{
  private const int MinOctave = 0;
  private const int MaxOctave = 8;

  /// <summary>Parses pitch letter and optional accidental from a note token.</summary>
  public static (PitchClass PitchClass, AccidentalType Accidental, int Octave) ParsePitchWithAccidental(
      string text,
      Token token)
  {
    if (text.Length == 0)
      throw InvalidNoteToken(text, token);

    var pitchChar = text[0];
    if (!TryParsePitchClass(pitchChar, out var pitchClass))
      throw UnknownPitchName(pitchChar, token);

    var index = 1;
    var accidental = AccidentalType.None;

    if (index < text.Length)
    {
      var symbol = text[index];
      if (IsSharpSymbol(symbol))
      {
        accidental = AccidentalType.Sharp;
        index++;
      }
      else if (IsFlatSymbol(symbol))
      {
        accidental = AccidentalType.Flat;
        index++;
      }
      else if (IsNaturalSymbol(symbol))
      {
        accidental = AccidentalType.Natural;
        index++;
      }
    }

    if (index < text.Length && IsAccidentalSymbol(text[index]))
      throw InvalidAccidentalSyntax(text, token);

    if (index >= text.Length || !int.TryParse(text[index..], out var octave))
      throw InvalidNoteToken(text, token);

    if (octave < MinOctave || octave > MaxOctave)
      throw InvalidOctave(octave, token);

    return (pitchClass, accidental, octave);
  }

  /// <summary>Maps short and long duration tokens to the canonical enum and beat value.</summary>
  public static (NoteDuration Duration, double Beats) ParseDurationAlias(string value, Token token)
  {
    if (IsRepeatedSingleLetterDuration(value))
      throw UnknownDuration(value, token);

    return value.ToLowerInvariant() switch
    {
      "q" or "quarter" => (NoteDuration.Quarter, NoteDuration.Quarter.ToBeats()),
      "h" or "half" => (NoteDuration.Half, NoteDuration.Half.ToBeats()),
      "e" or "eighth" => (NoteDuration.Eighth, NoteDuration.Eighth.ToBeats()),
      "w" or "whole" => (NoteDuration.Whole, NoteDuration.Whole.ToBeats()),
      _ => throw UnknownDuration(value, token)
    };
  }

  /// <summary>Builds a <see cref="NotatedNote"/> from validated pitch and duration data.</summary>
  public static NotatedNote BuildNotatedNote(
      PitchClass pitchClass,
      AccidentalType accidental,
      int octave,
      double durationBeats,
      NoteDuration? standardDuration = null,
      double startTime = 0.0)
  {
    return new NotatedNote
    {
      PitchClass = pitchClass,
      Accidental = accidental,
      Octave = octave,
      DurationBeats = durationBeats,
      StandardDuration = standardDuration,
      StartTime = startTime
    };
  }

  /// <summary>Builds a <see cref="NotatedNote"/> directly from a note token string.</summary>
  public static NotatedNote BuildNotatedNote(
      string text,
      Token token,
      double durationBeats = 1.0,
      NoteDuration? standardDuration = null,
      ArticulationType? articulation = null)
  {
    var (pitchClass, accidental, octave) = ParsePitchWithAccidental(text, token);
    return BuildNotatedNote(pitchClass, accidental, octave, durationBeats, standardDuration) with
    {
      Articulation = articulation
    };
  }

  /// <summary>Parses a rest statement duration from the token following <c>rest</c>.</summary>
  public static NotatedRest ParseRest(double durationBeats, NoteDuration? standardDuration = null, double startTime = 0.0) =>
      new()
      {
        DurationBeats = durationBeats,
        StandardDuration = standardDuration,
        StartTime = startTime
      };

  /// <summary>Validates and merges two tied notes into a single sustained pitch.</summary>
  public static NotatedNote ParseTie(NotatedNote source, NotatedNote target, Token token)
  {
    if (!source.MatchesPitch(target))
      throw Invalid(token, "Invalid tie: pitches differ");

    var tie = new NotatedTie
    {
      Source = source,
      Target = target
    };

    return source with
    {
      DurationBeats = tie.CombinedDurationBeats,
      StandardDuration = null,
      Articulation = target.Articulation ?? source.Articulation,
      IsTied = true
    };
  }

  /// <summary>Maps an articulation token to the canonical enum.</summary>
  public static ArticulationType ParseArticulation(string value, Token token) =>
      value.ToLowerInvariant() switch
      {
        "staccato" => ArticulationType.Staccato,
        "legato" => ArticulationType.Legato,
        "accent" => ArticulationType.Accent,
        _ => throw Invalid(token, $"Unknown articulation: '{value}'")
      };

  /// <summary>Maps a dynamic marking token to the canonical enum.</summary>
  public static DynamicLevel ParseDynamic(string value, Token token) =>
      value.ToLowerInvariant() switch
      {
        "p" => DynamicLevel.Piano,
        "mp" => DynamicLevel.MezzoPiano,
        "mf" => DynamicLevel.MezzoForte,
        "f" => DynamicLevel.Forte,
        _ => throw Invalid(token, $"Unknown dynamic marking: '{value}'")
      };

  /// <summary>
  /// Validates measure durations against a time signature. Returns warnings only.
  /// </summary>
  public static IReadOnlyList<string> ValidateMeasure(
      IReadOnlyList<double> measureDurations,
      int numerator,
      int denominator)
  {
    var warnings = new List<string>();
    var expectedBeats = numerator * (4.0 / denominator);

    for (var i = 0; i < measureDurations.Count; i++)
    {
      var actual = measureDurations[i];
      var measureNumber = i + 1;

      if (actual > expectedBeats + 0.0001)
        warnings.Add($"Measure {measureNumber} exceeds expected duration");

      if (actual < expectedBeats - 0.0001)
        warnings.Add($"Measure {measureNumber} incomplete: expected {expectedBeats} beats, got {TrimBeat(actual)}");
    }

    return warnings;
  }

  /// <summary>Rejects invalid rest duration tokens such as <c>qq</c>.</summary>
  public static void ValidateRestDurationToken(string value, Token token)
  {
    if (IsRepeatedSingleLetterDuration(value))
      throw Invalid(token, $"Invalid rest duration: 'rest {value}'");
  }

  /// <summary>Detects mistyped articulation tokens such as <c>sharpstaccato</c>.</summary>
  public static bool TryGetInvalidArticulationMessage(string text, out string message)
  {
    message = string.Empty;
    foreach (var articulation in new[] { "staccato", "legato", "accent" })
    {
      if (text.Contains(articulation, StringComparison.OrdinalIgnoreCase)
          && !text.Equals(articulation, StringComparison.OrdinalIgnoreCase))
      {
        message = $"Unknown articulation: '{text}'";
        return true;
      }
    }

    return false;
  }

  /// <summary>Detects unsupported dynamic markings such as <c>fff</c>.</summary>
  public static bool TryGetInvalidDynamicMessage(string text, out string message)
  {
    message = string.Empty;
    if (text.Equals("p", StringComparison.OrdinalIgnoreCase)
        || text.Equals("mp", StringComparison.OrdinalIgnoreCase)
        || text.Equals("mf", StringComparison.OrdinalIgnoreCase)
        || text.Equals("f", StringComparison.OrdinalIgnoreCase))
      return false;

    if (text.All(static c => c is 'p' or 'P' or 'f' or 'F') && text.Length > 1)
    {
      message = $"Unknown dynamic marking: '{text}'";
      return true;
    }

    return false;
  }

  /// <summary>Detects identifier tokens that look like invalid note spellings.</summary>
  public static bool TryGetInvalidNoteMessage(string text, out string message)
  {
    message = string.Empty;
    if (text.Length == 0)
      return false;

    if (TryParsePitchClass(text[0], out _))
      return TryGetInvalidPitchNoteMessage(text, out message);

    if (!char.IsLetter(text[0]))
      return false;

    if (!LooksLikeNoteSpelling(text))
      return false;

    message = $"Unknown pitch name: {char.ToUpperInvariant(text[0])}";
    return true;
  }

  private static bool TryGetInvalidPitchNoteMessage(string text, out string message)
  {
    message = string.Empty;
    var index = 1;
    var accidentalCount = 0;

    while (index < text.Length && IsAccidentalSymbol(text[index]))
    {
      accidentalCount++;
      index++;
    }

    if (accidentalCount > 1)
    {
      message = $"Invalid accidental syntax: '{text}'";
      return true;
    }

    if (index < text.Length && IsAccidentalSymbol(text[index]))
    {
      message = $"Invalid accidental syntax: '{text}'";
      return true;
    }

    if (index >= text.Length)
      return false;

    if (!int.TryParse(text[index..], out var octave))
    {
      message = $"Invalid note token: '{text}'";
      return true;
    }

    if (octave < MinOctave || octave > MaxOctave)
    {
      message = $"Invalid octave: {octave}";
      return true;
    }

    return false;
  }

  private static bool LooksLikeNoteSpelling(string text)
  {
    if (text.Length < 2)
      return false;

    var index = 1;
    while (index < text.Length && IsAccidentalSymbol(text[index]))
      index++;

    return index < text.Length && char.IsDigit(text[index]);
  }

  private static bool TryParsePitchClass(char pitchChar, out PitchClass pitchClass)
  {
    pitchClass = char.ToUpperInvariant(pitchChar) switch
    {
      'C' => PitchClass.C,
      'D' => PitchClass.D,
      'E' => PitchClass.E,
      'F' => PitchClass.F,
      'G' => PitchClass.G,
      'A' => PitchClass.A,
      'B' => PitchClass.B,
      _ => default
    };

    return char.ToUpperInvariant(pitchChar) is 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G';
  }

  private static bool IsSingleLetterDuration(char value) =>
      value is 'q' or 'h' or 'e' or 'w';

  private static bool IsRepeatedSingleLetterDuration(string value) =>
      value.Length > 1 && value.All(ch => ch == value[0]) && IsSingleLetterDuration(value[0]);

  private static bool IsSharpSymbol(char value) => value is '#' or '\u266F';

  private static bool IsFlatSymbol(char value) => value is 'b' or 'B' or '\u266D';

  private static bool IsNaturalSymbol(char value) => value is '\u266E';

  private static bool IsAccidentalSymbol(char value) =>
      IsSharpSymbol(value) || IsFlatSymbol(value) || IsNaturalSymbol(value);

  private static InvalidOperationException UnknownPitchName(char pitchChar, Token token) =>
      Invalid(token, $"Unknown pitch name: {char.ToUpperInvariant(pitchChar)}");

  private static InvalidOperationException InvalidOctave(int octave, Token token) =>
      Invalid(token, $"Invalid octave: {octave}");

  private static InvalidOperationException UnknownDuration(string value, Token token) =>
      Invalid(token, $"Unknown duration: '{value}'");

  private static InvalidOperationException InvalidAccidentalSyntax(string text, Token token) =>
      Invalid(token, $"Invalid accidental syntax: '{text}'");

  private static InvalidOperationException InvalidNoteToken(string text, Token token) =>
      Invalid(token, $"Invalid note token: '{text}'");

  private static string TrimBeat(double beats) =>
      beats.ToString(beats % 1 == 0 ? "0" : "0.##");

  private static InvalidOperationException Invalid(Token token, string message) =>
      new($"{message} (line {token.Line}, column {token.Column}).");
}

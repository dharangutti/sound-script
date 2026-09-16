namespace SoundScript.Core;

/// <summary>Unpitched playback classes, not MIDI pitches or inferred resonances.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<PercussionSound>))]
public enum PercussionSound { Kick, Snare, Hat, Click }

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

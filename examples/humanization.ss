// Music: apply deterministic timing and velocity variation to a short piano line.
tempo 120

track piano {
    instrument piano
    // MIDI humanization uses a fixed seed so repeated renders keep the same variation.
    humanize 0.03
    mf
    C4 q
    D4 q
    E4 q
    F4 q
    G4 h
}

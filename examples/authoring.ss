// Beginner: name a tempo and a rest length with compile-time constants.
let songTempo = 108
let phraseLength = 2

tempo songTempo
track authoring {
    instrument piano
    C4 q E4 q G4 q C5 q
    // This rest is measured in beats, not seconds.
    rest for phraseLength
    C5 q G4 q E4 q C4 q
}

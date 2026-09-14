// Constants and comments are additive authoring helpers.
let songTempo = 108
let phraseLength = 2

tempo songTempo
track authoring {
    instrument piano
    // Reusable scalar constants are resolved during compilation.
    C4 q E4 q G4 q C5 q
    rest for phraseLength
    C5 q G4 q E4 q C4 q
}

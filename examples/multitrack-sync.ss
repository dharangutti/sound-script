// Music: align melody, harmony, and bass on the same four-beat timeline.
tempo 120

// Every track starts at beat zero; note counts may differ while the four-beat span stays aligned.
track melody {
    instrument flute
    C5 q
    D5 q
    E5 q
    F5 q
}

track harmony {
    instrument piano
    Cmaj q
    Fmaj q
    G7 q
    Cmaj q
}

track bass {
    instrument bass
    C2 h
    G2 h
}

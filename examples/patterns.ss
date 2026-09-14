// Music: apply arpeggio, strum, and rhythm patterns to chords.
tempo 120

pattern arp {
    up
}

pattern strumPat {
    strum
}

pattern rhythm8 {
    rhythm e e q
}

track melody {
    instrument guitar
    play arp Cmaj q
    play strumPat Cmaj q
    play rhythm8 Dm h
}

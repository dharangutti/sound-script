import "import-lib.ss"

time 4/4
tempo 120 → 140 over 2 bars

block verse {
    phrase {
        curve balanced
        transition smooth
        mf
        play arp Cmaj q
    }
}

pattern arp {
    up
}

track melody {
    instrument flute
    layer flute
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
    reinforce bass
    Cmaj drop2 h
    Fmaj inv1 h
}

track bass {
    instrument bass
    play bassline
}

import "import-lib.ss"

time 4/4
tempo 120

block verse {
    phrase {
        mf
        play intro
    }
}

pattern arp {
    updown
}

track melody {
    instrument flute
    layer flute
    layer cello
    gain 0.9
    humanize 0.02
    play verse
    play arp Cmaj q
    f
    staccato C5 q
    legato D5 q
    accent E5 q
}

track harmony {
    instrument piano
    double octave
    reinforce bass
    Cmaj drop2 h
    Fmaj spread h
}

track bass {
    instrument bass
    play bassline
}

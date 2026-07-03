tempo 120
time 4/4

sequence intro {
    mf
    Cmaj q
    Dm q
    G7 q
}

track melody {
    instrument flute
    play intro
    f
    staccato C5 q
    legato D5 q
    accent E5 q
    C5 q ~ C5 q
}

track harmony {
    instrument piano
    Cmaj q
    Fmaj q
    G7 q
    Cmaj h
}

track bass {
    instrument bass
    velocity 90
    C2 h
    G2 h
}

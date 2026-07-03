tempo 120
time 4/4
instrument piano

sequence intro {
    Cmaj4 q
    Dm q
    G7 q
}

track melody {
    instrument flute
    play intro
    loop 2 {
        C5 q v100
        D5 q v80
    }
}

track bass {
    instrument bass
    velocity 90
    C2 h
    G2 h
}

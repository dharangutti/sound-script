tempo 132
time 4/4

block hook {
    E4 q E4 q E4 h
    E4 q E4 q E4 h
    E4 q G4 q C4 q D4 q
    E4 w
}

pattern strumPat {
    strum
}

track melody {
    instrument violin
    mf
    phrase {
        curve soft
        transition smooth
        play hook
    }
    tempo 132 → 112 over 4 bars
    phrase {
        transition abrupt
        f
        E4 q G4 q C4:1.5 D4 e
        E4 w
    }
}

track harmony {
    instrument piano
    p
    Cmaj w Cmaj w
    Cmaj w Gmaj w
    play strumPat Cmaj w
    Fmaj w Cmaj w
}

track bass {
    instrument bass
    mf
    C2 w C2 w
    C2 w G2 w
    C2 w
    F2 w C2 w
}

voice choirline {
    vocal choir
    mf
    sing "Jingle bells jingle bells" E4 q E4 q E4 h E4 q E4 q E4 h
    sing "Jingle all the way" E4 q G4 q C4 q D4 q E4 w
}

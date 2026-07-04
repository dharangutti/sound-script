tempo 100
instrument organ

block idle {
    phrase {
        curve gentle
        articulation legato
        mp
        C4 q E4 q G4 h
    }
}

block running {
    phrase {
        curve strong
        articulation accent
        mf
        C3 e G3 e C4 e G3 e
    }
}

block critical {
    phrase {
        transition sharp
        articulation staccato
        f
        G4 e G4 e G4 e rest e G4 e G4 e G4 e
    }
}

track machine {
    play idle
    play running
    play critical
}

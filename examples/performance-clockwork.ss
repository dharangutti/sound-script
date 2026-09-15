// Clockwork Garden — original scherzo for SoundScript.
// Fast, deliberately detached notes: expressive mode must preserve the pulse.
perform expressive
tempo 144
time 4/4
track tune {
    instrument piano
    mf
    phrase {
        articulation staccato
        C5 e E5 e G5 e E5 e D5 e F5 e A5 e F5 e |
        E5 e G5 e C6 e G5 e B5 e G5 e D5 e G5 e |
        A5 e F5 e C5 e F5 e G5 e E5 e C5 e E5 e |
        D5 e G5 e B4 e D5 e C5 q rest q |
    }
    phrase {
        articulation staccato
        E5 e E5 e G5 e E5 e F5 e F5 e A5 e F5 e |
        G5 e E5 e C5 e E5 e A5 e F5 e D5 e F5 e |
        G5 e D5 e B4 e D5 e F5 e D5 e B4 e G4 e |
        C5 q E5 q C5 q rest q |
    }
}
track bass {
    instrument bass
    p
    phrase {
        articulation staccato
        C3 q G2 q D3 q G2 q | C3 q G2 q G2 q D3 q |
        F2 q C3 q C3 q G2 q | G2 q D3 q C3 q rest q |
        C3 q G2 q F2 q C3 q | C3 q G2 q F2 q C3 q |
        G2 q D3 q G2 q D3 q | C3 q G2 q C3 q rest q |
    }
}

// Harbor Lights — original cinematic ballad, composed for SoundScript.
// A singing flute above a quiet broken-chord accompaniment; no borrowed melody.
perform expressive
tempo 72
time 4/4
track melody {
    instrument flute
    mp
    phrase {
        articulation legato
        curve expressive
        B4 q A4 q G4 h | D5 h B4 h | A4 q G4 q E4 h | G4 h rest h |
    }
    phrase {
        articulation legato
        crescendo
        G4 q B4 q D5 h | E5 h D5 q B4 q |
    }
    phrase {
        articulation legato
        decrescendo
        A4 h F#4 q A4 q | G4 w |
    }
}
track piano {
    instrument piano
    velocity 48
    G3 q D4 q B3 q D4 q | G3 q D4 q B3 q D4 q |
    C3 q G3 q E4 q G3 q | C3 q G3 q E4 q G3 q |
    E3 q B3 q G4 q B3 q | C3 q G3 q E4 q G3 q |
    D3 q A3 q F#4 q A3 q | G3 h B3 q D4 q |
}
track bass {
    instrument cello
    p
    G2 w | G2 w | C3 w | C3 w | E3 w | C3 w | D3 w | G2 w |
}

// Floating Lanterns — original sustained ensemble miniature for SoundScript.
// Layered flute/violin sustain evolves continuously; explicit rests are breaths.
perform expressive
tempo 64
time 4/4
track upper {
    layer flute
    layer violin
    p
    phrase {
        articulation legato
        crescendo
        E4 w | G4 w | A4 w | B4 h rest h |
    }
    phrase {
        articulation legato
        decrescendo
        C5 w | B4 h G4 h | A4 h F#4 h | E4 w |
    }
}
track lower {
    instrument cello
    p
    phrase {
        articulation legato
        crescendo
        E3 w | G3 w | A3 w | B3 h rest h |
    }
    phrase {
        articulation legato
        decrescendo
        A3 w | G3 w | B2 w | E3 w |
    }
}

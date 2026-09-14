// Integration / Export: contrast swing, push, and pull as conveyor-drift audio cues.
tempo 120
instrument synth

// Repeating the same pitches isolates the timing contrast between the three phrases.
track conveyor {
    phrase {
        swing 0.67
        mf
        C3 e C3 e C3 e C3 e C3 e C3 e C3 e C3 e
    }
    phrase {
        push 0.02
        mf
        C3 e C3 e C3 e C3 e C3 e C3 e C3 e C3 e
    }
    phrase {
        pull 0.03
        f
        C3 e C3 e C3 e C3 e C3 e C3 e C3 e C3 e
    }
}

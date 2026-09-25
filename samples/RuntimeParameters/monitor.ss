param intensity = 0.25
param xpos = 200

perform expressive
tempo 120
track cue {
    gain intensity
    C4 q E4 q G4 h
}
visual "indicator" for 4s {
    shape circle
    fill "#ef4444"
    set x xpos
    set y 300
    set width 100
    set height 100
    set opacity intensity
}

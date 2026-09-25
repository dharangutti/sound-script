param intensity = 0.25
param xpos = 200

perform expressive
tempo 120
track cue {
    instrument piano
    gain intensity
    C4 q E4 q G4 h
}
visual "indicator" for 4s {
    shape circle
    fill "#16a34a"
    set x xpos
    set y 360
    set width 120
    set height 120
    set opacity intensity
}

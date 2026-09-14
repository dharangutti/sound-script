// Integration / Export: resolve a local library and reuse its melody and bass definitions.
import "import-lib.ss"

tempo 120

track melody {
    instrument flute
    play intro
    C5 h
}

track bass {
    instrument bass
    play bassline
}

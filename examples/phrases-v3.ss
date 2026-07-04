tempo 108
instrument violin

block verse {
    phrase {
        curve gentle
        transition sharp
        crescendo
        articulation legato
        swing 0.67
        mf
        play arp Cmaj q
    }
}

pattern arp { up }

track melody {
    phrase {
        curve soft
        transition smooth
        mf
        C4 q
        E4 q
        G4 q
    }
    phrase {
        curve swell
        transition expressive
        mf
        C4 q E4 q G4 q
    }
    phrase {
        curve fade
        decrescendo
        f
        C5 q G4 q E4 q
    }
    play verse
}

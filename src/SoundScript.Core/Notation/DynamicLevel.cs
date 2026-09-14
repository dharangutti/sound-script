namespace SoundScript.Core.Notation;

/// <summary>Standard dynamic markings mapped to MIDI velocity.</summary>
public enum DynamicLevel
{
    Piano,
    MezzoPiano,
    MezzoForte,
    Forte,
    Pianissimo,
    Fortissimo,
    Sforzando,
    Fortepiano
}

public static class DynamicLevelExtensions
{
    public static int ToVelocity(this DynamicLevel level) => level switch
    {
        DynamicLevel.Piano => 48,
        DynamicLevel.MezzoPiano => 64,
        DynamicLevel.MezzoForte => 80,
        DynamicLevel.Forte => 96,
        DynamicLevel.Pianissimo => 32,
        DynamicLevel.Fortissimo => 112,
        DynamicLevel.Sforzando => 120,
        DynamicLevel.Fortepiano => 104,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };
}

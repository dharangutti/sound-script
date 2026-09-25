namespace SoundScript.RuntimeLab;

/// <summary>All Lab parameters are exact decimals; ranges are the intersection of their targets.</summary>
public sealed record RuntimeParameter(string Name, decimal Default, decimal Minimum, decimal Maximum)
{
    public Type ValueType => typeof(decimal);

    internal void Validate(decimal value)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(Name, value, $"Expected {Minimum} through {Maximum}.");
    }
}

/// <summary>Measured calls through the Lab compilation boundary, not renderer internals.</summary>
public sealed class CompilationCounters
{
    public int Tokenizations { get; internal set; }
    public int Parses { get; internal set; }
    public int TimelineCompilations { get; internal set; }
}

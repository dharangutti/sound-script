using SoundScript.Vocal.Wordbank;
using Xunit;

namespace SoundScript.Tests;

public sealed class EspeakFactAttribute : FactAttribute
{
    public EspeakFactAttribute()
    {
        if (!new EspeakRawSynthesizer().IsAvailable)
            Skip = "Real eSpeak integration requires espeak-ng or espeak on PATH.";
    }
}

// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Evaluates an <see cref="Adsr"/> amplitude curve as a pure function of
/// <c>(t, noteDuration) -&gt; amplitude</c>, where <c>t</c> is seconds since
/// the note started. Handles notes shorter than Attack+Decay by scaling
/// those two segments down proportionally so the curve still reaches 1.0
/// before easing toward sustain — never negative time, never a
/// divide-by-zero on a zero-length segment.
/// </summary>
public static class Envelope
{
    public static double Amplitude(Adsr adsr, double t, double noteDuration)
    {
        if (t < 0.0)
            return 0.0;

        var attack = Math.Max(0.0, adsr.Attack);
        var decay = Math.Max(0.0, adsr.Decay);
        var release = Math.Max(0.0, adsr.Release);
        var sustain = Math.Clamp(adsr.Sustain, 0.0, 1.0);
        var duration = Math.Max(0.0, noteDuration);

        var attackDecay = attack + decay;
        if (attackDecay > duration && attackDecay > 0.0)
        {
            var scale = duration / attackDecay;
            attack *= scale;
            decay *= scale;
        }

        if (t < attack)
            return attack <= 0.0 ? 1.0 : t / attack;

        if (t < attack + decay)
            return decay <= 0.0 ? sustain : 1.0 - (t - attack) / decay * (1.0 - sustain);

        if (t < duration)
            return sustain;

        var releaseElapsed = t - duration;
        if (release <= 0.0 || releaseElapsed >= release)
            return 0.0;

        return sustain * (1.0 - releaseElapsed / release);
    }
}

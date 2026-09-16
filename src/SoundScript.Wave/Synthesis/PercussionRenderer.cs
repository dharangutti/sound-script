using SoundScript.Core;

namespace SoundScript.Wave.Synthesis;

/// <summary>Deterministic synthetic kit; no note oscillator or acoustic pitch estimate.</summary>
public static class PercussionRenderer
{
    public static double TailSeconds(PercussionSound sound) => sound switch
    {
        PercussionSound.Kick => .3, PercussionSound.Snare => .2, PercussionSound.Hat => .09,
        PercussionSound.Click => .04, _ => throw new ArgumentOutOfRangeException(nameof(sound))
    };

    public static float[] Render(PercussionSound sound, double duration, double velocity, int sampleRate)
    {
        if (!Enum.IsDefined(sound) || sampleRate <= 0 || !double.IsFinite(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(sound));
        double tail = TailSeconds(sound);
        var buffer = new float[(int)Math.Ceiling((duration + tail) * sampleRate)];
        uint seed = 773;
        double previous = 0, low = 0;
        double alpha = 1 - Math.Exp(-2 * Math.PI * 1600 / sampleRate);
        for (int i = 0; i < buffer.Length; i++)
        {
            double t = i / (double)sampleRate;
            seed = unchecked(seed * 1664525 + 1013904223);
            double noise = seed / (double)uint.MaxValue * 2 - 1;
            low += alpha * (noise - low);
            double sample = sound switch
            {
                PercussionSound.Kick => Math.Sin(2 * Math.PI * (48 * t + 3 * (1 - Math.Exp(-t * 30)))) * Math.Exp(-t * 22),
                PercussionSound.Snare => (low * 1.6 + .2 * Math.Sin(2 * Math.PI * 180 * t)) * Math.Exp(-t * 32),
                PercussionSound.Hat => (noise - previous) * .6 * Math.Exp(-t * 65),
                _ => noise * Math.Exp(-t * 130)
            };
            buffer[i] = (float)(sample * Math.Clamp(velocity, 0, 1) * .75);
            previous = noise;
        }
        return buffer;
    }
}

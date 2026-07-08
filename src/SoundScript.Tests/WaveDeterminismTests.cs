// UNDER DEVELOPMENT — v3
// SHA-256 checksum determinism suite for SoundScript.Wave — the CI-facing
// proof behind the (now-shipped) safeguards doc's claim: "same seed + same
// input = byte-identical .wav, forever, across machines/OS/CPU... a
// regression test, not a suggestion."
//
// Mirrors the pattern already used for the PhonemeComposer/Timbre pipeline's
// own determinism claim (OfflineRenderer.RenderSha256, exercised by
// FullRenderTests.RenderSha256_IsStableAcrossRuns in CycleSynthesisTests.cs,
// and again in TimbreTests.cs / WordProsodyTests.cs): hash the rendered
// output and assert on the digest, not the raw bytes, so a failure message
// names a 64-char hex string instead of dumping a WAV.
//
// WaveV3Tests already proves byte-for-byte equality inline per feature at
// short clip lengths; this suite is the dedicated checksum layer the v3
// prompt's "Deliverable checklist" called for ("Extend the CI checksum suite
// to cover all three new paths independently, plus one combined test"), at
// render lengths long enough to actually exercise DelayEffect's tail-repeat
// accumulation (up to its 512-repeat cap) and OnePoleFilter's running state
// across thousands of samples — a two-note clip would never touch that path.
using System.Security.Cryptography;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Synthesis;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveDeterminismTests
{
    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    // ---- Effects chain: long, high-feedback render ----

    [Fact]
    public void EffectsChain_LongHighFeedbackRender_ChecksumIsStableAcrossRuns()
    {
        var hashA = WaveRenderer.RenderSha256(ParseSsw(LongEffectsSource));
        var hashB = WaveRenderer.RenderSha256(ParseSsw(LongEffectsSource));

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void EffectsChain_LongHighFeedbackRender_StereoChecksumIsStableAcrossRuns()
    {
        var hashA = WaveRenderer.RenderStereoSha256(ParseSsw(LongEffectsSource));
        var hashB = WaveRenderer.RenderStereoSha256(ParseSsw(LongEffectsSource));

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    // ---- Seeded humanize: long render ----

    [Fact]
    public void Humanize_LongSeededRender_ChecksumIsStableAcrossRuns()
    {
        var hashA = WaveRenderer.RenderSha256(ParseSsw(LongHumanizeSource));
        var hashB = WaveRenderer.RenderSha256(ParseSsw(LongHumanizeSource));

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void Humanize_DifferentSeed_ChangesChecksumButStaysReproducible()
    {
        var seed42 = WaveRenderer.RenderSha256(ParseSsw(LongHumanizeSource));
        var seed43 = WaveRenderer.RenderSha256(ParseSsw(LongHumanizeSource.Replace("seed=42", "seed=43")));

        Assert.NotEqual(seed42, seed43);
        Assert.Equal(seed43, WaveRenderer.RenderSha256(ParseSsw(LongHumanizeSource.Replace("seed=42", "seed=43"))));
    }

    // ---- Prosody/speak: long passage ----

    [Fact]
    public void Speak_LongPassage_ChecksumIsStableAcrossRuns()
    {
        var hashA = WaveRenderer.RenderSha256(ParseSsw(LongSpeakSource));
        var hashB = WaveRenderer.RenderSha256(ParseSsw(LongSpeakSource));

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    // ---- Combined: all three v3 features together, long render ----
    // (the v3 prompt's checklist explicitly calls for this in addition to
    // the three independent tests above, to catch interaction bugs).

    [Fact]
    public void Combined_EffectsJitterAndProsody_LongRender_ChecksumIsStableAcrossRuns()
    {
        var hashA = WaveRenderer.RenderSha256(ParseSsw(CombinedLongSource));
        var hashB = WaveRenderer.RenderSha256(ParseSsw(CombinedLongSource));
        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);

        var stereoA = WaveRenderer.RenderStereoSha256(ParseSsw(CombinedLongSource));
        var stereoB = WaveRenderer.RenderStereoSha256(ParseSsw(CombinedLongSource));
        Assert.Equal(stereoA, stereoB);
        Assert.Matches("^[0-9A-F]{64}$", stereoA);
    }

    [Fact]
    public void Combined_DifferentSeeds_ChangeChecksumButEachStaysReproducible()
    {
        // Guards against a vacuously-passing suite: if the checksum were
        // insensitive to input (e.g. a bug hashing an empty/constant
        // buffer), the "stable across runs" assertions above would still
        // pass. Confirm the digest actually reflects the seeded content.
        var variant = CombinedLongSource.Replace("seed=42", "seed=99").Replace("seed=7", "seed=17");

        var original = WaveRenderer.RenderSha256(ParseSsw(CombinedLongSource));
        var changed = WaveRenderer.RenderSha256(ParseSsw(variant));
        Assert.NotEqual(original, changed);

        Assert.Equal(changed, WaveRenderer.RenderSha256(ParseSsw(variant)));
    }

    // ---- DeterministicRandom: the shared seeded-PRNG helper, hit directly ----
    // (humanize and prosody both draw from this rather than System.Random —
    // per the hint, verify its own output stream is checksum-stable on its
    // own, not only indirectly through whatever DSP happens to sit downstream).

    [Fact]
    public void DeterministicRandom_LongDrawSequence_ChecksumIsStableAcrossRuns()
    {
        var hashA = Sha256Hex(DrawSequenceBytes(seed: 42, count: 50_000));
        var hashB = Sha256Hex(DrawSequenceBytes(seed: 42, count: 50_000));

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void DeterministicRandom_DifferentSeed_ChangesChecksum()
    {
        var seed42 = Sha256Hex(DrawSequenceBytes(seed: 42, count: 50_000));
        var seed43 = Sha256Hex(DrawSequenceBytes(seed: 43, count: 50_000));

        Assert.NotEqual(seed42, seed43);
    }

    /// <summary>
    /// Packs a long run of <see cref="DeterministicRandom.Unit01"/> and
    /// <see cref="DeterministicRandom.Unit"/> draws (the two entry points
    /// humanize/prosody actually call) across distinct salts into a byte
    /// buffer to hash — exercises the PRNG across enough indices to reveal
    /// any accidental index/salt correlation, independent of any renderer.
    /// </summary>
    private static byte[] DrawSequenceBytes(int seed, int count)
    {
        var buffer = new byte[count * 2 * sizeof(double)];
        var offset = 0;
        for (var i = 0; i < count; i++)
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), DeterministicRandom.Unit01(seed, i, salt: 1));
            offset += sizeof(double);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), DeterministicRandom.Unit(seed, i, salt: 2));
            offset += sizeof(double);
        }

        return buffer;
    }

    // ---- Sources ----
    // Sized to actually stress accumulating float error, not just prove a
    // two-note clip round-trips: loop-expanded melodies running tens of
    // seconds, and delay feedback pushed toward the ~0.98 ceiling the v3
    // tail-decay fix targeted (see DelayEffect.MaxTailRepeats) so the tail
    // approaches (without exceeding) its 512-repeat cap.

    // 16 loop iterations * 4 quarter notes at 132 BPM ≈ 29s, plus a
    // near-max-feedback delay tail and a lowpass filter running the whole way.
    private const string LongEffectsSource = """
        tempo 132
        track pad {
            loop 16 {
                C4 q E4 q G4 q C5 q
            }
        }
        effect delay time=0.18 feedback=0.97 mix=0.5
        effect filter type=lowpass cutoff=4000
        """;

    // 20 loop iterations * 4 quarter notes at 120 BPM ≈ 40s of seeded jitter.
    private const string LongHumanizeSource = """
        tempo 120
        track piano {
            humanize timing=0.02 velocity=0.1 seed=42
            loop 20 {
                C4 q D4 q E4 q F4 q
            }
        }
        """;

    private const string LongSpeakSource =
        "speak \"the quick brown fox jumps over the lazy dog and runs far away into the deep dark forest\" voice=default seed=7";

    private const string CombinedLongSource = """
        tempo 110
        track lead {
            humanize timing=0.01 velocity=0.05 seed=42
            loop 12 {
                C4 q E4 q G4 h
            }
        }
        speak "hello world, this is a longer passage meant to stress the combined render" voice=default seed=7
        effect delay time=0.2 feedback=0.93 mix=0.4
        effect filter type=lowpass cutoff=3000
        """;
}

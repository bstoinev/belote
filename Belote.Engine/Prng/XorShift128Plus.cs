using System.Buffers.Binary;

namespace Belote.Engine.Prng;

/// <summary>
/// Deterministic PRNG with stable output across platforms/runtimes.
/// This is intentionally not <see cref="Random"/> to ensure replay stability.
/// </summary>
public sealed class XorShift128Plus
{
    private ulong _s0;
    private ulong _s1;

    public XorShift128Plus(int seed)
    {
        // SplitMix64-style seeding for good diffusion from a 32-bit seed.
        var z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
        _s0 = SplitMix64(ref z);
        _s1 = SplitMix64(ref z);
        if (_s0 == 0 && _s1 == 0)
        {
            _s1 = 1;
        }
    }

    public ulong NextUInt64()
    {
        var s1 = _s0;
        var s0 = _s1;
        _s0 = s0;
        s1 ^= s1 << 23;
        _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
        var result = _s1 + s0;
        return result;
    }

    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax), exclusiveMax, "Must be > 0.");
        }

        // Rejection sampling for uniform distribution.
        var limit = (ulong.MaxValue / (ulong)exclusiveMax) * (ulong)exclusiveMax;
        ulong value;
        do
        {
            value = NextUInt64();
        } while (value >= limit);

        var result = (int)(value % (ulong)exclusiveMax);
        return result;
    }

    public void Shuffle<T>(T[] items)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static Guid DeterministicGuidFromSeed(int seed)
    {
        var rng = new XorShift128Plus(seed);
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[..8], rng.NextUInt64());
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], rng.NextUInt64());
        var guid = new Guid(bytes);
        return guid;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        var result = z ^ (z >> 31);
        return result;
    }
}


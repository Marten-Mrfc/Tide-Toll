namespace TideToll.Sim;

/// <summary>
/// Seeded random numbers, ported bit for bit from the browser toy
/// (prototypes/desire-lines/src/sim/rng.ts) so both give the same runs from the same seed.
/// A struct with no hidden state beyond one uint, so a subsystem can own one and copy it.
/// </summary>
public struct Mulberry32
{
    private uint _a;

    public Mulberry32(uint seed) => _a = seed;

    /// <summary>The raw state, for saving and restoring.</summary>
    public uint State { readonly get => _a; set => _a = value; }

    /// <summary>Next value in [0, 1).</summary>
    public double NextDouble()
    {
        unchecked
        {
            _a += 0x6d2b79f5u;
            uint t = _a;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}

public static class Hash
{
    /// <summary>
    /// Stateless hash of two integers to [0, 1), identical to the toy's hash2.
    /// Used for per-(walker, cell) taste and step noise without storing it.
    /// </summary>
    public static double Hash2(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967296.0;
        }
    }
}

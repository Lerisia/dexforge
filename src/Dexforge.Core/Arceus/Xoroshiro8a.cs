namespace Dexforge.Arceus;

/// <summary>
/// xoroshiro128+ as Legends: Arceus uses it: the second word starts at the fixed 0x82A2B175229D6A5B, and Rand(n) masks the
/// output to the next power of two and draws again while it is not under n.
/// </summary>
public struct Xoroshiro8a
{
    public const ulong Fixed = 0x82A2B175229D6A5B;
    public ulong S0, S1;

    public Xoroshiro8a(ulong seed, ulong second = Fixed) { S0 = seed; S1 = second; }

    public static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong Next()
    {
        ulong a = S0, b = S1, result = a + b;
        b ^= a;
        S0 = Rotl(a, 24) ^ b ^ (b << 16);
        S1 = Rotl(b, 37);
        return result;
    }

    public static ulong Mask(ulong n)
    {
        ulong m = n - 1;
        m |= m >> 1; m |= m >> 2; m |= m >> 4; m |= m >> 8; m |= m >> 16; m |= m >> 32;
        return m;
    }

    public ulong Rand(ulong n)
    {
        ulong m = Mask(n);
        while (true) { ulong v = Next() & m; if (v < n) return v; }
    }

    /// <summary>The state after this many draws, with no draw made: the state is a linear function of the seed.</summary>
    public static (ulong s0, ulong s1) StateAfter(ulong seed, ulong second, int draws)
    {
        var x = new Xoroshiro8a(seed, second);
        for (int i = 0; i < draws; i++) x.Next();
        return (x.S0, x.S1);
    }
}

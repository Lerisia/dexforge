namespace AlolaDexMaker.Sword;

/// <summary>
/// xoroshiro128+ as Sword/Shield calls it for eggs: state[1] is the fixed 0x82A2B175229D6A5B, and Next(max) masks the
/// output to the next power of two and rerolls until it is under max (Admiral Fish, RNGWriteups/Gen 8/Egg Generation.md).
/// </summary>
public struct Xoroshiro
{
    public const ulong Fixed = 0x82A2B175229D6A5B;
    private ulong s0, s1;

    public Xoroshiro(ulong seed, ulong second = Fixed)
    {
        s0 = seed;
        s1 = second;
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextState()
    {
        ulong a = s0, b = s1;
        ulong result = a + b;
        b ^= a;
        s0 = Rotl(a, 24) ^ b ^ (b << 16);
        s1 = Rotl(b, 37);
        return result;
    }

    public static ulong NextPowerTwoMask(ulong num)
    {
        if ((num & (num - 1)) == 0) return num - 1;
        ulong result = 1;
        while (result < num) result <<= 1;
        return result - 1;
    }

    /// <summary>A value in [0, max): masked, rerolled while too large.</summary>
    public ulong Next(ulong max)
    {
        ulong mask = NextPowerTwoMask(max);
        ulong result = NextState() & mask;
        while (result >= max) result = NextState() & mask;
        return result;
    }

    public uint NextUInt(uint max) => (uint)Next(max);
}

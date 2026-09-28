using PKHeX.Core;

// The arithmetic of the older games, written out as the games do it.
namespace AlolaDexMaker;

public static class Old
{
    public static uint Next(uint s) => s * 0x41C64E6Du + 0x6073u;
    public static uint Advance(uint s, uint n) => LCRNG.Advance(s, (int)n);

    public record Spread(uint Origin, uint Pid, int[] Ivs, int Rejected);

    static int[] Ivs(uint iv1, uint iv2) =>
        [(int)(iv1 & 31), (int)(iv1 >> 5 & 31), (int)(iv1 >> 10 & 31), (int)(iv2 >> 5 & 31), (int)(iv2 >> 10 & 31), (int)(iv2 & 31)]; // HP Atk Def SpA SpD Spe

    // Method 1: personality low half, high half, then the two halves of the individual values.
    public static Spread Method1(uint state)
    {
        var a = Next(state); var b = Next(a); var c = Next(b); var d = Next(c);
        return new(state, (b >> 16 << 16) | (a >> 16), Ivs(c >> 16, d >> 16), 0);
    }

    // Method J (Diamond, Pearl, Platinum): the nature is drawn first, and personalities are drawn until one has it.
    public static Spread MethodJ(uint state)
    {
        var s = Next(state);
        uint nature = (s >> 16) / 0xA3E;
        uint origin, pid; int rejected = -1;
        do
        {
            origin = s;
            var a = Next(s); var b = Next(a);
            pid = (b >> 16 << 16) | (a >> 16);
            s = b; rejected++;
        } while (pid % 25 != nature);
        var c = Next(s); var d = Next(c);
        return new(origin, pid, Ivs(c >> 16, d >> 16), rejected);
    }

    public static bool IsShiny34(ushort tid, ushort sid, uint pid) => (tid ^ sid ^ (pid >> 16) ^ (pid & 0xFFFF)) < 8;

    // Emerald, FireRed, LeafGreen: the visible id seeds the generator, and the secret id is what it gives some frames later.
    public static ushort SecretId3(ushort tid, uint frames) => (ushort)(Advance(tid, frames + 1) >> 16);

    // Diamond, Pearl, Platinum, HeartGold, SoulSilver: both ids are the second output of a Mersenne Twister seeded as the game is.
    public static (ushort Tid, ushort Sid) Ids4(uint seed)
    {
        var mt = new uint[624];
        mt[0] = seed;
        for (uint i = 1; i < 624; i++) mt[i] = 1812433253u * (mt[i - 1] ^ (mt[i - 1] >> 30)) + i;
        uint Out(int i)
        {
            uint y = (mt[i] & 0x80000000u) | (mt[i + 1] & 0x7FFFFFFFu);
            uint v = mt[i + 397] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908B0DFu : 0);
            v ^= v >> 11; v ^= (v << 7) & 0x9D2C5680u; v ^= (v << 15) & 0xEFC60000u; v ^= v >> 18;
            return v;
        }
        uint second = Out(1);
        return ((ushort)second, (ushort)(second >> 16));
    }
}

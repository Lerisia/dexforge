using System.Collections.Concurrent;
using System.Numerics;
using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>
/// The generator seeds whose second output is a given fixed seed. The second output is x0 + x1 of the state one step in,
/// and x1 alone gives the seed back by rotation, so the search guesses the low three bits of every byte of x1 (2^24 cases)
/// and derives the rest byte-slice by byte-slice from the subtraction, as pla-reverse's generator_seed_shader.cl does on a
/// GPU; this is that shader on the CPU, about 30 ms on a few cores. A sum loses information, so two fifths of fixed seeds
/// have no generator seed at all and some have several.
/// </summary>
public static class Generator8a
{
    static ulong X0FromX1(ulong x1) { x1 = BitOperations.RotateLeft(x1, 27); return BitOperations.RotateLeft(Xoroshiro128Plus.XOROSHIRO_CONST, 24) ^ x1 ^ (x1 << 16) ^ BitOperations.RotateLeft(x1, 24); }
    static ulong SeedFromX1(ulong x1) => BitOperations.RotateLeft(x1, 27) ^ Xoroshiro128Plus.XOROSHIRO_CONST;

    // one bit per byte, spread from the eight bits of the index
    static readonly ulong[] Slices = Enumerable.Range(0, 256).Select(i => { ulong v = 0; for (int b = 0; b < 8; b++) if ((i >> b & 1) != 0) v |= 1UL << (8 * b); return v; }).ToArray();

    public static List<ulong> Of(ulong fixedSeed)
    {
        var found = new ConcurrentBag<ulong>();
        Parallel.For(0, 256, x =>
        {
            for (int y = 0; y < 256; y++)
            for (int z = 0; z < 256; z++)
            {
                ulong x1s = Slices[x] | (Slices[y] << 1) | (Slices[z] << 2);
                ulong x0s = X0FromX1(x1s) & 0x3838383838383838;
                ulong sub = fixedSeed - x0s - x1s;
                ulong base3 = sub & 0x0808080808080808;
                ulong changed = ((sub - 0xc0c0c0c0c0c0c0c0) ^ sub) & 0x0808080808080808;
                // bit 3 of each byte may have borrowed from below: try every subset of the bytes where it might have
                ulong bytes = changed >> 3 & 0x0101010101010101;
                int n = System.Numerics.BitOperations.PopCount(bytes);
                for (int m = 0; m < (1 << n); m++)
                {
                    ulong part = 0; int bi = 0;
                    for (int by = 0; by < 8; by++)
                        if ((bytes >> (8 * by) & 1) != 0) { if ((m >> bi & 1) != 0) part |= 1UL << (8 * by + 3); bi++; }
                    ulong x1s3 = base3 ^ part;
                    ulong x0s3 = X0FromX1(x1s3) & 0x4040404040404040;
                    ulong x0 = x0s | x0s3, x1 = x1s | x1s3;
                    ulong x1s4 = (fixedSeed - x0 - x1) & 0x1010101010101010;
                    ulong x0s4 = X0FromX1(x1s4) & 0x8080808080808080;
                    x0 |= x0s4; x1 |= x1s4;
                    ulong x1s567 = (fixedSeed - x0 - x1) & 0xe0e0e0e0e0e0e0e0;
                    ulong x0s567 = X0FromX1(x1s567) & 0x0707070707070707;
                    x0 |= x0s567; x1 |= x1s567;
                    if (x0 + x1 == fixedSeed) found.Add(SeedFromX1(x1));
                }
            }
        });
        // only a seed that walks forward to this fixed seed counts; the slices can agree by accident
        return found.Distinct().Where(g => Spawn8a.FromGenerator(g).fixedSeed == fixedSeed).OrderBy(g => g).ToList();
    }
}

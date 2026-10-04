namespace Dexforge.Arceus;

/// <summary>
/// Seeds whose four size draws come out as asked, found by linear algebra instead of search.
/// <para>
/// The game draws height as rand(0x81) + rand(0x80) and weight the same way, right after the nature. rand(0x81) is the low
/// byte of an output (it is under 0x81 or drawn again) and rand(0x80) its low seven bits, and an output is s0 + s1, whose
/// low bits depend only on the low bits of s0 and s1. The state is a GF(2)-linear function of the seed (the update is
/// shifts, rotates and xors; only the output adds), so once the low byte of s0 at each of the four draws is chosen freely
/// and the low byte of s1 is set to what the sum needs, the seed is the solution of sixty linear equations in its 64 bits.
/// </para>
/// <para>
/// The draws sit at a known position only when nothing before them drew again: a shiny found at roll j, the guaranteed
/// IVs placed without a collision, and a nature or gender draw that was not rejected. A sample is therefore drawn in full
/// afterwards and kept only if it really is what was asked; about three in four are.
/// </para>
/// </summary>
public sealed class SizeSeeds8a
{
    const int Equations = 60;
    readonly int[] widths = [8, 7, 8, 7];
    readonly int[] targets;
    readonly ulong[] reduced = new ulong[Equations];   // the rows after elimination, pivot rows first
    readonly int[] pivotColumn = new int[Equations];
    readonly int rank;
    readonly bool[] reducedOffset = new bool[Equations];
    readonly int[][] combination;                       // which original equations each reduced row is the sum of
    readonly ulong[] nullspace;

    /// <summary>The smallest Pokémon: height 0 and weight 0.</summary>
    public static int[] Smallest => [0, 0, 0, 0];
    /// <summary>The largest a non-alpha can be: 0x80 + 0x7F = 255 for both.</summary>
    public static int[] Largest => [0x80, 0x7F, 0x80, 0x7F];

    /// <param name="position">the index of the height draw among the seed's draws</param>
    /// <param name="targets">what the four draws must give: rand(0x81), rand(0x80), rand(0x81), rand(0x80)</param>
    public SizeSeeds8a(int position, int[] targets)
    {
        this.targets = targets;
        var bits = new List<(int draw, int word, int bit)>();
        for (int d = 0; d < 4; d++)
            for (int w = 0; w < 2; w++)
                for (int b = 0; b < widths[d]; b++) bits.Add((position + d, w, b));

        var rows = new ulong[Equations];
        var offsets = new bool[Equations];
        for (int i = 0; i < Equations; i++)
        {
            var (draw, word, bit) = bits[i];
            var (a0, a1) = Xoroshiro8a.StateAfter(0, Xoroshiro8a.Fixed, draw);
            offsets[i] = (((word == 0 ? a0 : a1) >> bit) & 1) != 0;
        }
        for (int k = 0; k < 64; k++)
        {
            for (int i = 0; i < Equations; i++)
            {
                var (draw, word, bit) = bits[i];
                var (a0, a1) = Xoroshiro8a.StateAfter(1UL << k, 0, draw);
                if ((((word == 0 ? a0 : a1) >> bit) & 1) != 0) rows[i] |= 1UL << k;
            }
        }

        // Gauss-Jordan over GF(2), keeping track of which equations were summed into each row
        var comb = new ulong[Equations];
        for (int i = 0; i < Equations; i++) comb[i] = 1UL << i;
        var used = new bool[64];
        rank = 0;
        for (int col = 0; col < 64 && rank < Equations; col++)
        {
            int p = -1;
            for (int i = rank; i < Equations; i++) if (((rows[i] >> col) & 1) != 0) { p = i; break; }
            if (p < 0) continue;
            (rows[rank], rows[p]) = (rows[p], rows[rank]);
            (comb[rank], comb[p]) = (comb[p], comb[rank]);
            for (int i = 0; i < Equations; i++)
                if (i != rank && ((rows[i] >> col) & 1) != 0) { rows[i] ^= rows[rank]; comb[i] ^= comb[rank]; }
            pivotColumn[rank] = col; used[col] = true; rank++;
        }
        Array.Copy(rows, reduced, Equations);
        combination = comb.Select(m => Enumerable.Range(0, Equations).Where(i => ((m >> i) & 1) != 0).ToArray()).ToArray();
        for (int i = 0; i < Equations; i++) foreach (var j in combination[i]) reducedOffset[i] ^= offsets[j];
        nullspace = Enumerable.Range(0, 64).Where(c => !used[c]).Select(free =>
        {
            ulong v = 1UL << free;
            for (int i = 0; i < rank; i++) if (((reduced[i] >> free) & 1) != 0) v |= 1UL << pivotColumn[i];
            return v;
        }).ToArray();
    }

    public int Rank => rank;

    /// <summary>A seed meeting the size equations, chosen at random; null when this choice of low bytes has no solution.</summary>
    public ulong? Sample(Random rnd)
    {
        var target = new bool[Equations];
        int i = 0;
        for (int d = 0; d < 4; d++)
        {
            int mask = (1 << widths[d]) - 1;
            int a = rnd.Next(1 << widths[d]), b = (targets[d] - a) & mask;
            for (int bit = 0; bit < widths[d]; bit++) target[i++] = ((a >> bit) & 1) != 0;
            for (int bit = 0; bit < widths[d]; bit++) target[i++] = ((b >> bit) & 1) != 0;
        }
        ulong seed = 0;
        for (int k = 0; k < Equations; k++)
        {
            bool t = reducedOffset[k];
            foreach (var j in combination[k]) t ^= target[j];
            if (k < rank) { if (t) seed |= 1UL << pivotColumn[k]; }
            else if (t) return null;
        }
        foreach (var v in nullspace) if (rnd.Next(2) == 1) seed ^= v;
        return seed;
    }

    /// <summary>
    /// Where the height draw sits for a Pokémon whose shiny came at roll <paramref name="shinyRoll"/> (or that used every
    /// roll): the constant and the throwaway id, the PIDs, six IV draws (a guaranteed IV costs one draw to place instead of
    /// one to roll), the ability, the gender when the species rolls one, and the nature.
    /// </summary>
    public static int Position(int shinyRoll, bool rollsGender) => 2 + shinyRoll + 6 + 1 + (rollsGender ? 1 : 0) + 1;
}

using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// Seeds whose draws at a known position come out as asked, found by linear algebra instead of search.
/// <para>
/// The games draw a size as rand(0x81) + rand(0x80): rand(0x81) is the low byte of an output (it is under 0x81 or drawn
/// again) and rand(0x80) its low seven bits, and an output is s0 + s1, whose low bits depend only on the low bits of s0 and
/// s1. The state is a GF(2)-linear function of the seed (the update is shifts, rotates and xors; only the output adds), so
/// once the low bits of s0 at each draw are chosen freely and the low bits of s1 are set to what the sum needs, the seed is
/// the solution of a few linear equations in its 64 bits.
/// </para>
/// <para>
/// The draws sit at the position only when nothing before them drew again (a nature or a size rejected and redrawn, a shiny
/// found at a different roll): a sample is therefore generated in full afterwards and kept only if it really is what was asked.
/// </para>
/// </summary>
public sealed class SizeSeeds
{
    readonly int[] widths;
    readonly int[] targets;
    readonly int equations;
    readonly ulong[] reduced;        // the rows after elimination, pivot rows first
    readonly int[] pivotColumn;
    readonly int rank;
    readonly bool[] reducedOffset;
    readonly int[][] combination;    // which original equations each reduced row is the sum of
    readonly ulong[] nullspace;

    /// <param name="position">the index of the first draw among the seed's draws</param>
    /// <param name="widths">how many low bits each draw's output must match: 8 for rand(0x81), 7 for rand(0x80)</param>
    /// <param name="targets">what each draw must give</param>
    public SizeSeeds(int position, int[] widths, int[] targets)
    {
        if (widths.Length != targets.Length) throw new ArgumentException("one target per draw");
        this.widths = widths; this.targets = targets;
        equations = 2 * widths.Sum();
        reduced = new ulong[equations]; pivotColumn = new int[equations]; reducedOffset = new bool[equations];
        var bits = new List<(int draw, int word, int bit)>();
        for (int d = 0; d < widths.Length; d++)
            for (int w = 0; w < 2; w++)
                for (int b = 0; b < widths[d]; b++) bits.Add((position + d, w, b));

        var rows = new ulong[equations];
        var offsets = new bool[equations];
        for (int i = 0; i < equations; i++)
        {
            var (draw, word, bit) = bits[i];
            var (a0, a1) = StateAfter(0, Xoroshiro128Plus.XOROSHIRO_CONST, draw);
            offsets[i] = (((word == 0 ? a0 : a1) >> bit) & 1) != 0;
        }
        for (int k = 0; k < 64; k++)
        {
            for (int i = 0; i < equations; i++)
            {
                var (draw, word, bit) = bits[i];
                var (a0, a1) = StateAfter(1UL << k, 0, draw);
                if ((((word == 0 ? a0 : a1) >> bit) & 1) != 0) rows[i] |= 1UL << k;
            }
        }

        // Gauss-Jordan over GF(2), keeping track of which equations were summed into each row
        var comb = new ulong[equations];
        for (int i = 0; i < equations; i++) comb[i] = 1UL << i;
        var used = new bool[64];
        rank = 0;
        for (int col = 0; col < 64 && rank < equations; col++)
        {
            int p = -1;
            for (int i = rank; i < equations; i++) if (((rows[i] >> col) & 1) != 0) { p = i; break; }
            if (p < 0) continue;
            (rows[rank], rows[p]) = (rows[p], rows[rank]);
            (comb[rank], comb[p]) = (comb[p], comb[rank]);
            for (int i = 0; i < equations; i++)
                if (i != rank && ((rows[i] >> col) & 1) != 0) { rows[i] ^= rows[rank]; comb[i] ^= comb[rank]; }
            pivotColumn[rank] = col; used[col] = true; rank++;
        }
        Array.Copy(rows, reduced, equations);
        combination = comb.Select(m => Enumerable.Range(0, equations).Where(i => ((m >> i) & 1) != 0).ToArray()).ToArray();
        for (int i = 0; i < equations; i++) foreach (var j in combination[i]) reducedOffset[i] ^= offsets[j];
        nullspace = Enumerable.Range(0, 64).Where(c => !used[c]).Select(free =>
        {
            ulong v = 1UL << free;
            for (int i = 0; i < rank; i++) if (((reduced[i] >> free) & 1) != 0) v |= 1UL << pivotColumn[i];
            return v;
        }).ToArray();
    }

    public int Rank => rank;

    /// <summary>A seed meeting the equations, chosen at random; null when this choice of low bits has no solution.</summary>
    public ulong? Sample(Random rnd)
    {
        var target = new bool[equations];
        int i = 0;
        for (int d = 0; d < widths.Length; d++)
        {
            int mask = (1 << widths[d]) - 1;
            int a = rnd.Next(1 << widths[d]), b = (targets[d] - a) & mask;
            for (int bit = 0; bit < widths[d]; bit++) target[i++] = ((a >> bit) & 1) != 0;
            for (int bit = 0; bit < widths[d]; bit++) target[i++] = ((b >> bit) & 1) != 0;
        }
        ulong seed = 0;
        for (int k = 0; k < equations; k++)
        {
            bool t = reducedOffset[k];
            foreach (var j in combination[k]) t ^= target[j];
            if (k < rank) { if (t) seed |= 1UL << pivotColumn[k]; }
            else if (t) return null;
        }
        foreach (var v in nullspace) if (rnd.Next(2) == 1) seed ^= v;
        return seed;
    }

    /// <summary>The state after this many draws, with no draw made: the state is a linear function of the seed.</summary>
    private static (ulong s0, ulong s1) StateAfter(ulong seed, ulong second, int draws)
    {
        var x = new Xoroshiro128Plus(seed, second);
        for (int i = 0; i < draws; i++) x.Next();
        return x.GetState();
    }
}

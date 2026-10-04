namespace Dexforge.Arceus;

/// <summary>One slot of a spawner's table as the game weighs it at the chosen time of day and weather.</summary>
public readonly record struct TableSlot(ushort Species, byte Form, bool IsAlpha, float Weight);

/// <summary>
/// A field spawner that can produce a species, form and alpha-ness: the table it draws from at one time of day and
/// weather, and which slot is the one wanted. From tools/pla-spawners/build-spawners.py, which reads the game's spawner
/// data through the PLA bot's seed tools and keeps, for each target, the spawner where it takes the largest share.
/// </summary>
public sealed record FieldSpawner(ushort Species, byte Form, bool IsAlpha, string Area, ulong Table, string Time, string Weather,
                                  int Index, byte LevelMin, byte LevelMax, byte Flawless, byte? GenderLock, TableSlot[] Slots)
{
    public float Total => Slots.Aggregate(0f, (sum, s) => sum + s.Weight);

    /// <summary>
    /// Which slot a draw in [0, 1) picks: the draw scaled by the total, then each slot's weight taken off in turn until it
    /// would go under zero. In single precision, compared before subtracting, exactly as pla-reverse's EncounterAreaLA.calc_slot
    /// and the bot's verifier do it, because a draw within 1e-7 of a boundary rounds differently in double.
    /// </summary>
    public int Pick(double roll)
    {
        float scaled = (float)roll * Total;
        for (int i = 0; i < Slots.Length; i++)
        {
            if (scaled < Slots[i].Weight) return i;
            scaled -= Slots[i].Weight;
        }
        return Slots.Length - 1;
    }

    /// <summary>The share of the draw that lands on the wanted slot.</summary>
    public double Share => Slots[Index].Weight / Total;

    /// <summary>
    /// Where the wanted slot sits in [0, 1): the weights before it over the total, and that plus its own. Kept a little inside
    /// its edges so that a draw placed in it is never on the far side of a float rounding.
    /// </summary>
    public (double lo, double hi) Interval
    {
        get
        {
            double total = Total, before = 0;
            for (int i = 0; i < Index; i++) before += Slots[i].Weight;
            double lo = before / total, hi = (before + Slots[Index].Weight) / total, margin = (hi - lo) * 1e-3;
            return (lo + margin, hi - margin);
        }
    }

    /// <summary>A generator seed whose slot draw lands on the wanted slot, chosen uniformly inside it and checked with <see cref="Pick"/>.</summary>
    public ulong DrawGenerator(Random rnd)
    {
        var (lo, hi) = Interval;
        while (true)
        {
            double roll = lo + rnd.NextDouble() * (hi - lo);
            ulong first = (ulong)(roll * 18446744073709551616.0);
            // the first output is the seed plus the constant; the low bits the double cannot place are drawn at random
            ulong seed = unchecked(first - Xoroshiro8a.Fixed) ^ (ulong)rnd.NextInt64(0, 1L << 11);
            if (Pick(Spawn8a.SlotRoll(seed)) == Index) return seed;
        }
    }

    /// <summary>Whether this generator seed's draw lands on the wanted slot.</summary>
    public bool Lands(ulong generator) => Pick(Spawn8a.SlotRoll(generator)) == Index;
}

/// <summary>The spawner table the program carries, one spawner per species, form and alpha-ness.</summary>
public static class Spawners8a
{
    private static readonly Lazy<Dictionary<(ushort, byte, bool), FieldSpawner>> all = new(Load);

    public static FieldSpawner? For(ushort species, byte form, bool alpha) => all.Value.GetValueOrDefault((species, form, alpha));
    public static IReadOnlyCollection<FieldSpawner> All => all.Value.Values;

    private static Dictionary<(ushort, byte, bool), FieldSpawner> Load()
    {
        var table = new Dictionary<(ushort, byte, bool), FieldSpawner>();
        var rows = new Dexforge.EventBox.Rows(Embedded.Text("arceus.spawners"));
        foreach (var r in rows.All)
        {
            string G(string c) => rows.Get(r, c);
            var levels = G("레벨").Split('-');
            var slots = G("슬롯표").Split('|').Select(s =>
            {
                var p = s.Split('-');
                return new TableSlot(ushort.Parse(p[0]), byte.Parse(p[1]), p[2] == "1", float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture));
            }).ToArray();
            var f = new FieldSpawner(ushort.Parse(G("번호")), byte.Parse(G("폼")), G("우두") == "1", G("지역"), Convert.ToUInt64(G("표"), 16), G("시간"), G("날씨"),
                                     int.Parse(G("슬롯")), byte.Parse(levels[0]), byte.Parse(levels[1]), byte.Parse(G("보장IV")),
                                     G("성별고정").Length == 0 ? null : byte.Parse(G("성별고정")), slots);
            table[(f.Species, f.Form, f.IsAlpha)] = f;
        }
        return table;
    }
}

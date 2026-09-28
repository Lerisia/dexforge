namespace AlolaDexMaker;

/// <summary>The generator eggs are drawn from: TinyMT, whose state of four words the save carries from one egg to the next.</summary>
public sealed class Tiny
{
    private const uint Mat1 = 0x8f7011ee, Mat2 = 0xfc78ff1f, Tmat = 0x3793fdff;
    private readonly uint[] s;

    public Tiny(uint s0, uint s1, uint s2, uint s3) => s = [s0, s1, s2, s3];
    public uint[] State => (uint[])s.Clone();

    public uint Next()
    {
        uint y = s[3];
        uint x = (s[0] & 0x7FFFFFFF) ^ s[1] ^ s[2];
        x ^= x << 1;
        y ^= (y >> 1) ^ x;
        s[0] = s[1];
        s[1] = s[2];
        s[2] = x ^ (y << 10);
        s[3] = y;
        if ((y & 1) == 1) { s[1] ^= Mat1; s[2] ^= Mat2; }

        uint t0 = s[3];
        uint t1 = s[0] + (s[2] >> 8);
        t0 ^= t1;
        if ((t1 & 1) == 1) t0 ^= Tmat;
        return t0;
    }
}

/// <summary>What the parents hold: nothing that matters, an Everstone, or a Destiny Knot. (The items that pass one stat are left out.)</summary>
public enum Held : byte { Nothing = 0, Everstone = 1, DestinyKnot = 2 }

/// <summary>The two at the Nursery, and what about them decides the egg.</summary>
/// <param name="MaleIvs">Individual values of the one the game counts as the male: HP, Attack, Defense, Sp. Atk, Sp. Def, Speed.</param>
/// <param name="FemaleIvs">The same of the one the game counts as the female.</param>
/// <param name="Ability">The ability of the parent that passes it on, counted from 0; 2 is the hidden one.</param>
/// <param name="Ratio">The sex ratio of what hatches, as the game's table has it.</param>
/// <param name="EitherSex">What hatches is one of two species by its sex (Nidoran, Volbeat and Illumise).</param>
/// <param name="SameSpecies">Both parents are of one species, so the ball is either's.</param>
/// <param name="FemaleIsDitto">The one counted as the female is a Ditto.</param>
public sealed record Parents7(int[] MaleIvs, int[] FemaleIvs, Held MaleHolds, Held FemaleHolds, byte Ability, int Ratio, bool EitherSex,
                              bool SameSpecies, bool FemaleIsDitto, bool Charm, bool Masuda);

/// <summary>What one egg came to. The sex is as the tool counts it (0 none, 1 male, 2 female); the ball is 1 for the male's, 2 for the female's.</summary>
public sealed record Hatched7(byte Gender, byte Nature, byte Ability, int[] Ivs, int[] From, uint Ec, uint Pid, bool Shiny, byte Ball, int Used);

/// <summary>
/// An egg, generated the way the game generates it when the Nursery has one ready.
/// This follows 3DSRNGTool (Egg7) step for step and is checked against that tool's own code.
/// </summary>
public static class Egg7
{
    public static Hatched7 Lay(Tiny rng, Parents7 p, int tsv)
    {
        int used = 0;
        uint Rand() { used++; return rng.Next(); }

        bool random = 0x0F < p.Ratio && p.Ratio < 0xEF;
        byte settled = p.Ratio switch
        {
            0x1F or 0x3F or 0x7F or 0xBF or 0xE1 => (byte)(p.Ratio - 1),
            0x00 => 1,
            0xFE => 2,
            _ => 0,
        };
        // The tool takes the sex for drawn when its own number for it is above 15.
        bool drawn = settled > 0x0F;
        byte gender = p.EitherSex ? (byte)((Rand() & 1) + 1)
                    : drawn ? (byte)((int)(Rand() % 252) >= settled ? 1 : 2)
                    : settled;
        _ = random;

        byte nature = (byte)(Rand() % 25);

        bool everstone = p.MaleHolds == Held.Everstone || p.FemaleHolds == Held.Everstone;
        if (p.MaleHolds == Held.Everstone && p.FemaleHolds == Held.Everstone) Rand();
        _ = everstone;

        uint roll = Rand() % 100;
        byte ability = p.Ability switch
        {
            0 => (byte)(roll < 0x50 ? 1 : 2),
            1 => (byte)(roll < 0x14 ? 1 : 2),
            2 => (byte)(roll < 0x14 ? 1 : roll < 0x28 ? 2 : 3),
            _ => 0,
        };

        bool knot = p.MaleHolds == Held.DestinyKnot || p.FemaleHolds == Held.DestinyKnot;
        var from = new int[6]; // 0 drawn, 1 the male's, 2 the female's
        for (int i = 0; i < (knot ? 5 : 3); i++)
        {
            int stat;
            do stat = (int)(Rand() % 6); while (from[stat] != 0);
            from[stat] = (Rand() & 1) == 0 ? 1 : 2;
        }

        var ivs = new int[6];
        for (int j = 0; j < 6; j++)
        {
            ivs[j] = (int)(Rand() & 0x1F);
            if (from[j] == 1) ivs[j] = p.MaleIvs[j];
            else if (from[j] == 2) ivs[j] = p.FemaleIvs[j];
        }

        uint ec = Rand();

        uint pid = 0; bool shiny = false;
        for (int i = (p.Charm ? 2 : 0) + (p.Masuda ? 6 : 0); i > 0; i--)
        {
            pid = Rand();
            if ((((pid >> 16) ^ (pid & 0xFFFF)) >> 4) == tsv) { shiny = true; break; }
        }

        byte ball = (byte)(p.SameSpecies && Rand() % 100 >= 50 || p.FemaleIsDitto ? 1 : 2);

        used += 2; // taking the egg, and the Nursery being empty again
        return new Hatched7(gender, nature, ability, ivs, from, ec, pid, shiny, ball, used);
    }
}

/// <summary>An egg as it was found: what it came to, the state of the generator it came of, and the two it came from.</summary>
public sealed record Laid7(Hatched7 Egg, uint[] Seed, Parents7 Parents);

/// <summary>
/// Eggs drawn from a state the save's generator could have been in, of parents such as a breeder keeps:
/// the one of the species, and a Ditto from abroad, which makes it the Masuda method; the trainer carries the Shiny Charm.
/// </summary>
public sealed class Nursery7(Draw draw, Options opt)
{
    /// <summary>Species whose egg is one of two by its sex.</summary>
    private static readonly HashSet<int> EitherSex = [29, 32, 313, 314];

    public List<string> Notes { get; } = [];

    /// <summary>
    /// The two at the Nursery for an egg of this species. Where five or six perfect values are asked for, both are perfect and one holds
    /// the Destiny Knot; otherwise they are as they were caught and hold nothing.
    /// </summary>
    public Parents7 Pair(int species, int form, int ability)
    {
        var info = PKHeX.Core.PersonalTable.USUM.GetFormEntry((ushort)species, (byte)form);
        int ratio = info.Gender;
        bool bred = opt.Ivs != IvChoice.Random;
        int[] Ivs() => bred ? [31, 31, 31, 31, 31, 31] : [.. Enumerable.Range(0, 6).Select(_ => draw.Rnd.Next(32))];
        // What has no female is the one the game counts as the male, and the Ditto the female.
        bool dittoIsFemale = ratio is 0x00 or 0xFF;
        return new Parents7(Ivs(), Ivs(), dittoIsFemale ? Held.Nothing : bred ? Held.DestinyKnot : Held.Nothing, dittoIsFemale && bred ? Held.DestinyKnot : Held.Nothing,
                            (byte)ability, ratio, EitherSex.Contains(species), false, dittoIsFemale, Charm: true, Masuda: true);
    }

    /// <summary>An egg that came out as asked, of a state drawn afresh each time. A sex of none asks for either.</summary>
    public Laid7? Lay(Parents7 parents, ushort tid, ushort sid, bool shines, byte? gender, Func<Hatched7, bool> stands, int tries)
    {
        int tsv = (tid ^ sid) >> 4;
        byte? theirs = gender switch { null => null, 0 => 1, 1 => 2, _ => 0 };
        bool six = opt.Ivs == IvChoice.Six;
        for (int n = 0; n < tries; n++)
        {
            uint[] seed = [(uint)draw.Rnd.NextInt64(0, 1L << 32), (uint)draw.Rnd.NextInt64(0, 1L << 32), (uint)draw.Rnd.NextInt64(0, 1L << 32), (uint)draw.Rnd.NextInt64(0, 1L << 32)];
            if ((seed[0] & 0x7FFFFFFF) == 0 && seed[1] == 0 && seed[2] == 0 && seed[3] == 0) continue;
            var egg = Egg7.Lay(new Tiny(seed[0], seed[1], seed[2], seed[3]), parents, tsv);
            if (egg.Shiny != shines || theirs is { } sex && egg.Gender != sex || egg.Ec == 0) continue;
            if (six && egg.Ivs.Any(v => v != 31)) continue;
            if (!stands(egg)) continue;
            return new Laid7(egg, seed, parents);
        }
        return null;
    }

    /// <summary>The values of an egg, put on the Pokemon.</summary>
    public static void Put(PKHeX.Core.PK7 pk, Hatched7 egg)
    {
        pk.EncryptionConstant = egg.Ec;
        pk.PID = egg.Pid;
        pk.IV_HP = egg.Ivs[0]; pk.IV_ATK = egg.Ivs[1]; pk.IV_DEF = egg.Ivs[2]; pk.IV_SPA = egg.Ivs[3]; pk.IV_SPD = egg.Ivs[4]; pk.IV_SPE = egg.Ivs[5];
        pk.Nature = (PKHeX.Core.Nature)egg.Nature;
        pk.RefreshAbility(egg.Ability - 1);
        pk.Gender = egg.Gender switch { 1 => (byte)0, 2 => (byte)1, _ => (byte)2 };
    }
}

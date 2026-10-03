using PKHeX.Core;

namespace AlolaDexMaker.Sword;

/// <summary>One parent at the Nursery: what the egg generation reads from it.</summary>
public sealed record Parent(ushort Species, byte Gender, int AbilityIndex, Nature Nature, int[] Ivs, Ball Ball, ushort HeldItem, int Language)
{
    public bool IsDitto => Species == 132;
}

/// <summary>What one egg came out as, before it is put onto PKHeX's egg skeleton.</summary>
public sealed record Hatched(ulong Seed, ushort Species, byte Form, byte Gender, Nature Nature, int AbilityIndex, int[] Ivs, uint Ec, uint Pid, Ball Ball, bool Shiny);

/// <summary>
/// Sword/Shield egg generation, step by step after Admiral Fish's write-up (sub_7100787540). Species choice, egg moves and
/// friendship do not touch the RNG and are left to PKHeX; everything that does is here, in the game's order:
/// Nidoran / Volbeat / Indeedee, gender, nature (+Everstone), ability, IVs (+Destiny Knot), EC, PID (Masuda / Shiny Charm), ball.
/// </summary>
public static class Egg8
{
    private const ushort Everstone = 229, DestinyKnot = 280;

    /// <summary>Parents in the game's order: Male/Female, Ditto/Female, Male/Ditto, Genderless/Ditto.</summary>
    public static (Parent First, Parent Second) Order(Parent a, Parent b)
    {
        if (a.IsDitto || b.IsDitto)
        {
            var other = a.IsDitto ? b : a;
            var ditto = a.IsDitto ? a : b;
            return other.Gender == 1 ? (ditto, other) : (other, ditto);
        }
        return a.Gender == 0 ? (a, b) : (b, a);
    }

    public static Hatched Generate(ulong seed, Parent a, Parent b, ushort species, byte form, byte genderRatio, uint trainerId32, bool shinyCharm)
    {
        var (p1, p2) = Order(a, b);
        var rng = new Xoroshiro(seed);

        // Steps 2-5: species-specific rolls that happen before gender.
        if (species is 29 or 32) species = (ushort)(rng.Next(2) == 1 ? 29 : 32);          // Nidoran
        if (species is 313 or 314) species = (ushort)(rng.Next(2) == 1 ? 314 : 313);      // Illumise / Volbeat
        if (species == 876) form = (byte)rng.Next(2);                                      // Indeedee

        // Step 7: gender.
        byte gender = genderRatio switch
        {
            PersonalInfo.RatioMagicGenderless => 2,
            PersonalInfo.RatioMagicFemale => 1,
            PersonalInfo.RatioMagicMale => 0,
            _ => (byte)(rng.Next(252) + 1 < genderRatio ? 1 : 0),
        };

        // Step 8: nature, then Everstone.
        var nature = (Nature)rng.Next(25);
        if (p1.HeldItem == Everstone && p2.HeldItem == Everstone) nature = rng.Next(2) == 1 ? p2.Nature : p1.Nature;
        else if (p1.HeldItem == Everstone) nature = p1.Nature;
        else if (p2.HeldItem == Everstone) nature = p2.Nature;

        // Step 9: ability, from the non-Ditto parent (parent2 unless it is the Ditto).
        var source = p2.IsDitto ? p1 : p2;
        var rand = (int)rng.Next(100);
        int ability = source.AbilityIndex switch
        {
            0 => rand < 80 ? 0 : 1,
            1 => rand < 20 ? 0 : 1,
            _ => rand < 20 ? 0 : rand < 40 ? 1 : 2,
        };

        // Step 11: IVs: which stats come from which parent, then six rolls, then the inherited ones copied over.
        int inheritCount = p1.HeldItem == DestinyKnot || p2.HeldItem == DestinyKnot ? 5 : 3;
        var inherit = new int[6];
        Array.Fill(inherit, -1);
        for (int done = 0; done < inheritCount;)
        {
            int stat = (int)rng.Next(6);
            if (inherit[stat] != -1) continue;
            inherit[stat] = rng.Next(2) == 1 ? 2 : 1;
            done++;
        }
        var ivs = new int[6];
        for (int i = 0; i < 6; i++) ivs[i] = (int)rng.Next(32);
        for (int i = 0; i < 6; i++)
        {
            if (inherit[i] == 1) ivs[i] = p1.Ivs[i];
            else if (inherit[i] == 2) ivs[i] = p2.Ivs[i];
        }

        // Step 12: EC.
        uint ec = rng.NextUInt(0xFFFFFFFF);

        // Step 13: PID. Masuda gives 6 rolls, the Shiny Charm 2 more; without either the game uses something else, so both parents
        // are always of different languages here.
        bool masuda = p1.Language != p2.Language;
        if (!masuda) throw new InvalidOperationException("the egg PID without the Masuda method is not described; use parents of different languages");
        int rolls = 6 + (shinyCharm ? 2 : 0);
        uint pid = 0;
        bool shiny = false;
        for (int i = 0; i < rolls; i++)
        {
            pid = rng.NextUInt(0xFFFFFFFF);
            if (ShinyUtil.GetShinyXor(pid, trainerId32) < 16) { shiny = true; break; }
        }

        // Step 15: ball.
        Ball ball;
        if (p2.IsDitto) ball = p1.Ball;
        else if (p1.Species != p2.Species) ball = p2.Ball;
        else ball = rng.Next(100) + 1 < 51 ? p2.Ball : p1.Ball;
        if (ball is Ball.Master or Ball.Cherish) ball = Ball.Poke;

        return new Hatched(seed, species, form, gender, nature, ability, ivs, ec, pid, ball, shiny);
    }
}

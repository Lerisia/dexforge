using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>What is asked of a wild catch's size: the smallest (0), the largest (255), or whatever comes. An alpha is always 255.</summary>
public enum Scale9a { Random, Smallest, Largest }

/// <summary>
/// A wild Z-A Pokémon drawn from a 64-bit seed the way the game draws one: PKHeX's own generation (LumioseRNG) from
/// xoroshiro128+, in this order — the encryption constant, a throwaway trainer id, the PID (once per shiny roll until one
/// shines), six IVs (an alpha's three perfect ones are set beforehand from another seed, so only three are drawn), the
/// ability slot, the gender where the species rolls one, the nature, then the size as rand(0x81) + rand(0x80) (an alpha
/// is 255 without a draw). One shiny roll is used, as the hex bot does: a shiny found at the first roll is the same
/// Pokémon under the Shiny Charm's and hyperspace's extra rolls, so the seed holds whatever the hunter had.
/// </summary>
public static class Spawn9a
{
    public const int Rolls = 1;

    public static GenerateParam9a Param(EncounterSlot9a slot) => slot.GetParams(Plan9a.Table[slot.Species, slot.Form]);

    /// <summary>Whether the species rolls a gender (a draw of its own) or has a fixed one (the slot may fix it too).</summary>
    public static bool RollsGender(in GenerateParam9a param) =>
        param.GenderRatio is not (PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicFemale or PersonalInfo.RatioMagicMale);

    /// <summary>Where the size's first draw sits for a plain (non-alpha) catch at one shiny roll: the constant, the throwaway id, the PID, six IVs, the ability, the gender, the nature.</summary>
    public static int ScalePosition(bool rollsGender) => 2 + Rolls + 6 + 1 + (rollsGender ? 1 : 0) + 1;

    /// <summary>Fills a Pokémon (its trainer ids already set) from the seed; false when the seed does not meet the criteria (its shininess, sex, the alpha's perfect IVs).</summary>
    public static bool Apply(PA9 pk, in GenerateParam9a param, in EncounterCriteria criteria, ulong seed) => LumioseRNG.GenerateData(pk, param, criteria, seed);

    /// <summary>Whether PKHeX's own check finds the Pokémon to be what this seed gives.</summary>
    public static bool Matches(PA9 pk, in GenerateParam9a param, ulong seed) => LumioseRNG.IsMatch(pk, param, seed);

    /// <summary>
    /// The criteria a catch is drawn under: shiny or not, a sex, and for an alpha which three IVs are perfect (chosen here, so
    /// the draw is repeatable; the game sets them from a seed of its own).
    /// </summary>
    public static EncounterCriteria Criteria(EncounterSlot9a slot, bool shiny, byte? gender, Random rnd)
    {
        var c = EncounterCriteria.Unrestricted with
        {
            Shiny = shiny ? Shiny.Always : Shiny.Never,
            Gender = gender is { } g ? (g == 0 ? Gender.Male : Gender.Female) : Gender.Random,
        };
        if (!slot.IsAlpha) return c;
        Span<int> order = [0, 1, 2, 3, 4, 5];
        for (int i = 5; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        for (int k = 0; k < slot.FlawlessIVCount; k++)
        {
            c = order[k] switch
            {
                0 => c with { IV_HP = 31 }, 1 => c with { IV_ATK = 31 }, 2 => c with { IV_DEF = 31 },
                3 => c with { IV_SPA = 31 }, 4 => c with { IV_SPD = 31 }, _ => c with { IV_SPE = 31 },
            };
        }
        return c;
    }

    /// <summary>
    /// A seed whose Pokémon shines as asked and has the size asked: random seeds for whatever size (and for an alpha, whose
    /// size is fixed), and for a smallest or largest one the seeds of <see cref="SizeSeeds"/>, kept when the generation
    /// really lands there. None after the tries given.
    /// </summary>
    public static ulong? Find(PA9 pk, EncounterSlot9a slot, in EncounterCriteria criteria, Scale9a scale, Random rnd, Func<PA9, bool>? also = null, int tries = 4_000_000)
    {
        var param = Param(slot);
        if (scale == Scale9a.Random || slot.IsAlpha)
        {
            for (int i = 0; i < tries; i++)
            {
                ulong seed = (ulong)rnd.NextInt64() ^ ((ulong)rnd.Next() << 40);
                if (Apply(pk, param, criteria, seed) && (also is null || also(pk))) return seed;
            }
            return null;
        }
        int[] targets = scale == Scale9a.Smallest ? [0, 0] : [0x80, 0x7F];
        byte want = scale == Scale9a.Smallest ? (byte)0 : (byte)255;
        var sampler = new SizeSeeds(ScalePosition(RollsGender(param)), [8, 7], targets);
        for (int i = 0; i < tries; i++)
        {
            if (sampler.Sample(rnd) is not { } seed) continue;
            if (!Apply(pk, param, criteria, seed)) continue;
            if (pk.Scale == want && (also is null || also(pk))) return seed;
        }
        return null;
    }
}

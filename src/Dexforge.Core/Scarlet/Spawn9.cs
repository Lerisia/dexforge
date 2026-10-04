using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>What is asked of a wild spawn's scale: the smallest (0, the Mini Mark), the largest (255, the Jumbo Mark), or whatever comes.</summary>
public enum Scale9 { Random, Smallest, Largest }

/// <summary>
/// A wild Scarlet Pokémon drawn from a 64-bit seed the way the game draws one: PKHeX's own generation (Encounter9RNG) from
/// xoroshiro128+, in this order — the encryption constant, a throwaway id, the PID (once per shiny roll until one shines),
/// six IVs, the ability, the gender where the species rolls one, the nature, then height, weight and scale as
/// rand(0x81) + rand(0x80) each. The Tera type is the species' own, picked from the same seed. PKHeX does not hold a wild
/// catch to a seed, but the game does, and so does this.
/// </summary>
public static class Spawn9
{
    /// <summary>
    /// How many shiny rolls a catch gets, as a hunter has them: one, two more for the Shiny Charm, three more for a sandwich's
    /// Sparkling Power Lv. 3, and two more for a mass outbreak with sixty of the species knocked out.
    /// </summary>
    public const int Rolls = 8;

    public static GenerateParam9 Param(ushort species, byte form, int rolls = Rolls)
    {
        var pi = PersonalTable.SV.GetFormEntry(species, form);
        return new GenerateParam9(species, pi.Gender, 0, (byte)rolls, 0, 0, SizeType9.RANDOM, 0, AbilityPermission.Any12, Shiny.Random, Nature.Random, default);
    }

    /// <summary>Whether the species rolls a gender (a draw of its own) or has a fixed one.</summary>
    public static bool RollsGender(ushort species, byte form)
    {
        var g = PersonalTable.SV.GetFormEntry(species, form).Gender;
        return g is not (PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicFemale or PersonalInfo.RatioMagicMale);
    }

    /// <summary>
    /// Where the scale's first draw sits when a shiny came at roll <paramref name="shinyRoll"/> (or every roll was used),
    /// and nothing was drawn again: the constant and the throwaway id, the PIDs, six IVs, the ability, the gender, the
    /// nature, height and weight (two draws each).
    /// </summary>
    public static int ScalePosition(int shinyRoll, bool rollsGender) => 2 + shinyRoll + 6 + 1 + (rollsGender ? 1 : 0) + 1 + 2 + 2;

    /// <summary>Fills a Pokémon (its trainer ids already set) from the seed; false if the seed is one the game would not finish (none in practice).</summary>
    public static bool Apply(PK9 pk, ushort species, byte form, ulong seed, int rolls = Rolls)
    {
        var param = Param(species, form, rolls);
        if (!Encounter9RNG.GenerateData(pk, param, EncounterCriteria.Unrestricted, seed)) return false;
        pk.TeraTypeOriginal = (MoveType)Tera9RNG.GetTeraType(seed, GemType.Default, species, form);
        return true;
    }

    /// <summary>Whether PKHeX's own check finds the Pokémon to be what this seed gives.</summary>
    public static bool Matches(PK9 pk, ushort species, byte form, ulong seed, int rolls = Rolls) => Encounter9RNG.IsMatch(pk, Param(species, form, rolls), seed);

    public static byte ScaleOf(Scale9 s) => s switch { Scale9.Smallest => 0, Scale9.Largest => 255, _ => 0 };

    /// <summary>
    /// A seed whose Pokémon shines as asked and has the scale asked: random seeds for whatever scale, and for a smallest or
    /// largest one the seeds of <see cref="SizeSeeds"/>, one sampler per roll the shiny could have come at, kept when the
    /// generation really lands there. None after the tries given.
    /// </summary>
    /// <param name="gender">0 male, 1 female; none, and either will do.</param>
    /// <param name="also">A further condition on what comes out (the encryption constant Maushold's and Dudunsparce's forms hang on).</param>
    public static ulong? Find(PK9 pk, ushort species, byte form, bool shiny, Scale9 scale, Random rnd, byte? gender = null, Func<PK9, bool>? also = null, int rolls = Rolls, int tries = 4_000_000)
    {
        bool Fits() => pk.IsShiny == shiny && (gender is null || pk.Gender == gender) && (also is null || also(pk));
        if (scale == Scale9.Random)
        {
            for (int i = 0; i < tries; i++)
            {
                ulong seed = (ulong)rnd.NextInt64() ^ ((ulong)rnd.Next() << 40);
                if (Apply(pk, species, form, seed, rolls) && Fits()) return seed;
            }
            return null;
        }
        bool genders = RollsGender(species, form);
        int[] targets = scale == Scale9.Smallest ? [0, 0] : [0x80, 0x7F];
        // the shiny's roll decides the position: a shiny found at roll j, or no shiny after all the rolls
        var samplers = (shiny ? Enumerable.Range(1, rolls) : [rolls]).Select(j => new SizeSeeds(ScalePosition(j, genders), [8, 7], targets)).ToArray();
        byte want = ScaleOf(scale);
        for (int i = 0; i < tries; i++)
        {
            var sampler = samplers[i % samplers.Length];
            if (sampler.Sample(rnd) is not { } seed) continue;
            if (!Apply(pk, species, form, seed, rolls)) continue;
            if (Fits() && pk.Scale == want) return seed;
        }
        return null;
    }
}

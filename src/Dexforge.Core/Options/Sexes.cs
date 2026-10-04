using PKHeX.Core;

namespace Dexforge;

/// <summary>Whether a Pokemon of the template is free to be of the sex asked for, and which sex it is to be.</summary>
public static class Sexes
{
    /// <summary>The species and forms the template's boxes hold of both sexes: they stay as they are.</summary>
    public static HashSet<(ushort Species, byte Form)> Both(IEnumerable<PKM> boxes) =>
        boxes.Where(p => p.Gender != 2).GroupBy(p => (p.Species, p.Form)).Where(g => g.Select(p => p.Gender).Distinct().Count() == 2).Select(g => g.Key).ToHashSet();

    public static bool Free(PKM old, IEncounterTemplate enc, HashSet<(ushort, byte)> both)
    {
        if (old.Gender == 2) return false;
        var pi = old.PersonalInfo;
        if (pi.Genderless || pi.OnlyMale || pi.OnlyFemale) return false;
        if (enc is MysteryGift) return false;
        if (enc is IFixedGender { IsFixedGender: true }) return false;
        if (SpeciesCategory.IsFixedGenderFromDual(old.Species)) return false;
        return !both.Contains((old.Species, old.Form));
    }

    /// <summary>The sex it is to be (0 male, 1 female, 2 none); none where it may come out either way.</summary>
    public static byte? Wanted(SexChoice asked, PKM old, IEncounterTemplate enc, HashSet<(ushort, byte)> both) =>
        !Free(old, enc, both) ? old.Gender : asked switch { SexChoice.Male => 0, SexChoice.Female => 1, _ => null };
}

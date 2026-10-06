using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// What the evolvers of Sword, Scarlet and Legends: Z-A share: the stages between two species, whether one reaches the other,
/// the sex an evolution insists on, and the moves some evolutions need known. Each evolver keeps its own walk and its own way of
/// setting moves; Legends: Arceus finds its stages its own way and is not here.
/// </summary>
public static class Evolution
{
    /// <summary>The caught species and every stage up to the wanted one, oldest first.</summary>
    private static List<(ushort Species, byte Form)> Chain(EvolutionTree tree, ushort to, byte toForm)
    {
        var chain = tree.Reverse.GetPreEvolutions(to, toForm).Select(x => (x.Species, x.Form)).ToList();
        chain.Add((to, toForm));
        return chain;
    }

    /// <summary>The stages from the caught species (exclusive) to the wanted one (inclusive).</summary>
    public static List<(ushort Species, byte Form)> Path(EvolutionTree tree, GameStrings ko, ushort from, byte fromForm, ushort to, byte toForm)
    {
        var chain = Chain(tree, to, toForm);
        int start = chain.FindIndex(x => x.Species == from && x.Form == fromForm);
        if (start < 0) throw new InvalidOperationException($"{ko.specieslist[from]}은(는) {ko.specieslist[to]}으로 진화하지 않습니다.");
        return chain.Skip(start + 1).ToList();
    }

    /// <summary>Whether the one evolves, in however many stages, into the other.</summary>
    public static bool CanReach(EvolutionTree tree, ushort from, byte fromForm, ushort to, byte toForm) =>
        Chain(tree, to, toForm).Exists(x => x.Species == from && x.Form == fromForm);

    /// <summary>The sex an evolution path insists on, if every way of making one of its steps needs the same one.</summary>
    public static byte? NeededGender(EvolutionTree tree, GameStrings ko, ushort from, byte fromForm, ushort to, byte toForm)
    {
        var (species, form) = (from, fromForm);
        foreach (var (next, nextForm) in Path(tree, ko, from, fromForm, to, toForm))
        {
            var methods = tree.Forward.GetForward(species, form).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(form) == nextForm).ToList();
            var genders = methods.Select(m => GenderOf(m.Method)).ToList();
            if (genders.Count != 0 && genders.All(g => g == genders[0]) && genders[0] is { } g)
                return g;
            (species, form) = (next, nextForm);
        }
        return null;
    }

    private static byte? GenderOf(EvolutionType t) => t switch
    {
        EvolutionType.LevelUpMale or EvolutionType.UseItemMale or EvolutionType.LevelUpRecoilDamageMale => 0,
        EvolutionType.LevelUpFemale or EvolutionType.UseItemFemale or EvolutionType.LevelUpRecoilDamageFemale or EvolutionType.LevelUpFormFemale1 => 1,
        _ => null,
    };

    /// <summary>
    /// Evolutions that need a move known or used, as the species evolved into (PKHeX's table, which it keeps to itself). One table
    /// for every game: a game never evolves into a species it does not have, so the others' rows are never reached.
    /// </summary>
    public static ushort[] MoveToEvolve(ushort evolved) => (Species)evolved switch
    {
        Species.Sylveon => [(ushort)Move.Charm, (ushort)Move.BabyDollEyes, (ushort)Move.DisarmingVoice],
        Species.MrMime or Species.Sudowoodo => [(ushort)Move.Mimic],
        Species.Ambipom => [(ushort)Move.DoubleHit],
        Species.Lickilicky => [(ushort)Move.Rollout],
        Species.Tangrowth or Species.Yanmega or Species.Mamoswine => [(ushort)Move.AncientPower],
        Species.Tsareena => [(ushort)Move.Stomp],
        Species.Naganadel => [(ushort)Move.DragonPulse],
        Species.Grapploct => [(ushort)Move.Taunt],
        Species.Annihilape => [(ushort)Move.RageFist],
        Species.Dudunsparce => [(ushort)Move.HyperDrill],
        Species.Farigiraf => [(ushort)Move.TwinBeam],
        Species.Overqwil => [(ushort)Move.BarbBarrage],
        Species.Wyrdeer => [(ushort)Move.PsyshieldBash],
        Species.Hydrapple => [(ushort)Move.DragonCheer],
        _ => [],
    };
}

using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>
/// Evolves a caught Pokémon in place, Hisui-style: a level high enough for every step, the ability slot kept, the moves it
/// knows kept (an evolution that needs a move known gets it), the name updated, the size and alpha-ness carried over as the
/// game carries them. Items are used, never held, so nothing is held.
/// </summary>
public static class Evolve8a
{
    private static readonly EvolutionTree Tree = Plan8a.Tree;

    public static List<(ushort, byte)> Path(ushort from, byte fromForm, ushort to, byte toForm)
    {
        foreach (var (sp, fo) in Tree.Forward.GetEvolutions(from, fromForm))
        {
            if (sp == to && fo == toForm) return [(sp, fo)];
            if (TryPath(sp, fo, to, toForm) is { } rest) { rest.Insert(0, (sp, fo)); return rest; }
        }
        throw new InvalidOperationException($"{Plan8a.Label(from, fromForm)}은(는) {Plan8a.Label(to, toForm)}(으)로 진화하지 않습니다.");
    }

    private static List<(ushort, byte)>? TryPath(ushort from, byte fromForm, ushort to, byte toForm)
    {
        try { return Path(from, fromForm, to, toForm); } catch (InvalidOperationException) { return null; }
    }

    /// <summary>The gender a path insists on, when every way of making some step needs the same one (Gallade male, Froslass and Vespiquen female).</summary>
    public static byte? NeededGender(ushort from, byte fromForm, ushort to, byte toForm)
    {
        if (from == to) return null;   // the same species in another form is a form change, not an evolution
        var (species, form) = (from, fromForm);
        foreach (var (next, nextForm) in Path(from, fromForm, to, toForm))
        {
            var genders = Tree.Forward.GetForward(species, form).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(form) == nextForm).Select(m => GenderOf(m.Method)).ToList();
            if (genders.Count != 0 && genders.All(g => g == genders[0]) && genders[0] is { } g) return g;
            (species, form) = (next, nextForm);
        }
        return null;
    }

    private static byte? GenderOf(EvolutionType t) => t switch
    {
        EvolutionType.LevelUpMale or EvolutionType.UseItemMale or EvolutionType.LevelUpRecoilDamageMale => 0,
        EvolutionType.LevelUpFemale or EvolutionType.UseItemFemale or EvolutionType.LevelUpRecoilDamageFemale => 1,
        _ => null,
    };

    public static void Evolve(PA8 pk, ushort species, byte form)
    {
        int level = pk.CurrentLevel;
        int lastEvolution = pk.MetLevel;
        var (fromSpecies, fromForm) = (pk.Species, pk.Form);
        var needed = new List<ushort>();
        foreach (var (next, nextForm) in Path(fromSpecies, fromForm, species, form))
        {
            var methods = Tree.Forward.GetForward(fromSpecies, fromForm).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(fromForm) == nextForm && (GenderOf(m.Method) is not { } g || g == pk.Gender)).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"{Plan8a.Label(fromSpecies, fromForm)}은(는) 이 성별로 {Plan8a.Label(next, nextForm)}(으)로 진화하지 않습니다.");
            var m = methods[0];
            if (m.Level > 0) level = Math.Max(level, m.Level);
            if (m.Method.IsLevelUpRequired) level = Math.Max(level, lastEvolution + 1);
            ushort usedMove = m.Method switch
            {
                EvolutionType.LevelUpKnowMove => (ushort)m.Argument,
                EvolutionType.UseMoveAgileStyle => (ushort)Move.PsyshieldBash,      // Stantler, twenty agile-style uses
                EvolutionType.UseMoveStrongStyle or EvolutionType.UseMoveBarbBarrage => (ushort)Move.BarbBarrage,   // Qwilfish, twenty strong-style uses
                _ => 0,
            };
            if (usedMove != 0)
            {
                // it has to know the move: learned by levelling up, at the level the learnset gives it
                var learn = LearnSource8LA.Instance.GetLearnset(fromSpecies, fromForm);
                if (learn.TryGetLevelLearnMove(usedMove, out var at)) level = Math.Max(level, at);
                needed.Add(usedMove);
            }
            if (m.Method is EvolutionType.LevelUpAffection50MoveType)
            {
                // Sylveon: a Fairy move known; Baby-Doll Eyes is the earliest Eevee learns
                var learn = LearnSource8LA.Instance.GetLearnset(fromSpecies, fromForm);
                ushort fairy = learn.GetMoveRange(100).ToArray().FirstOrDefault(mv => MoveInfo.GetType(mv, EntityContext.Gen8a) == m.Argument);
                if (fairy != 0) { if (learn.TryGetLevelLearnMove(fairy, out var at)) level = Math.Max(level, at); needed.Add(fairy); }
            }
            lastEvolution = level;
            (fromSpecies, fromForm) = (next, nextForm);
        }
        if (level > 100) throw new InvalidOperationException("진화에 필요한 레벨이 100 을 넘습니다.");

        int abilityIndex = pk.AbilityNumber >> 1;
        pk.Species = species;
        pk.Form = form;
        pk.CurrentLevel = (byte)level;
        pk.RefreshAbility(abilityIndex);
        var pi = Plan8a.Table.GetFormEntry(species, form);
        if (pi.Genderless) pk.Gender = 2;
        else if (pi.OnlyFemale) pk.Gender = 1;
        else if (pi.OnlyMale) pk.Gender = 0;
        if (!pk.IsNicknamed) pk.Nickname = SpeciesName.GetSpeciesNameGeneration(species, pk.Language, 8);
        // Hisui hands an evolved Pokémon the moves of what it became: what it learns up to its level, in order, and then
        // what it learns on evolving (level 0 in the table); the last four of those. What it knew before is gone.
        pk.SetMoves(MovesAt(species, form, level));
        pk.SetMasteryFlags();
        pk.ResetHeight(); pk.ResetWeight();
        // what the evolution counted: Wyrdeer's agile Psyshield Bashes, Overqwil's strong Barb Barrages, Basculegion's recoil
        pk.FormArgument = species switch { 899 or 904 => 20 + (uint)Random.Shared.Next(10), 902 => 294 + (uint)Random.Shared.Next(100), _ => pk.FormArgument };
        pk.HealPP();
        pk.ResetPartyStats();
        pk.RefreshChecksum();
        // a level the table does not state (Stantler evolves from 31): PKHeX's verdict decides, a level at a time
        while (pk.CurrentLevel < 100 && new LegalityAnalysis(pk).Results.Any(r => !r.Valid && r.Result == LegalityCheckResultCode.EvoInvalid))
        {
            pk.CurrentLevel++;
            pk.SetMoves(MovesAt(species, form, pk.CurrentLevel));
            pk.SetMasteryFlags();
            pk.HealPP();
            pk.ResetPartyStats();
            pk.RefreshChecksum();
        }
    }

    /// <summary>The moves a Pokémon of this species has after evolving at this level.</summary>
    public static ushort[] MovesAt(ushort species, byte form, int level)
    {
        var (learnset, _) = LearnSource8LA.GetLearnsetAndMastery(species, form);
        var all = learnset.GetMoveRange(100).ToArray();
        var ordered = all.Where(m => learnset.TryGetLevelLearnMove(m, out var at) && at >= 1 && at <= level)
                         .Concat(all.Where(m => learnset.TryGetLevelLearnMove(m, out var at) && at == 0)).ToList();
        var last = ordered.Skip(Math.Max(0, ordered.Count - 4)).ToList();
        while (last.Count < 4) last.Add(0);
        return last.ToArray();
    }

    /// <summary>A form reached without evolving: Rotom's appliances, Arceus's plates, the crystals, the Gracidea, the Reveal Glass.</summary>
    public static void ChangeForm(PA8 pk, byte form)
    {
        pk.Form = form;
        pk.RefreshAbility(pk.AbilityNumber >> 1);
        pk.ResetHeight(); pk.ResetWeight();
        pk.ResetPartyStats();
        pk.RefreshChecksum();
    }
}

using PKHeX.Core;

namespace Dexforge.Sword;

/// <summary>
/// Evolves a hatched or caught Pokémon in place, Sword-style: a level high enough for every step (and for the move a step
/// needs), the ability slot kept, the moves it would have at that level, the counters some evolutions leave behind, and
/// a trade evolution shown as given to a friend and taken back.
/// </summary>
public static class Evolve8
{
    private static readonly EvolutionTree Tree = Plan8.Tree;

    public static bool CanReach(ushort from, byte fromForm, ushort to, byte toForm) => Evolution.CanReach(Tree, from, fromForm, to, toForm);

    public static void Evolve(PK8 pk, ushort species, byte form, ITrainerInfo friend, Random random)
    {
        int level = pk.CurrentLevel;
        byte metLevel = pk.MetLevel;
        int lastEvolution = metLevel; // the level the previous stage was reached at: a level-up evolution needs one more
        var (fromSpecies, fromForm) = (pk.Species, pk.Form);
        var steps = new List<(ushort From, byte FromForm, EvolutionMethod[] Methods)>();
        ushort requiredMove = 0;
        bool traded = false;
        foreach (var (next, nextForm) in Evolution.Path(Tree, Plan8.Ko, fromSpecies, fromForm, species, form))
        {
            var methods = Tree.Forward.GetForward(fromSpecies, fromForm).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(fromForm) == nextForm).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"{Plan8.Ko.specieslist[fromSpecies]}은(는) {Plan8.Ko.specieslist[next]}(으)로 진화하지 않습니다.");
            steps.Add((fromSpecies, fromForm, methods));
            var m = methods.FirstOrDefault(x => x.Method != EvolutionType.LevelUpBeauty, methods[0]);
            if (m.Level > 0) level = Math.Max(level, m.Level);
            if (m.Method.IsLevelUpRequired) level = Math.Max(level, lastEvolution + 1);
            lastEvolution = level;
            if (m.Method.IsTrade) traded = true;
            if (Evolution.MoveToEvolve(next) is { Length: > 0 } candidates)
            {
                var learn = LearnSource8SWSH.Instance.GetLearnset(fromSpecies, fromForm);
                var best = candidates.Select(mv => learn.TryGetLevelLearnMove(mv, out var at) ? (mv, at) : (mv, (byte)255)).MinBy(x => x.Item2);
                if (best.Item2 == 255) throw new InvalidOperationException($"{Plan8.Ko.specieslist[fromSpecies]}은(는) 진화에 필요한 기술을 레벨업으로 배우지 않습니다.");
                level = Math.Max(level, best.Item2);
                requiredMove = best.mv;
            }
            (fromSpecies, fromForm) = (next, nextForm);
        }
        if (level > 100) throw new InvalidOperationException("진화에 필요한 레벨이 100 을 넘습니다.");

        // Given to a friend for the trade and taken back: the friend's name stays as the handler.
        if (traded && pk.CurrentHandler == 0 && pk.HandlingTrainerName.Length == 0)
        {
            pk.UpdateHandler(friend);
            pk.CurrentHandler = 0;
        }

        int abilityIndex = pk.AbilityNumber >> 1;
        pk.Species = species;
        pk.Form = form;
        pk.CurrentLevel = (byte)level;
        pk.RefreshAbility(abilityIndex);
        var pi = Plan8.Table.GetFormEntry(species, form);
        if (pi.Genderless) pk.Gender = 2;
        else if (pi.OnlyFemale) pk.Gender = 1;
        else if (pi.OnlyMale) pk.Gender = 0;
        pk.Nickname = SpeciesName.GetSpeciesNameGeneration(species, pk.Language, 8);
        SetMoves(pk, (byte)level, requiredMove);
        pk.HealPP();
        pk.ResetPartyStats();

        // Alcremie: the sweet it was spun with. Others: whatever PKHeX suggests for the chain.
        if (species == 869) pk.FormArgument = (uint)random.Next(7);
        else
        {
            var history = new LegalityAnalysis(pk).Info.EvoChainsAllGens;
            pk.SetSuggestedFormArgument(species, form, EntityContext.Gen8, history, steps[0].From);
        }

        foreach (var (from, _, methods) in steps)
        {
            var results = methods.Select(m => m.Check(pk, (byte)level, metLevel, skipChecks: false, EvolutionRuleTweak.Default)).ToList();
            if (results.Contains(EvolutionCheckResult.Valid)) continue;
            throw new InvalidOperationException($"{Plan8.Ko.specieslist[from]}의 진화 조건을 못 맞췄습니다: {string.Join(", ", results)}");
        }
    }

    /// <summary>A form reached without evolving (an item, a key item): the form, and the item some forms must hold.</summary>
    public static void ChangeForm(PK8 pk, byte form)
    {
        pk.Form = form;
        pk.RefreshAbility(pk.AbilityNumber >> 1); // the same slot, the new form's ability
        var item = FormItem(pk.Species, form);
        if (item != 0) pk.HeldItem = item;
        if (pk.Species == 647)
        {
            // Keldeo: Resolute knows Secret Sword, Ordinary does not.
            const ushort secretSword = 548;
            var moves = pk.Moves.ToArray();
            if (form == 0 && moves.Contains(secretSword))
            {
                var learn = LearnSource8SWSH.Instance.GetLearnset(647, 0);
                Span<ushort> fresh = stackalloc ushort[4];
                learn.SetEncounterMovesBackwards(pk.CurrentLevel, fresh, sameDescend: false);
                for (int i = 0; i < 4; i++) if (moves[i] == secretSword) moves[i] = fresh.ToArray().FirstOrDefault(m => !moves.Contains(m));
                pk.SetMoves(moves);
                pk.HealPP();
            }
        }
        pk.ResetPartyStats();
    }

    /// <summary>Held items that are the form: Giratina's orb, Silvally's memories, Genesect's drives.</summary>
    private static int FormItem(ushort species, byte form) => species switch
    {
        487 when form == 1 => 112,                       // Griseous Orb
        773 when form != 0 => 903 + form,                 // Fighting Memory 904 .. Fairy Memory 920
        649 when form != 0 => 115 + form,                 // Douse 116, Shock 117, Burn 118, Chill 119
        _ => 0,
    };

    private static void SetMoves(PK8 pk, byte level, ushort requiredMove)
    {
        var learn = LearnSource8SWSH.Instance.GetLearnset(pk.Species, pk.Form);
        Span<ushort> moves = stackalloc ushort[4];
        learn.SetEncounterMovesBackwards(level, moves, sameDescend: false);
        if (requiredMove != 0 && !moves.Contains(requiredMove)) moves[3] = requiredMove;
        pk.SetMoves(moves);
        pk.SetRelearnMoves(pk.RelearnMoves); // keep what the egg skeleton gave
    }

    /// <summary>The gender an evolution path insists on, if every way of making a step needs the same one.</summary>
    public static byte? NeededGender(ushort from, byte fromForm, ushort to, byte toForm) =>
        from == to && fromForm == toForm ? null : Evolution.NeededGender(Tree, Plan8.Ko, from, fromForm, to, toForm);
}

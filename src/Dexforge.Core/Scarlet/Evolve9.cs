using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>
/// Evolves a hatched or caught Pokémon in place, Scarlet-style: a level high enough for every step (and for the move a step
/// needs), the ability slot kept, the moves it would have at that level, the counters some evolutions leave behind, and
/// a trade evolution shown as given to a friend and taken back.
/// </summary>
public static class Evolve9
{
    private static readonly EvolutionTree Tree = Plan9.Tree;

    public static bool CanReach(ushort from, byte fromForm, ushort to, byte toForm)
    {
        try { Path(from, fromForm, to, toForm); return true; }
        catch (InvalidOperationException) { return false; }
    }

    public static void Evolve(PK9 pk, ushort species, byte form, ITrainerInfo friend, Random random)
    {
        int level = pk.CurrentLevel;
        byte metLevel = pk.MetLevel;
        int lastEvolution = metLevel; // the level the previous stage was reached at: a level-up evolution needs one more
        var (fromSpecies, fromForm) = (pk.Species, pk.Form);
        var steps = new List<(ushort From, byte FromForm, EvolutionMethod[] Methods)>();
        ushort requiredMove = 0;
        bool traded = false;
        foreach (var (next, nextForm) in Path(fromSpecies, fromForm, species, form))
        {
            var methods = Tree.Forward.GetForward(fromSpecies, fromForm).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(fromForm) == nextForm).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"{Plan9.Ko.specieslist[fromSpecies]}은(는) {Plan9.Ko.specieslist[next]}(으)로 진화하지 않습니다.");
            steps.Add((fromSpecies, fromForm, methods));
            var m = methods.FirstOrDefault(x => x.Method != EvolutionType.LevelUpBeauty, methods[0]);
            if (m.Level > 0) level = Math.Max(level, m.Level);
            if (m.Method.IsLevelUpRequired) level = Math.Max(level, lastEvolution + 1);
            lastEvolution = level;
            if (IsTrade(m)) traded = true;
            if (MoveToEvolve(next) is { Length: > 0 } candidates)
            {
                var learn = LearnSource9SV.Instance.GetLearnset(fromSpecies, fromForm);
                var best = candidates.Select(mv => learn.TryGetLevelLearnMove(mv, out var at) ? (mv, at) : (mv, (byte)255)).MinBy(x => x.Item2);
                // learned by level, else taught (Dipplin's Dragon Cheer is a TM's): the move is set and PKHeX judges it
                if (best.Item2 != 255) level = Math.Max(level, best.Item2);
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
        var pi = Plan9.Table.GetFormEntry(species, form);
        if (pi.Genderless) pk.Gender = 2;
        else if (pi.OnlyFemale) pk.Gender = 1;
        else if (pi.OnlyMale) pk.Gender = 0;
        if (!pk.IsNicknamed) pk.Nickname = SpeciesName.GetSpeciesNameGeneration(species, pk.Language, 9);   // a nicknamed gift keeps its name
        SetMoves(pk, (byte)level, requiredMove);
        pk.HealPP();
        pk.ResetPartyStats();

        // Alcremie: the sweet it was spun with. Others: whatever PKHeX suggests for the chain.
        if (species == 869) pk.FormArgument = (uint)random.Next(7);
        else
        {
            var history = new LegalityAnalysis(pk).Info.EvoChainsAllGens;
            pk.SetSuggestedFormArgument(species, form, EntityContext.Gen9, history, steps[0].From);
            if (species == (int)Species.Overqwil) pk.FormArgument = 0;   // here it evolves by knowing the move; a count is Hisui's

        }

        foreach (var (from, _, methods) in steps)
        {
            var results = methods.Select(m => m.Check(pk, (byte)level, metLevel, skipChecks: false, EvolutionRuleTweak.Default)).ToList();
            if (results.Contains(EvolutionCheckResult.Valid)) continue;
            throw new InvalidOperationException($"{Plan9.Ko.specieslist[from]}의 진화 조건을 못 맞췄습니다: {string.Join(", ", results)}");
        }
    }

    /// <summary>A form reached without evolving: the form, and the item some forms must hold (Giratina's orb, Ogerpon's masks).</summary>
    public static void ChangeForm(PK9 pk, byte form)
    {
        pk.Form = form;
        pk.RefreshAbility(pk.AbilityNumber >> 1);
        var item = FormItem(pk.Species, form);
        if (item != 0) pk.HeldItem = item;
        pk.ResetPartyStats();
    }

    /// <summary>Held items that are the form.</summary>
    private static int FormItem(ushort species, byte form) => species switch
    {
        487 when form == 1 => 112,            // Griseous Orb
        1017 => PKHeX.Core.FormItem.GetItemOgerpon(form),   // the masks are not in form order: Cornerstone 2406, Wellspring 2407, Hearthflame 2408
        _ => 0,
    };

    private static void SetMoves(PK9 pk, byte level, ushort requiredMove)
    {
        var learn = LearnSource9SV.Instance.GetLearnset(pk.Species, pk.Form);
        Span<ushort> moves = stackalloc ushort[4];
        learn.SetEncounterMovesBackwards(level, moves, sameDescend: false);
        if (requiredMove != 0 && !moves.Contains(requiredMove))
        {
            moves[3] = requiredMove;
            // a move the species is taught (Dipplin's Dragon Cheer): the game flags the TM as learned on the Pokémon
            var pi = Plan9.Table.GetFormEntry(pk.Species, pk.Form);
            int index = pi.RecordPermitIndexes.IndexOf(requiredMove);
            if (index >= 0 && pi.GetIsLearnTM(index)) pk.SetMoveRecordFlag(index, true);
        }
        pk.SetMoves(moves);
        pk.SetRelearnMoves(pk.RelearnMoves); // keep what the egg skeleton gave
    }

    private static bool IsTrade(EvolutionMethod m) => m.Method is EvolutionType.Trade or EvolutionType.TradeHeldItem or EvolutionType.TradeShelmetKarrablast;

    /// <summary>Evolutions that need a move known or used, as the species evolved into (PKHeX's table).</summary>
    private static ushort[] MoveToEvolve(ushort evolved) => (Species)evolved switch
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

    /// <summary>The gender an evolution path insists on, if every way of making a step needs the same one.</summary>
    public static byte? NeededGender(ushort from, byte fromForm, ushort to, byte toForm)
    {
        if (from == to && fromForm == toForm) return null;
        var (species, form) = (from, fromForm);
        foreach (var (next, nextForm) in Path(from, fromForm, to, toForm))
        {
            var methods = Tree.Forward.GetForward(species, form).Span.ToArray()
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

    /// <summary>The stages from the caught species (exclusive) to the wanted one (inclusive).</summary>
    private static List<(ushort Species, byte Form)> Path(ushort from, byte fromForm, ushort to, byte toForm)
    {
        var chain = Tree.Reverse.GetPreEvolutions(to, toForm).Select(x => (x.Species, x.Form)).ToList();
        chain.Add((to, toForm));
        int start = chain.FindIndex(x => x.Species == from && x.Form == fromForm);
        if (start < 0) throw new InvalidOperationException($"{Plan9.Ko.specieslist[from]}은(는) {Plan9.Ko.specieslist[to]}으로 진화하지 않습니다.");
        return chain.Skip(start + 1).ToList();
    }
}

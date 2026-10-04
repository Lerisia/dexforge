using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>
/// Evolves a caught Z-A Pokémon in place, as the hex bot does: a level high enough for every step (and for the move a
/// step needs), the ability left as caught (Z-A has no abilities, and PKHeX discards an origin whose ability differs),
/// the moves it would have at that level with an alpha's mastered move kept, the counters some evolutions leave behind,
/// and a trade evolution shown as given to the trainer's other Z-A and taken back.
/// </summary>
public static class Evolve9a
{
    private static readonly EvolutionTree Tree = Plan9a.Tree;

    public static void Evolve(PA9 pk, ushort species, byte form, ITrainerInfo friend)
    {
        int level = pk.CurrentLevel;
        byte metLevel = pk.MetLevel;
        int lastEvolution = metLevel;
        var (fromSpecies, fromForm) = (pk.Species, pk.Form);
        var steps = new List<(ushort From, byte FromForm, EvolutionMethod[] Methods)>();
        ushort requiredMove = 0;
        bool traded = false;
        foreach (var (next, nextForm) in Path(fromSpecies, fromForm, species, form))
        {
            var methods = Tree.Forward.GetForward(fromSpecies, fromForm).Span.ToArray()
                .Where(m => m.Species == next && m.GetDestinationForm(fromForm) == nextForm).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"{Plan9a.Ko.specieslist[fromSpecies]}은(는) {Plan9a.Ko.specieslist[next]}(으)로 진화하지 않습니다.");
            steps.Add((fromSpecies, fromForm, methods));
            // beauty does not exist in Z-A: a trade-or-beauty step is a trade step
            var m = methods.FirstOrDefault(x => x.Method != EvolutionType.LevelUpBeauty, methods[0]);
            if (m.Level > 0) level = Math.Max(level, m.Level);
            if (m.Method.IsLevelUpRequired) level = Math.Max(level, lastEvolution + 1);
            lastEvolution = level;
            if (IsTrade(m)) traded = true;
            if (MoveToEvolve(next) is { Length: > 0 } candidates)
            {
                // the earlier stage must have been able to learn the move by then; the earliest of the candidates is taken
                var (learn, _) = LearnSource9ZA.GetLearnsetAndPlus(fromSpecies, fromForm);
                var best = candidates.Select(mv => learn.TryGetLevelLearnMove(mv, out var at) ? (mv, at) : (mv, (byte)255)).MinBy(x => x.Item2);
                if (best.Item2 == 255)
                    throw new InvalidOperationException($"{Plan9a.Ko.specieslist[fromSpecies]}은(는) {Plan9a.Ko.specieslist[next]}(으)로 진화하는 데 필요한 기술을 레벨업으로 배우지 않습니다.");
                level = Math.Max(level, best.Item2);
                requiredMove = best.mv;
            }
            (fromSpecies, fromForm) = (next, nextForm);
        }
        if (level > 100) throw new InvalidOperationException("진화에 필요한 레벨이 100 을 넘습니다.");

        // given to the trainer's other Z-A for the trade and taken back: that name stays as the handler
        if (traded && pk.CurrentHandler == 0 && pk.HandlingTrainerName.Length == 0)
        {
            pk.UpdateHandler(friend);
            pk.CurrentHandler = 0;
        }

        var caughtSpecies = pk.Species;
        var caughtAlphaMove = pk.IsAlpha ? Plan9a.Table[pk.Species, pk.Form].AlphaMove : (ushort)0;
        pk.Species = species;
        pk.Form = form;
        pk.CurrentLevel = (byte)level;
        var pi = Plan9a.Table[species, form];
        if (pi.Genderless) pk.Gender = 2;
        else if (pi.OnlyFemale) pk.Gender = 1;
        else if (pi.OnlyMale) pk.Gender = 0;
        if (!pk.IsNicknamed) pk.Nickname = SpeciesName.GetSpeciesNameGeneration(species, pk.Language, 9);
        SetMoves(pk, pi, (byte)level, requiredMove, caughtAlphaMove);
        pk.HealPP();
        pk.ResetPartyStats();

        // counters some evolutions leave behind (coins collected, HP lost, critical hits): PKHeX's suggestion for this chain
        var history = new LegalityAnalysis(pk).Info.EvoChainsAllGens;
        pk.SetSuggestedFormArgument(species, form, EntityContext.Gen9a, history, caughtSpecies);

        foreach (var (from, _, methods) in steps)
        {
            var results = methods.Select(m => m.Check(pk, (byte)level, metLevel, skipChecks: false, EvolutionRuleTweak.Default)).ToList();
            if (results.Contains(EvolutionCheckResult.Valid)) continue;
            throw new InvalidOperationException($"{Plan9a.Ko.specieslist[from]}의 진화 조건을 못 맞췄습니다: {string.Join(", ", results)}");
        }
    }

    /// <summary>
    /// A form reached without evolving (Furfrou's trims, Genesect's drives, Hoopa unbound, Keldeo resolute, Zygarde's 50%):
    /// the form, the item some need, the form's signature move mastered (Hoopa unbound's Hyperspace Fury, Keldeo
    /// resolute's Secret Sword), and the counter a trim leaves (the days it lasts).
    /// </summary>
    public static void ChangeForm(PA9 pk, byte form)
    {
        var caughtSpecies = pk.Species;
        pk.Form = form;
        var pi = Plan9a.Table[pk.Species, form];
        var item = FormItem(pk.Species, form);
        if (item != 0) pk.HeldItem = item;
        ushort signature = (Species)pk.Species switch
        {
            Species.Keldeo when form == 1 => (ushort)Move.SecretSword,
            Species.Hoopa when form == 1 => (ushort)Move.HyperspaceFury,
            _ => 0,
        };
        // the moves and mastery it came with stay (a gift's mastered move is checked for); the form's signature move joins them
        Span<ushort> moves = stackalloc ushort[4];
        pk.GetMoves(moves);
        if (signature != 0 && !moves.Contains(signature))
        {
            // Hoopa's Hyperspace Hole becomes Hyperspace Fury with the form; otherwise the signature takes the first slot
            int at = moves.IndexOf((ushort)Move.HyperspaceHole);
            if (at >= 0) moves[at] = signature;   // the Hole's mastery stays recorded; PKHeX looks for it
            else
            {
                for (int i = 3; i > 0; i--) moves[i] = moves[i - 1];
                moves[0] = signature;
            }
        }
        if (signature != 0 && pi.PlusMoveIndexes.IndexOf(signature) >= 0) pk.SetPlusFlagsSpecific(pi, signature);
        pk.SetMoves(moves);
        pk.HealPP();
        var history = new LegalityAnalysis(pk).Info.EvoChainsAllGens;
        pk.SetSuggestedFormArgument(pk.Species, form, EntityContext.Gen9a, history, caughtSpecies);
        pk.ResetPartyStats();
    }

    /// <summary>Held items that are the form: Genesect's drives.</summary>
    private static int FormItem(ushort species, byte form) => species switch
    {
        649 when form is >= 1 and <= 4 => 115 + form,   // Douse 116, Shock 117, Burn 118, Chill 119 Drive
        _ => 0,
    };

    private static void SetMoves(PA9 pk, PersonalInfo9ZA pi, byte level, ushort requiredMove, ushort caughtAlphaMove)
    {
        var (learn, plus) = LearnSource9ZA.GetLearnsetAndPlus(pk.Species, pk.Form);
        pk.ClearMovePlusFlags();
        pk.SetPlusFlagsEncounter(pi, plus, level);
        Span<ushort> moves = stackalloc ushort[4];
        learn.SetEncounterMovesBackwards(level, moves, sameDescend: false);
        if (requiredMove != 0 && !moves.Contains(requiredMove)) moves[3] = requiredMove;
        // an alpha keeps the alpha move it was caught with mastered; PKHeX checks for the caught species' move
        if (pk.IsAlpha && caughtAlphaMove != 0 && pi.PlusMoveIndexes.IndexOf(caughtAlphaMove) >= 0)
        {
            if (!moves.Contains(caughtAlphaMove)) moves[0] = caughtAlphaMove;
            pk.SetPlusFlagsSpecific(pi, caughtAlphaMove);
        }
        pk.SetMoves(moves);
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
        if (from == to) return null;   // the same species, perhaps another form: no evolution to insist on anything
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
        if (start < 0) throw new InvalidOperationException($"{Plan9a.Ko.specieslist[from]}은(는) {Plan9a.Ko.specieslist[to]}으로 진화하지 않습니다.");
        return chain.Skip(start + 1).ToList();
    }
}

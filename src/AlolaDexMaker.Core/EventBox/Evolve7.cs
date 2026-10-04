using PKHeX.Core;

namespace AlolaDexMaker.EventBox;

/// <summary>
/// Evolves a received Pokémon in place, as the games do: a level high enough for every step, the ability slot kept, the card's moves kept,
/// the name updated where it was not nicknamed. What came from an older generation evolved there (Kantonian forms); what is Alolan evolved in Alola.
/// </summary>
public static class Evolve7
{
    public static readonly EvolutionTree Tree = EvolutionTree.Evolves7;

    /// <summary>Every final stage reachable from here (each reached by one chain of steps).</summary>
    public static List<(ushort Species, byte Form)> Finals(ushort species, byte form)
    {
        var outList = new List<(ushort, byte)>();
        foreach (var (sp, fo) in Tree.Forward.GetEvolutions(species, form))
        {
            if (!Tree.Forward.GetEvolutions(sp, fo).Any()) outList.Add((sp, fo));
            else outList.AddRange(Finals(sp, fo));
        }
        return outList.Distinct().ToList();
    }

    /// <summary>The steps from one stage to another, as (species, form) after each step.</summary>
    public static List<(ushort, byte)> Path(ushort from, byte fromForm, ushort to, byte toForm)
    {
        foreach (var (sp, fo) in Tree.Forward.GetEvolutions(from, fromForm))
        {
            if (sp == to && fo == toForm) return [(sp, fo)];
            var rest = TryPath(sp, fo, to, toForm);
            if (rest is not null) { rest.Insert(0, (sp, fo)); return rest; }
        }
        throw new InvalidOperationException($"{from}-{fromForm} does not evolve into {to}-{toForm}");
    }

    private static List<(ushort, byte)>? TryPath(ushort from, byte fromForm, ushort to, byte toForm)
    {
        try { return Path(from, fromForm, to, toForm); } catch (InvalidOperationException) { return null; }
    }

    /// <summary>Whether this chain can be walked by a Pokémon of this gender (Gallade is male, Froslass female).</summary>
    public static bool GenderAllows(ushort species, byte form, byte gender)
    {
        var pi = PersonalTable.USUM.GetFormEntry(species, form);
        if (pi.Genderless) return true;
        if (pi.OnlyMale) return gender == 0;
        if (pi.OnlyFemale) return gender == 1;
        return true;
    }

    public static void Evolve(PK7 pk, ushort species, byte form)
    {
        int level = pk.CurrentLevel;
        int lastEvolution = pk.MetLevel;
        var (fromSpecies, fromForm) = (pk.Species, pk.Form);
        ushort requiredMove = 0;
        foreach (var (next, nextForm) in Path(fromSpecies, fromForm, species, form))
        {
            var methods = Tree.Forward.GetForward(fromSpecies, fromForm).Span.ToArray().Where(m => m.Species == next && m.GetDestinationForm(fromForm) == nextForm).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"no method from {fromSpecies}-{fromForm} to {next}-{nextForm}");
            var m = methods[0];
            if (m.Level > 0) level = Math.Max(level, m.Level);
            if (m.Method.IsLevelUpRequired) level = Math.Max(level, lastEvolution + 1);
            if (m.Method is EvolutionType.LevelUpKnowMove)
            {
                // it has to know the move (Steenee: Stomp): learned by levelling up, at the level the learnset gives it
                requiredMove = m.Argument;
                var learn = LearnSource7USUM.Instance.GetLearnset(fromSpecies, fromForm);
                if (learn.TryGetLevelLearnMove(requiredMove, out var at)) level = Math.Max(level, at);
            }
            lastEvolution = level;
            (fromSpecies, fromForm) = (next, nextForm);
        }
        if (level > 100) throw new InvalidOperationException("would need a level above 100");

        int abilityIndex = pk.AbilityNumber >> 1;
        bool nicknamed = pk.IsNicknamed;
        pk.Species = species;
        pk.Form = form;
        pk.CurrentLevel = (byte)level;
        pk.RefreshAbility(abilityIndex);
        var pi = PersonalTable.USUM.GetFormEntry(species, form);
        if (pi.Genderless) pk.Gender = 2;
        else if (pi.OnlyFemale) pk.Gender = 1;
        else if (pi.OnlyMale) pk.Gender = 0;
        if (!nicknamed) pk.Nickname = SpeciesName.GetSpeciesNameGeneration(species, pk.Language, 7);
        if (requiredMove != 0 && !pk.HasMove(requiredMove))
        {
            // the move it evolved with takes an empty slot, or the last one
            int slot = pk.Move1 == 0 ? 0 : pk.Move2 == 0 ? 1 : pk.Move3 == 0 ? 2 : 3;
            pk.SetMove(slot, requiredMove);
        }
        pk.HealPP();
        pk.ResetPartyStats();
        pk.RefreshChecksum();
    }
}

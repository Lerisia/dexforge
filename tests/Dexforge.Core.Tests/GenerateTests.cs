using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>What every save the generator makes must be, whatever was asked for.</summary>
public static class Every
{
    public static void IsWhole(Made m)
    {
        Assert.Empty(m.Generator.Problems);
        Assert.Empty(m.Result.Faults);
        Assert.Equal(m.Was.Count, m.All.Count);
        Assert.Equal(m.Result.Count, m.Result.Legal);
        Assert.True(m.Save.ChecksumsValid);
        Assert.Equal(807, m.All.Select(p => p.Species).Distinct().Count());
        Assert.All(m.All, p => Assert.True(new LegalityAnalysis(p).Valid, GameInfo.GetStrings("ko").Species[p.Species]));
    }

    public static void BelongsToWhoeverAsked(Made m)
    {
        Assert.Equal(m.Asked.Name, m.Save.OT);
        if (m.Asked.Tid is { } tid) Assert.Equal(tid, m.Save.TrainerTID7);
        if (m.Asked.Sid is { } sid) Assert.Equal(sid, m.Save.TrainerSID7);
        Assert.Equal(m.Asked.Name, m.Save.FieldMenu.RotomOT);
        Assert.Equal(1, m.Save.Gender);
        // Nobody else's numbers are left on anything, and no two Pokemon are the same.
        Assert.DoesNotContain(m.All, p => p.ID32 == m.Before.ID32 && m.Before.ID32 != m.Save.ID32);
        var drawn = m.All.Where(p => Tell.Of(p) != Came.Card).ToList();
        Assert.Equal(drawn.Count, drawn.Select(p => p.EncryptionConstant).Distinct().Count());
        Assert.Equal(drawn.Count, drawn.Select(p => p.PID).Distinct().Count());
        // Where nobody else has held a Pokemon, nothing is written where the holder's name goes.
        Assert.All(m.All.Where(p => p.HandlingTrainerName.Length == 0), p => Assert.False(p.HandlingTrainerTrash.ContainsAnyExcept((byte)0)));
    }

    /// <summary>Every Pokemon hatched or caught came of a state of the game that is written down, and drawing from that state again gives the Pokemon.</summary>
    public static void CanBeDrawnAgain(Made m)
    {
        var origins = m.Generator.AllOrigins.ToDictionary(o => o.Ec);
        var owed = m.All.Where(p => Tell.Of(p) is Came.Hatched or Came.Caught).ToList();
        Assert.Equal(owed.Count, origins.Count);
        foreach (var pk in owed)
        {
            string name = GameInfo.GetStrings("ko").Species[pk.Species];
            Assert.True(origins.TryGetValue(pk.EncryptionConstant, out var origin), name);
            Assert.True(origin!.Tid == pk.TID16 && origin.Sid == pk.SID16, name);
            switch (origin)
            {
                case StillOrigin o:
                {
                    var stream = new Stream7(o.Seed);
                    var (index, first, models) = Timeline7.Walk(stream, Timeline7.Begins, o.Index, o.Met.Npc + 1, o.Met.Raining).Last();
                    Assert.True(index == o.Index && first, name);
                    var d = new Meeting7(stream, index, models).Still(o.Met, o.Tsv, o.Charm);
                    Same(pk, d.Ec, d.Pid, d.Ivs, d.Nature, d.Ability, d.Gender, d.Shiny, name);
                    break;
                }
                case GrassOrigin o:
                {
                    var stream = new Stream7(o.Seed);
                    var (index, first, models) = Timeline7.Walk(stream, Timeline7.Begins, o.Index, o.Area.Npc + 1, o.Area.Raining).Last();
                    Assert.True(index == o.Index && first, name);
                    var d = o.Beast is { } b
                        ? new Meeting7(stream, index, models).Wild(o.Area, o.Moon, o.Night, o.Tsv, o.Charm, o.SpeciesForm, b.Level, b.Rate)
                        : new Meeting7(stream, index, models).Wild(o.Area, o.Moon, o.Night, o.Tsv, o.Charm, levels: o.Levels);
                    Assert.True(d.Species == o.SpeciesForm && d.Level == pk.MetLevel, name);
                    Same(pk, d.Ec, d.Pid, d.Ivs, d.Nature, d.Ability, d.Gender, d.Shiny, name);
                    break;
                }
                case EggOrigin o:
                {
                    var d = Egg7.Lay(new Tiny(o.State[0], o.State[1], o.State[2], o.State[3]), o.Parents, o.Tsv);
                    // Shedinja has no sex, whatever the Nincada it came of had.
                    byte sex = pk.Species == (int)Species.Shedinja ? (byte)0 : d.Gender;
                    Same(pk, d.Ec, d.Pid, d.Ivs, d.Nature, d.Ability, sex, d.Shiny, name);
                    break;
                }
                default: Assert.Fail($"{name}: no way is known to draw it again"); break;
            }
        }
    }

    private static void Same(PKM pk, uint ec, uint pid, int[] ivs, byte nature, byte ability, byte gender, bool shiny, string name)
    {
        Assert.True(pk.EncryptionConstant == ec && pk.PID == pid, name);
        Assert.True(new[] { pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE }.SequenceEqual(ivs), name);
        Assert.True((int)pk.Nature == nature, name);
        Assert.True(pk.AbilityNumber == (ability == 3 ? 4 : ability), name);
        Assert.True(pk.Gender == gender switch { 1 => 0, 2 => 1, _ => 2 }, name);
        Assert.True(pk.IsShiny == shiny, name);
    }

    /// <summary>
    /// What the options do not reach is as it is in the template: species, form, moves, where and how each was met;
    /// the level, but where level 100 is asked for; the sex, but where it is free to be asked for. The party is as it was.
    /// </summary>
    public static void IsOtherwiseTheTemplate(Made m)
    {
        int inBoxes = m.Was.Count - m.Before.PartyCount;
        var both = Sexes.Both(m.Was.Take(inBoxes));
        for (int i = 0; i < m.Was.Count; i++)
        {
            var o = m.Was[i]; var p = m.All[i];
            string name = GameInfo.GetStrings("ko").Species[o.Species];
            bool boxed = i < inBoxes;
            int level = boxed && m.Asked.Level == LevelChoice.Hundred ? 100 : o.CurrentLevel;
            bool sexFree = boxed && Sexes.Free(o, new LegalityAnalysis(o).EncounterMatch, both);
            Assert.True(o.Species == p.Species && o.Form == p.Form && p.CurrentLevel == level && o.MetLevel == p.MetLevel && (sexFree || o.Gender == p.Gender), name);
            Assert.True(o.Move1 == p.Move1 && o.Move2 == p.Move2 && o.Move3 == p.Move3 && o.Move4 == p.Move4, name);
            Assert.True(o.MetLocation == p.MetLocation && o.EggLocation == p.EggLocation && o.Version == p.Version && o.Language == p.Language, name);
            Assert.True(o.HeldItem == p.HeldItem && o.CurrentHandler == p.CurrentHandler && o.FatefulEncounter == p.FatefulEncounter, name);
        }
        for (int i = 0; i < 200; i++) Assert.Equal(m.Before.GetRecord(i), m.Save.GetRecord(i));
    }
}

/// <summary>Nothing asked for: 미월, a Poke Ball for everything, shiny, values as they come, the year 2018.</summary>
public sealed class AsItComes() : Made(new Options("미월", null, null, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 20180101, (int)Ball.Poke));

public class WhenNothingIsAskedFor(AsItComes m) : IClassFixture<AsItComes>
{
    [Fact] public void TheSaveIsWhole() => Every.IsWhole(m);
    [Fact] public void ItBelongsToWhoeverAsked() => Every.BelongsToWhoeverAsked(m);
    [Fact] public void EveryPokemonCanBeDrawnAgain() => Every.CanBeDrawnAgain(m);
    [Fact] public void TheRestIsTheTemplate() => Every.IsOtherwiseTheTemplate(m);

    [Fact]
    public void WhatCanBeShinyIs()
    {
        for (int i = 0; i < m.Was.Count; i++) Assert.Equal(m.Was[i].IsShiny, m.All[i].IsShiny);
    }

    [Fact]
    public void EverythingIsInAPokeBallButWhatCameOnACard()
    {
        for (int i = 0; i < m.Was.Count; i++)
        {
            if (Tell.Of(m.Was[i]) == Came.Card) Assert.Equal(m.Was[i].Ball, m.All[i].Ball);
            else Assert.Equal((int)Ball.Poke, m.All[i].Ball);
        }
    }

    [Fact]
    public void TheDaysAreWithinTheYear()
    {
        var party = m.Save.PartyData.Select(p => p.EncryptionConstant).ToHashSet();
        foreach (var p in m.All.Where(p => Tell.Of(p) is Came.Hatched or Came.Caught && !party.Contains(p.EncryptionConstant)))
            Assert.InRange(p.MetDate!.Value, m.Asked.From, m.Asked.To);
        Assert.True(m.Generator.Began < m.Asked.From.AddDays(-20));
        Assert.True(m.Generator.Began >= new DateOnly(2017, 11, 17));
    }
}

/// <summary>Everything asked for: a name and both numbers, plain colour, one ball, five perfect values from eggs, a single day, males, level 100.</summary>
public sealed class AsAsked() : Made(new Options("테스트", 567890, 1234, new DateOnly(2018, 5, 5), new DateOnly(2018, 5, 5), 20180505,
                                                  Array.IndexOf(GameInfo.GetStrings("ko").balllist, "럭셔리볼"), IvChoice.FiveFromEggs, false,
                                                  LevelChoice.Hundred, SexChoice.Male, "Luna", "ルナ", "月"));

public class WhenEverythingIsAskedFor(AsAsked m) : IClassFixture<AsAsked>
{
    [Fact] public void TheSaveIsWhole() => Every.IsWhole(m);
    [Fact] public void ItBelongsToWhoeverAsked() => Every.BelongsToWhoeverAsked(m);
    [Fact] public void EveryPokemonCanBeDrawnAgain() => Every.CanBeDrawnAgain(m);
    [Fact] public void TheRestIsTheTemplate() => Every.IsOtherwiseTheTemplate(m);

    [Fact]
    public void TheTemplatesNameIsNowhere()
    {
        Assert.False(Template.Holds(m.Bytes, m.Before.OT));
        Assert.DoesNotContain(m.All, p => p.OriginalTrainerName == m.Before.OT || p.HandlingTrainerName == m.Before.OT);
    }

    [Fact]
    public void OnlyWhatACardMakesShinyIs()
    {
        var shiny = m.All.Where(p => p.IsShiny).ToList();
        Assert.All(shiny, p => Assert.Equal(Came.Card, Tell.Of(p)));
        Assert.Equal(4, shiny.Count);   // Jirachi, Arceus, Genesect, Diancie: mythicals whose cards are shiny; the rest of the template is caught plain since 2026-10-05
    }

    [Fact]
    public void WhoeverPlaysInAnotherLanguageHasTheNameAskedFor()
    {
        var names = new Dictionary<string, string> { [ForeignNames.English] = "Luna", [ForeignNames.Japanese] = "ルナ", [ForeignNames.Chinese] = "月" };
        int renamed = 0;
        for (int i = 0; i < m.Was.Count; i++)
            if (names.TryGetValue(m.Was[i].OriginalTrainerName, out var name)) { Assert.Equal(name, m.All[i].OriginalTrainerName); renamed++; }
        Assert.Equal(22, renamed);
        Assert.DoesNotContain(m.All, p => names.ContainsKey(p.OriginalTrainerName));
    }

    [Fact]
    public void WhateverIsFreeToBeMaleIs()
    {
        var both = Sexes.Both(m.Was.Take(m.Was.Count - 3));
        int free = 0;
        for (int i = 0; i < m.Was.Count - 3; i++)
        {
            var was = m.Was[i]; var p = m.All[i];
            if (Sexes.Free(was, new LegalityAnalysis(was).EncounterMatch, both)) { free++; Assert.Equal(0, p.Gender); }
            else Assert.Equal(was.Gender, p.Gender);
        }
        Assert.True(free > 700, $"{free}");
        // The species kept in both sexes, as they were.
        Assert.Contains(m.All, p => p.Species == (int)Species.Hippowdon && p.Gender == 1);
    }

    [Fact]
    public void TheBoxesAreAtLevel100AndThePartyIsAsItWas()
    {
        for (int i = 0; i < m.Was.Count; i++)
        {
            bool party = i >= m.Was.Count - 3;
            Assert.Equal(party ? m.Was[i].CurrentLevel : 100, m.All[i].CurrentLevel);
            Assert.Equal(m.Was[i].Moves, m.All[i].Moves);
        }
    }

    [Fact]
    public void WhatHatchedHasFivePerfectValues()
    {
        Assert.All(m.All.Where(p => Tell.Of(p) == Came.Hatched), p => Assert.True(Tell.Perfect(p) >= 5));
    }

    [Fact]
    public void EverythingIsInTheBallOrAPokeBall()
    {
        int want = m.Asked.Ball!.Value, inIt = 0;
        for (int i = 0; i < m.Was.Count; i++)
        {
            var p = m.All[i];
            if (Tell.Of(m.Was[i]) == Came.Card) { Assert.Equal(m.Was[i].Ball, p.Ball); continue; }
            Assert.True(p.Ball == want || p.Ball == (int)Ball.Poke);
            if (p.Ball == want) { inIt++; continue; }
            // A Poke Ball only where the ball asked for will not do.
            var t = (PK7)p.Clone(); t.Ball = (byte)want; t.RefreshChecksum();
            Assert.False(new LegalityAnalysis(t).Valid);
        }
        Assert.True(inIt > 800);
    }

    /// <summary>The count the window shows for the ball is the count a save made with it comes to, whatever the seed.</summary>
    [Fact]
    public void AsManyCannotGoInTheBallAsTheWindowSays()
    {
        int cannot = 0;
        for (int i = 0; i < m.Was.Count - m.Before.PartyCount; i++)
            // Every card keeps its ball, the Ranger's Manaphy egg among them.
            if (new LegalityAnalysis(m.Was[i]).EncounterMatch is not MysteryGift && m.All[i].Ball != m.Asked.Ball) cannot++;
        Assert.Equal(BallFits.CannotGoIn[(Ball)m.Asked.Ball!.Value], cannot);
    }

    [Fact]
    public void EverythingIsOfTheOneDay()
    {
        var party = m.Save.PartyData.Select(p => p.EncryptionConstant).ToHashSet();
        foreach (var p in m.All.Where(p => (Tell.Of(p) is Came.Hatched or Came.Caught || Tell.Of(p) == Came.Older && p.Generation < 6) && !party.Contains(p.EncryptionConstant)))
            Assert.Equal(m.Asked.From, p.MetDate!.Value);
    }
}

/// <summary>Six perfect values, plain colour, the balls picked for each.</summary>
public sealed class Perfect() : Made(new Options("미월", null, null, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 20180606, null, IvChoice.Six, false));

public class WhenSixPerfectValuesAreAskedFor(Perfect m) : IClassFixture<Perfect>
{
    [Fact] public void TheSaveIsWhole() => Every.IsWhole(m);
    [Fact] public void EveryPokemonCanBeDrawnAgain() => Every.CanBeDrawnAgain(m);

    [Fact]
    public void WhatHatchedAndWhatIsPromisedThreeHaveSix()
    {
        Assert.All(m.All.Where(p => Tell.Of(p) == Came.Hatched), p => Assert.Equal(6, Tell.Perfect(p)));
        Assert.All(m.All.Where(p => Tell.Of(p) == Came.Caught && Tell.Promised(p)), p => Assert.Equal(6, Tell.Perfect(p)));
    }

    [Fact]
    public void EachIsInTheFirstBallOfItsListItCanBeIn()
    {
        // The Ralts line and Mimikyu go by sex: a Moon Ball when male, a Love Ball when female; a card's ball is the card's.
        for (int i = 0; i < m.Was.Count; i++)
        {
            var p = m.All[i];
            if (new LegalityAnalysis(p).EncounterMatch is MysteryGift) { Assert.Equal(m.Was[i].Ball, p.Ball); continue; }
            Assert.Contains((Ball)p.Ball, Balls.Shared.Prefer(p.Species, p.Form, p.Gender == 2 ? null : p.Gender, p.IsShiny));
        }
        Assert.All(m.All.Where(p => p.Species is 280 or 281 or 282 or 778 && p.Gender == 0), p => Assert.Equal((int)Ball.Moon, p.Ball));
        Assert.All(m.All.Where(p => p.Species is 280 or 281 or 282 or 778 && p.Gender == 1), p => Assert.Equal((int)Ball.Love, p.Ball));
    }
}

/// <summary>Six perfect values and shiny: the game's random numbers hold few such moments, and finding them takes minutes.</summary>
public class WhenSixPerfectValuesAndShinyAreAskedFor
{
    [SlowFact]
    public void TheSaveIsWholeAndCanBeDrawnAgain()
    {
        var m = new PerfectAndShiny();
        Every.IsWhole(m);
        Every.CanBeDrawnAgain(m);
        Assert.All(m.All.Where(p => Tell.Of(p) == Came.Hatched), p => Assert.True(p.IsShiny && Tell.Perfect(p) == 6));
    }

    private sealed class PerfectAndShiny() : Made(new Options("미월", null, null, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 20180707, null, IvChoice.Six, true));
}

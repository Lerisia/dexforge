using Dexforge.Sword;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>Ribbons: the list the program offers, what it puts on, and what it leaves alone.</summary>
public class RibbonTests
{
    [Fact]
    public void Every_ribbon_offered_is_a_flag_PKHeX_knows_with_a_name_the_game_uses()
    {
        var ko = GameInfo.GetStrings("ko");
        var names = ko.ribbons.Select(l => l.Split('\t').Last()).ToHashSet();
        foreach (var r in Ribbons.All)
        {
            Assert.NotNull(typeof(PK7).GetProperty(r.Key));
            Assert.Contains(r.Name.Replace(" (ORAS)", ""), names.Select(n => n.Split(" (")[0]).Concat(names));
            Assert.NotNull(Ribbons.Find(r.Name));
            Assert.Equal(r, Ribbons.Find(r.Name.Replace("리본", "")));
        }
        Assert.Null(Ribbons.Find("클래식리본"));   // event-only ribbons are not on offer
        Assert.Equal(23, Ribbons.All.Count);
    }

    [Fact]
    public void Ribbons_go_on_where_PKHeX_allows_and_not_on_event_Pokemon()
    {
        var sav = Template.Read(Template.Bytes);   // no active trainer set: that is PKHeX's global state and would reach the other tests
        var all = Template.Pokemon(sav, party: false);
        var pikachu = all.First(p => p.Species == 25 && !p.FatefulEncounter && new LegalityAnalysis(p).EncounterMatch is not MysteryGift);
        var pk = (PK7)pikachu.Clone();
        int put = Ribbons.Put(pk, ["RibbonChampionAlola", "RibbonBestFriends", "RibbonEffort", "RibbonChampionSinnoh"]);
        Assert.Equal(3, put);   // the Sinnoh one needs a game this Pokémon never saw
        Assert.True(pk.RibbonChampionAlola && pk.RibbonBestFriends && pk.RibbonEffort && !pk.RibbonChampionSinnoh);
        Assert.Equal(510, pk.EVTotal);
        Assert.Equal(Effort.Spread(Effort.UltraSun.Of(25, 0, pk.Gender)!.Value), new[] { pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPE, pk.EV_SPA, pk.EV_SPD });
        Assert.Equal(255, pk.OriginalTrainerAffection);
        Assert.True(new LegalityAnalysis(pk).Valid);

        var older = all.First(p => p.Generation == 4 && !p.FatefulEncounter && new LegalityAnalysis(p).EncounterMatch is not MysteryGift);
        var pk4 = (PK7)older.Clone();
        Assert.Equal(2, Ribbons.Put(pk4, ["RibbonChampionSinnoh", "RibbonLegend"]));
        Assert.True(new LegalityAnalysis(pk4).Valid);

        var card = all.First(p => new LegalityAnalysis(p).EncounterMatch is MysteryGift);
        var pkc = (PK7)card.Clone();
        Assert.Equal(0, Ribbons.Put(pkc, ["RibbonChampionAlola"]));
        Assert.False(pkc.RibbonChampionAlola);
    }

    [Fact]
    public void Every_Pokemon_of_the_dex_has_an_effort_class_and_the_known_ones_are_right()
    {
        var sav = Template.Read(Template.Bytes);
        foreach (var p in Template.Pokemon(sav, party: false))
            if (!p.FatefulEncounter && new LegalityAnalysis(p).EncounterMatch is not MysteryGift)
                Assert.True(Effort.UltraSun.Of(p.Species, p.Form, p.Gender).HasValue, $"{p.Species}-{p.Form}");
        Assert.Equal(EffortClass.Tank, Effort.UltraSun.Of(143, 0, 0));            // Snorlax
        Assert.Equal(EffortClass.FastPhysical, Effort.UltraSun.Of(445, 0, 0));    // Garchomp
        Assert.Equal(EffortClass.FastSpecial, Effort.UltraSun.Of(65, 0, 0));      // Alakazam
        Assert.Equal(EffortClass.SlowSpecial, Effort.UltraSun.Of(133, 0, 0));     // Eevee, after Sylveon as the owner chose
        Assert.Equal(EffortClass.FastSpecial, Effort.UltraSun.Of(789, 0, 2));     // Cosmog, after Lunala
        Assert.Equal(EffortClass.Tank, Effort.UltraSun.Of(412, 1, 1));            // Burmy (Sandy), after Wormadam
        Assert.Null(Effort.UltraSun.Of(25, 7, 0));                                // a cap Pikachu is from a card and has no line
        Assert.Equal([252, 0, 252, 0, 0, 6], Effort.Spread(EffortClass.Tank));
        Assert.Equal([6, 0, 0, 252, 252, 0], Effort.Spread(EffortClass.FastSpecial));
    }

    [Fact]
    public void The_Sword_ribbons_go_on_a_hatched_Pokemon_and_the_rank_one_not_on_Melmetal()
    {
        var sav = new SAV8SWSH(Embedded.Bytes("sword.main"));
        var tr = new SimpleTrainerInfo(GameVersion.SW) { OT = "우리", ID32 = 123456, Gender = 0, Language = (int)LanguageID.Korean };
        Assert.Equal(5, Ribbons.Sword.Count);
        foreach (var r in Ribbons.Sword) { Assert.NotNull(typeof(PK8).GetProperty(r.Key)); Assert.Equal(r, Ribbons.Find(r.Name.Replace("리본", ""), Ribbons.Sword)); }
        Assert.Null(Ribbons.Find("알로라챔피언", Ribbons.Sword));

        var friend = new SimpleTrainerInfo(GameVersion.SW) { OT = "새아", Gender = 1, Language = 8, ID32 = 987654321 };
        var egg = new Maker8(tr, friend, new Balls8(Plan8.Ko), new Random(1), 2021, true, null).Make(Plan8.All().First(x => x.Species == 25 && x.Form == 0)).Pk;
        Assert.True(new LegalityAnalysis(egg).Valid);
        int put = Ribbons.Put(egg, Ribbons.Sword.Select(r => r.Key));
        Assert.Equal(5, put);
        Assert.True(egg.RibbonChampionGalar && egg.RibbonTowerMaster && egg.RibbonMasterRank && egg.RibbonEffort && egg.RibbonBestFriends);
        Assert.Equal(510, egg.EVTotal);
        Assert.Equal(Effort.Spread(Effort.Sword.Of(25, 0, egg.Gender)!.Value), new[] { egg.EV_HP, egg.EV_ATK, egg.EV_DEF, egg.EV_SPE, egg.EV_SPA, egg.EV_SPD });
        Assert.Equal(255, egg.CurrentFriendship);
        Assert.True(new LegalityAnalysis(egg).Valid);

        var rank = Ribbons.Sword.First(r => r.Key == "RibbonMasterRank");
        Assert.False(rank.Allowed!(new PK8 { Species = 809 }));   // Melmetal, a mythical: barred from Ranked Battles
        Assert.True(rank.Allowed!(egg));
    }

    [Fact]
    public void The_Sword_effort_table_covers_the_dex_and_keeps_the_Ultra_Sun_classes()
    {
        // every entry a ribbon can go on: not a card, and not a HOME gift (fateful, so left as it came)
        foreach (var e in Plan8.All())
        {
            if (e.Source is Source.None or Source.Card || e.Template is IFatefulEncounterReadOnly { FatefulEncounter: true }) continue;
            Assert.True(Effort.Sword.Of(e.Species, e.Form, 0).HasValue || Effort.Sword.Of(e.Species, e.Form, 1).HasValue, $"{e.Species}-{e.Form}");
        }
        Assert.Equal(Effort.UltraSun.Of(143, 0, 0), Effort.Sword.Of(143, 0, 0));             // Snorlax, the same table
        Assert.Equal(EffortClass.Tank, Effort.Sword.Of(79, 1, 0));                           // Galarian Slowpoke, from the gen 8 sets
        Assert.Equal(EffortClass.SlowSpecial, Effort.Sword.Of(133, 0, 0));                   // Eevee after Sylveon, as in Ultra Sun
        Assert.Equal(EffortClass.FastPhysical, Effort.Sword.Of(888, 0, 2));                  // Zacian
        Assert.Equal(EffortClass.FastSpecial, Effort.Sword.Of(840, 0, 0));                   // Applin after Appletun, as the owner chose (Choice Specs first on Smogon ZU, over the defensive set)
    }
}

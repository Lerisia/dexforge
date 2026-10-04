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
}

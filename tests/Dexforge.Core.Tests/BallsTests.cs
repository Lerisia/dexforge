using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>The one ball table: lists in order of preference, by sex where the owner split them, the first allowed taken.</summary>
public class BallsTests
{
    [Theory]
    [InlineData(37, 1, 0, Ball.Premier)]   // Alolan Vulpix, male
    [InlineData(37, 1, 1, Ball.Love)]      // Alolan Vulpix, female
    [InlineData(280, 0, 0, Ball.Moon)]     // Ralts, male
    [InlineData(778, 0, 1, Ball.Love)]     // Mimikyu, female
    public void The_ball_of_the_sex_comes_first_and_is_legal_on_the_templates_Pokemon(int species, int form, int gender, Ball ball)
    {
        Assert.Equal(ball, Balls.Shared.Prefer((ushort)species, (byte)form, (byte)gender, shiny: true)[0]);
        Assert.True(Balls.Shared.HangsOnSex((ushort)species, (byte)form));
        var sav = Template.Read(Template.Bytes);
        var pk = (PK7)Template.Pokemon(sav, party: false).First(p => p.Species == species && p.Form == form).Clone();
        pk.Gender = (byte)gender; pk.Ball = (byte)ball; pk.RefreshChecksum();
        Assert.True(new LegalityAnalysis(pk).Valid, string.Join(" | ", new LegalityAnalysis(pk).Report().Split('\n').Where(l => l.Contains("Invalid"))));
    }

    [Fact]
    public void Lists_end_in_a_Poke_Ball_and_fall_through_in_order()
    {
        Assert.Equal([Ball.Moon, Ball.Poke], Balls.Shared.Prefer(37, 0, 0, true));   // the Kantonian Vulpix: one pick
        var caterpie = Balls.Shared.Prefer(10, 0, null, true);                          // the Butterfree line: a Dream Ball, else the Love Ball the national dex had
        Assert.Equal([Ball.Dream, Ball.Love, Ball.Poke], caterpie);
        Assert.Equal([Ball.Poke], Balls.Shared.Prefer(9999, 0, null, true));          // nothing picked: a Poké Ball
        Assert.Equal([Ball.Dream, Ball.Love, Ball.Heal, Ball.Poke], Balls.Shared.Prefer(669, 0, null, true));   // Flabébé: three ranks at most, then a Poké Ball
        Assert.All(Enumerable.Range(1, 1025).SelectMany(sp => new[] { true, false }.Select(sh => Balls.Shared.Prefer((ushort)sp, 0, null, sh))), l => Assert.InRange(l.Count, 1, Balls.Ranks + 1));
        Assert.Equal([Ball.Master, Ball.Poke], Balls.Shared.Wanted((int)Ball.Master, 10, 0, null, true));   // one ball asked for
        Assert.Equal([Ball.Poke], Balls.Shared.Wanted((int)Ball.Poke, 10, 0, null, true));
        // a Dream Ball is not to be had on a Caterpie of Ultra Sun, so the template's falls to the Love Ball; the Z-A pick stays for the Z-A dex
        var sav = Template.Read(Template.Bytes);
        var pk = (PK7)Template.Pokemon(sav, party: false).First(p => p.Species == 10).Clone();
        var first = Balls.Choose(pk, caterpie);
        Assert.Equal(Ball.Dream, first);
        Assert.True(new LegalityAnalysis(pk).Valid);
        Assert.Contains((Ball)pk.Ball, caterpie);
    }

    [Fact]
    public void The_table_covers_every_dex()
    {
        Assert.True(Balls.Shared.Count > 1300);
        // a pick for every entry of every dex (a male's where the entry does not say), shiny or plain, with a Poké Ball only where that was the pick
        // nothing was ever picked for the Hisuian Growlithe line and the Fancy Vivillon line: a Poké Ball, as before
        var unpicked = Scarlet.Plan9.All.Where(e => !Balls.Shared.Has(e.Species, e.Form)).Select(e => (e.Species, e.Form)).Distinct().ToList();
        // and nothing yet for the Hisuian lines and Phione that hatch from parents brought through HOME (new on 2026-10-05, to be picked on the page)
        Assert.Equal([(58, 1), (59, 1), (100, 1), (101, 1), (215, 1), (570, 1), (571, 1), (664, 18), (665, 18), (666, 18), (903, 0)], unpicked.Select(x => ((int)x.Species, (int)x.Form)).ToArray());
        Assert.All(ZA.Plan9a.All.Where(e => e.Template.FixedBall == Ball.None), e => Assert.True(Balls.Shared.Has(e.Species, e.Form), ZA.Plan9a.Label(e.Species, e.Form)));   // Magearna is a gift in its own ball
        Assert.All(Sword.Plan8.All().Where(e => e.Source != Sword.Source.None && e.Template?.FixedBall is null or Ball.None), e => Assert.True(Balls.Shared.Has(e.Species, e.Form), $"{e.Species}-{e.Form}"));
    }

    [Fact]
    public void A_pick_made_where_no_shiny_was_to_be_had_is_a_pick_for_a_plain_one()
    {
        // Koraidon: picked in Scarlet, where it cannot shine — the plain row; a shiny one (none can be made) falls back to it
        Assert.True(Balls.Shared.PickedFor(1007, 0, null, shiny: false));
        Assert.False(Balls.Shared.PickedFor(1007, 0, null, shiny: true));
        Assert.Equal(Balls.Shared.Prefer(1007, 0, null, false), Balls.Shared.Prefer(1007, 0, null, true));
        // Heatran: Sword's Dynamax Adventure can shine (the row for either colour), Z-A's static cannot (the plain row)
        Assert.True(Balls.Shared.PickedFor(485, 0, null, false));
        Assert.Equal(Ball.Heavy, Balls.Shared.Prefer(485, 0, null, true)[0]);
    }
}

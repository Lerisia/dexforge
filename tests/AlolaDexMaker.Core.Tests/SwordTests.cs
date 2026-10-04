using AlolaDexMaker.Sword;
using PKHeX.Core;
using Xunit;

namespace AlolaDexMaker.Tests;

/// <summary>The Sword dex: the egg RNG as the write-up describes it, the plan, a few made and checked, and a whole save.</summary>
public class SwordTests
{
    [Fact]
    public void Xoroshiro_masks_to_the_next_power_of_two()
    {
        Assert.Equal(0ul, Xoroshiro.NextPowerTwoMask(1));
        Assert.Equal(1ul, Xoroshiro.NextPowerTwoMask(2));
        Assert.Equal(31ul, Xoroshiro.NextPowerTwoMask(25));
        Assert.Equal(255ul, Xoroshiro.NextPowerTwoMask(252));
        Assert.Equal(0xFFFFFFFFul, Xoroshiro.NextPowerTwoMask(0xFFFFFFFF));
        var rng = new Xoroshiro(1);
        for (int i = 0; i < 1000; i++) Assert.InRange(rng.Next(25), 0ul, 24ul);
    }

    [Fact]
    public void Egg_parents_are_ordered_as_the_game_does()
    {
        var male = new Parent(25, 0, 0, Nature.Hardy, new int[6], Ball.Poke, 0, 8);
        var female = new Parent(25, 1, 0, Nature.Hardy, new int[6], Ball.Poke, 0, 8);
        var ditto = new Parent(132, 2, 0, Nature.Hardy, new int[6], Ball.Poke, 0, 1);
        Assert.Equal((male, female), Egg8.Order(female, male));
        Assert.Equal((ditto, female), Egg8.Order(female, ditto));
        Assert.Equal((male, ditto), Egg8.Order(ditto, male));
    }

    [Fact]
    public void Egg_takes_the_ball_and_ability_from_the_species_parent_and_inherits_five_with_a_destiny_knot()
    {
        var parent = new Parent(25, 1, 1, Nature.Adamant, [1, 2, 3, 4, 5, 6], Ball.Heavy, 0, 8);
        var ditto = new Parent(132, 2, 0, Nature.Hardy, [31, 31, 31, 31, 31, 31], Ball.Poke, 280, 1);
        int fromParents = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var h = Egg8.Generate(seed, parent, ditto, 25, 0, 127, 1234, false);
            Assert.Equal(Ball.Heavy, h.Ball);
            Assert.InRange(h.AbilityIndex, 0, 1);
            Assert.True(h.Ivs.Count(v => v == 31 || v is >= 1 and <= 6) >= 5, "five inherited");
            fromParents++;
        }
        Assert.Equal(200, fromParents);
    }

    [Fact]
    public void Plan_covers_the_three_dexes_with_eggs_first()
    {
        var all = Plan8.All();
        Assert.True(all.Count >= 750, all.Count.ToString());
        Assert.True(all.Count(e => e.Source == Source.Egg) >= 600);
        Assert.Equal(2, all.Count(e => e.Source == Source.None)); // Diancie, ordinary Magearna
        var garchomp = all.Single(e => e.Species == 445);
        Assert.Equal(Source.Egg, garchomp.Source); Assert.Equal(443, garchomp.FromSpecies);
        Assert.Equal(4, all.Count(e => e.Source == Source.Fossil));
        Assert.Equal(10, all.Count(e => e.Species == 869)); // nine creams and the shiny one
    }

    [Theory]
    [InlineData(25, 0)]    // Pikachu: hatched
    [InlineData(445, 0)]   // Garchomp: hatched as Gible, evolved
    [InlineData(880, 0)]   // Dracozolt: fossil
    [InlineData(888, 0)]   // Zacian: static, not shiny
    [InlineData(150, 0)]   // Mewtwo: Dynamax Adventure
    [InlineData(865, 0)]   // Sirfetch'd: hatched as Galarian Farfetch'd, evolved
    public void A_few_made_pass_the_check(int species, int form)
    {
        var e = Plan8.All().First(x => x.Species == species && x.Form == form);
        var random = new Random(1);
        var tr = new SimpleTrainerInfo(GameVersion.SW) { OT = "우리", Gender = 1, Language = 8, ID32 = 123456789 };
        var friend = new SimpleTrainerInfo(GameVersion.SW) { OT = "새아", Gender = 1, Language = 8, ID32 = 987654321 };
        var m = new Maker8(tr, friend, new Balls8(Plan8.Ko), random, 2021, true, null).Make(e);
        Assert.True(m.Legal, m.Report);
        Assert.Equal(species, m.Pk.Species);
        Assert.Equal(2021, m.Pk.MetDate!.Value.Year);
    }

    [Fact]
    public void A_whole_sword_save_is_made_and_passes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sword-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var made = Making8.Run(new Options8("우리", null, null, 2021, 20210101), dir, dir);
            Assert.Equal(0, made.Code);
            Assert.Contains(made.Lines, l => l.StartsWith("합법") && l.Contains("760 / 760"));
            var sav = new SAV8SWSH(File.ReadAllBytes(Path.Combine(dir, "main")));
            Assert.Equal("우리", sav.OT);
            Assert.Equal(760, Enumerable.Range(0, sav.SlotCount).Count(i => sav.GetBoxSlotAtIndex(i).Species != 0));
            Assert.True(File.Exists(Path.Combine(dir, "backup")) && File.Exists(Path.Combine(dir, ".nx_save_meta.bin")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

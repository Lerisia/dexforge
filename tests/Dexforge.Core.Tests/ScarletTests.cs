using Dexforge.Scarlet;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>Scarlet: a wild catch drawn from a seed the way the game draws one.</summary>
public class ScarletTests
{
    private static PK9 Blank(ushort species, byte form) => new() { Species = species, Form = form, ID32 = 123456789, OriginalTrainerName = "재연", Language = (int)LanguageID.Korean };

    [Fact]
    public void A_seed_gives_what_PKHeX_says_it_gives()
    {
        var rnd = new Random(1);
        for (int i = 0; i < 50; i++)
        {
            var pk = Blank(25, 0);
            ulong seed = (ulong)rnd.NextInt64();
            Assert.True(Spawn9.Apply(pk, 25, 0, seed));
            Assert.True(Spawn9.Matches(pk, 25, 0, seed));
            Assert.Contains((int)pk.TeraTypeOriginal, new[] { (int)PersonalTable.SV[25].Type1, (int)PersonalTable.SV[25].Type2 });
        }
    }

    [Theory]
    [InlineData(25, 0, true, Scale9.Smallest)]    // Pikachu: rolls a gender
    [InlineData(25, 0, false, Scale9.Largest)]
    [InlineData(81, 0, true, Scale9.Largest)]     // Magnemite: genderless
    [InlineData(1006, 0, true, Scale9.Smallest)]  // Iron Valiant
    public void The_seed_found_shines_and_sizes_as_asked(int species, int form, bool shiny, Scale9 scale)
    {
        var rnd = new Random(7);
        var pk = Blank((ushort)species, (byte)form);
        var seed = Spawn9.Find(pk, (ushort)species, (byte)form, shiny, scale, rnd);
        Assert.NotNull(seed);
        Assert.Equal(shiny, pk.IsShiny);
        Assert.Equal(Spawn9.ScaleOf(scale), pk.Scale);
        Assert.True(Spawn9.Matches(pk, (ushort)species, (byte)form, seed.Value));
    }

    [Fact]
    public void A_shiny_of_whatever_size_comes_in_a_few_hundred_seeds()
    {
        var rnd = new Random(3);
        var pk = Blank(133, 0);
        var seed = Spawn9.Find(pk, 133, 0, true, Scale9.Random, rnd, tries: 20_000);
        Assert.NotNull(seed);
        Assert.True(pk.IsShiny);
    }

    [Fact]
    public void A_slot_catch_drawn_from_a_seed_is_legal_with_its_mark()
    {
        var slots = (EncounterArea9[])typeof(PK9).Assembly.GetType("PKHeX.Core.Encounters9")!.GetField("Slots", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(null)!;
        var slot = slots.SelectMany(a => a.Slots).First(x => x.Species == 25 && x.Form == 0);
        var tr = new SimpleTrainerInfo(GameVersion.SL) { OT = "재연", ID32 = 123456789, Gender = 1, Language = (int)LanguageID.Korean };
        var pk = slot.ConvertToPKM(tr);
        var seed = Spawn9.Find(pk, 25, 0, true, Scale9.Smallest, new Random(11));
        Assert.NotNull(seed);
        pk.RibbonMarkMini = true;
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        Assert.True(la.Valid, la.Report());
        Assert.True(pk.IsShiny && pk.Scale == 0);
        Assert.Equal(Spawn9.Rolls, 8);
    }

    [Fact]
    public void The_plan_resolves_every_row_the_owner_kept()
    {
        var all = Plan9.All;
        var kept = Plan9.Rows.Count(r => r.Source != "없음");
        Assert.Equal(kept + 3, all.Count);   // Hippopotas, Hippowdon and Pyroar twice, a sex each, as the national dex keeps them
        Assert.Equal(2, all.Count(e => e.Species == 668));
        Assert.Equal([0, 1], all.Where(e => e.Species == 449).Select(e => e.Gender!.Value).Order().ToArray());
        Assert.All(all, e => Assert.NotNull(e.Template));
        var by = all.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(by[Source9.Wild] > 700, by[Source9.Wild].ToString());
        Assert.Equal(9 + 1 + 7, by[Source9.Egg]);      // the three starter lines, the Alolan Persian, and what only an egg of a parent brought through HOME gives (the Hisuian Voltorb, Sneasel and Zorua lines, Phione)
        Assert.Equal(1, by[Source9.Trade]);
        Assert.Contains(all, e => e.Species == 1008 && e.Violet && e.Source == Source9.Static);   // Miraidon from Violet
        Assert.Contains(all, e => e.Species == 1006 && e.Violet && e.Source == Source9.Wild);     // Iron Valiant from Violet
        Assert.Contains(all, e => e.Species == 3 && e.FromSpecies == 1 && e.Source == Source9.Wild); // Venusaur from a wild Bulbasaur
        Assert.Contains(all, e => e.Species == 1000 && e.FromSpecies == 999 && e.Source == Source9.Static);   // Gholdengo from the chest
        Assert.Equal(35, all.Count(e => e.Violet));   // Serebii's exclusives and Palkia, whose raid is Violet's
        Assert.All(all.Where(e => e.Source == Source9.Raid), e => Assert.NotEmpty(e.RaidWindows));
        Assert.Contains(all, e => e.Species == 150 && e.RaidWindows.Single() == (new DateOnly(2023, 9, 1), new DateOnly(2023, 9, 18)));
    }

    [Fact]
    public void A_whole_save_is_legal_and_reads_back()
    {
        var into = Path.Combine(Path.GetTempPath(), "dexforge-scarlet-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = Making9.Run(new Options9("재연", 123456, 1234, new DateOnly(2023, 1, 1), new DateOnly(2024, 12, 31), 20261005), into, into);
            Assert.True(made.Code == 0, string.Join("\n", made.Refused.Take(40)));
            var sav = new SAV9SV(File.ReadAllBytes(Path.Combine(into, "main")));
            Assert.True(sav.ChecksumsValid);
            Assert.Equal("재연", sav.OT);
            var boxed = sav.BoxData.Where(p => p.Species != 0).ToList();
            var analyses = boxed.ToDictionary(p => p, p => new LegalityAnalysis(p, sav.Personal));
            Assert.All(boxed, p => Assert.True(analyses[p].Valid, Plan9.Label(p.Species, p.Form)));
            // the dex first, then every distribution and its final evolutions
            var dex = boxed.Take(Plan9.All.Count).ToList();
            var gifts = boxed.Skip(Plan9.All.Count).ToList();
            Assert.Equal(Plan9.All.Count, dex.Count);
            Assert.All(dex, p => Assert.False(analyses[p].EncounterMatch is WC9));
            Assert.All(gifts, p => Assert.True(analyses[p].EncounterMatch is WC9));
            Assert.Equal(Events9.All.Count, gifts.Count(p => p.Species == ((WC9)analyses[p].EncounterMatch).Species));   // one as received per card
            Assert.Equal(100, gifts.Count);   // 82 cards and 18 final evolutions (Oinkologne's other sex and Maushold's other form cannot be reached)
            Assert.True(dex.Count(p => p.IsShiny) > 700);
            Assert.All(dex, p => Assert.InRange(p.MetDate!.Value, new DateOnly(2023, 1, 1), new DateOnly(2024, 12, 31)));
            Assert.All(dex.Where(p => p.Species == 150), p => Assert.InRange(p.MetDate!.Value, new DateOnly(2023, 9, 1), new DateOnly(2023, 9, 18)));   // the Mewtwo raid's window
            Assert.Contains(dex, p => p.Species == 484 && p.Version == GameVersion.VL);   // Palkia from Violet's raid
            // every gift on a day its card was really handed out, by PKHeX's own windows
            Assert.All(gifts, p => Assert.True(((WC9)analyses[p].EncounterMatch).IsWithinDistributionWindow(p.MetDate!.Value), Plan9.Label(p.Species, p.Form) + " " + p.MetDate));
            // what one code handed over together was received on one day: CoroCoro's Iron Valiant and Roaring Moon
            var corocoro = gifts.Where(p => ((WC9)analyses[p].EncounterMatch).CardID == 36).Select(p => p.MetDate).Distinct().ToList();
            Assert.Single(corocoro);
            // a card only Violet could receive came from the trainer's own Violet: Koraidon's shiny
            Assert.Contains(gifts, p => p.Species == 1007 && p.IsShiny && p.Version == GameVersion.VL && p.CurrentHandler == 1);
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }

    [Fact]
    public void A_hatchling_takes_the_size_asked_without_a_mark()
    {
        var trainer = new SimpleTrainerInfo(GameVersion.SL) { OT = "재연", Gender = 1, Language = (int)LanguageID.Korean, ID32 = 123456789 };
        var egg = Plan9.All.First(e => e.Source == Source9.Egg);
        foreach (var size in new[] { SizeChoice.Smallest, SizeChoice.Largest })
        {
            var made = new Maker9(trainer, new Random(2), new Options9("재연", 123456, 1234, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 2, Size: size)).Make(egg);
            Assert.True(made.Legal, made.Report);
            Assert.Equal(size == SizeChoice.Smallest ? 0 : 255, made.Pk.Scale);
            Assert.Equal(made.Pk.Scale, made.Pk.HeightScalar);
            Assert.False(made.Pk.RibbonMarkMini || made.Pk.RibbonMarkJumbo);
        }
    }

    [Fact]
    public void Every_distribution_scarlet_received_is_listed()
    {
        var all = Events9.All;
        Assert.Equal(82, all.Count);
        Assert.All(all, e => Assert.True(e.End is null || e.Start <= e.End, e.Title));
        // only Violet's cards: Talonflame, Gyarados and the shiny Koraidon
        Assert.Equal([49, 52, 1540], all.Where(e => e.Violet).Select(e => e.Card.CardID).Order().ToArray());
        // what one code hands over together shares a group (CoroCoro's pairs, the shiny Koraidon and Miraidon, and the sets given out over one window by one trainer: the birthday Flabébé colours, the mythical trios); a card alone is its own
        Assert.Equal(9, all.GroupBy(e => e.Group).Count(g => g.Count() > 1));
        Assert.Equal(all.Count - 14, all.GroupBy(e => e.Group).Count());
        Assert.Single(all.Where(e => e.Card.CardID is 1011 or 1012 or 1013).Select(e => e.Group).Distinct());
        // a plain gift in a Poké Ball is not evolved (the anime's three starters); one in another ball is, unless it is Pikachu
        Assert.All(all.Where(e => e.Card.Species is 25 or 906 or 909 or 912), e => Assert.Empty(e.Evolutions));
        Assert.Contains(all, e => e.Card.Species == 915 && e.Evolutions.Select(x => x.Species).Distinct().Single() == 916);   // Lechonk (Cherish Ball) → Oinkologne, both sexes' forms
        Assert.Contains(all, e => e.Card.Species == 172 && e.Card.IsShiny && e.Evolutions.Single() == (26, 0));   // the shiny Pichu goes up to Raichu; only Pikachu itself stays
        Assert.Equal(18 + 2, all.Sum(e => e.Evolutions.Count));
        // the Mew gift rolled its Tera type (and the move that goes with it) at receipt: PKHeX holds one card per type
        Assert.Equal(18, all.Single(e => e.Card.Species == 151).Variants.Count);
        Assert.Equal(18, all.Single(e => e.Card.Species == 151).Variants.Select(c => c.TeraType).Distinct().Count());
    }
}

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
        Assert.Equal(kept, all.Count);
        Assert.All(all, e => Assert.NotNull(e.Template));
        var by = all.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(by[Source9.Wild] > 700, by[Source9.Wild].ToString());
        Assert.Equal(9 + 1, by[Source9.Egg]);          // the three starter lines and the Alolan Persian
        Assert.Equal(1, by[Source9.Trade]);
        Assert.Contains(all, e => e.Species == 1008 && e.Violet && e.Source == Source9.Static);   // Miraidon from Violet
        Assert.Contains(all, e => e.Species == 1006 && e.Violet && e.Source == Source9.Wild);     // Iron Valiant from Violet
        Assert.Contains(all, e => e.Species == 3 && e.FromSpecies == 1 && e.Source == Source9.Wild); // Venusaur from a wild Bulbasaur
        Assert.Contains(all, e => e.Species == 1000 && e.FromSpecies == 999 && e.Source == Source9.Static);   // Gholdengo from the chest
        Assert.Equal(34, all.Count(e => e.Violet));
    }

    [Fact]
    public void A_whole_save_is_legal_and_reads_back()
    {
        var into = Path.Combine(Path.GetTempPath(), "dexforge-scarlet-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = Making9.Run(new Options9("재연", 123456, 1234, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 20261005), into, into);
            Assert.True(made.Code == 0, string.Join("\n", made.Refused.Take(40)));
            var sav = new SAV9SV(File.ReadAllBytes(Path.Combine(into, "main")));
            Assert.True(sav.ChecksumsValid);
            Assert.Equal("재연", sav.OT);
            var boxed = sav.BoxData.Where(p => p.Species != 0).ToList();
            Assert.Equal(Plan9.All.Count, boxed.Count);
            Assert.All(boxed, p => Assert.True(new LegalityAnalysis(p, sav.Personal).Valid, Plan9.Label(p.Species, p.Form)));
            Assert.True(boxed.Count(p => p.IsShiny) > 700);
            Assert.All(boxed, p => Assert.InRange(p.MetDate!.Value, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }
}

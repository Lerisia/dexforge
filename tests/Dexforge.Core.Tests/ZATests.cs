using Dexforge.ZA;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>Legends: Z-A: a hyperspace catch drawn from a seed the way the game draws one, and the dex built from it.</summary>
public class ZATests
{
    private static readonly SimpleTrainerInfo Me = new(GameVersion.ZA) { OT = "재연", ID32 = 123456789, Gender = 1, Language = (int)LanguageID.Korean };

    [Fact]
    public void A_seed_gives_what_PKHeX_says_it_gives()
    {
        var slot = Plan9a.Slot(25, 0, alpha: false, null)!;
        var param = Spawn9a.Param(slot);
        Assert.Equal(1, param.RollCount);
        var rnd = new Random(1);
        for (int i = 0; i < 50; i++)
        {
            var pk = slot.ConvertToPKM(Me);
            ulong seed = (ulong)rnd.NextInt64();
            Assert.True(Spawn9a.Apply(pk, param, EncounterCriteria.Unrestricted, seed));
            Assert.True(Spawn9a.Matches(pk, param, seed));
            Assert.True(LumioseRNG.Verify(pk, param, seed));
        }
    }

    [Theory]
    [InlineData(25, 0, true, Scale9a.Smallest)]    // Pikachu: rolls a gender
    [InlineData(25, 0, false, Scale9a.Largest)]
    [InlineData(707, 0, true, Scale9a.Largest)]    // Klefki: genderless
    [InlineData(678, 1, true, Scale9a.Smallest)]   // Meowstic ♀: the slot fixes the sex, no gender draw
    public void The_seed_found_shines_and_sizes_as_asked(int species, int form, bool shiny, Scale9a scale)
    {
        var rnd = new Random(7);
        var slot = Plan9a.Slot((ushort)species, (byte)form, alpha: false, null)!;
        var criteria = Spawn9a.Criteria(slot, shiny, null, rnd);
        var pk = slot.ConvertToPKM(Me, criteria);
        var seed = Spawn9a.Find(pk, slot, criteria, scale, rnd);
        Assert.NotNull(seed);
        Assert.Equal(shiny, pk.IsShiny);
        Assert.Equal(scale == Scale9a.Smallest ? 0 : 255, pk.Scale);
        Assert.True(Spawn9a.Matches(pk, Spawn9a.Param(slot), seed.Value));
        pk.RefreshChecksum();
        Assert.True(new LegalityAnalysis(pk).Valid);
    }

    [Fact]
    public void An_alpha_has_its_three_perfect_IVs_where_the_draw_put_them_and_is_255()
    {
        var rnd = new Random(5);
        var slot = Plan9a.Slot(25, 0, alpha: true, null)!;
        var criteria = Spawn9a.Criteria(slot, true, null, rnd);
        var pk = slot.ConvertToPKM(Me, criteria);
        var seed = Spawn9a.Find(pk, slot, criteria, Scale9a.Random, rnd);
        Assert.NotNull(seed);
        Assert.True(pk.IsAlpha);
        Assert.True(pk.IsShiny);
        Assert.Equal(255, pk.Scale);
        Assert.Equal(3, new[] { pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE }.Count(v => v == 31));
        Assert.True(Spawn9a.Matches(pk, Spawn9a.Param(slot), seed.Value));
        // the same seed and criteria give the same alpha again: nothing of it hangs on PKHeX's own random
        var again = slot.ConvertToPKM(Me, criteria);
        Assert.True(Spawn9a.Apply(again, Spawn9a.Param(slot), criteria, seed.Value));
        Assert.Equal(pk.IV32, again.IV32);
        Assert.Equal(pk.PID, again.PID);
        pk.RefreshChecksum();
        Assert.True(new LegalityAnalysis(pk).Valid);
    }

    [Fact]
    public void The_plan_covers_the_Lumiose_dex()
    {
        var all = Plan9a.All;
        Assert.Equal(436, all.Count);   // 433 species and forms, Hippopotas, Hippowdon and Pyroar twice
        Assert.Equal(364, all.Select(e => e.Species).Distinct().Count());
        Assert.DoesNotContain(all, e => FormInfo.IsBattleOnlyForm(e.Species, e.Form, 9));
        Assert.Equal(392, all.Count(e => e.Source == Source9a.Hyperspace));
        Assert.Contains(all, e => e.Species == 350 && e.FromSpecies == 349 && e.Source == Source9a.Hyperspace);   // Milotic from a hyperspace Feebas
        Assert.Contains(all, e => e.Species == 711 && e.Form == 1 && e.FromSpecies == 710 && e.Source == Source9a.Wild);   // a small Gourgeist from the wild Pumpkaboo
        Assert.Contains(all, e => e.Species == 666 && e.Form == 8 && e.FromSpecies == 665 && e.Source == Source9a.Gift);   // the marine Vivillon from the gift Spewpa
        Assert.Equal(9, all.Count(e => e.Species == 676 && e.ChangesForm));   // Furfrou's trims
        Assert.Contains(all, e => e.Species == 720 && e.Form == 1 && e.ChangesForm);   // Hoopa unbound
        Assert.Contains(all, e => e.Species == 718 && e.Form == 3 && e.FromForm == 2);   // Zygarde 50% from the 10% caught
        Assert.Contains(all, e => e.Species == 150 && e.Source == Source9a.Static && e.ShinyLocked);
        Assert.Contains(all, e => e.Species == 380 && e.Source == Source9a.Static && !e.ShinyLocked);   // Latias in hyperspace can shine
        // left out: the Vivillon patterns Lumiose never shows, and what no form change reaches
        Assert.Equal(57, Plan9a.LeftOut.Count);
        Assert.Equal(52, Plan9a.LeftOut.Count(x => x.Species is 664 or 665 or 666));   // 18 Scatterbug, 17 Spewpa (the gift is the marine one), 17 Vivillon
        Assert.Contains(Plan9a.LeftOut, x => x.Species == 718 && x.Form == 0);
    }

    [Fact]
    public void A_whole_save_is_legal_and_reads_back()
    {
        var into = Path.Combine(Path.GetTempPath(), "dexforge-za-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = Making9a.Run(new Options9a("재연", 123456, 1234, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 20261005, Size: SizeChoice.Alpha), into, into);
            Assert.True(made.Code == 0, string.Join("\n", made.Refused.Take(40)));
            var sav = new SAV9ZA(File.ReadAllBytes(Path.Combine(into, "main")));
            Assert.True(sav.ChecksumsValid);
            Assert.Equal("재연", sav.OT);
            var boxed = sav.BoxData.Where(p => p.Species != 0).Cast<PA9>().ToList();
            Assert.Equal(Plan9a.All.Count, boxed.Count);
            Assert.All(boxed, p => Assert.True(new LegalityAnalysis(p, sav.Personal).Valid, Plan9a.Label(p.Species, p.Form)));
            Assert.True(boxed.Count(p => p.IsShiny) > 390);
            Assert.Equal(392, boxed.Count(p => p.IsAlpha));
            Assert.All(boxed.Where(p => p.IsAlpha), p => Assert.Equal(255, p.Scale));
            Assert.All(boxed, p => Assert.InRange(p.MetDate!.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            for (ushort s = 1; s <= sav.MaxSpeciesID; s++) if (Plan9a.All.Any(e => e.Species == s)) Assert.True(sav.GetCaught(s), s.ToString());
            Assert.Contains(boxed, p => p.Species == 350 && p.CurrentHandler == 0 && p.HandlingTrainerName == "재연" && p.HandlingTrainerGender == p.OriginalTrainerGender);   // Milotic: traded to the other Z-A and back
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }
}

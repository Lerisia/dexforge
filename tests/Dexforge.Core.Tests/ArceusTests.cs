using Dexforge.Arceus;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>Legends: Arceus: the seed arithmetic, the plan, and a whole save.</summary>
public class ArceusTests
{
    [Fact]
    public void A_generator_seed_comes_back_from_its_fixed_seed()
    {
        var rnd = new Random(1);
        for (int i = 0; i < 4; i++)
        {
            ulong g = (ulong)rnd.NextInt64() ^ ((ulong)rnd.NextInt64() << 11);
            var (_, f) = Spawn8a.FromGenerator(g);
            Assert.Contains(g, Generator8a.Of(f));
        }
    }

    [Fact]
    public void The_fixed_seed_draw_is_PKHeXs_own()
    {
        // PKHeX's catch, then our draw from the seed PKHeX finds for it: every field the same
        var sav = new SAV8LA(Embedded.Bytes("arceus.main"));
        var slot = Plan8a.SlotsOf(25, 0, false).First(s => s.Type == SlotType8a.Standard);
        for (int i = 0; i < 20; i++)
        {
            var pk = slot.ConvertToPKM(sav);
            Assert.Equal(SeedCorrelationResult.Success, slot.TryGetSeed(pk, out var seed));
            var d = Spawn8a.FromFixed(seed, new SpawnParams(sav.GetShinyRolls(25), slot.FlawlessIVCount, 127, false), sav.ID32);
            Assert.Equal(pk.EncryptionConstant, d.Ec);
            Assert.Equal(pk.PID, d.Pid);
            Assert.Equal([pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE], d.Ivs);   // the draw's order, not PKHeX's stat order
            Assert.Equal((int)pk.Nature, d.Nature);
            Assert.Equal(pk.Gender, d.Gender);
            Assert.Equal(pk.HeightScalar, d.Height);
            Assert.Equal(pk.WeightScalar, d.Weight);
        }
    }

    [Fact]
    public void The_smallest_seeds_come_from_the_equations()
    {
        var rnd = new Random(7);
        var sampler = new SizeSeeds8a(SizeSeeds8a.Position(1, rollsGender: true), SizeSeeds8a.Smallest);
        Assert.Equal(60, sampler.Rank);
        int tiny = 0, n = 2000;
        for (int i = 0; i < n; i++)
        {
            if (sampler.Sample(rnd) is not { } s) continue;
            var d = Spawn8a.FromFixed(s, new SpawnParams(1, 0, 127, false));
            if (d.Height == 0 && d.Weight == 0) tiny++;
        }
        Assert.InRange(tiny, n * 65 / 100, n);   // about three in four; the rest had a nature or gender drawn again
    }

    [Fact]
    public void The_plan_covers_the_Hisui_dex()
    {
        var all = Plan8a.All;
        Assert.Equal(313, all.Count);
        Assert.Equal(242, all.Select(e => e.Species).Distinct().Count());
        Assert.Equal(311, all.Select(e => (e.Species, e.Form)).Distinct().Count());
        Assert.Equal(2, all.Count(e => e.Species == 449));   // Hippopotas in both sexes
        Assert.DoesNotContain(all, e => e.Species == 900 && e.Form == 1);   // the lord Kleavor
        Assert.Contains(all, e => e.Species == 26 && e.Source == Source8a.Field && e.FromSpecies == 25);   // Raichu from a wild Pikachu
        Assert.Contains(all, e => e.Species == 479 && e.Form == 1 && e.ChangesForm);   // Heat Rotom from a caught Rotom
        Assert.Contains(all, e => e.Species == 492 && e.Source == Source8a.Static && !e.Shiny);   // Shaymin, locked
        Assert.All(all.Where(e => e.Source == Source8a.Field), e => Assert.NotNull(e.Spawner));
        Assert.Equal(263, all.Count(e => e.CanBeAlpha));
    }

    [Fact]
    public void A_field_catch_lands_on_its_slot()
    {
        var sp = Spawners8a.For(25, 0, true)!;
        var rnd = new Random(3);
        for (int i = 0; i < 50; i++) Assert.True(sp.Lands(sp.DrawGenerator(rnd)));
        Assert.InRange(sp.Share, 0.001, 0.05);
    }

    [Fact]
    public void A_whole_save_of_alphas_is_legal_and_reads_back()
    {
        var into = Path.Combine(Path.GetTempPath(), "dexforge-arceus-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = Making8a.Run(new Options8a("재연", 123456, 1234, Making8a.Released, new DateOnly(2022, 12, 31), 20261004, Size: SizeChoice.Alpha, Sex: SexChoice.Female), into, into);
            Assert.Equal(0, made.Code);
            Assert.True(File.Exists(Path.Combine(into, "main")) && File.Exists(Path.Combine(into, "backup")) && File.Exists(Path.Combine(into, "main2")) && File.Exists(Path.Combine(into, ".nx_save_meta.bin")));
            var sav = new SAV8LA(File.ReadAllBytes(Path.Combine(into, "main")));
            Assert.True(sav.ChecksumsValid);
            Assert.Equal("재연", sav.OT);
            Assert.Equal(123456u, sav.DisplayTID);
            var boxed = sav.BoxData.Where(p => p.Species != 0).ToList();
            Assert.Equal(313, boxed.Count);
            Assert.All(boxed, p => Assert.True(new LegalityAnalysis(p, sav.Personal).Valid, Plan8a.Label(p.Species, p.Form)));
            Assert.Equal(263, boxed.Count(p => ((PA8)p).IsAlpha));
            Assert.Equal(268, boxed.Count(p => p.IsShiny));
            Assert.All(boxed, p => Assert.InRange(p.MetDate!.Value, Making8a.Released, new DateOnly(2022, 12, 31)));
            Assert.All(boxed.Where(p => p.Species == 25), p => Assert.Equal(1, p.Gender));
            Assert.Contains(boxed, p => p.Species == 449 && p.Gender == 0);   // the species boxed in both sexes keeps its male
            int complete = 0; for (ushort s = 1; s <= sav.MaxSpeciesID; s++) if (Plan8a.Table.IsSpeciesInGame(s) && sav.PokedexSave.IsComplete(s)) complete++;
            Assert.InRange(complete, 240, 242);
            Assert.True(sav.AdventureStart.Timestamp >= Making8a.Released.ToDateTime(TimeOnly.MinValue));
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }

    [SlowFact]
    public void A_whole_save_of_the_smallest_is_legal()
    {
        var into = Path.Combine(Path.GetTempPath(), "dexforge-arceus-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = Making8a.Run(new Options8a("재연", null, null, Making8a.Released, new DateOnly(2022, 12, 31), 5, Size: SizeChoice.Smallest), into, into);
            Assert.Equal(0, made.Code);
            var sav = new SAV8LA(File.ReadAllBytes(Path.Combine(into, "main")));
            var boxed = sav.BoxData.Where(p => p.Species != 0).ToList();
            Assert.Equal(313, boxed.Count);
            Assert.All(boxed, p => Assert.True(new LegalityAnalysis(p, sav.Personal).Valid));
            Assert.InRange(boxed.Count(p => ((PA8)p).HeightScalar == 0 && ((PA8)p).WeightScalar == 0), 260, 275);
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }
}

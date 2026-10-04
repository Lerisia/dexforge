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
}

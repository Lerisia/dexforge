using PKHeX.Core;
using Xunit;

namespace AlolaDexMaker.Tests;

/// <summary>The save the generator carries inside it, and the mending of it.</summary>
public class TemplateTests
{
    private static readonly SAV7 Sav = Template.Read(Template.Bytes);
    private static readonly List<PK7> All = Template.Pokemon(Sav);

    [Fact]
    public void HoldsEverySpeciesOnceOrMore()
    {
        var held = Template.Pokemon(Sav, party: false).Select(p => (int)p.Species).ToHashSet();
        Assert.DoesNotContain(Enumerable.Range(1, 807), s => !held.Contains(s));
        for (ushort s = 1; s <= 807; s++) Assert.True(Sav.Zukan.GetSeen(s) && Sav.Zukan.GetCaught(s));
    }

    [Fact]
    public void IsLegalThroughout()
    {
        Assert.True(Sav.ChecksumsValid);
        Assert.All(All, p => Assert.True(new LegalityAnalysis(p).Valid, GameInfo.GetStrings("ko").Species[p.Species]));
        Assert.DoesNotContain(All, p => p.IsEgg);
    }

    [Fact]
    public void KeepsNoNameButItsTrainers()
    {
        Assert.Equal(Sav.OT, Sav.FieldMenu.RotomOT);
        Assert.False(Template.Holds(Template.Bytes, "엘리스"));
        Assert.DoesNotContain(All, p => p.OriginalTrainerName.Contains("엘리스") || p.HandlingTrainerName.Contains("엘리스") || p.Nickname.Contains("엘리스"));
        Assert.All(All.Where(p => p.HandlingTrainerName.Length == 0), p => Assert.False(p.HandlingTrainerTrash.ContainsAnyExcept((byte)0)));
    }

    [Fact]
    public void IsOfAKoreanConsole()
    {
        Assert.Equal((5, 136, 2), (Sav.ConsoleRegion, Sav.Country, Sav.Region));
        Assert.Equal((int)LanguageID.Korean, Sav.Language);
    }

    /// <summary>Mending draws the values of what hatched or was caught again and leaves everything else as it is.</summary>
    [Fact]
    public void MendingChangesTheValuesAndNothingElse()
    {
        var asked = new Options(Sav.OT, Sav.TrainerTID7, Sav.TrainerSID7, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 7, null, IvChoice.Random, true);
        var gen = new Generator(Template.Bytes, asked);
        var mended = Template.Read(gen.Refresh());
        Assert.Empty(gen.Problems);
        var after = Template.Pokemon(mended);
        Assert.Equal(All.Count, after.Count);
        // The bytes of a stored Pokemon that hold what is drawn: the constant, the checksum, the ability and its number, the personality value, the nature and sex, the individual values.
        static bool Drawn(int i) => i is >= 0x00 and <= 0x03 or 0x06 or 0x07 or 0x14 or 0x15 or >= 0x18 and <= 0x1D or >= 0x74 and <= 0x77;
        int again = 0;
        for (int i = 0; i < All.Count; i++)
        {
            var o = All[i]; var p = after[i];
            var x = o.Data.ToArray()[..o.SIZE_STORED]; var y = p.Data.ToArray()[..p.SIZE_STORED];
            string name = GameInfo.GetStrings("ko").Species[o.Species];
            if (Tell.Of(o) is Came.Card or Came.Older) { Assert.True(x.SequenceEqual(y), name); continue; }
            Assert.True(o.EncryptionConstant != p.EncryptionConstant, name);
            Assert.DoesNotContain(Enumerable.Range(0, x.Length), k => x[k] != y[k] && !Drawn(k));
            Assert.True(o.Gender == p.Gender && o.IsShiny == p.IsShiny && new LegalityAnalysis(p).Valid, name);
            again++;
        }
        Assert.True(again > 800);
        Assert.Equal(Sav.OT, mended.OT);
        Assert.Equal(Sav.ID32, mended.ID32);
        Assert.Equal(Sav.GameTime.SecondsToStart, mended.GameTime.SecondsToStart);
    }

    [Fact]
    public void MendingIsForTheSavesOwnTrainerOnly()
    {
        var asked = new Options("다른이름", 1, 2, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 7);
        Assert.Throws<ArgumentException>(() => new Generator(Template.Bytes, asked).Refresh());
    }
}

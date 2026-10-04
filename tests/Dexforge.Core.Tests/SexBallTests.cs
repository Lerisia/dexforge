using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>The balls that go by sex: each one the game would accept on a hatched Pokémon of that sex.</summary>
public class SexBallTests
{
    [Theory]
    [InlineData(37, 1, 0, Ball.Premier)]   // Alolan Vulpix, male
    [InlineData(37, 1, 1, Ball.Love)]      // Alolan Vulpix, female
    [InlineData(280, 0, 0, Ball.Moon)]     // Ralts, male
    [InlineData(778, 0, 1, Ball.Love)]     // Mimikyu, female
    public void The_ball_of_the_sex_is_legal_on_the_templates_Pokemon(int species, int form, int gender, Ball ball)
    {
        Assert.Equal(ball, SexBalls.For((ushort)species, (byte)form, (byte)gender));
        var sav = Template.Read(Template.Bytes);
        var pk = (PK7)Template.Pokemon(sav, party: false).First(p => p.Species == species && p.Form == form).Clone();
        pk.Gender = (byte)gender; pk.Ball = (byte)ball; pk.RefreshChecksum();
        Assert.True(new LegalityAnalysis(pk).Valid, string.Join(" | ", new LegalityAnalysis(pk).Report().Split('\n').Where(l => l.Contains("Invalid"))));
    }

    [Fact]
    public void Species_without_a_rule_keep_their_ball()
    {
        Assert.Null(SexBalls.For(37, 0, 0));    // the Kantonian Vulpix stays in its Moon Ball
        Assert.Null(SexBalls.For(653, 0, 0));   // a male Fennekin cannot be in a Dream Ball here, so none is asked for
    }
}

using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>What the person at the keyboard may type, and what is made of it.</summary>
public class OptionTests
{
    private static readonly string[] Balls = GameInfo.GetStrings("ko").balllist;

    [Theory]
    [InlineData("럭셔리볼", "럭셔리볼")]
    [InlineData("럭셔리", "럭셔리볼")]
    [InlineData("러브볼", "러브러브볼")]
    [InlineData("타이마볼", "타이머볼")]
    [InlineData("콤페볼", "컴퍼티션볼")]
    [InlineData("몬볼", "몬스터볼")]
    [InlineData("울트라볼", "울트라볼")]
    [InlineData(" 프렌드 볼 ", "프렌드볼")]
    public void ABallIsKnownByTheNamesPeopleCallIt(string asked, string meant)
    {
        Assert.True(BallNames.Find(asked, out var ball, out _));
        Assert.Equal(meant, Balls[ball!.Value]);
    }

    [Fact]
    public void TheMatchedBallsAreNoBallAtAll()
    {
        Assert.True(BallNames.Find("볼맞춤", out var ball, out _));
        Assert.Null(ball);
    }

    [Theory]
    [InlineData("프레셔스볼")]
    [InlineData("무지개볼")]
    [InlineData("")]
    public void ABallThatCannotBeChosenIsTurnedDownWithAReason(string asked)
    {
        Assert.False(BallNames.Find(asked, out _, out var why));
        Assert.NotEmpty(why);
    }

    [Theory]
    [InlineData("랜덤", IvChoice.Random)]
    [InlineData("5V", IvChoice.FiveFromEggs)]
    [InlineData("5v", IvChoice.FiveFromEggs)]
    public void IndividualValues(string asked, IvChoice meant)
    {
        Assert.True(IvNames.Find(asked, out var choice));
        Assert.Equal(meant, choice);
    }

    [Fact]
    public void IndividualValuesOtherwiseAreTurnedDown()
    {
        Assert.False(IvNames.Find("7V", out _));
        // Not offered until the grass is looked up beforehand.
        Assert.False(IvNames.Find("6V", out _));
    }

    [Theory]
    [InlineData("수컷", SexChoice.Male)]
    [InlineData("암컷", SexChoice.Female)]
    [InlineData("랜덤", SexChoice.Random)]
    [InlineData(" 수 ", SexChoice.Male)]
    public void Sexes(string asked, SexChoice meant)
    {
        Assert.True(SexNames.Find(asked, out var choice));
        Assert.Equal(meant, choice);
    }

    [Theory]
    [InlineData("최저", LevelChoice.Lowest)]
    [InlineData("100", LevelChoice.Hundred)]
    public void Levels(string asked, LevelChoice meant)
    {
        Assert.True(LevelNames.Find(asked, out var choice));
        Assert.Equal(meant, choice);
    }

    [Fact]
    public void OtherSexesAndLevelsAreTurnedDown()
    {
        Assert.False(SexNames.Find("무성", out _));
        Assert.False(LevelNames.Find("50", out _));
    }

    [Theory]
    [InlineData("Selene", "ミヅキ", "美月", true)]
    [InlineData("Luna", "ルナ", "月", true)]
    [InlineData("Christina", "ミヅキ", "美月", false)]   // Emerald in English holds seven letters
    [InlineData("Selene", "ミヅキミヅキ", "美月", false)] // the old Japanese games hold five
    [InlineData("Selene", "ミヅキ", "", false)]
    public void ForeignNamesMustFitTheOldestGame(string english, string japanese, string chinese, bool fits)
    {
        var asked = new Options("미월", null, null, default, default, 0, English: english, Japanese: japanese, Chinese: chinese);
        Assert.Equal(fits, ForeignNames.Refused(asked) is null);
    }

    [Fact]
    public void BySexAndLevelNothingIsAsked() => Assert.Equal((SexChoice.Random, LevelChoice.Lowest), (new Options("미월", null, null, default, default, 0).Sex, new Options("미월", null, null, default, default, 0).Level));

    [Theory]
    [InlineData("일반", false)]
    [InlineData("일반색", false)]
    [InlineData("일반 색", false)]
    [InlineData("이로치", true)]
    public void Colour(string asked, bool shiny)
    {
        Assert.True(ColourNames.Find(asked, out var got));
        Assert.Equal(shiny, got);
    }

    [Fact]
    public void ColourOtherwiseIsTurnedDown() => Assert.False(ColourNames.Find("무지개", out _));

    private static Options Window(DateOnly from, DateOnly to) => new("미월", null, null, from, to, 1);

    [Theory]
    [InlineData(999999u, 4294u, false)]
    [InlineData(967295u, 4294u, true)]
    [InlineData(1000000u, 0u, false)]
    [InlineData(0u, 4295u, false)]
    [InlineData(0u, 0u, true)]
    public void AnSidAndTidAreOneNumberThatMustFit(uint tid, uint sid, bool fits) => Assert.Equal(fits, Ids7.Refused(tid, sid) is null);

    [Fact]
    public void WhatIsNotGivenOfAnSidAndTidIsDrawnToFit()
    {
        for (int i = 0; i < 2000; i++)
        {
            var draw = new Draw(new Random(i));
            var (t, s) = Ids7.Halves(i % 2 == 0 ? 999999u : null, i % 3 == 0 ? 4294u : null, draw);
            uint id32 = (uint)s << 16 | t;
            if (i % 2 == 0 && i % 3 != 0) Assert.Equal(999999u, id32 % 1000000);
            if (i % 3 == 0 && i % 2 != 0) Assert.Equal(4294u, id32 / 1000000);
        }
    }

    [Fact]
    public void TheLastDayIsNotBeforeTheFirst()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Generator(Template.Bytes, Window(new DateOnly(2018, 6, 1), new DateOnly(2018, 5, 1))).Run());
        Assert.StartsWith("the last day is before", ex.Message);
    }

    [Theory]
    [InlineData(2000, 1, 1)]
    [InlineData(2100, 1, 1)]
    public void TheDaysAreWhatTheClockHolds(int y, int m, int d)
    {
        var day = new DateOnly(y, m, d);
        var ex = Assert.Throws<ArgumentException>(() => new Generator(Template.Bytes, Window(day, day)).Run());
        Assert.StartsWith("the days must be within", ex.Message);
    }

    /// <summary>The clock can be set to any day: before the game came out, before Celebi could come up, in the future.</summary>
    [Theory]
    [InlineData(2000, 1, 2, 2000, 1, 2)]
    [InlineData(2005, 5, 5, 2006, 5, 5)]
    [InlineData(2090, 1, 1, 2099, 12, 31)]
    public void AnyPeriodTheClockHoldsWillDo(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var asked = Window(new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2));
        var made = Making.Run(asked, Path.Combine(Path.GetTempPath(), $"dexforge-period-{Guid.NewGuid():N}"), Path.GetTempPath());
        Assert.True(made.Code == 0, string.Join(" / ", made.Refused.Take(5)));
        Assert.Equal(made.Checked!.Count, made.Checked.Legal);
        Directory.Delete(made.Folder!, true);
    }
}

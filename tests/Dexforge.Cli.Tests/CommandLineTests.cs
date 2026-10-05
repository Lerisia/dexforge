using PKHeX.Core;
using Xunit;

namespace Dexforge.Cli.Tests;

/// <summary>The command line: what it reads from the arguments, what it asks, and what it writes.</summary>
public class CommandLineTests
{
    private static string Fresh()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dexforge-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (int Code, string Out, string Err) Run(string[] args, string input = "", string? here = null)
    {
        var output = new StringWriter(); var error = new StringWriter();
        int code = new Runner(new StringReader(input), output, error, here ?? Fresh()).Run(args);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void ReadsEveryOption()
    {
        var o = Arguments.Parse(["--name", "달님", "--english", "Luna", "--japanese", "ルナ", "--chinese", "月", "--sid", "1234", "--tid", "567890",
                                 "--ball", "럭셔리", "--ivs", "5V", "--sex", "수컷", "--level", "100",
                                 "--from", "2019-02-03", "--to", "2019-04-05", "--seed", "7", "--out", "somewhere"], out var why);
        Assert.NotNull(o); Assert.Equal("", why);
        Assert.Equal(("달님", "Luna", "ルナ", "月"), (o!.Name, o.English, o.Japanese, o.Chinese));
        Assert.Equal((1234u, 567890u), (o.Sid, o.Tid));
        Assert.Equal("럭셔리", o.Ball);
        Assert.True(o.Shiny);   // always: there is no colour to choose
        Assert.Equal((IvChoice.FiveFromEggs, SexChoice.Male, LevelChoice.Hundred), (o.Ivs, o.Sex, o.Level));
        Assert.Equal((new DateOnly(2019, 2, 3), new DateOnly(2019, 4, 5)), (o.From, o.To));
        Assert.Equal((7, "somewhere"), (o.Seed, o.Out));
        var asked = o.ToOptions(out why);
        Assert.NotNull(asked);
        Assert.Equal((int)Ball.Luxury, asked!.Ball);
    }

    [Fact]
    public void NothingGivenIsWhatWasDecided()
    {
        var asked = Arguments.Parse([], out _)!.ToOptions(out _)!;
        Assert.Equal("미월", asked.Name);
        Assert.Equal((int)Ball.Poke, asked.Ball);
        Assert.True(asked.Shiny);
        Assert.Equal((IvChoice.Random, SexChoice.Random, LevelChoice.Lowest), (asked.Ivs, asked.Sex, asked.Level));
        Assert.Equal((new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31)), (asked.From, asked.To));
        Assert.Null(asked.Tid); Assert.Null(asked.Sid);
    }

    [Theory]
    [InlineData(new[] { "--what" }, "모르는 옵션")]
    [InlineData(new[] { "--tid", "abc" }, "TID 는 여섯 자리")]
    [InlineData(new[] { "--from", "어제" }, "2018-01-01 같은 모양")]
    [InlineData(new[] { "--name" }, "--name 뒤에 값이 없습니다")]
    [InlineData(new[] { "--ivs", "6V" }, "6V 는 지금 고를 수 없습니다")]
    [InlineData(new[] { "--sex", "무성" }, "수컷, 암컷, 랜덤")]
    [InlineData(new[] { "--level", "50" }, "최저, 100")]
    public void TurnsDownWhatCannotBeRead(string[] args, string said)
    {
        Assert.Null(Arguments.Parse(args, out var why));
        Assert.Contains(said, why);
        var (code, _, err) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains(said, err);
    }

    [Theory]
    [InlineData("--name", "일곱글자이름임", "6글자")]
    [InlineData("--ball", "프레셔스볼", "프레셔스볼")]
    public void TurnsDownWhatCannotBeMade(string option, string value, string said)
    {
        var (code, _, err) = Run([option, value]);
        Assert.Equal(2, code);
        Assert.Contains(said, err);
    }

    [Fact]
    public void TheGeneratorsOwnRefusalIsPassedOn()
    {
        var (code, _, err) = Run(["--sid", "4294", "--tid", "999999"]);
        Assert.Equal(2, code);
        Assert.Contains("967295", err);
    }

    [Fact]
    public void AsksOneThingAtATimeWhenNothingIsGiven()
    {
        // Every answer wrong once, then right; the last Enter closes the window.
        var answers = string.Join("\n",
            "일곱글자이름임", "달님",
            "Christina", "Luna",
            "ミヅキミヅキ", "ルナ",
            "", // Chinese: as it is
            "4295", "1234",
            "1000000", "567890",
            "무지개볼", "럭셔리볼",
            "7V", "5V",
            "무성", "암컷",
            "50", "100",
            "어제", "2019-05-01",
            "", // the last day: a year on
            "") + "\n";
        var order = new Order { Seed = 1 };
        var output = new StringWriter();
        new Questions(new StringReader(answers), output).Fill(order);
        Assert.Equal(("달님", "Luna", "ルナ", "美月"), (order.Name, order.English, order.Japanese, order.Chinese));
        Assert.Equal((1234u, 567890u), (order.Sid, order.Tid));
        Assert.Equal("럭셔리볼", order.Ball);
        Assert.True(order.Shiny);
        Assert.Equal((IvChoice.FiveFromEggs, SexChoice.Female, LevelChoice.Hundred), (order.Ivs, order.Sex, order.Level));
        Assert.Equal((new DateOnly(2019, 5, 1), new DateOnly(2020, 4, 30)), (order.From, order.To));
        var said = output.ToString();
        foreach (var why in new[] { "6글자까지", "7글자까지", "5글자까지", "0000에서 4294", "000000에서 999999", "'무지개볼' 라는 볼은 없습니다", "랜덤, 5V", "수컷, 암컷, 랜덤", "최저, 100", "2018-01-01 같은 모양" })
            Assert.Contains(why, said);
    }

    [Fact]
    public void ATidTooLargeForTheSidIsAskedAgainWithTheReason()
    {
        var order = new Order();
        var output = new StringWriter();
        var answers = string.Join("\n", "", "", "", "", "4294", "999999", "967295", "", "", "", "", "", "", "") + "\n";
        new Questions(new StringReader(answers), output).Fill(order);
        Assert.Equal((4294u, 967295u), (order.Sid, order.Tid));
        Assert.Contains("967295 까지", output.ToString());
    }

    [Fact]
    public void MakesTheSaveAndSaysWhere()
    {
        var here = Fresh();
        var (code, output, err) = Run(["--name", "달님", "--sid", "1234", "--tid", "567890", "--seed", "3"], here: here);
        Assert.True(code == 0, err);
        var folder = Path.Combine(here, "Dexforge-달님-567890");
        Assert.Contains(Path.Combine(folder, "main"), output);
        Assert.True(SaveUtil.TryGetSaveFile(File.ReadAllBytes(Path.Combine(folder, "main")), out var sav));
        Assert.Equal(567890u, sav!.TrainerTID7);
        Assert.True(File.Exists(Path.Combine(folder, "만든기록.txt")));
        Directory.Delete(here, true);
    }

    [Fact]
    public void AskedOneThingAtATimeItMakesTheSaveAndWaitsForEnter()
    {
        var here = Fresh();
        var answers = string.Concat(Enumerable.Repeat("\n", 16));
        var (code, output, _) = Run([], answers, here);
        Assert.Equal(0, code);
        Assert.Contains("Enter 를 누르면 닫힙니다.", output);
        Assert.Single(Directory.GetDirectories(here));
        Directory.Delete(here, true);
    }

    [Fact]
    public void MendsOnlyWhatItIsToldTo()
    {
        var dir = Fresh();
        var template = Path.Combine(dir, "template");
        File.WriteAllBytes(template, Making.Template());
        var (code, output, err) = Run(["--refresh", template, Path.Combine(dir, "mended"), "--seed", "5", "--only", "751,752"]);
        Assert.True(code == 0, err);
        Assert.Contains("다시 뽑은 것  2마리", output);
        Assert.True(SaveUtil.TryGetSaveFile(File.ReadAllBytes(Path.Combine(dir, "mended", "main")), out _));
        Directory.Delete(dir, true);
    }

    [Theory]
    [InlineData(new[] { "--seed" }, "뒤에 값이 없습니다")]
    [InlineData(new[] { "--only", "물거미" }, "수로 적어 주세요")]
    [InlineData(new[] { "--what", "1" }, "모르는 옵션")]
    public void TurnsDownAMendItCannotRead(string[] rest, string said)
    {
        var (code, _, err) = Run(["--refresh", "a", "b", .. rest]);
        Assert.Equal(2, code);
        Assert.Contains(said, err);
    }

    [Fact]
    public void ScarletIsAskedForWithItsOwnOptions()
    {
        var o = Arguments.Parse(["--game", "스칼렛", "--name", "재연", "--size", "최대", "--sex", "암컷", "--ball", "볼맞춤"], out var why);
        Assert.NotNull(o); Assert.Equal("", why);
        Assert.Equal(Game.Scarlet, o!.Game);
        var asked = o.ToOptions9(out why);
        Assert.NotNull(asked); Assert.Equal("", why);
        Assert.Equal(SizeChoice.Largest, asked!.Size);
        Assert.Equal(SexChoice.Female, asked.Sex);
        Assert.Null(asked.Ball);
        Assert.Equal(new DateOnly(2024, 1, 1), asked.From);   // no period typed: 2024
        var o2 = Arguments.Parse(["--game", "sv", "--size", "우두머리"], out _)!;
        Assert.Equal(SizeChoice.Alpha, o2.ToOptions9(out _)!.Size);   // refused later by the maker, which has no alphas
    }

    [Fact]
    public void ZAIsAskedForWithItsOwnOptions()
    {
        var o = Arguments.Parse(["--game", "za", "--name", "재연", "--size", "우두머리", "--ball", "문볼"], out var why);
        Assert.NotNull(o); Assert.Equal("", why);
        Assert.Equal(Game.ZA, o!.Game);
        var asked = o.ToOptions9a(out why);
        Assert.NotNull(asked); Assert.Equal("", why);
        Assert.Equal(SizeChoice.Alpha, asked!.Size);
        Assert.Equal((int)PKHeX.Core.Ball.Moon, asked.Ball);
        Assert.Equal(new DateOnly(2026, 1, 1), asked.From);   // no period typed: 2026, up to today while the year runs
        Assert.Equal(Dexforge.ZA.Making9a.DefaultPeriod().To, asked.To);
        Assert.True(asked.To <= DateOnly.FromDateTime(DateTime.Now) || asked.To == new DateOnly(2026, 12, 31));
        Assert.Null(Arguments.Parse(["--game", "lumiose"], out var refused));
        Assert.Contains("za", refused);
    }

    [Fact]
    public void ArceusIsAskedForWithItsOwnOptions()
    {
        var o = Arguments.Parse(["--game", "아르세우스", "--name", "달님", "--ball", "페더볼", "--size", "우두머리", "--sex", "암컷", "--seed", "5"], out var why);
        Assert.NotNull(o); Assert.Equal("", why);
        Assert.Equal(Game.Arceus, o!.Game);
        var asked = o.ToOptions8a(out why);
        Assert.NotNull(asked); Assert.Equal("", why);
        Assert.Equal((int)PKHeX.Core.Ball.LAFeather, asked!.Ball);
        Assert.Equal(SizeChoice.Alpha, asked.Size);
        Assert.Equal(SexChoice.Female, asked.Sex);
        Assert.Equal(Dexforge.Arceus.Making8a.Released, asked.From);   // no period typed: the release year
        Assert.Null(Arguments.Parse(["--game", "arceus", "--size", "거대"], out why));
        Assert.Contains("최소, 최대(스칼렛), 우두머리(아르세우스), 랜덤", why);
        Assert.Null(Arguments.Parse(["--game", "arceus", "--ball", "마스터볼"], out _)?.ToOptions8a(out why));
        Assert.Contains("히스이 볼", why);
    }

    [Fact]
    public void RibbonsAreNamedInKoreanOrByKey()
    {
        var o = Arguments.Parse(["--ribbons", "알로라챔피언,절친리본,RibbonEffort"], out var why);
        Assert.NotNull(o); Assert.Equal("", why);
        var asked = o!.ToOptions(out why);
        Assert.NotNull(asked); Assert.Equal("", why);
        Assert.Equal(["RibbonChampionAlola", "RibbonBestFriends", "RibbonEffort"], asked!.Ribbons);
        Assert.Null(Arguments.Parse(["--ribbons", "클래식"], out _)!.ToOptions(out why));
        Assert.Contains("없거나 붙일 수 없습니다", why);

        // Sword has its own five
        var sword = Arguments.Parse(["--game", "sword", "--ribbons", "가라르챔피언,마스터타워,마스터랭크"], out why);
        Assert.NotNull(sword);
        var asked8 = sword!.ToOptions8(out why);
        Assert.NotNull(asked8); Assert.Equal("", why);
        Assert.Equal(["RibbonChampionGalar", "RibbonTowerMaster", "RibbonMasterRank"], asked8!.Ribbons);
        Assert.Null(Arguments.Parse(["--game", "sword", "--ribbons", "알로라챔피언"], out _)!.ToOptions8(out why));
        Assert.Contains("소드에서 붙일 수 없습니다", why);
    }
}

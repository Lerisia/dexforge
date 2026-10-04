using PKHeX.Core;

namespace Dexforge.Cli;

/// <summary>What has been asked for so far, as it was typed: made into options once everything is in.</summary>
internal sealed class Order
{
    /// <summary>Which game's save: Ultra Sun (the seventh generation's national dex) or Sword.</summary>
    public Game Game = Game.UltraSun;
    public int Year = Dexforge.Sword.Maker8.DefaultYear;
    public string Name = "미월";
    public string English = ForeignNames.English, Japanese = ForeignNames.Japanese, Chinese = ForeignNames.Chinese;
    public uint? Tid, Sid;
    public string? Ball;
    public bool Shiny = true;
    public IvChoice Ivs = IvChoice.Random;
    public SexChoice Sex = SexChoice.Random;
    public LevelChoice Level = LevelChoice.Lowest;
    public DateOnly From = new(2018, 1, 1), To = new(2018, 12, 31);
    /// <summary>Whether a period was typed; Legends: Arceus otherwise takes its own release year.</summary>
    public bool DatesGiven;
    public SizeChoice Size = SizeChoice.Random;
    public int Seed = Random.Shared.Next();
    public string? Out;
    /// <summary>Event box: received within the first so many days of each distribution (0: anywhere in its window), and what was picked for the spare room.</summary>
    public int FirstDays = 0;
    public List<string> Picks = [];
    public bool ListPicks;

    /// <summary>The options asked for; or why they cannot be. The rest is refused by the generator itself, with its reason.</summary>
    public Options? ToOptions(out string why)
    {
        why = "";
        if (Name.Length is < 1 or > 6) { why = "어버이 이름은 1글자에서 6글자 사이여야 합니다."; return null; }
        int? ball = (int)PKHeX.Core.Ball.Poke;
        if (Ball is not null && !BallNames.Find(Ball, out ball, out why)) return null;
        return new Options(Name, Tid, Sid, From, To, Seed, ball, Ivs, Shiny, Level, Sex, English, Japanese, Chinese);
    }

    /// <summary>The Sword options asked for; or why they cannot be.</summary>
    public Options8? ToOptions8(out string why)
    {
        why = "";
        int? ball = null;
        if (Ball is not null && !BallNames.Find(Ball, out ball, out why)) return null;
        return new Options8(Name, Tid, Sid, Year, Seed, ball, Shiny);
    }

    /// <summary>The Legends: Arceus options asked for; or why they cannot be.</summary>
    public Options8a? ToOptions8a(out string why)
    {
        why = "";
        int ball = (int)PKHeX.Core.Ball.LAPoke;
        if (Ball is not null && !BallNames8a.Find(Ball, out ball, out why)) return null;
        var from = DatesGiven ? From : Dexforge.Arceus.Making8a.Released;
        var to = DatesGiven ? To : new DateOnly(2022, 12, 31);
        return new Options8a(Name, Tid, Sid, from, to, Seed, ball, Shiny, Size, Level, Sex);
    }

    /// <summary>The event box options asked for; or why they cannot be.</summary>
    public Dexforge.EventBox.EventOptions? ToEventOptions(out string why)
    {
        why = "";
        if (Name.Length is < 1 or > 6) { why = "어버이 이름은 1글자에서 6글자 사이여야 합니다."; return null; }
        if (FirstDays < 0) { why = "--first-days 는 0 이상입니다."; return null; }
        return new Dexforge.EventBox.EventOptions(Name, Tid, Sid, Seed, FirstDays, Picks);
    }
}

/// <summary>The games a save can be made for: the national dex of Ultra Sun, the Sword dex, the event box (an Ultra Sun save of every distribution), and the Hisui dex of Legends: Arceus.</summary>
internal enum Game { UltraSun, Sword, EventBox, Arceus }

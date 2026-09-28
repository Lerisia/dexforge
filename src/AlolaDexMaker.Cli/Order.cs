using PKHeX.Core;

namespace AlolaDexMaker.Cli;

/// <summary>What has been asked for so far, as it was typed: made into options once everything is in.</summary>
internal sealed class Order
{
    public string Name = "미월";
    public string English = ForeignNames.English, Japanese = ForeignNames.Japanese, Chinese = ForeignNames.Chinese;
    public uint? Tid, Sid;
    public string? Ball;
    public bool Shiny = true;
    public IvChoice Ivs = IvChoice.Random;
    public SexChoice Sex = SexChoice.Random;
    public LevelChoice Level = LevelChoice.Lowest;
    public DateOnly From = new(2018, 1, 1), To = new(2018, 12, 31);
    public int Seed = Random.Shared.Next();
    public string? Out;

    /// <summary>The options asked for; or why they cannot be. The rest is refused by the generator itself, with its reason.</summary>
    public Options? ToOptions(out string why)
    {
        why = "";
        if (Name.Length is < 1 or > 6) { why = "어버이 이름은 1글자에서 6글자 사이여야 합니다."; return null; }
        int? ball = (int)PKHeX.Core.Ball.Poke;
        if (Ball is not null && !BallNames.Find(Ball, out ball, out why)) return null;
        return new Options(Name, Tid, Sid, From, To, Seed, ball, Ivs, Shiny, Level, Sex, English, Japanese, Chinese);
    }
}

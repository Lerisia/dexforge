using PKHeX.Core;

namespace AlolaDexMaker;

/// <summary>What the person asking has chosen.</summary>
/// <param name="Ball">The one ball everything goes into where it can; none, and each keeps the ball picked for it.</param>
/// <param name="Ivs">How high the individual values are drawn.</param>
/// <param name="Shiny">Shiny wherever it can be; or plain wherever it can be.</param>
/// <param name="Level">At the lowest level each can be, or at 100.</param>
/// <param name="Sex">Which sex, where a Pokemon is free to be either.</param>
/// <param name="English">The name of whoever plays the games in English, French, German, Italian and Spanish: Selene in each of them.</param>
/// <param name="Japanese">The name of whoever plays the games in Japanese: ミヅキ.</param>
/// <param name="Chinese">The name of whoever plays the game in Chinese: 美月.</param>
/// <param name="Tid">The six-digit TID the game shows (seventh generation), or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
public sealed record Options(string Name, uint? Tid, uint? Sid, DateOnly From, DateOnly To, int Seed, int? Ball = null, IvChoice Ivs = IvChoice.Random, bool Shiny = true,
                             LevelChoice Level = LevelChoice.Lowest, SexChoice Sex = SexChoice.Random,
                             string English = ForeignNames.English, string Japanese = ForeignNames.Japanese, string Chinese = ForeignNames.Chinese);

/// <summary>
/// How the individual values are drawn, for what was hatched or met in this generation.
/// What came on a card has what the card gives it; what came up from an older game has what that game's random numbers gave it.
/// </summary>
public enum IvChoice
{
    /// <summary>As they come: whatever the encounter promises perfect, and the rest by chance.</summary>
    Random,
    /// <summary>What hatched has five perfect, as careful breeding gives; the rest as they come.</summary>
    FiveFromEggs,
    /// <summary>All six perfect.</summary>
    Six,
}

/// <summary>How high the boxes' Pokemon are. The moves stay what they are either way: the levels come of Rare Candies.</summary>
public enum LevelChoice
{
    /// <summary>The lowest each can be at.</summary>
    Lowest,
    /// <summary>100.</summary>
    Hundred,
}

/// <summary>
/// Which sex the boxes' Pokemon are, where they are free to be either. Not free: what has none, a species of one sex, what a card or
/// the meeting settles, and the species the boxes hold both of because the two look different. The party is left as it is.
/// </summary>
public enum SexChoice
{
    /// <summary>As the game's random numbers give it, by the species' own ratio.</summary>
    Random,
    Male,
    Female,
}

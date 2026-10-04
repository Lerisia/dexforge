using Dexforge;
using PKHeX.Core;
// For each ball one can ask for, how many of the boxes' Pokemon cannot go in it and go in a Poke Ball instead: a save is made for
// each ball and counted. Cards are left out (they keep the card's ball whatever is asked), and so is the party.
// Prints Dexforge/BallFits.cs.
//   balltable [seed]
int seed = args.Length > 0 ? int.Parse(args[0]) : 20180101;
var shelf = new[] { Ball.Poke, Ball.Great, Ball.Ultra, Ball.Master, Ball.Premier, Ball.Heal, Ball.Net, Ball.Nest, Ball.Dive, Ball.Dusk, Ball.Timer, Ball.Quick, Ball.Repeat, Ball.Luxury,
                    Ball.Level, Ball.Lure, Ball.Moon, Ball.Friend, Ball.Love, Ball.Heavy, Ball.Fast, Ball.Safari, Ball.Sport, Ball.Dream, Ball.Beast };
var ko = GameInfo.GetStrings("ko");
var rows = new List<string>();
int total = 0;
foreach (var ball in shelf)
{
    var dir = Path.Combine(Path.GetTempPath(), $"balltable-{Guid.NewGuid():N}");
    var made = Making.Run(new Options("미월", null, null, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), seed, (int)ball), dir, dir);
    if (made.Code != 0) throw new Exception($"{ko.balllist[(int)ball]}: {string.Join(" / ", made.Refused)}");
    SaveUtil.TryGetSaveFile(File.ReadAllBytes(made.Save!), out var sav);
    var s = (SAV7)sav!;
    int boxed = 0, outOf = 0;
    for (int b = 0; b < s.BoxCount; b++)
        foreach (var p in s.GetBoxData(b))
        {
            if (p.Species == 0 || new LegalityAnalysis(p).EncounterMatch is MysteryGift) continue;
            boxed++; total = boxed;
            if (p.Ball != (int)ball) outOf++;
        }
    Console.Error.WriteLine($"{ko.balllist[(int)ball]}: {outOf} / {boxed}");
    rows.Add($"        [Ball.{ball}] = {outOf},");
    Directory.Delete(dir, true);
}
Console.WriteLine($$"""
using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// How many of the boxes' Pokemon cannot go in each ball, where one ball is asked for, and go in a Poke Ball instead: counted from a save made
/// with each ball (dex-tools/balltable, seed {{seed}}). Cards keep their own ball and are not counted; neither is the party. Taken over by a script; not written by hand.
/// </summary>
public static class BallFits
{
    /// <summary>What can be put in a ball of one's choosing: the boxes without the cards.</summary>
    public const int Boxed = {{total}};

    public static readonly IReadOnlyDictionary<Ball, int> CannotGoIn = new Dictionary<Ball, int>
    {
{{string.Join("\n", rows)}}
    };
}
""");

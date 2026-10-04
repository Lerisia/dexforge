using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// How many of the boxes' Pokemon cannot go in each ball, where one ball is asked for, and go in a Poke Ball instead: counted from a save made
/// with each ball (dex-tools/balltable, seed 20180101). Cards keep their own ball and are not counted; neither is the party. Taken over by a script; not written by hand.
/// </summary>
public static class BallFits
{
    /// <summary>What can be put in a ball of one's choosing: the boxes without the cards.</summary>
    public const int Boxed = 918;

    public static readonly IReadOnlyDictionary<Ball, int> CannotGoIn = new Dictionary<Ball, int>
    {
        [Ball.Poke] = 0,
        [Ball.Great] = 25,
        [Ball.Ultra] = 25,
        [Ball.Master] = 834,
        [Ball.Premier] = 25,
        [Ball.Heal] = 27,
        [Ball.Net] = 25,
        [Ball.Nest] = 25,
        [Ball.Dive] = 25,
        [Ball.Dusk] = 27,
        [Ball.Timer] = 25,
        [Ball.Quick] = 27,
        [Ball.Repeat] = 25,
        [Ball.Luxury] = 25,
        [Ball.Level] = 151,
        [Ball.Lure] = 151,
        [Ball.Moon] = 151,
        [Ball.Friend] = 151,
        [Ball.Love] = 151,
        [Ball.Heavy] = 151,
        [Ball.Fast] = 151,
        [Ball.Safari] = 637,
        [Ball.Sport] = 892,
        [Ball.Dream] = 357,
        [Ball.Beast] = 264,
    };
}

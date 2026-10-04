using PKHeX.Core;

namespace Dexforge;

/// <summary>How big the Pokémon are asked to be.</summary>
public enum SizeChoice
{
    /// <summary>Height 0 and weight 0: the smallest the game draws (XXXS). Alphas cannot be.</summary>
    Smallest,
    /// <summary>An alpha wherever the game has one; the rest as the game draws them.</summary>
    Alpha,
    /// <summary>As the game draws them.</summary>
    Random,
}

/// <summary>What is asked for a Legends: Arceus save: the game plays in Korean, the trainer is whoever the template was, renamed.</summary>
/// <param name="Name">The trainer's name, up to six Korean letters (twelve of the Latin alphabet).</param>
/// <param name="Tid">The six-digit TID the game shows, or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
/// <param name="From">The first day anything was caught.</param>
/// <param name="To">The last.</param>
/// <param name="Ball">The one Hisuian ball everything is caught in, where the encounter does not fix its own.</param>
/// <param name="Shiny">Shiny wherever it can be; or plain.</param>
/// <param name="Size">Smallest, alpha, or as drawn.</param>
/// <param name="Level">The level each was caught at, or 100.</param>
/// <param name="Sex">A sex for the species that have either, or as drawn.</param>
public sealed record Options8a(string Name, uint? Tid, uint? Sid, DateOnly From, DateOnly To, int Seed, int Ball = (int)PKHeX.Core.Ball.LAPoke,
                               bool Shiny = true, SizeChoice Size = SizeChoice.Random, LevelChoice Level = LevelChoice.Lowest, SexChoice Sex = SexChoice.Random);

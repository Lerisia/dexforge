using Dexforge.Arceus;

namespace Dexforge.Scarlet;

/// <summary>What is asked for a Scarlet save: the game plays in Korean, the trainer is whoever the template was, renamed.</summary>
/// <param name="Name">The trainer's name, up to six Korean letters.</param>
/// <param name="Tid">The six-digit TID the game shows, or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
/// <param name="From">The first day anything was caught.</param>
/// <param name="To">The last day.</param>
/// <param name="Seed">The seed of the draw.</param>
/// <param name="Ball">The one ball everything goes into where it can; none, and each keeps the ball picked for it.</param>
/// <param name="Shiny">Shiny wherever it can be (the wild and the eggs); or plain.</param>
/// <param name="Size">The scale of every wild catch: the smallest (with the Mini Mark), the largest (Jumbo Mark), or as it comes.</param>
/// <param name="Level">Caught level, or 100.</param>
/// <param name="Sex">Male, female or as it comes, for the species that have both.</param>
public sealed record Options9(string Name, uint? Tid, uint? Sid, DateOnly From, DateOnly To, int Seed, int? Ball = null, bool Shiny = true,
                              SizeChoice Size = SizeChoice.Random, LevelChoice Level = LevelChoice.Lowest, SexChoice Sex = SexChoice.Random);

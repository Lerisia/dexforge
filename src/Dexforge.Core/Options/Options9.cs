using Dexforge.Arceus;

namespace Dexforge;

/// <summary>What is asked for a Scarlet or a Legends: Z-A save: the game plays in Korean, the trainer is whoever the template was, renamed.</summary>
/// <param name="Name">The trainer's name, up to six Korean letters.</param>
/// <param name="Tid">The six-digit TID the game shows, or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
/// <param name="From">The first day anything was caught.</param>
/// <param name="To">The last day.</param>
/// <param name="Seed">The seed of the draw.</param>
/// <param name="Ball">The one ball everything goes into where it can; none, and each keeps the ball picked for it.</param>
/// <param name="Shiny">Shiny wherever it can be (Scarlet: the wild and the eggs; Z-A: hyperspace and the wild); or plain.</param>
/// <param name="Size">The size of every wild catch: the smallest (0; Scarlet adds the Mini Mark), the largest (255; Scarlet adds the Jumbo Mark),
/// an alpha wherever hyperspace has one (Z-A only), or as it comes.</param>
/// <param name="Level">Caught level, or 100.</param>
/// <param name="Sex">Male, female or as it comes, for the species that have both.</param>
public sealed record Options9(string Name, uint? Tid, uint? Sid, DateOnly From, DateOnly To, int Seed, int? Ball = null, bool Shiny = true,
                              SizeChoice Size = SizeChoice.Random, LevelChoice Level = LevelChoice.Lowest, SexChoice Sex = SexChoice.Random);

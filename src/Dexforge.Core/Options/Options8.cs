using PKHeX.Core;

namespace Dexforge;

/// <summary>What is asked for a Sword save: the game plays in Korean, the trainer is whoever the template was, renamed.</summary>
/// <param name="Name">The trainer's name, up to six Korean letters (twelve of the Latin alphabet).</param>
/// <param name="Tid">The six-digit TID the game shows, or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
/// <param name="Year">The year everything was hatched, caught or received in; what a card dates itself outside it keeps its own date.</param>
/// <param name="Ball">The one ball everything goes into where it can; none, and each keeps the ball picked for it.</param>
/// <param name="Shiny">Shiny wherever it can be; or plain.</param>
public sealed record Options8(string Name, uint? Tid, uint? Sid, int Year, int Seed, int? Ball = null, bool Shiny = true);

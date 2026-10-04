namespace Dexforge.Arceus;

/// <summary>
/// Seeds whose four size draws (height, then weight, each rand(0x81) + rand(0x80), right after the nature) come out as asked:
/// the general <see cref="SizeSeeds"/> at the height draw's position.
/// <para>
/// The draws sit at a known position only when nothing before them drew again: a shiny found at roll j, the guaranteed
/// IVs placed without a collision, and a nature or gender draw that was not rejected. A sample is therefore drawn in full
/// afterwards and kept only if it really is what was asked; about three in four are.
/// </para>
/// </summary>
public sealed class SizeSeeds8a
{
    private readonly SizeSeeds seeds;

    /// <summary>The smallest Pokémon: height 0 and weight 0.</summary>
    public static int[] Smallest => [0, 0, 0, 0];
    /// <summary>The largest a non-alpha can be: 0x80 + 0x7F = 255 for both.</summary>
    public static int[] Largest => [0x80, 0x7F, 0x80, 0x7F];

    /// <param name="position">the index of the height draw among the seed's draws</param>
    /// <param name="targets">what the four draws must give: rand(0x81), rand(0x80), rand(0x81), rand(0x80)</param>
    public SizeSeeds8a(int position, int[] targets) => seeds = new SizeSeeds(position, [8, 7, 8, 7], targets);

    public int Rank => seeds.Rank;

    /// <summary>A seed meeting the size equations, chosen at random; null when this choice of low bytes has no solution.</summary>
    public ulong? Sample(Random rnd) => seeds.Sample(rnd);

    /// <summary>
    /// Where the height draw sits for a Pokémon whose shiny came at roll <paramref name="shinyRoll"/> (or that used every
    /// roll): the constant and the throwaway id, the PIDs, six IV draws (a guaranteed IV costs one draw to place instead of
    /// one to roll), the ability, the gender when the species rolls one, and the nature.
    /// </summary>
    public static int Position(int shinyRoll, bool rollsGender) => 2 + shinyRoll + 6 + 1 + (rollsGender ? 1 : 0) + 1;
}

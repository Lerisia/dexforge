using PKHeX.Core;

namespace AlolaDexMaker;

/// <summary>Where the ball picked for a species hangs on its sex: the Ralts line, in a Moon Ball when male and a Love Ball when female. Asked for by the owner, 2026-10-04.</summary>
public static class SexBalls
{
    /// <summary>The ball for this species and sex, or none where the species keeps the ball picked for it.</summary>
    public static Ball? For(ushort species, byte gender) => species switch
    {
        280 or 281 or 282 => gender == 0 ? Ball.Moon : Ball.Love, // Ralts, Kirlia, Gardevoir
        778 => gender == 0 ? Ball.Moon : Ball.Love,              // Mimikyu
        _ => null,
    };

    /// <summary>What the template's Pokemon would be in, were it of the sex it is: the picked ball, or the sex's ball.</summary>
    public static int Expected(PKM template, byte gender) => For(template.Species, gender) is { } b ? (int)b : template.Ball;
}

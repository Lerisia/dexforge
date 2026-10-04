using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// Where the ball picked for a species hangs on its sex: the Ralts line and Mimikyu in a Moon Ball when male and a Love Ball
/// when female, the Alolan Vulpix line in a Premier Ball when male and a Love Ball when female. Asked for by the owner,
/// 2026-10-04. (The Fennekin line was asked for in a Dream Ball when male, but no Fennekin can be in one in this generation,
/// so it keeps its Love Ball either way.)
/// </summary>
public static class SexBalls
{
    /// <summary>The ball for this species, form and sex, or none where the species keeps the ball picked for it.</summary>
    public static Ball? For(ushort species, byte form, byte gender) => (species, form) switch
    {
        (280 or 281 or 282, _) => gender == 0 ? Ball.Moon : Ball.Love,   // Ralts, Kirlia, Gardevoir
        (778, _) => gender == 0 ? Ball.Moon : Ball.Love,                 // Mimikyu
        (37 or 38, 1) => gender == 0 ? Ball.Premier : Ball.Love,         // Alolan Vulpix, Ninetales
        _ => null,
    };

    /// <summary>What the template's Pokemon would be in, were it of the sex it is: the picked ball, or the sex's ball.</summary>
    public static int Expected(PKM template, byte gender) => For(template.Species, template.Form, gender) is { } b ? (int)b : template.Ball;
}

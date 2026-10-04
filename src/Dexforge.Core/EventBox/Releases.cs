using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>When the games that carry Pokémon up came out, by language — the earliest day a transfer could have happened.</summary>
public static class Releases
{
    /// <summary>Diamond and Pearl, where Pal Park takes gen 3 Pokémon.</summary>
    public static DateOnly DiamondPearl(LanguageID language) => language switch
    {
        LanguageID.Japanese => new(2006, 9, 28),
        LanguageID.Korean => new(2008, 2, 14),
        LanguageID.English => new(2007, 4, 22),
        _ => new(2007, 7, 27),  // Europe
    };

    /// <summary>Black and White, where Poké Transfer takes gen 4 Pokémon.</summary>
    public static DateOnly BlackWhite(LanguageID language) => language switch
    {
        LanguageID.Japanese => new(2010, 9, 18),
        LanguageID.Korean => new(2011, 4, 21),
        LanguageID.English => new(2011, 3, 6),
        _ => new(2011, 3, 4),
    };
}

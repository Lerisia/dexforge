using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>
/// Whoever received a card: a trainer of the game and language the card calls for. One trainer per game and language,
/// drawn once with random ids, so everything one game received shares its ids. The names are the heroine of Ultra Sun in
/// each language, except the Korean one, which is whatever was asked for (it becomes the save's trainer too).
/// </summary>
public sealed class Receivers(Random rnd, string koreanName, byte gender)
{
    private readonly Dictionary<string, SimpleTrainerInfo> made = [];

    /// <summary>The save's own trainer is given, not drawn: the template save's, with the name asked for.</summary>
    public void SetSave(SimpleTrainerInfo save) => made[$"{save.Version}/{(LanguageID)save.Language}"] = save;
    public IReadOnlyDictionary<string, SimpleTrainerInfo> All => made;

    public static string NameFor(LanguageID language, string koreanName) => language switch
    {
        LanguageID.Korean => koreanName,
        LanguageID.Japanese => "ミヅキ",
        LanguageID.ChineseS or LanguageID.ChineseT => "美月",
        _ => "Selene",
    };

    /// <summary>Old games hold fewer letters: Japanese and Korean games before the sixth generation five, Western ones seven.</summary>
    public static string Fit(string name, LanguageID language, int generation)
    {
        int room = generation >= 6 ? (language is LanguageID.Japanese or LanguageID.Korean or LanguageID.ChineseS or LanguageID.ChineseT ? 6 : 12)
                 : language is LanguageID.Japanese or LanguageID.Korean ? 5 : 7;
        return name.Length > room ? name[..room] : name;
    }

    public static int GenerationOf(GameVersion v) => v switch
    {
        GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.FR or GameVersion.LG or GameVersion.CXD => 3,
        GameVersion.D or GameVersion.P or GameVersion.Pt or GameVersion.HG or GameVersion.SS => 4,
        GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2 => 5,
        GameVersion.X or GameVersion.Y or GameVersion.OR or GameVersion.AS => 6,
        _ => 7,
    };

    public SimpleTrainerInfo For(GameVersion version, LanguageID language)
    {
        string key = $"{version}/{language}";
        if (made.TryGetValue(key, out var tr)) return tr;
        int gen = GenerationOf(version);
        // the console's region, as the game's language suggests (only 3DS games keep these three)
        var (console, country, region) = gen < 6 ? ((byte)0, (byte)0, (byte)0) : language switch
        {
            LanguageID.Korean => ((byte)5, (byte)136, (byte)2),   // Korea, Seoul
            LanguageID.Japanese => ((byte)0, (byte)1, (byte)13),  // Japan, Tokyo
            LanguageID.ChineseS or LanguageID.ChineseT => ((byte)4, (byte)160, (byte)1),  // Taiwan
            _ => ((byte)1, (byte)49, (byte)5),                    // USA, California
        };
        tr = new SimpleTrainerInfo(version)
        {
            OT = Fit(NameFor(language, koreanName), language, gen),
            Gender = gender,
            TID16 = (ushort)rnd.Next(1, 65536),
            SID16 = (ushort)rnd.Next(0, 65536),
            Language = (int)language,
            ConsoleRegion = console, Country = country, Region = region,
        };
        made[key] = tr;
        return tr;
    }
}

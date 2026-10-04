namespace Dexforge;

/// <summary>
/// The trainers of the template who play in another language, by the names the template gives them (the heroine of Ultra Sun in that language),
/// and the names asked for in their place. Each name has to fit the oldest game it is written into.
/// </summary>
public static class ForeignNames
{
    public const string English = "Selene", Japanese = "ミヅキ", Chinese = "美月";
    /// <summary>Emerald in English holds seven letters; Emerald, Platinum and Crystal in Japanese hold five; Ultra Sun in Chinese six.</summary>
    public const int EnglishRoom = 7, JapaneseRoom = 5, ChineseRoom = 6;

    /// <summary>The name asked for in place of one the template gives; any other name is kept.</summary>
    public static string Of(Options opt, string was) => was switch
    {
        English => opt.English,
        Japanese => opt.Japanese,
        Chinese => opt.Chinese,
        _ => was,
    };

    /// <summary>Why the names asked for cannot be written, or nothing.</summary>
    public static string? Refused(Options opt)
    {
        if (opt.English.Length is < 1 or > EnglishRoom) return $"영어 이름은 1글자에서 {EnglishRoom}글자 사이여야 합니다. 에메랄드 영어판이 {EnglishRoom}글자까지입니다.";
        if (opt.Japanese.Length is < 1 or > JapaneseRoom) return $"일본어 이름은 1글자에서 {JapaneseRoom}글자 사이여야 합니다. 옛 일본어판 게임이 {JapaneseRoom}글자까지입니다.";
        if (opt.Chinese.Length is < 1 or > ChineseRoom) return $"중국어 이름은 1글자에서 {ChineseRoom}글자 사이여야 합니다.";
        return null;
    }
}

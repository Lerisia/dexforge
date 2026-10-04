namespace Dexforge;

/// <summary>How the colour is asked for, in words.</summary>
public static class ColourNames
{
    public const string Help = "색은 일반, 이로치 중 하나로 적어 주세요.";

    public static bool Find(string asked, out bool shiny)
    {
        switch (asked.Replace(" ", "").ToLowerInvariant())
        {
            case "일반" or "일반색" or "normal": shiny = false; return true;
            case "이로치" or "색이다른" or "shiny": shiny = true; return true;
            default: shiny = true; return false;
        }
    }

    public static string Said(bool shiny) => shiny
        ? "이로치 (이로치가 막힌 포켓몬은 일반 색)"
        : "일반 색 (카드가 이로치로 정한 배포는 이로치)";
}

/// <summary>How the individual values are asked for, in words.</summary>
public static class IvNames
{
    public const string Help = "개체값은 랜덤, 5V 중 하나로 적어 주세요. (6V 는 지금 고를 수 없습니다.)";

    public static bool Find(string asked, out IvChoice choice)
    {
        switch (asked.Replace(" ", "").ToLowerInvariant())
        {
            case "랜덤" or "무작위" or "random": choice = IvChoice.Random; return true;
            case "5v" or "5브이": choice = IvChoice.FiveFromEggs; return true;
            // Six perfect values are not offered until the sessions that give them in the grass are looked up beforehand (the generator keeps IvChoice.Six).
            default: choice = IvChoice.Random; return false;
        }
    }

    public static string Said(IvChoice choice) => choice switch
    {
        IvChoice.FiveFromEggs => "알에서 나온 포켓몬은 5V, 나머지는 랜덤 (배포와 구세대 출신은 카드와 그 게임이 정한 대로)",
        IvChoice.Six => "6V (배포와 구세대 출신은 카드와 그 게임이 정한 대로; 잡은 것 중 게임이 31 을 보장하지 않는 야생·스타팅은 난수가 준 대로)",
        _ => "랜덤",
    };
}

/// <summary>How the sex is asked for, in words.</summary>
public static class SexNames
{
    public const string Help = "성별은 수컷, 암컷, 랜덤 중 하나로 적어 주세요.";

    public static bool Find(string asked, out SexChoice choice)
    {
        switch (asked.Replace(" ", "").ToLowerInvariant())
        {
            case "수컷" or "수" or "male" or "♂": choice = SexChoice.Male; return true;
            case "암컷" or "암" or "female" or "♀": choice = SexChoice.Female; return true;
            case "랜덤" or "무작위" or "random": choice = SexChoice.Random; return true;
            default: choice = SexChoice.Random; return false;
        }
    }

    public static string Said(SexChoice choice) => choice switch
    {
        SexChoice.Male => "수컷 (무성, 성별이 정해진 종, 카드나 만남이 정한 것, 암수 모습이 달라 둘 다 넣은 종은 그대로; 파티도 그대로)",
        SexChoice.Female => "암컷 (무성, 성별이 정해진 종, 카드나 만남이 정한 것, 암수 모습이 달라 둘 다 넣은 종은 그대로; 파티도 그대로)",
        _ => "랜덤 (종의 성비대로; 파티는 그대로)",
    };
}

/// <summary>How the level is asked for, in words.</summary>
public static class LevelNames
{
    public const string Help = "레벨은 최저, 100 중 하나로 적어 주세요.";

    public static bool Find(string asked, out LevelChoice choice)
    {
        switch (asked.Replace(" ", "").ToLowerInvariant())
        {
            case "최저" or "가능한최저" or "lowest" or "min": choice = LevelChoice.Lowest; return true;
            case "100" or "lv100" or "레벨100": choice = LevelChoice.Hundred; return true;
            default: choice = LevelChoice.Lowest; return false;
        }
    }

    public static string Said(LevelChoice choice) => choice == LevelChoice.Hundred
        ? "100 (박스의 포켓몬; 기술은 그대로, 파티는 그대로)"
        : "가능한 최저";
}

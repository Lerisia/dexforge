namespace Dexforge;

/// <summary>How the colour is asked for, in words.</summary>
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

/// <summary>How the size is asked for, in words (Legends: Arceus).</summary>
public static class SizeNames
{
    public const string Help = "크기는 최소, 최대(스칼렛), 우두머리(아르세우스), 랜덤 중 하나로 적어 주세요.";

    public static bool Find(string asked, out SizeChoice choice)
    {
        switch (asked.Replace(" ", "").ToLowerInvariant())
        {
            case "최소" or "가장작게" or "xxxs" or "smallest" or "min": choice = SizeChoice.Smallest; return true;
            case "우두머리" or "우두" or "alpha": choice = SizeChoice.Alpha; return true;
            case "최대" or "가장크게" or "xxxl" or "largest" or "max": choice = SizeChoice.Largest; return true;
            case "랜덤" or "무작위" or "random": choice = SizeChoice.Random; return true;
            default: choice = SizeChoice.Random; return false;
        }
    }

    public static string Said(SizeChoice choice) => choice switch
    {
        SizeChoice.Smallest => "가장 작게 (키 0, 무게 0; 우두머리와 고정 조우는 제외)",
        SizeChoice.Alpha => "우두머리 (우두머리가 있는 종은 전부)",
        SizeChoice.Largest => "가장 크게 (스케일 255, 커다란 증표)",
        _ => "게임이 뽑은 대로",
    };
}

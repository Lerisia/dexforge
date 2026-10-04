using System.Reflection;
using PKHeX.Core;

namespace Dexforge;

/// <summary>One ribbon a player can earn for a Pokémon and so ask for on every Pokémon of the dex that can take it.</summary>
/// <param name="Key">PKHeX's name for the flag (RibbonChampionAlola).</param>
/// <param name="Name">The game's Korean name of the ribbon.</param>
/// <param name="Title">The title it lends from the eighth generation on, in Korean (as players have written it down; not the game's text).</param>
/// <param name="Note">What it takes, in a line.</param>
/// <param name="OnlyOlder">Earned in a game a seventh-generation Pokémon cannot visit: only what came up from the fourth generation takes it.</param>
public sealed record Ribbon(string Key, string Name, string Title, string Note, bool OnlyOlder = false)
{
    public string Image => Key.ToLowerInvariant() + ".png";
}

/// <summary>
/// The ribbons a player can put on a Pokémon by playing (every event-only ribbon left out), as surveyed on the dex in
/// October 2026: tried on each of the 914 non-event Pokémon with PKHeX's legality analysis. Each is put on where PKHeX
/// accepts it and left off where it does not; Pokémon from cards keep what their card gave them.
/// </summary>
public static class Ribbons
{
    public static readonly IReadOnlyList<Ribbon> All =
    [
        new("RibbonChampionAlola", "알로라챔피언리본", "알로라 챔피언", "알로라 포켓몬리그 전당등록"),
        new("RibbonEffort", "노력리본", "한때는 노력했던", "기초 포인트 510 — 붙이면 HP·방어·특수방어에 170씩 채웁니다"),
        new("RibbonBestFriends", "절친리본", "절친", "절친도와 친밀도를 최대로 둡니다"),
        new("RibbonFootprint", "발자국리본", "발자국이 훌륭한", "만난 레벨보다 30 이상 올라야 합니다 — 레벨 100 이면 전부, 최저 레벨이면 되는 포켓몬만"),
        new("RibbonBattleTreeGreat", "그레이트트리리본", "트리 위너", "배틀트리 20연승"),
        new("RibbonBattleTreeMaster", "마스터트리리본", "트리 마스터", "배틀트리 슈퍼 50연승 — 출전 금지 전설·환상은 못 받습니다"),
        new("RibbonBattleRoyale", "로열마스터리본", "로열 마스터", "배틀로열 마스터랭크 — 출전 금지 전설·환상은 못 받습니다"),
        new("RibbonChampionSinnoh", "신오챔피언리본", "신오 챔피언", "DPPt 전당등록", OnlyOlder: true),
        new("RibbonChampionKalos", "칼로스챔피언리본", "칼로스 챔피언", "XY 전당등록", OnlyOlder: true),
        new("RibbonChampionG6Hoenn", "호연챔피언리본", "호연 챔피언", "ORAS 전당등록", OnlyOlder: true),
        new("RibbonBattlerSkillful", "그레이트배틀리본", "베테랑", "XY·ORAS 배틀하우스 20연승", OnlyOlder: true),
        new("RibbonBattlerExpert", "마스터배틀리본", "달인", "XY·ORAS 배틀하우스 슈퍼 50연승", OnlyOlder: true),
        new("RibbonAlert", "뚝심리본", "한때는 뚝심 있던", "HGSS·XY 월요일 리본", OnlyOlder: true),
        new("RibbonShock", "철렁리본", "한때는 겁쟁이였던", "HGSS·XY 화요일 리본", OnlyOlder: true),
        new("RibbonDowncast", "풀죽기리본", "슬픈 일이 있었던", "HGSS·XY 수요일 리본", OnlyOlder: true),
        new("RibbonCareless", "덜렁이리본", "실수하는 날도 있던", "HGSS·XY 목요일 리본", OnlyOlder: true),
        new("RibbonRelax", "상쾌리본", "한때는 산뜻했던", "HGSS·XY 금요일 리본", OnlyOlder: true),
        new("RibbonSnooze", "잠보리본", "잠만 자던", "HGSS·XY 토요일 리본", OnlyOlder: true),
        new("RibbonSmile", "방글방글리본", "잘 웃던", "HGSS·XY 일요일 리본", OnlyOlder: true),
        new("RibbonGorgeous", "고저스리본", "고저스", "리본 가게 10,000원 (DPPt·XY·ORAS)", OnlyOlder: true),
        new("RibbonRoyal", "로열리본", "로열", "리본 가게 100,000원", OnlyOlder: true),
        new("RibbonGorgeousRoyal", "고저스로열리본", "고저스&로열", "리본 가게 999,999원", OnlyOlder: true),
        new("RibbonLegend", "레전드리본", "굉장한 기록을 보유한", "HGSS 은빛산의 레드를 이기고", OnlyOlder: true),
    ];

    private static readonly Dictionary<string, Ribbon> byKey = All.ToDictionary(r => r.Key);
    private static readonly Dictionary<string, PropertyInfo> flags = typeof(PK7).GetProperties().Where(p => p.Name.StartsWith("Ribbon") && p.PropertyType == typeof(bool)).ToDictionary(p => p.Name);

    /// <summary>The ribbon a word names: its key, or its Korean name with or without '리본'.</summary>
    public static Ribbon? Find(string word)
    {
        var w = word.Trim().Replace(" ", "");
        if (byKey.TryGetValue(w, out var r)) return r;
        return All.FirstOrDefault(x => x.Name == w || x.Name == w + "리본" || x.Key.Equals(w, StringComparison.OrdinalIgnoreCase) || x.Key.Equals("Ribbon" + w, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Puts the ribbons asked for on a Pokémon, each one only if PKHeX still finds the Pokémon legal with it; a Pokémon from a
    /// card is left as its card made it. Returns how many were put on.
    /// </summary>
    public static int Put(PK7 pk, IEnumerable<string> keys, LegalityAnalysis? known = null)
    {
        if (!keys.Any()) return 0;
        var la = known ?? new LegalityAnalysis(pk);
        if (la.EncounterMatch is MysteryGift || pk.FatefulEncounter) return 0;
        int put = 0;
        foreach (var key in keys)
        {
            if (!flags.TryGetValue(key, out var flag) || (bool)flag.GetValue(pk)!) continue;
            var trial = (PK7)pk.Clone();
            flag.SetValue(trial, true);
            Prepare(trial, key);
            trial.RefreshChecksum();
            if (!new LegalityAnalysis(trial).Valid) continue;
            flag.SetValue(pk, true);
            Prepare(pk, key);
            pk.RefreshChecksum();
            put++;
        }
        return put;
    }

    /// <summary>What a ribbon needs besides its flag.</summary>
    private static void Prepare(PK7 pk, string key)
    {
        switch (key)
        {
            case "RibbonEffort":
                // 510 effort points, spread where they change the least about how it fights: HP, Defense, Sp. Defense
                if (pk.EVTotal < 510) { pk.EV_HP = 170; pk.EV_DEF = 170; pk.EV_SPD = 170; pk.EV_ATK = pk.EV_SPA = pk.EV_SPE = 0; }
                break;
            case "RibbonBestFriends":
                pk.CurrentFriendship = 255;
                pk.OriginalTrainerAffection = 255;
                break;
        }
    }
}

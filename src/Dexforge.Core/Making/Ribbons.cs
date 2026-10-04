using System.Reflection;
using PKHeX.Core;

namespace Dexforge;

/// <summary>One ribbon a player can earn for a Pokémon and so ask for on every Pokémon of the dex that can take it.</summary>
/// <param name="Key">PKHeX's name for the flag (RibbonChampionAlola).</param>
/// <param name="Name">The game's Korean name of the ribbon.</param>
/// <param name="Title">The title it lends from the eighth generation on, in Korean (as players have written it down; not the game's text).</param>
/// <param name="Note">What it takes, in a line.</param>
/// <param name="OnlyOlder">Earned in a game a seventh-generation Pokémon cannot visit: only what came up from the fourth generation takes it.</param>
/// <param name="Allowed">Which Pokémon may earn it at all, where the game bars some species; none means every one.</param>
public sealed record Ribbon(string Key, string Name, string Title, string Note, bool OnlyOlder = false, Func<PKM, bool>? Allowed = null)
{
    public string Image => Key.ToLowerInvariant() + ".png";
}

/// <summary>
/// The ribbons a player can put on a Pokémon by playing (every event-only ribbon left out), as surveyed on each dex in
/// October 2026: tried on each non-event Pokémon with PKHeX's legality analysis. Each is put on where PKHeX accepts it and
/// left off where it does not; Pokémon from cards keep what their card gave them.
/// </summary>
public static class Ribbons
{
    /// <summary>The Ultra Sun dex: the seven any Pokémon of it may earn, and sixteen from older games that only what came up from the fourth generation can have.</summary>
    public static readonly IReadOnlyList<Ribbon> All =
    [
        new("RibbonChampionAlola", "알로라챔피언리본", "알로라 챔피언", "알로라 포켓몬리그 전당등록"),
        new("RibbonEffort", "노력리본", "한때는 노력했던", "기초 포인트 510 — 종마다 탱커·저속·고속 어태커로 252/252/6 을 채웁니다"),
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

    /// <summary>
    /// The Sword dex, all of it born in the eighth generation: the five a player earns in Sword itself. The Battle Tower bars
    /// nothing; Ranked Battles bar mythical Pokémon, of which the dex has Meltan and Melmetal outside cards. Marks go only on
    /// what was caught in the wild, which the dex's eggs and gifts were not.
    /// </summary>
    public static readonly IReadOnlyList<Ribbon> Sword =
    [
        new("RibbonChampionGalar", "가라르챔피언리본", "가라르 챔피언", "가라르 포켓몬리그 전당등록"),
        new("RibbonTowerMaster", "마스터타워리본", "타워 마스터", "배틀타워 마스터볼급에서 단델을 이기고 — 출전 제한이 없어 전부"),
        new("RibbonMasterRank", "마스터랭크리본", "랭크 마스터", "랭크배틀 마스터볼급 승리 — 환상(멜탄·멜메탈)은 못 받습니다", Allowed: pk => pk.Species is not (808 or 809)),
        new("RibbonEffort", "노력리본", "한때는 노력했던", "기초 포인트 510 — 종마다 탱커·저속·고속 어태커로 252/252/6 을 채웁니다"),
        new("RibbonBestFriends", "절친리본", "절친", "친밀도를 최대로 둡니다"),
    ];

    /// <summary>The list for a Pokémon, by the game it is from.</summary>
    public static IReadOnlyList<Ribbon> For(PKM pk) => pk is PK8 ? Sword : All;

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> flagsOf = new();

    private static Dictionary<string, PropertyInfo> Flags(Type t)
    {
        lock (flagsOf)
        {
            if (!flagsOf.TryGetValue(t, out var f))
                flagsOf[t] = f = t.GetProperties().Where(p => p.Name.StartsWith("Ribbon") && p.PropertyType == typeof(bool)).ToDictionary(p => p.Name);
            return f;
        }
    }

    /// <summary>The ribbon a word names among a list (the Ultra Sun one unless told): its key, or its Korean name with or without '리본'.</summary>
    public static Ribbon? Find(string word, IReadOnlyList<Ribbon>? among = null)
    {
        var w = word.Trim().Replace(" ", "");
        return (among ?? All).FirstOrDefault(x => x.Key == w || x.Name == w || x.Name == w + "리본" || x.Key.Equals(w, StringComparison.OrdinalIgnoreCase) || x.Key.Equals("Ribbon" + w, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Puts the ribbons asked for (by key or name, from the list of the Pokémon's game) on a Pokémon, each one only if PKHeX
    /// still finds the Pokémon legal with it; a Pokémon from a card is left as its card made it. Returns how many were put on.
    /// </summary>
    public static int Put(PKM pk, IEnumerable<string> keys, LegalityAnalysis? known = null)
    {
        var among = For(pk);
        var wanted = keys.Select(k => Find(k, among)).OfType<Ribbon>().ToList();
        if (wanted.Count == 0) return 0;
        var la = known ?? new LegalityAnalysis(pk);
        if (la.EncounterMatch is MysteryGift || pk.FatefulEncounter) return 0;
        var flags = Flags(pk.GetType());
        int put = 0;
        foreach (var r in wanted)
        {
            if (!flags.TryGetValue(r.Key, out var flag) || (bool)flag.GetValue(pk)! || r.Allowed?.Invoke(pk) == false) continue;
            var trial = pk.Clone();
            flag.SetValue(trial, true);
            Prepare(trial, r.Key);
            trial.RefreshChecksum();
            if (!new LegalityAnalysis(trial).Valid) continue;
            flag.SetValue(pk, true);
            Prepare(pk, r.Key);
            pk.RefreshChecksum();
            put++;
        }
        return put;
    }

    /// <summary>What a ribbon needs besides its flag.</summary>
    private static void Prepare(PKM pk, string key)
    {
        switch (key)
        {
            case "RibbonEffort":
                // 510 effort points, spent the way the species is trained (see Effort)
                if (pk.EVTotal < 510) Effort.For(pk).Apply(pk);
                break;
            case "RibbonBestFriends":
                pk.CurrentFriendship = 255;
                if (pk is IAffection a) a.OriginalTrainerAffection = 255;
                break;
        }
    }
}

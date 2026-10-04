using PKHeX.Core;

namespace Dexforge;

/// <summary>The balls by the names the game gives them, and by the names people call them.</summary>
public static class BallNames
{
    public const string Matched = "볼맞춤";
    private static readonly Dictionary<string, string> Also = new()
    {
        ["몬볼"] = "몬스터볼", ["러브볼"] = "러브러브볼", ["타이마볼"] = "타이머볼", ["콤페볼"] = "컴퍼티션볼", ["컴페볼"] = "컴퍼티션볼",
        ["비스트볼"] = "울트라볼", ["울트라비스트볼"] = "울트라볼", ["체리시볼"] = "프레셔스볼",
    };

    /// <summary>Which ball is meant; none for the balls picked for each Pokemon.</summary>
    public static bool Find(string asked, out int? ball, out string why)
    {
        ball = null; why = "";
        var name = asked.Replace(" ", "");
        if (name == Matched) return true;
        if (Also.TryGetValue(name, out var proper)) name = proper;
        if (!name.EndsWith('볼')) name += "볼";
        var list = GameInfo.GetStrings("ko").balllist;
        int index = Array.IndexOf(list, name);
        if (index <= 0) { why = $"'{asked}' 라는 볼은 없습니다. 고를 수 있는 볼: {string.Join(", ", list.Skip(1).Where(b => b.Length != 0 && b != "프레셔스볼"))}"; return false; }
        if (index == (int)Ball.Cherish) { why = "프레셔스볼은 배포 포켓몬만 들어가는 볼이라 고를 수 없습니다."; return false; }
        ball = index;
        return true;
    }
}

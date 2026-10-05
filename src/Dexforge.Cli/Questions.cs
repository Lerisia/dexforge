namespace Dexforge.Cli;

/// <summary>What is asked one at a time, when nothing is given on the command line. Left empty, an answer keeps what is in brackets.</summary>
internal sealed class Questions(TextReader input, TextWriter output)
{
    private string Ask(string what)
    {
        output.Write(what + ": ");
        return (input.ReadLine() ?? "").Trim();
    }

    /// <summary>Asks until the answer is empty or <paramref name="take"/> takes it; otherwise says why not and asks again.</summary>
    private void Until(string question, Func<string, string?> take)
    {
        while (true)
        {
            var s = Ask(question);
            if (s.Length == 0 || take(s) is not { } refusal) return;
            output.WriteLine("  " + refusal);
        }
    }

    /// <summary>A name of at most <paramref name="room"/> letters: set, or why not.</summary>
    private static string? Name(string s, int room, Action<string> set)
    {
        if (s.Length > room) return $"{room}글자까지 적어 주세요.";
        set(s);
        return null;
    }

    public void Fill(Order o)
    {
        output.WriteLine("울트라썬 전국도감 세이브를 만듭니다. (한국 본체, 한국어, 여자 주인공)");
        output.WriteLine("빈 칸으로 두고 Enter 를 누르면 괄호 안의 값을 씁니다.");
        output.WriteLine();

        Until($"어버이 이름, 6글자까지 ({o.Name})", s => Name(s, 6, v => o.Name = v));
        Until($"영어·프랑스어·독일어·이탈리아어·스페인어 게임의 트레이너 이름, {ForeignNames.EnglishRoom}글자까지 ({o.English})",
              s => Name(s, ForeignNames.EnglishRoom, v => o.English = v));
        Until($"일본어 게임의 트레이너 이름, {ForeignNames.JapaneseRoom}글자까지 ({o.Japanese})", s => Name(s, ForeignNames.JapaneseRoom, v => o.Japanese = v));
        Until($"중국어 게임의 트레이너 이름, {ForeignNames.ChineseRoom}글자까지 ({o.Chinese})", s => Name(s, ForeignNames.ChineseRoom, v => o.Chinese = v));

        Until("SID, 네 자리 0000~4294 (무작위)", s =>
        {
            if (s.Length > 4 || !uint.TryParse(s, out var v) || Ids7.Refused(null, v) is not null) return "0000에서 4294 사이의 수를 적어 주세요.";
            o.Sid = v;
            return null;
        });
        Until("TID, 여섯 자리 000000~999999 (무작위)", s =>
        {
            if (s.Length > 6 || !uint.TryParse(s, out var v)) return "000000에서 999999 사이의 수를 적어 주세요.";
            if (Ids7.Refused(v, o.Sid) is { } why) return why;
            o.Tid = v;
            return null;
        });

        Until($"볼: 이름을 적으면 그 볼로 통일하고 안 되는 포켓몬은 몬스터볼, '{BallNames.Matched}'이라고 적으면 포켓몬마다 골라 둔 볼 (몬스터볼)", s =>
        {
            if (!BallNames.Find(s, out _, out var why)) return why;
            o.Ball = s;
            return null;
        });
        Until("개체값: 랜덤 / 5V (알에서 나온 포켓몬은 5V) (랜덤)", s =>
        {
            if (!IvNames.Find(s, out var v)) return IvNames.Help;
            o.Ivs = v;
            return null;
        });
        Until("성별: 수컷 / 암컷 / 랜덤 (랜덤)", s =>
        {
            if (!SexNames.Find(s, out var v)) return SexNames.Help;
            o.Sex = v;
            return null;
        });
        Until("레벨: 최저 / 100 (최저)", s =>
        {
            if (!LevelNames.Find(s, out var v)) return LevelNames.Help;
            o.Level = v;
            return null;
        });

        Until($"포켓몬을 얻은 기간의 첫날 ({o.From:yyyy-MM-dd})", s =>
        {
            if (!DateOnly.TryParse(s, out var v)) return "2018-01-01 같은 모양으로 적어 주세요.";
            o.From = v;
            return null;
        });
        // A first day after the last one offered moves the last one to a year on.
        if (o.To < o.From) o.To = o.From.AddYears(1).AddDays(-1);
        Until($"포켓몬을 얻은 기간의 마지막 날 ({o.To:yyyy-MM-dd})", s =>
        {
            if (!DateOnly.TryParse(s, out var v)) return "2018-12-31 같은 모양으로 적어 주세요.";
            o.To = v;
            return null;
        });
        output.WriteLine();
    }
}

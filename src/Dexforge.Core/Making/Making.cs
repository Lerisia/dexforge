using System.Reflection;
using System.Text;
using PKHeX.Core;

namespace Dexforge;

/// <summary>What came of one order: the folder the save and its record were written to, or why nothing was written.</summary>
/// <param name="Code">0 written; 1 what was made did not pass; 2 what was asked for cannot be made.</param>
/// <param name="Lines">The record, as far as it got.</param>
/// <param name="Refused">Why nothing was written, said to whoever asked.</param>
/// <param name="Me">Whose save it is, once that is drawn.</param>
/// <param name="Checked">What the check of the save counted, once it is made.</param>
public sealed record Made(int Code, string? Folder, IReadOnlyList<string> Lines, IReadOnlyList<string> Refused, Trainer? Me = null, Check.Result? Checked = null)
{
    public string? Save => Folder is null ? null : Path.Combine(Folder, Making.SaveName);
    public string? Record => Folder is null ? null : Path.Combine(Folder, Making.RecordName);
}

/// <summary>One save made to order: drawn, checked, and written with its record. Whoever asks - the command line or the window - asks here.</summary>
public static class Making
{
    public const string SaveName = "main";
    public const string RecordName = "만든기록.txt";

    /// <summary>The save everything is drawn after, which the program carries inside.</summary>
    public static byte[] Template()
    {
        using var stream = typeof(Making).Assembly.GetManifestResourceStream("template.dex") ?? throw new InvalidOperationException("no template inside");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>The folder a save is written to when none is named: under <paramref name="under"/>, called after the trainer.</summary>
    public static string FolderFor(string under, Trainer me) => Path.Combine(under, $"Dexforge-{me.Name}-{me.Shown:000000}");

    /// <param name="outDir">Where to write; none, and the folder is named after the trainer, under <paramref name="under"/>.</param>
    /// <param name="step">Told how many of the boxes are done, and of how many.</param>
    public static Made Run(Options opt, string? outDir, string under, Action<int, int>? step = null)
    {
        var template = Template();
        var balls = GameInfo.GetStrings("ko").balllist;
        Generator gen;
        try { gen = new Generator(template, opt) { Step = step }; }
        catch (ArgumentException ex) { return new Made(2, null, [], [Words.Korean(ex.Message)]); }
        byte[] made;
        try { made = gen.Run(); }
        catch (ArgumentException ex) { return new Made(2, null, [], [Words.Korean(ex.Message)]); }

        var lines = new List<string>
        {
            "울트라썬 전국도감 세이브 — 만든 기록",
            "",
            $"어버이        {gen.Me.Name} (여)",
            $"외국어 게임   영어·프랑스어·독일어·이탈리아어·스페인어 {opt.English}, 일본어 {opt.Japanese}, 중국어 {opt.Chinese}",
            $"SID / TID     {gen.Me.Sid7:0000} / {gen.Me.Shown:000000}   (3DSRNGTool 에 넣는 16비트 값: TID {gen.Me.Tid:00000}, SID {gen.Me.Sid:00000})",
            $"볼            {(opt.Ball is { } b ? $"{balls[b]}로 통일 (안 되는 포켓몬은 몬스터볼, 배포는 카드가 정한 볼)" : $"{BallNames.Matched} (포켓몬마다 골라 둔 볼)")}",
            $"색            {ColourNames.Said(opt.Shiny)}",
            $"개체값        {IvNames.Said(opt.Ivs)}",
            $"성별          {SexNames.Said(opt.Sex)}",
            $"레벨          {LevelNames.Said(opt.Level)}",
            $"얻은 기간     {opt.From:yyyy-MM-dd} ~ {opt.To:yyyy-MM-dd}",
            $"모험 시작     {gen.Began:yyyy-MM-dd}",
            $"상대 버전     울트라문의 {gen.Moon.Name} {gen.Moon.Tid:00000}/{gen.Moon.Sid:00000} (화면 {gen.Moon.Shown:000000})",
            $"시드          {opt.Seed}",
            $"리본          {(opt.Ribbons.Count == 0 ? "없음" : string.Join(", ", opt.Ribbons.Select(k => Ribbons.Find(k)?.Name ?? k)))}",
            "",
        };
        if (gen.Problems.Count != 0)
        {
            var refused = new List<string> { "만들지 못한 개체가 있어 세이브를 쓰지 않습니다." };
            foreach (var p in gen.Problems.Take(20)) refused.Add("  " + p);
            if (gen.Problems.Any(p => p.Contains("OT") || p.Contains("Wordfilter") || p.Contains("name") || p.Contains("character")))
                refused.Add("어버이 이름이나 외국어 게임의 이름에, 그 게임에서 쓸 수 없는 글자가 있을 수 있습니다. 예를 들어 옛 일본어판은 가나만, 영어판은 알파벳만 씁니다.");
            return new Made(1, null, lines, refused, gen.Me);
        }
        var result = Check.Run(template, made, gen.Me, gen.Was, gen.Began, opt.Ball, opt.Ivs, opt.Shiny, opt.Level, opt.Sex, opt);
        lines.Add($"포켓몬        {result.Count}마리 (박스 {result.Count - 3}, 파티 3), {result.Species}종");
        lines.Add($"합법          {result.Legal} / {result.Count}");
        lines.Add($"이로치        {result.Shiny}마리");
        if (opt.Ball is not null)
        {
            lines.Add($"볼            {string.Join(", ", gen.Balls.OrderByDescending(x => x.Value).Select(x => $"{balls[x.Key]} {x.Value}"))}");
            if (gen.AbilityGivenUp != 0) lines.Add($"              그 볼에 넣으려고 숨겨진 특성을 일반 특성으로 바꾼 것 {gen.AbilityGivenUp}마리");
        }
        if (opt.Ribbons.Count != 0)
            lines.Add($"리본 붙은 수  {string.Join(", ", opt.Ribbons.Select(k => $"{Ribbons.Find(k)?.Name ?? k} {gen.RibbonsPut.GetValueOrDefault(k)}"))} (배포 포켓몬과 PKHeX 가 거절한 것은 제외)");
        lines.Add($"새로 뽑은 것  {result.Redrawn}마리");
        lines.Add($"카드가 값을 정해 둔 것 (누가 받아도 같음): {(result.Fixed.Count == 0 ? "없음" : string.Join(", ", result.Fixed))}");
        lines.Add("");
        lines.Add("이 세대에서 잡거나 받은 개체 — 게임의 난수에서 뽑은 자리 (3DSRNGTool 로 같은 시드와 번호를 넣으면 같은 개체가 나옵니다)");
        foreach (var n in gen.Sessions) lines.Add("  " + n);
        lines.Add("");
        lines.Add("알에서 나온 개체 — 알이 만들어질 때의 난수 상태와 부모 (도구: 0 없음, 1 변함없는돌, 2 빨간실; 부모 중 하나는 외국의 메타몽, 빛나는부적 있음)");
        foreach (var n in gen.Eggs) lines.Add("  " + n);
        lines.Add("");
        lines.Add("구세대에서 온 개체");
        foreach (var n in gen.Notes) lines.Add("  " + n);
        lines.Add("");
        lines.Add("배포를 받은 날");
        foreach (var d in gen.Days.OrderBy(d => d.Value)) lines.Add($"  {d.Value:yyyy-MM-dd}  {d.Key}");

        if (result.Faults.Count != 0 || result.Legal != result.Count)
        {
            var refused = new List<string> { "만든 세이브가 검사를 통과하지 못해 쓰지 않습니다." };
            foreach (var f in result.Faults.Take(20)) refused.Add("  " + f);
            return new Made(1, null, lines, refused, gen.Me, result);
        }
        outDir ??= FolderFor(under, gen.Me);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, SaveName), made);
        File.WriteAllLines(Path.Combine(outDir, RecordName), lines, new UTF8Encoding(true));
        return new Made(0, outDir, lines, [], gen.Me, result);
    }
}

using System.Text;
using AlolaDexMaker;
using PKHeX.Core;

namespace AlolaDexMaker.Cli;

/// <summary>
/// For whoever keeps the template: a save put right in place, its hatched and met Pokemon drawn again from the game's random numbers
/// and nothing else about them or the save changed.   AlolaDexMaker.Cli --refresh &lt;save&gt; &lt;folder&gt; [--seed n] [--only 590,591]
/// </summary>
internal static class Mend
{
    public static int Run(string path, string outDir, int seed, IReadOnlySet<ushort>? only, TextWriter output, TextWriter error)
    {
        var bytes = File.ReadAllBytes(path);
        if (!SaveUtil.TryGetSaveFile(bytes.ToArray(), out var read) || read is not SAV7 s7) { error.WriteLine("7세대 세이브가 아닙니다."); return 2; }
        var opt = new Options(s7.OT, s7.TrainerTID7, s7.TrainerSID7, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), seed, null, IvChoice.Random, true);
        var gen = new Generator(bytes, opt);
        var made = gen.Refresh(only);
        if (gen.Problems.Count != 0)
        {
            error.WriteLine("다시 뽑지 못한 개체가 있어 쓰지 않습니다.");
            foreach (var p in gen.Problems.Take(30)) error.WriteLine("  " + p);
            return 1;
        }
        var lines = new List<string>
        {
            "울트라썬 전국도감 세이브 — 난수 기록",
            "",
            $"어버이        {gen.Me.Name}  [{gen.Me.Sid7:0000}]{gen.Me.Shown:000000}  (16비트 {gen.Me.Tid:00000}/{gen.Me.Sid:00000})",
            $"다시 뽑은 것  {gen.Done[Kind.Plain]}마리 (알과 이 세대에서 잡거나 받은 것). 날짜·볼·어버이는 그대로.",
            $"그대로 둔 것  배포 {gen.Left[Kind.Card]}마리, 구세대 출신 {gen.Left[Kind.Older]}마리",
            $"시드          {seed}",
            "",
            "이 세대에서 잡거나 받은 개체 — 게임의 난수에서 뽑은 자리 (3DSRNGTool 로 같은 시드와 번호를 넣으면 같은 개체가 나옵니다)",
        };
        foreach (var n in gen.Sessions) lines.Add("  " + n);
        lines.Add("");
        lines.Add("알에서 나온 개체 — 알이 만들어질 때의 난수 상태와 부모 (도구: 0 없음, 1 변함없는돌, 2 빨간실; 부모 중 하나는 외국의 메타몽, 빛나는부적 있음)");
        foreach (var n in gen.Eggs) lines.Add("  " + n);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "main"), made);
        File.WriteAllLines(Path.Combine(outDir, "만든기록.txt"), lines, new UTF8Encoding(true));
        foreach (var l in lines.Take(6)) output.WriteLine(l);
        output.WriteLine($"썼습니다: {Path.Combine(outDir, "main")}");
        return 0;
    }
}

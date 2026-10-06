using System.Text;
using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>One Legends: Z-A save made to order: every species and form the plan holds, drawn, checked, and written as a JKSV backup folder with its record.</summary>
public static class Making9a
{
    public static readonly DateOnly Released = new(2025, 10, 16);

    /// <summary>The period offered first: 2026 (Mega Dimension's year), up to today while the year is still running (owner, 2026-10-05).</summary>
    public static (DateOnly From, DateOnly To) DefaultPeriod()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var end = new DateOnly(2026, 12, 31);
        return (new DateOnly(2026, 1, 1), today < end ? (today < new DateOnly(2026, 1, 1) ? end : today) : end);
    }

    /// <summary>The files JKSV wants in a backup folder besides the save itself, carried inside with the template.</summary>
    private static readonly (string Resource, string File)[] Sidecars = [("za.backup", "backup"), ("za.nx_save_meta", ".nx_save_meta.bin")];

    public static string FolderFor(string under, Trainer me) => Path.Combine(under, $"Dexforge-ZA-{me.Name}-{me.Shown:000000}");

    /// <param name="outDir">Where to write; none, and the folder is named after the trainer, under <paramref name="under"/>.</param>
    /// <param name="step">Told how many are done, and of how many.</param>
    public static Made Run(Options9 opt, string? outDir, string under, Action<int, int>? step = null)
    {
        var ko = Plan9a.Ko;
        int room = Legal.GetMaxLengthOT(8, LanguageID.Korean);   // six Korean letters, as in the eighth generation
        if (opt.Name.Length < 1 || opt.Name.Length > room) return new Made(2, null, [], [$"어버이 이름은 1글자에서 {room}글자 사이여야 합니다."]);
        if (opt.Sid is > 4294) return new Made(2, null, [], ["SID 는 0000 에서 4294 사이여야 합니다."]);
        if (opt.Tid is > 999_999) return new Made(2, null, [], ["TID 는 000000 에서 999999 사이여야 합니다."]);
        if (opt.Sid == 4294 && opt.Tid > 967_295) return new Made(2, null, [], ["SID 4294 에서는 TID 가 967295 까지입니다."]);
        if (opt.From < Released) return new Made(2, null, [], [$"첫날은 {Released:yyyy-MM-dd} (Z-A 발매일) 이후여야 합니다."]);
        if (opt.To < opt.From) return new Made(2, null, [], ["마지막 날이 첫날보다 앞섭니다."]);
        if (opt.To.Year > 2099) return new Made(2, null, [], ["마지막 날은 2099년까지입니다."]);

        var random = new Random(opt.Seed);
        var sav = new SAV9ZA(Embedded.Bytes("za.main"));
        uint sid7 = opt.Sid ?? (uint)random.Next(0, 4295);
        uint tid7 = opt.Tid ?? (uint)random.Next(0, sid7 == 4294 ? 967_296 : 1_000_000);
        uint id32 = sid7 * 1_000_000 + tid7;
        var me = new Trainer(opt.Name, sav.Gender, (ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16), sav.Language, GameVersion.ZA, 0, 0, 0);
        var trainer = new SimpleTrainerInfo(GameVersion.ZA) { OT = opt.Name, Gender = sav.Gender, Language = sav.Language, ID32 = id32 };
        var maker = new Maker9a(trainer, random, opt);

        var entries = Plan9a.All;
        var made = new List<Made9a>(); var failed = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var m = maker.Make(e);
            if (m.Legal) made.Add(m);
            else failed.Add($"{Plan9a.Label(e.Species, e.Form)}: {(m.Report.Contains("Invalid") ? string.Join(" | ", m.Report.Split('\n').Where(l => l.Contains("Invalid"))) : m.Report)}");
            step?.Invoke(i + 1, entries.Count);
        }
        if (made.Count > sav.SlotCount) failed.Add($"박스가 모자랍니다: {made.Count} > {sav.SlotCount}");

        var lines = new List<string>
        {
            "Z-A 도감 세이브 — 만든 기록",
            "",
            $"어버이        {me.Name} ({(me.Gender == 0 ? "남" : "여")})",
            $"SID / TID     {me.Sid7:0000} / {me.Shown:000000}",
            $"다른 Z-A      같은 이름, SID / TID {maker.Other.ID32 / 1_000_000:0000} / {maker.Other.ID32 % 1_000_000:000000} — 통신교환 진화는 거기에 줬다 돌려받음",
            $"볼            {(opt.Ball is { } ob ? $"{ko.balllist[ob]}로 통일 (안 되는 포켓몬은 몬스터볼)" : "포켓몬마다 골라 둔 볼")}",
            $"색            {(opt.Shiny ? "이로치 (이로치가 막힌 고정·선물·교환은 일반)" : "일반")}",
            $"크기          {opt.Size switch { SizeChoice.Smallest => "가장 작게 (크기 0)", SizeChoice.Largest => "가장 크게 (크기 255)", SizeChoice.Alpha => "우두머리 (이차원에 있는 종 전부)", _ => "게임이 뽑은 대로" }}",
            $"레벨          {(opt.Level == LevelChoice.Hundred ? "100" : "잡은 레벨 그대로 (진화에 필요한 만큼만 올림)")}",
            $"성별          {opt.Sex switch { SexChoice.Male => "수컷 (가능한 종)", SexChoice.Female => "암컷 (가능한 종)", _ => "게임이 뽑은 대로" }}",
            $"잡은 기간     {opt.From:yyyy-MM-dd} ~ {opt.To:yyyy-MM-dd}",
            $"시드          {opt.Seed}",
            "",
        };
        if (failed.Count != 0)
        {
            var refused = new List<string> { $"만들지 못한 개체가 {failed.Count}마리 있어 세이브를 쓰지 않습니다." };
            foreach (var f in failed.Take(30)) refused.Add("  " + f);
            return new Made(1, null, lines, refused, me);
        }
        sav.OT = me.Name;
        sav.ID32 = id32;
        for (int i = 0; i < sav.SlotCount; i++) sav.SetBoxSlotAtIndex(sav.BlankPKM, i, EntityImportSettings.None);
        for (int i = 0; i < made.Count; i++) sav.SetBoxSlotAtIndex(made[i].Pk, i, EntityImportSettings.None);
        foreach (var m in made) sav.Zukan.SetDex(m.Pk);
        var data = sav.Write().ToArray();

        int seen = 0, caught = 0;
        for (ushort s = 1; s <= sav.MaxSpeciesID; s++) { if (sav.GetSeen(s)) seen++; if (sav.GetCaught(s)) caught++; }
        lines.Add($"포켓몬        {made.Count}마리 (박스), {made.Select(m => m.Pk.Species).Distinct().Count()}종, 폼까지 {made.Select(m => (m.Pk.Species, m.Pk.Form)).Distinct().Count()}");
        lines.Add($"합법          {made.Count} / {made.Count}");
        lines.Add($"이로치        {made.Count(m => m.Pk.IsShiny)}마리");
        lines.Add($"우두머리      {made.Count(m => m.Pk.IsAlpha)}마리");
        lines.Add($"출처          이차원 {made.Count(m => m.Entry.Source == Source9a.Hyperspace)}, 야생 {made.Count(m => m.Entry.Source == Source9a.Wild)}, 고정 {made.Count(m => m.Entry.Source == Source9a.Static)}, 선물 {made.Count(m => m.Entry.Source == Source9a.Gift)}, 교환 {made.Count(m => m.Entry.Source == Source9a.Trade)}; 진화시킨 것 {made.Count(m => m.Entry.Evolves)}, 폼을 바꾼 것 {made.Count(m => m.Entry.ChangesForm)}");
        lines.Add($"도감          본 것 {seen}, 잡은 것 {caught}");
        lines.Add($"못 넣는 것    {Plan9a.LeftOut.Count}가지 (게임 데이터에는 있지만 얻을 길이 없는 폼): {string.Join(", ", Plan9a.LeftOut.Select(x => Plan9a.Label(x.Species, x.Form)))}");
        lines.Add("");
        lines.Add("마리마다: 이름 · 폼 · 색 · 레벨 · 성격 · 개체값 · 크기 · 볼 · 출처 · 시드");
        foreach (var m in made)
        {
            var pk = m.Pk;
            lines.Add($"  {Plan9a.Label(pk.Species, pk.Form)}{(m.Entry.Gender is { } g9 ? (g9 == 0 ? " ♂" : " ♀") : "")}{(pk.IsShiny ? " ★" : "")}{(pk.IsAlpha ? " 우두머리" : "")} Lv{pk.CurrentLevel} {ko.natures[(int)pk.Nature]} {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE} 크기{pk.Scale} {ko.balllist[pk.Ball]} — {m.How}{(m.Entry.Evolves || m.Entry.ChangesForm ? $" → {m.Entry.Note}" : "")}{(m.Seed is { } s ? $" · 시드 {s:X16}" : "")}");
        }

        outDir ??= FolderFor(under, me);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "main"), data);
        foreach (var (res, file) in Sidecars) File.WriteAllBytes(Path.Combine(outDir, file), Embedded.Bytes(res));
        File.WriteAllLines(Path.Combine(outDir, Making.RecordName), lines, new UTF8Encoding(true));
        return new Made(0, outDir, lines, [], me);
    }
}

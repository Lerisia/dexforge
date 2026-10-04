using System.Reflection;
using System.Text;
using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>One Legends: Arceus save made to order: every species and form the game can hold, drawn, checked, and written as a JKSV backup folder with its record.</summary>
public static class Making8a
{
    public const string RecordName = "만든기록.txt";
    public static readonly DateOnly Released = new(2022, 1, 28);

    /// <summary>The files JKSV wants in a backup folder besides the save itself, carried inside with the template.</summary>
    private static readonly (string Resource, string File)[] Sidecars = [("arceus.backup", "backup"), ("arceus.main2", "main2"), ("arceus.nx_save_meta", ".nx_save_meta.bin")];

    public static string FolderFor(string under, Trainer me) => Path.Combine(under, $"Dexforge-Arceus-{me.Name}-{me.Shown:000000}");

    /// <summary>The Hisuian balls a catch can be asked to go in, by PKHeX's number.</summary>
    public static readonly Ball[] Balls = [Ball.LAPoke, Ball.LAGreat, Ball.LAUltra, Ball.LAFeather, Ball.LAWing, Ball.LAJet, Ball.LAHeavy, Ball.LALeaden, Ball.LAGigaton];

    /// <param name="outDir">Where to write; none, and the folder is named after the trainer, under <paramref name="under"/>.</param>
    /// <param name="step">Told how many are done, and of how many.</param>
    public static Made Run(Options8a opt, string? outDir, string under, Action<int, int>? step = null)
    {
        var ko = Plan8a.Ko;
        int room = Legal.GetMaxLengthOT(8, LanguageID.Korean);
        if (opt.Name.Length < 1 || opt.Name.Length > room) return new Made(2, null, [], [$"어버이 이름은 1글자에서 {room}글자 사이여야 합니다."]);
        if (opt.Sid is > 4294) return new Made(2, null, [], ["SID 는 0000 에서 4294 사이여야 합니다."]);
        if (opt.Tid is > 999_999) return new Made(2, null, [], ["TID 는 000000 에서 999999 사이여야 합니다."]);
        if (opt.Sid == 4294 && opt.Tid > 967_295) return new Made(2, null, [], ["SID 4294 에서는 TID 가 967295 까지입니다."]);
        if (opt.From < Released) return new Made(2, null, [], [$"첫날은 {Released:yyyy-MM-dd} (LEGENDS 아르세우스 발매일) 이후여야 합니다."]);
        if (opt.To < opt.From) return new Made(2, null, [], ["마지막 날이 첫날보다 앞섭니다."]);
        if (opt.To.Year > 2099) return new Made(2, null, [], ["마지막 날은 2099년까지입니다."]);
        if (!Balls.Contains((Ball)opt.Ball)) return new Made(2, null, [], ["볼은 히스이 지방의 볼이어야 합니다: " + string.Join(", ", Balls.Select(b => ko.balllist[(int)b]))]);
        if (opt.Size == SizeChoice.Largest) return new Made(2, null, [], ["LEGENDS 아르세우스에는 '최대' 크기가 없습니다 (최소·우두머리·랜덤)."]);

        var random = new Random(opt.Seed);
        var sav = new SAV8LA(Embedded.Bytes("arceus.main"));
        uint sid7 = opt.Sid ?? (uint)random.Next(0, 4295);
        uint tid7 = opt.Tid ?? (uint)random.Next(0, sid7 == 4294 ? 967_296 : 1_000_000);
        uint id32 = sid7 * 1_000_000 + tid7;
        var me = new Trainer(opt.Name, sav.Gender, (ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16), sav.Language, GameVersion.PLA, 0, 0, 0);
        var trainer = new SimpleTrainerInfo(GameVersion.PLA) { OT = opt.Name, Gender = sav.Gender, Language = sav.Language, ID32 = id32 };
        // the research first: the save's research standing decides how many shiny rolls each catch gets, so it is settled
        // before anything is drawn
        int researched = CompleteResearch(sav);
        var maker = new Maker8a(sav, trainer, random, opt);

        var entries = Plan8a.All;
        var made = new List<Made8a>(); var failed = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var m = maker.Make(e);
            if (m.Legal) made.Add(m);
            else failed.Add($"{Plan8a.Label(e.Species, e.Form)}: {(m.Report.Contains("Invalid") ? string.Join(" | ", m.Report.Split('\n').Where(l => l.Contains("Invalid"))) : m.Report)}");
            step?.Invoke(i + 1, entries.Count);
        }

        var lines = new List<string>
        {
            "LEGENDS 아르세우스 히스이도감 세이브 — 만든 기록",
            "",
            $"어버이        {me.Name} ({(me.Gender == 0 ? "남" : "여")})",
            $"SID / TID     {me.Sid7:0000} / {me.Shown:000000}",
            $"볼            {ko.balllist[opt.Ball]}로 통일 (조우가 볼을 정한 것은 그 볼)",
            $"색            {(opt.Shiny ? "이로치 (고정 조우는 일반)" : "일반")}",
            $"크기          {opt.Size switch { SizeChoice.Smallest => "가장 작게 (키 0, 무게 0; 고정 조우는 게임이 정한 크기)", SizeChoice.Alpha => "우두머리 (우두머리가 있는 종은 전부)", _ => "게임이 뽑은 대로" }}",
            $"레벨          {(opt.Level == LevelChoice.Hundred ? "100" : "잡은 레벨 그대로 (진화에 필요한 만큼만 올림)")}",
            $"성별          {opt.Sex switch { SexChoice.Male => "수컷 (가능한 종)", SexChoice.Female => "암컷 (가능한 종)", _ => "게임이 뽑은 대로" }}",
            $"잡은 기간     {opt.From:yyyy-MM-dd} ~ {opt.To:yyyy-MM-dd}",
            $"시드          {opt.Seed}",
            "",
        };
        if (failed.Count != 0)
        {
            var refused = new List<string> { "만들지 못한 개체가 있어 세이브를 쓰지 않습니다." };
            foreach (var f in failed.Take(20)) refused.Add("  " + f);
            return new Made(1, null, lines, refused, me);
        }

        // the trainer: renamed, with the adventure begun before the first catch and the party caught on its first day
        sav.OT = me.Name;
        sav.ID32 = id32;
        var began = opt.From.AddDays(-random.Next(7, 22));
        if (began < Released) began = Released;
        sav.AdventureStart.Timestamp = began.ToDateTime(new TimeOnly(random.Next(9, 22), random.Next(60)));
        sav.LastSaved.Timestamp = opt.To.AddDays(random.Next(1, 15)).ToDateTime(new TimeOnly(random.Next(9, 23), random.Next(60)));
        for (int i = 0; i < sav.PartyCount; i++)
        {
            var pp = sav.GetPartySlotAtIndex(i);
            if (pp.CurrentHandler == 0 && pp.HandlingTrainerName.Length == 0)
            {
                pp.OriginalTrainerName = me.Name; pp.ID32 = id32; pp.OriginalTrainerGender = me.Gender;
                pp.MetDate = began;
                pp.RefreshChecksum();
                sav.SetPartySlotAtIndex(pp, i);
            }
        }
        for (int i = 0; i < sav.SlotCount; i++) sav.SetBoxSlotAtIndex(sav.BlankPKM, i, EntityImportSettings.None);
        for (int i = 0; i < made.Count && i < sav.SlotCount; i++) sav.SetBoxSlotAtIndex(made[i].Pk, i, EntityImportSettings.None);

        // the Pokédex: every box entry seen and obtained, and every species researched to level 10
        var dex = sav.PokedexSave;
        foreach (var m in made) dex.SetPokeSeenInWildAndObtained(m.Pk, true);
        foreach (var p in sav.PartyData) if (p.Species != 0) dex.SetPokeSeenInWildAndObtained(p, true);
        researched = CompleteResearch(sav);   // reported again now that the boxes have given the form and catch tasks their counts
        var data = sav.Write().ToArray();

        var shiny = made.Count(m => m.Pk.IsShiny);
        int caught = 0;
        for (ushort s = 1; s <= sav.MaxSpeciesID; s++) if (Plan8a.Table.IsSpeciesInGame(s) && sav.GetCaught(s)) caught++;
        lines.Add($"포켓몬        {made.Count}마리 (목장), {made.Select(m => m.Pk.Species).Distinct().Count()}종, 폼까지 {made.Select(m => (m.Pk.Species, m.Pk.Form)).Distinct().Count()}");
        lines.Add($"합법          {made.Count} / {made.Count}");
        lines.Add($"이로치        {shiny}마리");
        lines.Add($"우두머리      {made.Count(m => m.Pk.IsAlpha)}마리");
        lines.Add($"도감          잡은 것 {caught}종, 연구 레벨 10 이 {researched}종 (나머지는 의뢰·폼 과제가 남는 종)");
        lines.Add("");
        lines.Add("마리마다: 이름 · 폼 · 색 · 레벨 · 성격 · 특성 · 개체값 · 크기 · 볼 · 출처 · 생성기 시드 · 고정 시드");
        foreach (var m in made)
        {
            var pk = m.Pk;
            lines.Add($"  {Plan8a.Label(pk.Species, pk.Form)}{(pk.IsShiny ? " ★" : "")}{(pk.IsAlpha ? " 우두머리" : "")} Lv{pk.CurrentLevel} {ko.natures[(int)pk.Nature]} {ko.abilitylist[pk.Ability]} {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE} 키{pk.HeightScalar} 무게{pk.WeightScalar} {ko.balllist[pk.Ball]} — {m.How}{(m.Generator is { } g ? $" · G {g:X16}" : "")}{(m.FixedSeed is { } f ? $" · F {f:X16}" : "")}");
        }

        outDir ??= FolderFor(under, me);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "main"), data);
        foreach (var (res, file) in Sidecars) File.WriteAllBytes(Path.Combine(outDir, file), Embedded.Bytes(res));
        File.WriteAllLines(Path.Combine(outDir, RecordName), lines, new UTF8Encoding(true));
        return new Made(0, outDir, lines, [], me);
    }

    /// <summary>
    /// Every species' research tasks at their last threshold and reported, which puts the research level at 10 wherever the
    /// tasks allow it. Tasks the game derives from elsewhere (forms obtained, requests, Arceus) are left to what the boxes gave.
    /// </summary>
    public static int CompleteResearch(SAV8LA sav)
    {
        var tasksOf = (PokedexResearchTask8a[][])typeof(PA8).Assembly.GetType("PKHeX.Core.PokedexConstants8a")!.GetField("ResearchTasks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var dex = sav.PokedexSave;
        int complete = 0;
        for (ushort sp = 1; sp <= sav.MaxSpeciesID; sp++)
        {
            if (!Plan8a.Table.IsSpeciesInGame(sp)) continue;
            int index = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, sp);
            if (index == 0 || index > tasksOf.Length) continue;
            foreach (var task in tasksOf[index - 1])
            {
                int max = task.TaskThresholds.Length > 0 ? task.TaskThresholds[^1] : task.Threshold;
                try { dex.SetResearchTaskProgressByForce(sp, task, max); } catch (ArgumentOutOfRangeException) { /* derived elsewhere */ }
            }
            dex.UpdateSpecificReportPoke(sp);
            if (dex.IsComplete(sp)) complete++;
        }
        return complete;
    }
}

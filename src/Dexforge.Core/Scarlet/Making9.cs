using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>One Scarlet save made to order: every species and form the plan holds, drawn, checked, and written as a JKSV backup folder with its record.</summary>
public static class Making9
{
    public static readonly DateOnly Released = new(2022, 11, 18);

    /// <summary>The files JKSV wants in a backup folder besides the save itself, carried inside with the template.</summary>
    private static readonly (string Resource, string File)[] Sidecars = [("scarlet.backup", "backup"), ("scarlet.poke_trade", "poke_trade"), ("scarlet.nx_save_meta", ".nx_save_meta.bin")];

    /// <param name="outDir">Where to write; none, and the folder is named after the trainer, under <paramref name="under"/>.</param>
    /// <param name="step">Told how many are done, and of how many.</param>
    public static Made Run(Options9 opt, string? outDir, string under, Action<int, int>? step = null)
    {
        var ko = Plan9.Ko;
        if ((SwitchMaking.RefusedTrainer(opt.Name, opt.Tid, opt.Sid) ?? SwitchMaking.RefusedPeriod(opt.From, opt.To, Released, "스칼렛")) is { } why) return SwitchMaking.Refused(why);
        if (opt.Size == SizeChoice.Alpha) return SwitchMaking.Refused("스칼렛에는 우두머리가 없습니다 (최소·최대·랜덤).");

        var random = new Random(opt.Seed);
        var sav = new SAV9SV(Embedded.Bytes("scarlet.main"));
        uint id32 = SwitchMaking.Id32(random, opt.Tid, opt.Sid);
        var me = new Trainer(opt.Name, sav.Gender, (ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16), sav.Language, GameVersion.SL, 0, 0, 0);
        var trainer = new SimpleTrainerInfo(GameVersion.SL) { OT = opt.Name, Gender = sav.Gender, Language = sav.Language, ID32 = id32 };
        var maker = new Maker9(trainer, random, opt);

        var entries = Plan9.All;
        var made = new List<Made9>(); var failed = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var m = maker.Make(e);
            if (m.Legal) made.Add(m);
            else failed.Add($"{Plan9.Label(e.Species, e.Form)}: {SwitchMaking.Faults(m.Report)}");
            step?.Invoke(i + 1, entries.Count);
        }

        // the distributions Scarlet received, after the dex: every card, then its final evolutions
        var events = new List<Made9>();
        foreach (var ev in Events9.All)
            foreach (var m in maker.MakeEvent(ev))
            {
                if (m.Legal) events.Add(m);
                else failed.Add($"배포 {ev.Title} → {Plan9.Label(m.Entry.Species, m.Entry.Form)}: {SwitchMaking.Faults(m.Report)}");
            }
        if (made.Count + events.Count > sav.SlotCount) failed.Add($"박스가 모자랍니다: 도감 {made.Count} + 배포 {events.Count} > {sav.SlotCount}");

        List<string> lines =
        [
            "스칼렛 도감 세이브 — 만든 기록",
            "",
            .. SwitchMaking.TrainerLines(me),
            $"바이올렛      같은 이름, SID / TID {maker.Violet.ID32 / 1_000_000:0000} / {maker.Violet.ID32 % 1_000_000:000000} — 바이올렛 전용은 거기서 잡아 교환",
            $"볼            {(opt.Ball is { } ob ? $"{ko.balllist[ob]}로 통일 (안 되는 포켓몬은 몬스터볼)" : "포켓몬마다 골라 둔 볼")}",
            $"색            {(opt.Shiny ? "이로치 (고정·레이드·교환은 일반)" : "일반")}",
            $"크기          {opt.Size switch { SizeChoice.Smallest => "가장 작게 (스케일 0, 조그만 증표)", SizeChoice.Largest => "가장 크게 (스케일 255, 커다란 증표)", _ => "게임이 뽑은 대로" }}",
            $"레벨          {(opt.Level == LevelChoice.Hundred ? "100" : "잡은 레벨 그대로 (진화에 필요한 만큼만 올림)")}",
            $"성별          {opt.Sex switch { SexChoice.Male => "수컷 (가능한 종)", SexChoice.Female => "암컷 (가능한 종)", _ => "게임이 뽑은 대로" }}",
            $"잡은 기간     {opt.From:yyyy-MM-dd} ~ {opt.To:yyyy-MM-dd}",
            $"시드          {opt.Seed}",
            "",
        ];
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
        for (int i = 0; i < events.Count; i++) sav.SetBoxSlotAtIndex(events[i].Pk, made.Count + i, EntityImportSettings.None);
        foreach (var m in made.Concat(events)) sav.Zukan.SetDex(m.Pk);
        var data = sav.Write().ToArray();

        int seen = 0, caught = 0;
        for (ushort s = 1; s <= sav.MaxSpeciesID; s++) { if (sav.GetSeen(s)) seen++; if (sav.GetCaught(s)) caught++; }
        lines.Add($"포켓몬        {made.Count}마리 (박스), {made.Select(m => m.Pk.Species).Distinct().Count()}종, 폼까지 {made.Select(m => (m.Pk.Species, m.Pk.Form)).Distinct().Count()}");
        lines.Add($"합법          {made.Count} / {made.Count}");
        lines.Add($"이로치        {made.Count(m => m.Pk.IsShiny)}마리");
        lines.Add($"출처          야생 {made.Count(m => m.Entry.Source == Source9.Wild)}, 고정 {made.Count(m => m.Entry.Source == Source9.Static)}, 알 {made.Count(m => m.Entry.Source == Source9.Egg)}, 레이드 {made.Count(m => m.Entry.Source == Source9.Raid)}, 교환 {made.Count(m => m.Entry.Source == Source9.Trade)}; 바이올렛에서 {made.Count(m => m.Entry.Violet)}");
        lines.Add($"도감          본 것 {seen}, 잡은 것 {caught}");
        lines.Add($"배포          {Events9.All.Count}건 (스칼렛에서 받을 수 있는 게임 내 배포 전부, 각각 배포 기간 안의 날짜; 바이올렛만 받는 카드는 거기서 받아 교환) + 최종 진화체 {events.Count - Events9.All.Count}마리 = {events.Count}마리, 도감 뒤에. 전부 {made.Count + events.Count} / {sav.SlotCount}칸");
        lines.Add("");
        lines.Add("마리마다: 이름 · 폼 · 색 · 레벨 · 성격 · 특성 · 개체값 · 스케일 · 테라 · 볼 · 출처 · 시드");
        foreach (var m in made.Concat(events))
        {
            var pk = m.Pk;
            lines.Add($"  {Plan9.Label(pk.Species, pk.Form)}{(m.Entry.Gender is { } g9 ? (g9 == 0 ? " ♂" : " ♀") : "")}{(pk.IsShiny ? " ★" : "")} Lv{pk.CurrentLevel} {ko.natures[(int)pk.Nature]} {ko.abilitylist[pk.Ability]} {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE} 스케일{pk.Scale} {((int)pk.TeraTypeOriginal < ko.types.Length ? ko.types[(int)pk.TeraTypeOriginal] : "스텔라")} {ko.balllist[pk.Ball]} — {m.How}{(m.Entry.Evolves ? $" → {m.Entry.Note}" : "")}{(m.Seed is { } s ? $" · 시드 {s:X16}" : "")}");
        }

        return SwitchMaking.Write(outDir, under, "Scarlet", me, data, Sidecars, lines);
    }
}

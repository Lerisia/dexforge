using PKHeX.Core;

namespace Dexforge.Sword;

/// <summary>One Sword save made to order: every species and form the game can hold, drawn, checked, and written as a JKSV backup folder with its record.</summary>
public static class Making8
{

    /// <summary>The files JKSV wants in a backup folder besides the save itself, carried inside with the template.</summary>
    private static readonly (string Resource, string File)[] Sidecars = [("sword.backup", "backup"), ("sword.poke_trade", "poke_trade"), ("sword.nx_save_meta", ".nx_save_meta.bin")];

    /// <param name="outDir">Where to write; none, and the folder is named after the trainer, under <paramref name="under"/>.</param>
    /// <param name="step">Told how many are done, and of how many.</param>
    public static Made Run(Options8 opt, string? outDir, string under, Action<int, int>? step = null)
    {
        var ko = Plan8.Ko;
        if ((SwitchMaking.RefusedTrainer(opt.Name, opt.Tid, opt.Sid)) is { } why) return SwitchMaking.Refused(why);
        if (opt.Year is < 2019 or > 2099) return SwitchMaking.Refused("해는 2019 부터 2099 까지입니다 (소드실드는 2019년 11월에 나왔습니다).");

        var random = new Random(opt.Seed);
        var sav = new SAV8SWSH(Resources.Bytes("sword.main"));
        uint id32 = SwitchMaking.Id32(random, opt.Tid, opt.Sid);
        var me = new Trainer(opt.Name, sav.Gender, (ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16), sav.Language, GameVersion.SW, 0, 0, 0);
        var trainer = new SimpleTrainerInfo(GameVersion.SW) { OT = opt.Name, Gender = sav.Gender, Language = sav.Language, ID32 = id32 };
        var friend = new SimpleTrainerInfo(GameVersion.SW) { OT = "새아", Gender = 1, Language = sav.Language, ID32 = (uint)random.Next(0, 4295) * 1_000_000 + (uint)random.Next(0, 1_000_000) };
        var maker = new Maker8(trainer, friend, random, opt.Year, opt.Shiny, opt.Ball is { } b ? (Ball)b : null);

        var entries = Plan8.All().Where(e => e.Source != Source.None).ToList();
        var made = new List<Made8>(); var failed = new List<string>();
        var ribbonsPut = new Dictionary<string, int>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            try
            {
                var m = maker.Make(e);
                if (m.Legal)
                {
                    foreach (var key in opt.Ribbons) if (Ribbons.Put(m.Pk, [key]) > 0) ribbonsPut[key] = ribbonsPut.GetValueOrDefault(key) + 1;
                    made.Add(m);
                }
                else failed.Add($"{ko.specieslist[e.Species]} {Plan8.FormName(e.Species, e.Form)}: {string.Join(" | ", m.Report.Split('\n').Where(l => l.Contains("Invalid")))}");
            }
            catch (InvalidOperationException ex) { failed.Add($"{ko.specieslist[e.Species]} {Plan8.FormName(e.Species, e.Form)}: {ex.Message}"); }
            step?.Invoke(i + 1, entries.Count);
        }

        // the distributions Sword received, after the dex: every card, then its final evolutions
        var events = new List<Made8>();
        foreach (var ev in Events8.All)
            foreach (var m in maker.MakeEvent(ev))
            {
                if (m.Legal) events.Add(m);
                else failed.Add($"배포 {ev.Title} → {ko.specieslist[m.Entry.Species]}: {SwitchMaking.Faults(m.Report)}");
            }
        if (made.Count + events.Count > sav.SlotCount) failed.Add($"박스가 모자랍니다: 도감 {made.Count} + 배포 {events.Count} > {sav.SlotCount}");

        List<string> lines =
        [
            "소드 전국도감 세이브 — 만든 기록",
            "",
            .. SwitchMaking.TrainerLines(me),
            $"볼            {(opt.Ball is { } ob ? $"{ko.balllist[ob]}로 통일 (안 되는 포켓몬은 몬스터볼, 선물·배포는 정해진 볼)" : $"{BallNames.Matched} (포켓몬마다 골라 둔 볼)")}",
            $"색            {(opt.Shiny ? "이로치 (안 되는 것은 일반)" : "일반")}",
            $"해            {opt.Year}년 (배포 카드가 그 해에 없던 것은 카드의 날짜)",
            $"레벨          가능한 최저",
            $"리본          {(opt.Ribbons.Count == 0 ? "없음" : string.Join(", ", opt.Ribbons.Select(k => Ribbons.Find(k, Ribbons.Sword)?.Name ?? k)))}",
            $"시드          {opt.Seed}",
            "",
        ];
        if (failed.Count != 0)
        {
            var refused = new List<string> { "만들지 못한 개체가 있어 세이브를 쓰지 않습니다." };
            foreach (var f in failed.Take(20)) refused.Add("  " + f);
            return new Made(1, null, lines, refused, me);
        }

        sav.OT = me.Name;
        sav.ID32 = id32;
        for (int i = 0; i < sav.PartyCount; i++)
        {
            // The partner in the party was this trainer's from the start.
            var pp = sav.GetPartySlotAtIndex(i);
            if (pp.CurrentHandler == 0 && pp.HandlingTrainerName.Length == 0) { pp.OriginalTrainerName = me.Name; pp.ID32 = id32; pp.OriginalTrainerGender = me.Gender; pp.RefreshChecksum(); sav.SetPartySlotAtIndex(pp, i); }
        }
        for (int i = 0; i < made.Count && i < sav.SlotCount; i++)
            sav.SetBoxSlotAtIndex(made[i].Pk, i, EntityImportSettings.All);
        for (int i = 0; i < events.Count && made.Count + i < sav.SlotCount; i++)
            sav.SetBoxSlotAtIndex(events[i].Pk, made.Count + i, EntityImportSettings.All);
        var data = sav.Write().ToArray();

        var shiny = made.Count(m => m.Pk.IsShiny);
        int seen = 0, caught = 0;
        for (ushort s = 1; s <= sav.MaxSpeciesID; s++) { if (sav.Blocks.Zukan.GetSeen(s)) seen++; if (sav.Blocks.Zukan.GetCaught(s)) caught++; }
        lines.Add($"포켓몬        {made.Count}마리 (박스), {made.Select(m => m.Pk.Species).Distinct().Count()}종, 폼까지 {made.Count}");
        lines.Add($"합법          {made.Count} / {made.Count}");
        lines.Add($"이로치        {shiny}마리");
        lines.Add($"도감          본 것 {seen}, 잡은 것 {caught} (가라르·갑옷섬·왕관설원 세 도감의 종; 도감 밖의 종은 박스에만)");
        lines.Add($"빠진 것       디안시, 마기아나(노말 색) — 소드에서 얻는 길이 없음");
        lines.Add($"배포          {Events8.All.Count}건 (소드에서 받을 수 있는 게임 내 배포 전부, 각각 배포 기간 안의 날짜) + 최종 진화체 {events.Count - Events8.All.Count}마리 = {events.Count}마리, 도감 뒤에");
        lines.Add($"              HOME 선물은 트래커가 있는 넷만 (도감 쪽에). 전부 {made.Count + events.Count} / {sav.SlotCount}칸");
        if (opt.Ribbons.Count != 0)
            lines.Add($"리본 붙은 수  {string.Join(", ", opt.Ribbons.Select(k => $"{Ribbons.Find(k, Ribbons.Sword)?.Name ?? k} {ribbonsPut.GetValueOrDefault(k)}"))} (배포 포켓몬과 PKHeX 가 거절한 것은 제외)");
        lines.Add("");
        lines.Add("마리마다: 이름 · 폼 · 색 · 레벨 · 성격 · 특성 · 개체값 · 볼 · 출처 · 시드");
        foreach (var m in made.Concat(events))
        {
            var pk = m.Pk;
            lines.Add($"  {ko.specieslist[pk.Species]}{(pk.Form != 0 ? " " + Plan8.FormName(pk.Species, pk.Form) : "")}{(pk.IsShiny ? " ★" : "")} Lv{pk.CurrentLevel} {ko.natures[(int)pk.Nature]} {ko.abilitylist[pk.Ability]} {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE} {ko.balllist[pk.Ball]} — {m.How}");
        }

        return SwitchMaking.Write(outDir, under, "Sword", me, data, Sidecars, lines);
    }
}

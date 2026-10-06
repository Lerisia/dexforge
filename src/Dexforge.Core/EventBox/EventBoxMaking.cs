using System.Text;
using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>What the person asking has chosen for the event box.</summary>
/// <param name="Name">The save's trainer, and whoever hatched the eggs received by Korean games.</param>
/// <param name="Tid">The six-digit TID the game shows, or none to draw one.</param>
/// <param name="Sid">The four-digit SID that goes with it, or none to draw one.</param>
/// <param name="FirstDays">Received within the first so many days of each distribution; 0 for anywhere in its window.</param>
/// <param name="Picks">Keys of what to put in the spare room (see <see cref="Custom"/>).</param>
public sealed record EventOptions(string Name, uint? Tid, uint? Sid, int Seed, int FirstDays = 0, IReadOnlyList<string>? Picks = null);

/// <summary>One event box made to order: every distribution the rules keep, its evolutions, and what was picked for the spare room, written as a save with its record.</summary>
public static class EventBoxMaking
{
    /// <summary>The day the Korean game came out; the adventure begins after it, and this save receives its own cards a few days later still.</summary>
    public static readonly DateOnly Released = new(2017, 11, 17);
    /// <summary>Open-ended distributions are drawn up to this day.</summary>
    public static readonly DateOnly OpenEndCap = new(2019, 12, 31);

    /// <summary>The rules drop the Pokémon Center Japan and New York gifts (owner, 2026-10-04).</summary>
    public static bool Dropped(Dist d) => d["대표 파일"] is "PCJP" or "PCNY";

    private static int? planned;
    /// <summary>How many the rules put in the boxes: the kept distributions and their final evolutions.</summary>
    public static int Planned
    {
        get
        {
            if (planned is { } p) return p;
            var ko = GameInfo.GetStrings("ko");
            var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
            var rows = Rows.Distributions();
            int n = 0;
            foreach (var r in rows.All)
            {
                var d = new Dist(rows, r);
                if (Dropped(d)) continue;
                n++;
                if (EventMaker.Evolves(d)) n += Evolve7.Finals(numbers[d.Species], byte.Parse(d["폼"])).Count;
            }
            return (planned = n).Value;
        }
    }
    /// <summary>The slots left for whoever asks to fill: 32 boxes of 30, less what the rules put in.</summary>
    public static int Room => 32 * 30 - Planned;

    public static string FolderFor(string under, Trainer me) => Path.Combine(under, $"Dexforge-EventBox-{me.Name}-{me.Shown:000000}");

    public static Made Run(EventOptions opt, string? outDir, string under, Action<int, int>? step = null)
    {
        if (opt.Name.Length is < 1 or > 6) return new Made(2, null, [], ["어버이 이름은 1글자에서 6글자 사이여야 합니다."]);
        if (opt.FirstDays < 0) return new Made(2, null, [], ["처음 n일의 n 은 0 이상이어야 합니다."]);
        var ko = GameInfo.GetStrings("ko");
        var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
        var rows = Rows.Distributions();
        var dists = rows.All.Select(r => new Dist(rows, r)).ToList();
        var cards = Cards.All();
        foreach (var d in dists)
        {
            var c = cards[d.CardRow];
            ushort sp = c is MysteryGift g ? g.Species : (ushort)c.GetType().GetProperty("Species")!.GetValue(c)!;
            if (ko.Species[sp] != d.Species) return new Made(2, null, [], [$"프로그램 안의 배포 목록이 이 PKHeX 의 카드 순서와 맞지 않습니다 ({d.Label})."]);
        }
        var candidates = Custom.Candidates(rows, numbers).ToDictionary(c => c.Key);
        var picks = (opt.Picks ?? []).Distinct().ToList();
        if (picks.FirstOrDefault(p => !candidates.ContainsKey(p)) is { } unknown) return new Made(2, null, [], [$"고를 수 없는 것입니다: {unknown}"]);

        // The trainer, adventure and party come of the national dex's own prologue; the days there only place the adventure's start after the game came out.
        var options = new Options(opt.Name, opt.Tid, opt.Sid, new DateOnly(2018, 1, 10), new DateOnly(2018, 1, 10), opt.Seed);
        Generator gen;
        // the adventure begins on the day the game came out: the early-purchase gifts were there to be had from the first days
        try { gen = new Generator(Making.Template(), options) { BeganOn = Released }; }
        catch (ArgumentException ex) { return new Made(2, null, [], [ex.Message]); }

        var record = new List<string>(); var problems = new List<string>(); var notes = new List<string>();
        int originals = 0, evolutions = 0, picked = 0, shinyMade = 0, shinyLeft = 0, albumCount = 0; int receiversCount = 0;
        byte[] data;
        try
        {
            data = gen.RunWith((sav, me, began) =>
            {
                var rnd = new Random(opt.Seed ^ 0x5A5A5A5A);
                var receivers = new Receivers(rnd, me.Name, me.Gender);
                receivers.SetSave(me.Info);
                var kept = dists.Where(d => !Dropped(d)).ToList();
                var maker = new EventMaker(rnd, receivers, cards, OpenEndCap) { NotBefore = began.AddDays(1), FirstDays = opt.FirstDays, Sets = EventMaker.Groups(kept) };
                var made = new List<Made7>(); var evolved = new List<Made7>(); var extra = new List<Made7>();
                int done = 0, of = kept.Count;
                foreach (var d in kept)
                {
                    try { made.Add(maker.Make(d)); }
                    catch (Exception ex) { problems.Add($"{d.Label}: {ex.Message}"); }
                    step?.Invoke(++done, of);
                }
                foreach (var d in kept)
                    foreach (var (m, target, problem) in maker.MakeEvolutions(d, numbers))
                        if (m is not null) evolved.Add(m); else problems.Add($"{d.Label} → {target}: {problem}");
                foreach (var key in picks)
                {
                    var c = candidates[key];
                    try
                    {
                        if (c.EvolvesTo == 0) extra.Add(maker.Make(c.Dist));
                        else
                        {
                            var one = maker.MakeEvolution(c.Dist, c.EvolvesTo, c.EvolvesToForm, out var why);
                            if (one is null) problems.Add($"{c.Label}: {why}"); else extra.Add(one);
                        }
                    }
                    catch (Exception ex) { problems.Add($"{c.Label}: {ex.Message}"); }
                }
                int Order(Made7 m) => m.Dist.Generation * 10000 + m.Dist.CardRow;
                originals = made.Count; evolutions = evolved.Count; picked = extra.Count;
                var all = made.OrderBy(Order).Concat(evolved.OrderBy(Order)).Concat(extra).ToList();
                shinyMade = all.Count(m => m.Notes.Contains("랜덤→이로치")); shinyLeft = all.Count(m => m.Notes.Contains("못 만듦"));
                receiversCount = receivers.All.Count;
                if (all.Count > sav.BoxCount * sav.BoxSlotCount) problems.Add($"박스가 모자랍니다: {all.Count}마리, 칸은 {sav.BoxCount * sav.BoxSlotCount}");

                for (int b = 0; b < sav.BoxCount; b++)
                    for (int s = 0; s < sav.BoxSlotCount; s++)
                        sav.SetBoxSlotAtIndex(sav.BlankPKM, b, s, EntityImportSettings.None);
                record.Add("박스\t칸\t세대\t포켓몬\t폼\t이로치\t레벨\t어버이\tTID\t언어\t받은 게임\t받은 날\t만난 날(세이브 기준)\t핸들러\t메모");
                for (int i = 0; i < all.Count && i < sav.BoxCount * sav.BoxSlotCount; i++)
                {
                    var m = all[i]; var pk = m.Pokemon;
                    int box = i / sav.BoxSlotCount, slot = i % sav.BoxSlotCount;
                    sav.SetBoxSlotAtIndex(pk, box, slot, EntityImportSettings.None);
                    record.Add(string.Join('\t', box + 1, slot + 1, m.Dist.Generation, ko.Species[pk.Species], pk.Form, pk.IsShiny ? "★" : "", pk.CurrentLevel,
                                           pk.OriginalTrainerName, pk.DisplayTID, (LanguageID)pk.Language, m.Receiver.Version, m.Received.ToString("yyyy-MM-dd"),
                                           pk.MetDate?.ToString("yyyy-MM-dd") ?? "", pk.CurrentHandler == 0 ? "어버이" : pk.HandlingTrainerName, m.Notes));
                }
                albumCount = Album((SAV7USUM)sav, made.OrderBy(Order).ToList());
                notes.AddRange(receivers.All.Values.Select(t => $"{t.Version}/{(LanguageID)t.Language} {t.OT} {t.TID16:00000}/{t.SID16:00000}"));
            });
        }
        catch (Exception ex) { return new Made(2, null, record, [ex.Message]); }

        // Read back and check every slot. Nothing of the program's global state is touched: a maker leaves no trace for the
        // next one made in the same window (PKHeX's "active trainer" setting is program-wide, so it is not used here).
        // Whose each Pokémon is and who handles it was set by the maker itself; the checksums and the save are what is read back.
        if (!SaveUtil.TryGetSaveFile(data.ToArray(), out var again) || again is not SAV7USUM check) return new Made(1, null, record, ["쓴 세이브가 다시 읽히지 않습니다."]);
        int count = 0, legal = 0, shiny = 0;
        for (int b = 0; b < check.BoxCount; b++)
            foreach (var p in check.GetBoxData(b))
            {
                if (p.Species == 0) continue;
                count++; if (p.IsShiny) shiny++;
                var la = new LegalityAnalysis(p, check.Personal);
                if (la.Valid) legal++; else problems.Add($"박스 {b + 1} {ko.Species[p.Species]}: {EventMaker.Problems(la)}");
            }

        var me = gen.Me;
        var lines = new List<string>
        {
            "배포 박스 세이브 — 만든 기록",
            "",
            $"어버이        {me.Name} ({(me.Gender == 0 ? "남" : "여")})",
            $"SID / TID     {me.Sid7:0000} / {me.Shown:000000}   (16비트: TID {me.Tid:00000}, SID {me.Sid:00000})",
            $"모험 시작     {gen.Began:yyyy-MM-dd}",
            $"받은 날       {(opt.FirstDays > 0 ? $"배포 기간의 처음 {opt.FirstDays}일 안" : "배포 기간 안 아무 날")} (같은 기간·어버이·지역의 서로 다른 종 둘·셋은 같은 날)",
            $"박스          원본 {originals} + 최종 진화체 {evolutions} + 직접 고른 것 {picked} = {count}마리, 빈 칸 {check.BoxCount * check.BoxSlotCount - count}",
            $"합법          {legal} / {count} (PKHeX, 세이브 문맥)",
            $"이로치        {shiny} (색이 랜덤인 카드 {shinyMade}건을 이로치로; {shinyLeft}건은 PKHeX 가 이로치를 허용하지 않아 일반 색)",
            $"앨범          이 세이브가 직접 받은 7세대 카드 {albumCount}장 (최대 48, 나중 것이 남음)",
            $"받는 트레이너  {receiversCount}명 (게임 × 언어마다 하나; 한국어는 {me.Name}, 일본어 ミヅキ, 영어 Selene, 중국어 美月)",
            $"시드          {opt.Seed}",
            "",
        };
        if (problems.Count > 0) { lines.Add($"문제 {problems.Count}건:"); lines.AddRange(problems.Select(p => "  " + p)); lines.Add(""); }
        lines.Add("받는 트레이너:"); lines.AddRange(notes.Select(n => "  " + n)); lines.Add("");
        lines.Add("박스:"); lines.AddRange(record);

        int code = problems.Count > 0 || legal != count ? 1 : 0;
        outDir ??= FolderFor(under, me);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, Making.SaveName), data);
        File.WriteAllLines(Path.Combine(outDir, Making.RecordName), lines, new UTF8Encoding(true));
        return new Made(code, outDir, lines, code == 0 ? [] : problems, me);
    }

    /// <summary>
    /// The album holds 48 cards; a player who keeps receiving deletes the oldest to make room, so the last 48 this save received remain,
    /// each dated the day it was received. Cards PKHeX only simulated stay out. Every card received sets its flag.
    /// </summary>
    public static int Album(SAV7USUM sav, List<Made7> originals)
    {
        var album = (MysteryBlock7)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        var mine = originals.Where(m => m.Card is WC7 { IsEntity: true } w && m.Receiver.Version == sav.Version && m.Receiver.TID16 == sav.TID16 && m.Receiver.SID16 == sav.SID16
                                        && !w.CardTitle.Contains("Simulated") && w.CardID != 0)
                            .OrderBy(m => m.Received).ToList();
        album.ClearReceivedFlags();
        foreach (var m in mine)
        {
            var w = (WC7)m.Card;
            if (w.CardID < album.MysteryGiftReceivedFlagMax) album.SetMysteryGiftReceivedFlag(w.CardID, true);
        }
        var keep = mine.Skip(Math.Max(0, mine.Count - album.GiftCountMax)).ToList();
        for (int i = 0; i < album.GiftCountMax; i++)
        {
            if (i < keep.Count)
            {
                var w = (WC7)((WC7)keep[i].Card).Clone();
                w.Date = keep[i].Received;
                album.SetMysteryGift(i, w);
            }
            else album.SetMysteryGift(i, new WC7());
        }
        return keep.Count;
    }
}

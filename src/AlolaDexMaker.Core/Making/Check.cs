using PKHeX.Core;

namespace AlolaDexMaker;

/// <summary>A save that has been written, read back and held against the template it came from.</summary>
public static class Check
{
    public sealed record Result(int Count, int Legal, int Shiny, int Species, int Redrawn, List<string> Faults, List<string> Fixed);

    private static List<PK7> All(SAV7 s)
    {
        var l = new List<PK7>();
        for (int b = 0; b < s.BoxCount; b++) foreach (var p in s.GetBoxData(b)) if (p.Species != 0) l.Add((PK7)p);
        foreach (var p in s.PartyData) l.Add((PK7)p);
        return l;
    }

    public static Result Run(byte[] template, byte[] written, Trainer me, Trainer was, DateOnly began, int? ball = null, IvChoice ivs = IvChoice.Random, bool shiny = true,
                             LevelChoice level = LevelChoice.Lowest, SexChoice sex = SexChoice.Random, Options? asked = null)
    {
        var ko = GameInfo.GetStrings("ko");
        var faults = new List<string>(); var fixedByCard = new List<string>();
        if (!SaveUtil.TryGetSaveFile(template.ToArray(), out var a) || a is not SAV7 t) throw new InvalidOperationException("the template does not read");
        if (!SaveUtil.TryGetSaveFile(written.ToArray(), out var b) || b is not SAV7 g) { faults.Add("쓴 세이브가 읽히지 않음"); return new(0, 0, 0, 0, 0, faults, fixedByCard); }
        if (!g.ChecksumsValid) faults.Add("체크섬이 맞지 않음");
        if (g.OT != me.Name || g.TID16 != me.Tid || g.SID16 != me.Sid) faults.Add("세이브의 트레이너가 요청과 다름");
        var old = All(t); var made = All(g);
        if (old.Count != made.Count) { faults.Add($"마릿수가 다름: {old.Count} → {made.Count}"); return new(made.Count, 0, 0, 0, 0, faults, fixedByCard); }
        int legal = 0, redrawn = 0;
        int inBoxes = old.Count - t.PartyCount;
        var both = Sexes.Both(old.Take(inBoxes));
        var pids = new HashSet<uint>(); var ecs = new HashSet<uint>();
        for (int i = 0; i < made.Count; i++)
        {
            var p = made[i]; var o = old[i];
            string name = ko.Species[p.Species] + (p.Form != 0 ? $"-{p.Form}" : "");
            var la = new LegalityAnalysis(p);
            if (la.Valid) legal++; else faults.Add($"{name}: {string.Join(" | ", la.Report().Split('\n').Where(l => l.Contains("Invalid")).Take(2))}");
            if (p.PID != o.PID || p.EncryptionConstant != o.EncryptionConstant) redrawn++;
            else if (la.EncounterMatch is MysteryGift) fixedByCard.Add(name);
            else faults.Add($"{name}: 틀과 같은 값 그대로");
            if (was.Name != me.Name && (p.OriginalTrainerName == was.Name || p.HandlingTrainerName == was.Name)) faults.Add($"{name}: 틀의 이름이 남음");
            if (p.ID32 == was.Id32 && was.Id32 != me.Id32) faults.Add($"{name}: 틀의 ID 가 남음");
            // Whoever plays in another language has the name asked for.
            if (asked is not null && ForeignNames.Of(asked, o.OriginalTrainerName) is var foreign && foreign != o.OriginalTrainerName && p.OriginalTrainerName != foreign)
                faults.Add($"{name}: 외국어 트레이너 이름이 요청과 다름 ({p.OriginalTrainerName})");
            bool own = p.ID32 == g.ID32 && p.OriginalTrainerName == g.OT && p.Version == g.Version;
            if (own && (p.MetDate < began || (p.EggMetDate is { } e && e < began))) faults.Add($"{name}: 모험 시작보다 앞선 날짜");
            if (la.EncounterMatch is not MysteryGift && (!pids.Add(p.PID) || !ecs.Add(p.EncryptionConstant))) faults.Add($"{name}: 같은 세이브 안에서 값이 겹침");
            // At the level asked for; the party as it was. The moves are the template's either way.
            bool boxed = i < inBoxes;
            int wantLevel = boxed && level == LevelChoice.Hundred ? 100 : o.CurrentLevel;
            if (p.CurrentLevel != wantLevel) faults.Add($"{name}: 레벨이 요청과 다름 ({p.CurrentLevel})");
            if (p.Move1 != o.Move1 || p.Move2 != o.Move2 || p.Move3 != o.Move3 || p.Move4 != o.Move4) faults.Add($"{name}: 기술이 틀과 다름");
            // Of the sex asked for, where it is free to be either; as it was where it is not, and in the party.
            {
                var enc = new LegalityAnalysis(o).EncounterMatch;
                byte? wantSex = boxed ? Sexes.Wanted(sex, o, enc, both) : o.Gender;
                if (wantSex is { } ws && p.Gender != ws) faults.Add($"{name}: 성별이 요청과 다름");
            }
            // Shiny or plain as asked, where the Pokemon is free to be either.
            {
                bool forced = new LegalityAnalysis(o).EncounterMatch is MysteryGift card and not PGT { IsManaphyEgg: true } && card.Shiny != Shiny.Random;
                bool wanted = o.IsShiny && (shiny || forced);
                if (p.IsShiny != wanted) faults.Add($"{name}: 색이 요청과 다름 ({(p.IsShiny ? "이로치" : "일반 색")})");
            }
            // As many perfect individual values as were asked for, where they are free to be asked for.
            if (la.EncounterMatch is not MysteryGift && p.Generation >= 7)
            {
                int perfect = Enumerable.Range(0, 6).Count(k => p.GetIV(k) == 31);
                // What hatched can have any values. What was met or handed over has the values a session of the game gave it, and six perfect
                // ones are asked only where the game promises three of them.
                bool free = la.EncounterMatch is IEncounterEgg || la.EncounterMatch is IFlawlessIVCount { FlawlessIVCount: >= 3 };
                if (ivs == IvChoice.Six && free && perfect != 6) faults.Add($"{name}: 6V 가 아님 ({perfect})");
                if (ivs == IvChoice.FiveFromEggs && la.EncounterMatch is IEncounterEgg && perfect < 5) faults.Add($"{name}: 알인데 5V 가 아님 ({perfect})");
            }
            // Each in the ball picked for it; or, where one ball was asked for, in that one, and in a Poke Ball only where that one will not do.
            if (ball is not { } want) { if (p.Ball != SexBalls.Expected(o, p.Gender)) faults.Add($"{name}: 볼이 틀과 다름"); }
            else if (la.EncounterMatch is MysteryGift) { if (p.Ball != o.Ball) faults.Add($"{name}: 카드가 정한 볼이 바뀜"); }
            else if (p.Ball != want)
            {
                if (p.Ball != (int)Ball.Poke) faults.Add($"{name}: 고른 볼도 몬스터볼도 아님 ({ko.balllist[p.Ball]})");
                else
                {
                    var inIt = (PK7)p.Clone(); inIt.Ball = (byte)want; inIt.RefreshChecksum();
                    if (new LegalityAnalysis(inIt).Valid) faults.Add($"{name}: 고른 볼이 되는데 몬스터볼에 들어감");
                }
            }
        }
        for (int i = 0; i < 200; i++) if (g.GetRecord(i) != t.GetRecord(i)) { faults.Add($"게임 기록 #{i} 이 틀과 다름"); break; }
        // Only what was meant to change may differ: the trainer, the party, what Rotom calls the player, the clock's beginning, the boxes,
        // the Festival Plaza's records of the player, the album, the signature.
        var ra = template; var rb = written;
        var meant = new HashSet<uint> { 3, 4, 10, 12, 14, 20, 21, 27, 36 };
        // The Pokedex, where it has seen a sex it had not.
        if (sex != SexChoice.Female) meant.Add(6);
        if (g.FieldMenu.RotomOT != me.Name) faults.Add("로토무가 부르는 이름이 요청과 다름");
        foreach (var (id, at) in Generator.PlazaRecords)
        {
            var blk = g.AllBlocks.Single(x => x.ID == id);
            if (StringConverter7.GetString(rb.AsSpan(blk.Offset + at + 8, 0x1A)) != me.Name) faults.Add($"페스서클 기록(블록 {id})의 이름이 요청과 다름");
        }
        // The template's name is nowhere in the blocks, unless it is the name asked for. Pokemon are stored enciphered and are looked at above.
        if (was.Name != me.Name)
        {
            var pattern = System.Text.Encoding.Unicode.GetBytes(was.Name);
            foreach (var blk in g.AllBlocks)
                if (blk.ID != 4 && blk.ID != 14 && rb.AsSpan(blk.Offset, blk.Length).IndexOf(pattern) >= 0) faults.Add($"블록 {blk.ID} 에 틀의 이름이 남음");
        }
        // Nothing is left after the end of a name where nobody else has held the Pokemon.
        foreach (var p in made)
            if (p.HandlingTrainerName.Length == 0 && p.HandlingTrainerTrash.ContainsAnyExcept((byte)0)) { faults.Add($"{ko.Species[p.Species]}: 주인 칸에 글자가 남음"); break; }
        foreach (var blk in t.AllBlocks)
            if (!meant.Contains(blk.ID) && !ra.AsSpan(blk.Offset, blk.Length).SequenceEqual(rb.AsSpan(blk.Offset, blk.Length))) faults.Add($"바뀌면 안 되는 블록 {blk.ID} 가 바뀜");
        int species = made.Select(p => p.Species).Distinct().Count();
        return new(made.Count, legal, made.Count(p => p.IsShiny), species, redrawn, faults, fixedByCard);
    }
}

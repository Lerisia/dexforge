using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// What comes from the games before the sixth generation, drawn the way those games draw:
/// the trainer's ids as that game gives them, the Pokemon from a state that game can be in.
/// PKHeX checks the run of random numbers behind each; the rest is checked against PokeFinder's vectors.
/// </summary>
public sealed class Older(Draw draw, SAV7 sav, Trainer me, Trainer was, GameStrings ko, Options opt, DateOnly lettersDay, DateOnly platinumDay)
{
    private readonly Random rnd = draw.Rnd;
    private readonly Dictionary<string, SimpleTrainerInfo> trainers = [];
    private readonly Dictionary<string, DateOnly> began = [];
    public List<string> Notes { get; } = [];

    /// <summary>The trainer's own older games carry the trainer's name, as much of it as they have room for. Anybody else keeps theirs.</summary>
    private string NameFor(PK7 old, int room) => old.OriginalTrainerName != was.Name ? ForeignNames.Of(opt, old.OriginalTrainerName) : me.Name.Length > room ? me.Name[..room] : me.Name;

    public PK7? Again(PK7 old, LegalityAnalysis la) => (old.Species, old.Version) switch
    {
        (151 or 386, GameVersion.E) => Emerald(old),
        (491 or 492, GameVersion.Pt) => Platinum(old),
        (490, GameVersion.Pt) => Manaphy(old),
        (251, GameVersion.C) => Crystal(old),
        (201, GameVersion.HG) => Letter(old),
        _ => throw new InvalidOperationException("no way is known to draw this one again"),
    };

    /// <summary>All of these are shiny in the template; whether the one made now is, is what was asked for.</summary>
    private bool Shines(PK7 old) => old.IsShiny && opt.Shiny;

    private static string Problems(LegalityAnalysis la) => string.Join(" | ", la.Report().Split('\n').Where(l => l.Contains("Invalid")).Take(3));

    private static void SetSpread(PKM pk, Old.Spread sp)
    {
        pk.PID = sp.Pid;
        pk.IV_HP = sp.Ivs[0]; pk.IV_ATK = sp.Ivs[1]; pk.IV_DEF = sp.Ivs[2]; pk.IV_SPA = sp.Ivs[3]; pk.IV_SPD = sp.Ivs[4]; pk.IV_SPE = sp.Ivs[5];
        pk.RefreshAbility((int)(sp.Pid & 1));
    }

    /// <summary>As it is once it has come up through the generations to this save.</summary>
    private PK7 MoveUp(PKM caught, PK7 old, DateOnly day, uint? origin, (byte console, byte country, byte region)? from = null)
    {
        caught.Ball = old.Ball;
        caught.ResetPartyStats();
        caught.RefreshChecksum();
        var la0 = new LegalityAnalysis(caught);
        if (!la0.Valid) throw new InvalidOperationException("as caught: " + Problems(la0));
        if (origin is { } seed && la0.Info.PIDIV.OriginSeed != seed) throw new InvalidOperationException($"PKHeX reads the seed as {la0.Info.PIDIV.OriginSeed:X8}, not {seed:X8}");
        var moved = EntityConverter.ConvertToType(caught, typeof(PK7), out var result) as PK7 ?? throw new InvalidOperationException($"would not move up: {result}");
        moved.MetDate = day;
        moved.HeldItem = 0;
        if (from is { } f) { moved.ConsoleRegion = f.console; moved.Country = f.country; moved.Region = f.region; }
        moved.UpdateHandler(sav);
        moved.RefreshChecksum();
        var la7 = new LegalityAnalysis(moved);
        if (!la7.Valid) throw new InvalidOperationException("after moving up: " + Problems(la7));
        if (moved.IsShiny != Shines(old)) throw new InvalidOperationException("its colour changed on the way up");
        return moved;
    }

    private DateOnly Day(DateOnly from, DateOnly to) => draw.Day(from, to);
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // Emerald starts its generator at 0 every time it is switched on, and steps it once a frame.
    // A new game seeds it with the visible id, and draws the secret id when the professor has finished speaking.
    private PK7 Emerald(PK7 old)
    {
        var language = (LanguageID)old.Language;
        string name = NameFor(old, language == LanguageID.Japanese ? 5 : 7);
        ushort tid, sid; uint idFrames, frame; Old.Spread sp;
        do
        {
            tid = draw.Id16();
            idFrames = (uint)rnd.Next(3500, 12001);
            sid = Old.SecretId3(tid, idFrames);
            frame = (uint)rnd.Next(3000, 216001);
            sp = Old.Method1(Old.Advance(0, frame));
        } while (Old.IsShiny34(tid, sid, sp.Pid) != Shines(old));
        var tr = new SimpleTrainerInfo(GameVersion.E) { OT = name, Gender = old.OriginalTrainerGender, TID16 = tid, SID16 = sid, Language = (int)language };
        var template = new PK7 { Species = old.Species, CurrentLevel = 100, Gender = 2 };
        var enc = EncounterMovesetGenerator.GenerateEncounters(template, tr, ReadOnlyMemory<ushort>.Empty, GameVersion.E).OfType<EncounterStatic3>().Single(e => e.Species == old.Species && e.Version == GameVersion.E);
        var pk = enc.ConvertToPKM(tr, EncounterCriteria.Unrestricted);
        SetSpread(pk, sp);
        Notes.Add($"{ko.Species[old.Species]}: 에메랄드({language}) {name} {tid:00000}/{sid:00000} — 보이는 ID 로 시드, {idFrames} 프레임 뒤 비밀 ID; 전원 켠 뒤 {frame} 프레임(약 {frame / 59.7275 / 60:0.0}분)에 조우, Method 1 원점 {sp.Origin:X8}");
        return MoveUp(pk, old, Day(opt.From, opt.To), sp.Origin);
    }

    // Platinum seeds its generator from the clock and the delay when the game is continued, and draws the nature before the personality value.
    // A new game takes both ids from a Mersenne Twister seeded the same way.
    private PK7 Platinum(PK7 old)
    {
        var language = (LanguageID)old.Language;
        bool korean = language == LanguageID.Korean;
        string name = NameFor(old, korean || language == LanguageID.Japanese ? 5 : 7);
        // The game's release to a month before the key item was handed out; then the days it was handed out and a month after.
        var (startFrom, startTo, metFrom, metTo) = (old.Species, korean) switch
        {
            (491, true) => (D(2009, 7, 2), D(2009, 10, 21), D(2009, 11, 21), D(2010, 1, 26)),   // Member Card, Korea: 2009-11-21 to 12-27
            (492, false) => (D(2008, 9, 13), D(2009, 3, 18), D(2009, 4, 18), D(2009, 6, 10)),   // Oak's Letter, Japan: 2009-04-18 to 05-11
            _ => throw new InvalidOperationException("that key item was not handed out to games of this language"),
        };
        ushort tid, sid; Old.Spread sp; DateOnly start, met; uint bh, bm, bs, bd, idSeed, h, m, s, delay, adv, initial;
        do
        {
            start = Day(startFrom, startTo); bh = (uint)rnd.Next(24); bm = (uint)rnd.Next(60); bs = (uint)rnd.Next(60); bd = (uint)rnd.Next(700, 6001);
            idSeed = ClassicEraRNG.GetInitialSeed((uint)start.Year, (uint)start.Month, (uint)start.Day, bh, bm, bs, bd);
            (tid, sid) = Old.Ids4(idSeed);
            met = Day(metFrom, metTo); h = (uint)rnd.Next(24); m = (uint)rnd.Next(60); s = (uint)rnd.Next(60); delay = (uint)rnd.Next(600, 3001); adv = (uint)rnd.Next(0, 31);
            initial = ClassicEraRNG.GetInitialSeed((uint)met.Year, (uint)met.Month, (uint)met.Day, h, m, s, delay);
            sp = Old.MethodJ(Old.Advance(initial, adv));
        } while (tid == 0 || Old.IsShiny34(tid, sid, sp.Pid) != Shines(old));
        var tr = new SimpleTrainerInfo(GameVersion.Pt) { OT = name, Gender = old.OriginalTrainerGender, TID16 = tid, SID16 = sid, Language = (int)language };
        string key = $"Pt/{old.Language}";
        trainers[key] = tr; began[key] = start;
        var template = new PK7 { Species = old.Species, CurrentLevel = 100, Gender = 2 };
        var enc = EncounterMovesetGenerator.GenerateEncounters(template, tr, ReadOnlyMemory<ushort>.Empty, GameVersion.Pt).OfType<EncounterStatic4>().Single(e => e.Species == old.Species && e.Version == GameVersion.Pt);
        var pk = enc.ConvertToPKM(tr, EncounterCriteria.Unrestricted);
        pk.MetDate = met;
        SetSpread(pk, sp);
        Notes.Add($"{ko.Species[old.Species]}: 플라티나({language}) {name} {tid:00000}/{sid:00000} — 새 게임 {start:yyyy-MM-dd} {bh:00}:{bm:00}:{bs:00} 지연 {bd}; 조우 {met:yyyy-MM-dd} {h:00}:{m:00}:{s:00} 지연 {delay} 초기 시드 {initial:X8} 에서 {adv} 걸음, 버린 PID {sp.Rejected}, Method J 원점 {sp.Origin:X8}");
        return MoveUp(pk, old, old.Species == 492 ? platinumDay : Day(opt.From, opt.To), sp.Origin);
    }

    // The egg from Pokemon Ranger has its personality value drawn when it is taken from the deliveryman, and drawn again
    // only if that would be shiny for whoever takes it. Traded to somebody for whom the value is shiny, it hatches shiny.
    private PK7 Manaphy(PK7 old)
    {
        string key = $"Pt/{old.Language}";
        if (!trainers.TryGetValue(key, out var hatcher)) throw new InvalidOperationException("whoever hatches it has not been made yet");
        var firstDay = began[key].AddDays(30) > D(2009, 1, 1) ? began[key].AddDays(30) : D(2009, 1, 1);
        var traded = Day(firstDay, D(2010, 9, 17)); // up to the day before the fifth generation came out in Japan
        ushort tidA, sidA; uint initial, adv; Old.Spread sp; DateOnly taken; uint h, m, s, delay;
        do
        {
            var start = Day(D(2006, 9, 28), D(2008, 12, 31));
            var idSeed = ClassicEraRNG.GetInitialSeed((uint)start.Year, (uint)start.Month, (uint)start.Day, (uint)rnd.Next(24), (uint)rnd.Next(60), (uint)rnd.Next(60), (uint)rnd.Next(700, 6001));
            (tidA, sidA) = Old.Ids4(idSeed);
            taken = Day(traded.AddDays(-30), traded); h = (uint)rnd.Next(24); m = (uint)rnd.Next(60); s = (uint)rnd.Next(60); delay = (uint)rnd.Next(600, 3001); adv = (uint)rnd.Next(0, 301);
            initial = ClassicEraRNG.GetInitialSeed((uint)taken.Year, (uint)taken.Month, (uint)taken.Day, h, m, s, delay);
            sp = Old.Method1(Old.Advance(initial, adv));
        } while (Old.IsShiny34(hatcher.TID16, hatcher.SID16, sp.Pid) != Shines(old) || Old.IsShiny34(tidA, sidA, sp.Pid));
        var template = new PK7 { Species = 490, Gender = 2, CurrentLevel = 100 };
        var egg = EncounterMovesetGenerator.GenerateEncounters(template, hatcher, ReadOnlyMemory<ushort>.Empty, GameVersion.Pt).OfType<PGT>().Single(g => g.IsManaphyEgg);
        // Asked for a shiny one, PKHeX hands over the egg as it is once traded and hatched, which is how this one came to be whatever its colour;
        // asked for a plain one it hands over the egg unhatched. The values it draws are replaced just below.
        var pk = egg.ConvertToPKM(hatcher, EncounterCriteria.Unrestricted with { Shiny = Shiny.Always });
        SetSpread(pk, sp);
        pk.EggMetDate = traded; pk.MetDate = traded;
        pk.CurrentLevel = 1;
        Notes.Add($"마나피: 레인저의 알 — 받은 쪽 {tidA:00000}/{sidA:00000} 이 {taken:yyyy-MM-dd} {h:00}:{m:00}:{s:00} 지연 {delay} 초기 시드 {initial:X8} 에서 {adv} 걸음 뒤 수령, Method 1 원점 {sp.Origin:X8}; {traded:yyyy-MM-dd} 에 {hatcher.OT} {hatcher.TID16:00000}/{hatcher.SID16:00000} 에게 건너가 부화");
        return MoveUp(pk, old, platinumDay, sp.Origin);
    }

    // Crystal on the Virtual Console. Shininess is in the DVs; what it becomes on the way up is drawn then. The console is of the game's region.
    private PK7 Crystal(PK7 old)
    {
        ushort tid = draw.Id16();
        var english = new SimpleTrainerInfo(GameVersion.C) { OT = "KRIS", Gender = old.OriginalTrainerGender, TID16 = tid, Language = (int)LanguageID.English };
        var template = new PK7 { Species = old.Species, CurrentLevel = 100, Gender = 2 };
        var enc = EncounterMovesetGenerator.GenerateEncounters(template, english, ReadOnlyMemory<ushort>.Empty, GameVersion.C).OfType<EncounterStatic2>().Single(e => e.Species == old.Species);
        var lent = enc.ConvertToPKM(english, EncounterCriteria.Unrestricted);
        // PKHeX writes the generated one as a Western game's; the Japanese game's is laid out differently, so it is written afresh.
        var pk = new PK2(jp: true)
        {
            Species = old.Species, CurrentLevel = enc.LevelMin, TID16 = tid,
            OriginalTrainerFriendship = lent.OriginalTrainerFriendship,
            Move1 = lent.Move1, Move2 = lent.Move2, Move3 = lent.Move3, Move4 = lent.Move4,
            Move1_PP = lent.Move1_PP, Move2_PP = lent.Move2_PP, Move3_PP = lent.Move3_PP, Move4_PP = lent.Move4_PP,
        };
        pk.SetNotNicknamed((int)LanguageID.Japanese);
        pk.OriginalTrainerName = ForeignNames.Of(opt, old.OriginalTrainerName);
        pk.OriginalTrainerGender = old.OriginalTrainerGender;
        pk.MetLevel = enc.LevelMin; pk.MetLocation = enc.Location; pk.MetTimeOfDay = rnd.Next(1, 4);
        int[] shinyAttack = [2, 3, 6, 7, 10, 11, 14, 15];
        if (Shines(old)) { pk.IV_ATK = shinyAttack[rnd.Next(shinyAttack.Length)]; pk.IV_DEF = 10; pk.IV_SPE = 10; pk.IV_SPC = 10; }
        else
        {
            // Any values but the ones that make it shiny.
            do { pk.IV_ATK = rnd.Next(16); pk.IV_DEF = rnd.Next(16); pk.IV_SPE = rnd.Next(16); pk.IV_SPC = rnd.Next(16); }
            while (pk.IV_DEF == 10 && pk.IV_SPE == 10 && pk.IV_SPC == 10 && shinyAttack.Contains(pk.IV_ATK));
        }
        byte prefecture = (byte)rnd.Next(2, 49);
        var from = opt.From;
        Notes.Add($"{ko.Species[old.Species]}: 크리스탈 VC(일본어) {pk.OriginalTrainerName} {tid:00000} — DV 공격 {pk.IV_ATK} 방어 {pk.IV_DEF} 스피드 {pk.IV_SPE} 특수 {pk.IV_SPC}; 일본 본체(지역 {prefecture})에서 올림");
        var up = MoveUp(pk, old, Day(from, opt.To), null, from: (0, 1, prefecture));
        // Its individual values are drawn on the way up and hang on nothing: where all six were asked for, it has all six.
        if (opt.Ivs == IvChoice.Six)
        {
            var six = (PK7)up.Clone();
            for (int i = 0; i < 6; i++) six.SetIV(i, 31);
            six.RefreshChecksum();
            if (new LegalityAnalysis(six).Valid) up = six;
        }
        return up;
    }

    // The letters of Unown, caught in the Ruins of Alph of the trainer's own HeartGold and brought up together.
    private PK7 Letter(PK7 old)
    {
        const string key = "HG/own";
        if (!trainers.TryGetValue(key, out var gold))
        {
            string name = NameFor(old, 5);
            ushort tid, sid; DateOnly start; uint idSeed;
            do
            {
                start = Day(D(2010, 2, 4), D(2010, 12, 31)); // from the day the Korean game came out
                idSeed = ClassicEraRNG.GetInitialSeed((uint)start.Year, (uint)start.Month, (uint)start.Day, (uint)rnd.Next(24), (uint)rnd.Next(60), (uint)rnd.Next(60), (uint)rnd.Next(700, 6001));
                (tid, sid) = Old.Ids4(idSeed);
            } while (tid == 0);
            trainers[key] = gold = new SimpleTrainerInfo(GameVersion.HG) { OT = name, Gender = old.OriginalTrainerGender, TID16 = tid, SID16 = sid, Language = old.Language };
            Notes.Add($"안농 28글자: 하트골드({(LanguageID)old.Language}) {name} {tid:00000}/{sid:00000} — 새 게임 {start:yyyy-MM-dd}, 시드 {idSeed:X8}; 알프의 유적에서 잡아 {lettersDay:yyyy-MM-dd} 에 함께 올림");
        }
        var template = new PK7 { Species = 201, Form = old.Form, Gender = 2, CurrentLevel = 100 };
        var encs = EncounterMovesetGenerator.GenerateEncounters(template, gold, ReadOnlyMemory<ushort>.Empty, GameVersion.HG)
                       .OfType<EncounterSlot4>().Where(e => e.Shiny != Shiny.Never && e.LevelMin == old.MetLevel).ToList();
        Exception? last = null;
        foreach (var enc in encs)
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var criteria = EncounterCriteria.Unrestricted with { Shiny = Shines(old) ? Shiny.Always : Shiny.Never, Nature = draw.Nature(), LevelMin = enc.LevelMin, LevelMax = enc.LevelMin, Form = (sbyte)old.Form };
                PK4 pk;
                try { pk = (PK4)enc.ConvertToPKM(gold, criteria); } catch (Exception ex) { last = ex; continue; }
                if (pk.Form != old.Form || pk.IsShiny != Shines(old)) continue;
                try { return MoveUp(pk, old, lettersDay, null); } catch (Exception ex) { last = ex; }
            }
        throw new InvalidOperationException($"no letter {old.Form} could be caught" + (last is null ? "" : ": " + last.Message));
    }
}

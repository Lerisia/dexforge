using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>One made Pokémon with how it came to be, for the record.</summary>
public sealed record Made9(Entry9 Entry, PK9 Pk, string How, ulong? Seed, bool Legal, string Report);

/// <summary>
/// Makes each entry of the Scarlet dex: a wild catch drawn from a seed the way the game draws one (a mass outbreak hunt,
/// shiny, the scale asked for), a static as the game fixes it, a hatch for the starters, a raid or a trade where nothing
/// else gives the species; then evolves it as needed, in the owner's ball, on a day of the period.
/// </summary>
public sealed class Maker9(SimpleTrainerInfo trainer, Random random, Options9 opt)
{
    /// <summary>The trainer's own Violet: the same name, its own ids. What only Violet has is caught there and traded over.</summary>
    public SimpleTrainerInfo Violet { get; } = new(GameVersion.VL) { OT = trainer.OT, Gender = trainer.Gender, Language = trainer.Language, ID32 = (uint)random.Next(0, 4295) * 1_000_000u + (uint)random.Next(0, 1_000_000) };

    /// <summary>A day of the period. The console's clock is the player's to set, so no day is held back for a DLC's release (owner, 2026-10-05).</summary>
    private DateOnly Day(Entry9 e) => opt.From.AddDays(random.Next(opt.To.DayNumber - opt.From.DayNumber + 1));
    private Scale9 Scale => opt.Size switch { SizeChoice.Smallest => Scale9.Smallest, SizeChoice.Largest => Scale9.Largest, _ => Scale9.Random };

    private byte? WantedGender(ushort species, byte form)
    {
        var pi = Plan9.Table.GetFormEntry(species, form);
        if (pi.Genderless || pi.OnlyFemale || pi.OnlyMale) return null;
        return opt.Sex switch { SexChoice.Male => 0, SexChoice.Female => 1, _ => null };
    }

    public Made9 Make(Entry9 e)
    {
        try { return MakeOne(e); }
        catch (InvalidOperationException ex) { return new Made9(e, new PK9(), "", null, false, ex.Message); }
    }

    private Made9 MakeOne(Entry9 e)
    {
        var catcher = e.Violet ? Violet : trainer;
        PK9 pk; string how; ulong? seed = null;
        // the sex the entry or the evolution insists on, else the one asked for
        byte? gender = e.Gender ?? Evolve9.NeededGender(e.FromSpecies, e.FromForm, e.Species, e.Form) ?? WantedGender(e.FromSpecies, e.FromForm);
        var criteria = EncounterCriteria.Unrestricted with { Gender = gender is { } g ? (g == 0 ? Gender.Male : Gender.Female) : Gender.Random };
        bool wantShiny = opt.Shiny && !e.ShinyLocked && e.Template.Shiny != Shiny.Never;
        switch (e.Source)
        {
            case Source9.Wild:
            {
                var slot = (EncounterSlot9)e.Template;
                pk = slot.ConvertToPKM(catcher, criteria);
                if (slot.IsRandomUnspecificForm) pk.Form = e.FromForm;
                // the lowest level the slot gives, and the moves it has there
                SetLevel(pk, slot.LevelMin);
                seed = Spawn9.Find(pk, e.FromSpecies, e.FromForm, wantShiny, Scale, random, gender, EcRule(e))
                    ?? throw new InvalidOperationException("시드를 찾지 못했습니다.");
                if (Scale == Scale9.Smallest) pk.RibbonMarkMini = true;
                if (Scale == Scale9.Largest) pk.RibbonMarkJumbo = true;
                how = $"야생 {Plan9.Ko.GetLocationName(false, slot.Location, 9, 9, GameVersion.SL)} Lv{slot.LevelMin}" + (e.Violet ? " (바이올렛에서 잡아 교환)" : "");
                break;
            }
            case Source9.Egg:
            {
                var egg = (EncounterEgg9)e.Template;
                pk = egg.ConvertToPKM(trainer, criteria with { Shiny = wantShiny ? Shiny.Always : Shiny.Never });
                how = "알";
                break;
            }
            default:
            {
                var t = (IEncounterConvertible)e.Template;
                var crit = criteria with { Shiny = wantShiny ? Shiny.Always : Shiny.Never };
                pk = (PK9)t.ConvertToPKM(catcher, crit);
                for (int i = 0; i < 20 && wantShiny && !pk.IsShiny; i++) pk = (PK9)t.ConvertToPKM(catcher, crit);
                if (wantShiny && !pk.IsShiny) throw new InvalidOperationException("이로치가 나오지 않습니다.");
                how = e.Source switch { Source9.Static => "고정", Source9.Raid => "레이드", Source9.Trade => "게임 안 교환", _ => e.Source.ToString() }
                    + (e.Template is ILocation l && e.Source != Source9.Raid ? " " + Plan9.Ko.GetLocationName(false, l.Location, 9, 9, GameVersion.SL) : "")
                    + (e.Violet ? " (바이올렛에서 잡아 교환)" : "");
                break;
            }
        }

        var day = Day(e);
        if (e.Source == Source9.Egg) { pk.EggMetDate = day; pk.MetDate = day.AddDays(random.Next(3)) is var d && d <= opt.To ? d : day; }
        else if (e.Source == Source9.Raid)
        {
            // an event raid: a day it ran, inside the period where the two overlap, else a day it ran at all
            var inside = e.RaidWindows.Select(w => (From: w.From > opt.From ? w.From : opt.From, To: w.To < opt.To ? w.To : opt.To)).Where(w => w.From <= w.To).ToList();
            var windows = inside.Count > 0 ? inside : e.RaidWindows.ToList();
            var w = windows[random.Next(windows.Count)];
            pk.MetDate = w.From.AddDays(random.Next(w.To.DayNumber - w.From.DayNumber + 1));
            how += $" {pk.MetDate:yyyy-MM-dd}" + (inside.Count == 0 ? " (레이드 기간이 고른 기간 밖이라 그 기간의 날)" : "");
        }
        else pk.MetDate = day;
        if (e.Violet) pk.UpdateHandler(trainer);

        // the balls the owner wants, best first, the first the game allows
        if (e.Template.FixedBall == Ball.None)
        {
            var first = Balls.Choose(pk, Balls.Shared.Wanted(opt.Ball, e.Species, e.Form, pk.Gender == 2 ? null : pk.Gender, pk.IsShiny));
            if ((Ball)pk.Ball != first) how += $" (볼 {Plan9.Ko.balllist[(int)first]} 불가 → {Plan9.Ko.balllist[pk.Ball]})";
        }

        if (e.Evolves) Evolve9.Evolve(pk, e.Species, e.Form, trainer, Violet, random);
        if (opt.Level == LevelChoice.Hundred) { pk.CurrentLevel = 100; pk.ResetPartyStats(); }
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        return new Made9(e, pk, how, seed, la.Valid, la.Valid ? "" : la.Report());
    }

    private readonly Dictionary<string, DateOnly> eventDays = new();

    /// <summary>
    /// A distribution this trainer received on a day of its window (what one code handed over together on one day), and the
    /// final evolutions the event box's rules add to it. A card only Violet could receive is received in the trainer's own
    /// Violet and traded over.
    /// </summary>
    public List<Made9> MakeEvent(Event9 ev)
    {
        // a gift rolled at receipt (Mew's Tera type) is one of its rolls
        var wc = ev.Variants.Count > 1 ? ev.Variants[random.Next(ev.Variants.Count)] : ev.Card;
        var receiver = ev.Violet ? Violet : trainer;
        var pk = (PK9)wc.ConvertToPKM(receiver, EncounterCriteria.Unrestricted);
        if (ev.Violet) pk.UpdateHandler(trainer);
        if (!eventDays.TryGetValue(ev.Group, out var day)) eventDays[ev.Group] = day = GroupDay(ev);
        // PKHeX knows when each card was really handed out (with a day's tolerance); the sources' window is kept to that
        if (wc.GetDistributionWindow(out var known) && !wc.IsWithinDistributionWindow(day))
        {
            var knownEnd = known.End ?? known.Start.AddYears(1);
            var lo = known.Start > ev.Start ? known.Start : ev.Start;
            var hi = ev.End is { } e9 && e9 < knownEnd ? e9 : knownEnd;
            day = lo <= hi ? lo.AddDays(random.Next(hi.DayNumber - lo.DayNumber + 1)) : known.Start;
            for (int i = 0; i < 400 && !wc.IsWithinDistributionWindow(day); i++) day = day.AddDays(1);
            eventDays[ev.Group] = day;
        }
        pk.MetDate = day;
        // a card's trainer id changed with patch 2.0.0 (2023-09-13): what the day says it was
        if (wc.OTGender < 2) pk.ID32 = day.DayNumber <= new DateOnly(2023, 9, 13).DayNumber ? wc.ID32Old : wc.ID32;
        pk.RefreshChecksum();
        string when = ev.End is { } end ? $"{ev.Start:yyyy-MM-dd}~{end:yyyy-MM-dd}" : $"{ev.Start:yyyy-MM-dd}~";
        var entry = new Entry9(wc.Species, wc.Form, Source9.Card, wc.Species, wc.Form, wc, ev.Violet, !wc.IsShiny, ev.Title);
        var la = new LegalityAnalysis(pk);
        var made = new List<Made9> { new(entry, pk, $"배포 카드 {ev.Title} ({ev.Region} {when}){(ev.Violet ? " 바이올렛에서 받아 교환" : "")}", null, la.Valid, la.Valid ? "" : la.Report()) };
        foreach (var (sp, f) in ev.Evolutions)
        {
            // a final form the received Pokémon cannot reach: Oinkologne's form is its sex, Maushold's its encryption constant
            if (sp == 916 && f != pk.Gender) continue;
            if (sp == 925 && (pk.EncryptionConstant % 100 == 0) != (f == 0)) continue;
            if (sp == 982 && (pk.EncryptionConstant % 100 == 0) != (f == 1)) continue;
            var e2 = new Entry9(sp, f, Source9.Card, wc.Species, wc.Form, wc, ev.Violet, !wc.IsShiny, ev.Title);
            var pk2 = (PK9)pk.Clone();
            try
            {
                byte target = Evolve9.CanReach(pk2.Species, pk2.Form, sp, f) ? f : (byte)0;
                Evolve9.Evolve(pk2, sp, target, trainer, Violet, random);
                if (pk2.Form != f) Evolve9.ChangeForm(pk2, f);
            }
            catch (InvalidOperationException ex) { made.Add(new Made9(e2, pk2, made[0].How, null, false, ex.Message)); continue; }
            pk2.RefreshChecksum();
            var la2 = new LegalityAnalysis(pk2);
            made.Add(new Made9(e2, pk2, made[0].How + " → " + Plan9.Label(sp, f), null, la2.Valid, la2.Valid ? "" : la2.Report()));
        }
        return made;
    }

    /// <summary>
    /// A day for what one code handed over together: inside every member's window (the sources' and PKHeX's own), so the
    /// day holds for each of them; a card alone gets a day of its own window.
    /// </summary>
    private DateOnly GroupDay(Event9 ev)
    {
        var members = Events9.All.Where(e => e.Group == ev.Group).ToList();
        if (members.Count == 1) return ev.Day(random);
        var lo = members.Max(e => e.Start);
        var hi = members.Min(e => e.End ?? e.Start.AddYears(1));
        foreach (var m in members)
        {
            if (!m.Card.GetDistributionWindow(out var w)) continue;
            if (w.Start > lo) lo = w.Start;
            if (w.End is { } end && end.AddDays(-1) < hi) hi = end.AddDays(-1);
        }
        if (lo > hi) return ev.Day(random);
        for (int i = 0; i < 200; i++)
        {
            var day = lo.AddDays(random.Next(hi.DayNumber - lo.DayNumber + 1));
            if (members.All(m => !m.Card.GetDistributionWindow(out _) || m.Card.IsWithinDistributionWindow(day))) return day;
        }
        return lo;
    }

    /// <summary>Maushold and Dudunsparce take the form their encryption constant decides (one in a hundred is the rare one): the catch must already have the right one.</summary>
    private static Func<PK9, bool>? EcRule(Entry9 e) => e.Species switch
    {
        // PKHeX: rare = EC % 100 == 0 → Maushold Family of Three (form 0), Dudunsparce Three-Segment (form 1)
        925 => pk => (pk.EncryptionConstant % 100 == 0) == (e.Form == 0),
        982 => pk => (pk.EncryptionConstant % 100 == 0) == (e.Form == 1),
        _ => null,
    };

    /// <summary>The level met and current, and the moves a wild catch has at it.</summary>
    private static void SetLevel(PK9 pk, byte level)
    {
        pk.MetLevel = level; pk.CurrentLevel = level; pk.ObedienceLevel = level;
        var learn = LearnSource9SV.Instance.GetLearnset(pk.Species, pk.Form);
        Span<ushort> moves = stackalloc ushort[4];
        learn.SetEncounterMovesBackwards(level, moves, sameDescend: false);
        pk.SetMoves(moves);
        pk.HealPP();
        pk.ResetPartyStats();
    }
}

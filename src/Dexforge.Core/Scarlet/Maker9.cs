using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>One made Pokémon with how it came to be, for the record.</summary>
public sealed record Made9(Entry9 Entry, PK9 Pk, string How, ulong? Seed, bool Legal, string Report);

/// <summary>
/// Makes each entry of the Scarlet dex: a wild catch drawn from a seed the way the game draws one (a mass outbreak hunt,
/// shiny, the scale asked for), a static as the game fixes it, a hatch for the starters, a raid or a trade where nothing
/// else gives the species; then evolves it as needed, in the owner's ball, on a day of the period.
/// </summary>
public sealed class Maker9(SimpleTrainerInfo trainer, Balls9 balls, Random random, Options9 opt)
{
    /// <summary>The trainer's own Violet: the same name, its own ids. What only Violet has is caught there and traded over.</summary>
    public SimpleTrainerInfo Violet { get; } = new(GameVersion.VL) { OT = trainer.OT, Gender = trainer.Gender, Language = trainer.Language, ID32 = (uint)random.Next(0, 4295) * 1_000_000u + (uint)random.Next(0, 1_000_000) };

    public static readonly DateOnly TealMask = new(2023, 9, 13), IndigoDisk = new(2023, 12, 14), Epilogue = new(2024, 1, 11);

    /// <summary>The legendaries Snacksworth's treats bring out, in Paldea's own places but only once The Indigo Disk is there.</summary>
    private static readonly HashSet<ushort> Snacksworth = [144, 145, 146, 243, 244, 245, 249, 250, 380, 381, 382, 383, 384, 638, 639, 640, 643, 644, 646, 791, 792, 800, 891, 896, 897];

    /// <summary>The first day a catch could have happened: the game's release, or the DLC's that holds the place or the Pokémon.</summary>
    public static DateOnly NotBefore(Entry9 e)
    {
        if (e.FromSpecies == 1025) return Epilogue;                                   // Pecharunt: the epilogue
        if (Snacksworth.Contains(e.FromSpecies) && e.Source == Source9.Static) return IndigoDisk;
        if (e.Template is ILocation l)
        {
            if (l.Location is >= 174 and <= 198) return IndigoDisk;                    // the Terarium and the Underdepths
            if (l.Location is >= 132 and <= 170) return TealMask;                      // Kitakami
        }
        return Making9.Released;
    }

    /// <summary>A day of the period, not before the catch could have happened.</summary>
    private DateOnly Day(Entry9 e)
    {
        var from = NotBefore(e) > opt.From ? NotBefore(e) : opt.From;
        if (from > opt.To) throw new InvalidOperationException($"고른 기간이 {Plan9.Label(e.FromSpecies, e.FromForm)}을(를) 얻을 수 있게 된 {from:yyyy-MM-dd} 보다 앞섭니다.");
        return from.AddDays(random.Next(opt.To.DayNumber - from.DayNumber + 1));
    }
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
        // the sex the evolution needs, else the one asked for
        byte? gender = Evolve9.NeededGender(e.FromSpecies, e.FromForm, e.Species, e.Form) ?? WantedGender(e.FromSpecies, e.FromForm);
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

        // the ball the owner picked, when the Pokémon can be in it
        if (e.Template.FixedBall == Ball.None)
        {
            var wanted = (opt.Ball is { } one ? new[] { (Ball)one } : balls.For(e.Species, e.Form, pk.Gender == 2 ? null : pk.Gender) is { } picked ? new[] { picked } : []).Append(Ball.Poke).Distinct().ToArray();
            foreach (var b in wanted)
            {
                pk.Ball = (byte)b; pk.RefreshChecksum();
                if (new LegalityAnalysis(pk).Valid) break;
            }
            if ((Ball)pk.Ball != wanted[0]) how += $" (볼 {Plan9.Ko.balllist[(int)wanted[0]]} 불가 → {Plan9.Ko.balllist[pk.Ball]})";
        }

        if (e.Evolves) Evolve9.Evolve(pk, e.Species, e.Form, trainer, Violet, random);
        if (opt.Level == LevelChoice.Hundred) { pk.CurrentLevel = 100; pk.ResetPartyStats(); }
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        return new Made9(e, pk, how, seed, la.Valid, la.Valid ? "" : la.Report());
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

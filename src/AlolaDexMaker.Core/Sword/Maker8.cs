using PKHeX.Core;

namespace AlolaDexMaker.Sword;

/// <summary>One made Pokémon with how it came to be, for the record.</summary>
public sealed record Made8(Entry Entry, PK8 Pk, string How, ulong? Seed, bool Legal, string Report);

/// <summary>
/// Makes each entry: hatches, revives, catches or receives the first stage the way the game draws it, then evolves and
/// changes form as needed, dates it in 2021, and checks it with PKHeX.
/// </summary>
public sealed class Maker8(SimpleTrainerInfo trainer, SimpleTrainerInfo friend, Balls8 balls, Random random, int year, bool shiny, Ball? oneBall)
{
    /// <summary>The owner's HOME profile: the same name, its own id. HOME gifts are received in it, then handled by the game's trainer.</summary>
    private readonly SimpleTrainerInfo home = new(GameVersion.SW) { OT = trainer.OT, Gender = trainer.Gender, Language = trainer.Language, ID32 = (uint)random.Next(0, 4295) * 1_000_000u + (uint)random.Next(0, 1_000_000) };

    private const int ForeignLanguage = 1; // the Ditto is Japanese: a different language from the trainer makes the Masuda method
    public const int DefaultYear = 2021;
    public int Year => year;

    public static readonly int[] SixV = [31, 31, 31, 31, 31, 31];

    private DateOnly Day() => new DateOnly(Year, 1, 1).AddDays(random.Next(DateTime.IsLeapYear(Year) ? 366 : 365));

    public Made8 Make(Entry first)
    {
        Made8? last = null;
        foreach (var e in new[] { first }.Concat(first.Fallbacks))
        {
            try
            {
                last = MakeOne(e);
                if (last.Legal) return last;
            }
            catch (InvalidOperationException ex)
            {
                last = new Made8(e, new PK8(), "", null, false, ex.Message);
            }
        }
        return last!;
    }

    private Made8 MakeOne(Entry e)
    {
        PK8 pk;
        string how;
        ulong? seed = null;
        // The one ball asked for, else the ball picked for this species, and a Poke Ball to fall back on.
        Ball[] wanted = (e.BallOverride is { } ob ? new[] { ob } : oneBall is { } one ? new[] { one } : balls.For(e.Species, e.Form) is { } picked ? new[] { picked } : [])
            .Append(Ball.Poke).Distinct().ToArray();
        switch (e.Source)
        {
            case Source.Egg:
                (pk, how, seed) = Hatch(e, wanted);
                break;
            case Source.Card:
                var wc = (WC8)e.Template!;
                how = "배포 카드 " + wc.CardTitle;
                if (wc.IsHOMEGift)
                {
                    pk = (PK8)wc.ConvertToPKM(home, EncounterCriteria.Unrestricted);
                    pk.UpdateHandler(trainer);
                    if (Plan8.Trackers.TryGetValue(e.Species, out var tracker)) { pk.Tracker = tracker; how += $" (HOME 트래커 {tracker:X16})"; }
                }
                else
                {
                    pk = (PK8)wc.ConvertToPKM(trainer, EncounterCriteria.Unrestricted);
                }
                break;
            default:
            {
                var t = (IEncounterConvertible)e.Template!;
                bool wantShiny = shiny && e.Shiny && e.Template!.Shiny != Shiny.Never;
                var criteria = EncounterCriteria.Unrestricted with { Shiny = wantShiny ? Shiny.Always : Shiny.Never };
                pk = AsPk8(t.ConvertToPKM(trainer, criteria));
                for (int i = 0; i < 50 && wantShiny && !pk.IsShiny; i++)
                    pk = AsPk8(t.ConvertToPKM(trainer, criteria));
                how = Describe(e.Source) + (e.Template is ILocation l ? " " + Plan8.Ko.GetLocationName(false, l.Location, 8, 8, GameVersion.SW) : "");
                break;
            }
        }

        // Dates: everything in the one year.
        if (e.Source == Source.Go)
        {
            // Inside the encounter's window, in the year if the window reaches it.
            var go = (EncounterSlot8GO)e.Template!;
            var (from, to) = GoWindow(go);
            var lo = from < new DateOnly(Year, 1, 1) ? new DateOnly(Year, 1, 1) : from;
            var hi = to > new DateOnly(Year, 12, 31) ? new DateOnly(Year, 12, 31) : to;
            if (lo <= hi) pk.MetDate = lo.AddDays(random.Next(hi.DayNumber - lo.DayNumber + 1));
            if (Plan8.Trackers.TryGetValue(e.Species, out var tracker)) { pk.Tracker = tracker; how += $" (HOME 트래커 {tracker:X16})"; }
        }
        else if (e.Source == Source.Egg)
        {
            var egg = Day();
            pk.EggMetDate = egg;
            pk.MetDate = egg.AddDays(random.Next(4)) is var d && d.Year == Year ? d : egg;
        }
        else
        {
            var before = pk.MetDate;
            pk.RefreshChecksum();
            bool wasValid = e.Source != Source.Card || new LegalityAnalysis(pk).Valid;
            pk.MetDate = Day();
            // A card with a distribution window keeps its own date when the year falls outside it.
            if (e.Source == Source.Card)
            {
                pk.RefreshChecksum();
                if (wasValid && !new LegalityAnalysis(pk).Valid) { pk.MetDate = before; how += $" (배포 기간 밖이라 날짜 {before:yyyy-MM-dd})"; }
            }
        }

        // The ball the owner wants, when the Pokémon can be in it; eggs already got it from the parent.
        if (e.Source != Source.Egg && e.Template!.FixedBall == Ball.None)
        {
            foreach (var b in wanted)
            {
                pk.Ball = (byte)b;
                pk.RefreshChecksum();
                if (new LegalityAnalysis(pk).Valid) break;
            }
        }
        if ((Ball)pk.Ball != wanted[0]) how += $" (볼 {Plan8.Ko.balllist[(int)wanted[0]]} 불가 → {Plan8.Ko.balllist[pk.Ball]})";

        Finish(pk, e);

        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        return new Made8(e, pk, how, seed, la.Valid, la.Valid ? "" : la.Report());
    }

    /// <summary>Evolves and changes form as the entry asks: to the wanted species (at the form reachable), then the form itself if it is a form change.</summary>
    private void Finish(PK8 pk, Entry e)
    {
        if (e.Evolves)
        {
            byte target = Evolve8.CanReach(pk.Species, pk.Form, e.Species, e.Form) ? e.Form : (byte)0;
            Evolve8.Evolve(pk, e.Species, target, trainer, friend, random);
        }
        if (pk.Form != e.Form)
            Evolve8.ChangeForm(pk, e.Form);
    }

    /// <summary>An egg from the first stage's species and a Japanese 6V Ditto holding a Destiny Knot, drawn with the game's egg RNG until it is shiny.</summary>
    private (PK8, string, ulong) Hatch(Entry e, Ball[] wanted)
    {
        var egg = new EncounterEgg8(e.FromSpecies, e.FromForm, GameVersion.SW);
        var pk = egg.ConvertToPKM(trainer);
        var pi = Plan8.Table.GetFormEntry(e.FromSpecies, e.FromForm);

        // The species parent: the egg's ball and ability come from it.
        byte parentGender = pi.Genderless ? (byte)2 : pi.OnlyFemale ? (byte)1 : pi.OnlyMale ? (byte)0 : (byte)random.Next(2);
        int parentAbility = pi.GetIndexOfAbility(pk.Ability) is >= 0 and var ai ? ai : 0;
        var parent = new Parent(e.FromSpecies, parentGender, parentAbility, (Nature)random.Next(25), RandomIvs(), Ball.Poke, 0, trainer.Language);
        var ditto = new Parent(132, 2, 0, Nature.Hardy, SixV, Ball.Poke, 280, ForeignLanguage);

        // What the egg must come out as for this entry.
        byte? needGender = NeededGender(e);
        Func<Nature, bool>? needNature = NeededNature(e);

        // Probe: one egg of any colour that meets the entry's conditions, finished and checked, so an impossible path is found out at once.
        {
            var probe = egg.ConvertToPKM(trainer);
            Hatched ph;
            ulong ps = 0x1234_5678_9ABC_DEF0UL;
            for (int i = 0; ; i++, ps += 0x9E3779B97F4A7C15UL)
            {
                ph = Egg8.Generate(ps, parent with { Ball = wanted[0] }, ditto, e.FromSpecies, e.FromForm, pi.Gender, trainer.ID32, false);
                if (Fits(ph, e, needGender, needNature)) break;
                if (i > 10_000) throw new InvalidOperationException("알의 성별·성격·폼 조건을 맞출 수 없습니다.");
            }
            Apply(probe, ph, e);
            probe.EggMetDate = probe.MetDate = new DateOnly(Year, 6, 1);
            Finish(probe, e);
            probe.RefreshChecksum();
            var pl = new LegalityAnalysis(probe);
            if (!pl.Valid && !pl.Report().Contains("shiny", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("알로는 안 됨: " + string.Join(" | ", pl.Report().Split((char)10).Where(l => l.Contains("Invalid"))));
        }

        Hatched h;
        ulong seed;
        var why = new Dictionary<string, int>();
        for (int tries = 0; ; tries++)
        {
            if (tries > 200_000) throw new InvalidOperationException($"{Plan8.Ko.specieslist[e.Species]}: 조건에 맞는 알 시드를 찾지 못했습니다 ({string.Join(", ", why.Select(kv => kv.Key + " " + kv.Value))}).");
            seed = (ulong)random.NextInt64() ^ ((ulong)random.Next() << 32);
            foreach (var b in wanted)
            {
                var p = parent with { Ball = b };
                h = Egg8.Generate(seed, p, ditto, e.FromSpecies, e.FromForm, pi.Gender, trainer.ID32, shinyCharm: false);
                if (h.Shiny != (shiny && e.Shiny)) { why["색"] = why.GetValueOrDefault("색") + 1; break; }
                if (!Fits(h, e, needGender, needNature)) { why["성별·성격·폼"] = why.GetValueOrDefault("성별·성격·폼") + 1; break; }
                Apply(pk, h, e);
                pk.RefreshChecksum();
                var check = new LegalityAnalysis(pk);
                if (check.Valid)
                    goto done;
                var key = $"[{pk.Species}-{pk.Form} ball {pk.Ball} ab# {pk.AbilityNumber} g {pk.Gender} h {pk.HeightScalar}] " + string.Join("|", check.Report().Split((char)10).Where(l => l.Contains("Invalid")));
                why[key] = why.GetValueOrDefault(key) + 1;
            }
        }
        done:
        return (pk, $"알 (부모 {Plan8.Ko.specieslist[e.FromSpecies]} × 일본산 메타몽, 시드 {seed:X16})", seed);
    }

    /// <summary>Whether a hatched one is what the entry needs: the species the egg was for, the gender, the nature, the form that is a gender.</summary>
    private static bool Fits(Hatched h, Entry e, byte? needGender, Func<Nature, bool>? needNature)
    {
        if (h.Species != e.FromSpecies) return false;
        if (needGender is { } g && h.Gender != g) return false;
        if (needNature is not null && !needNature(h.Nature)) return false;
        if (e.FromSpecies == 876 && h.Form != e.FromForm) return false; // Indeedee: the form is the gender
        return true;
    }

    private void Apply(PK8 pk, Hatched h, Entry e)
    {
        pk.EncryptionConstant = h.Ec;
        pk.PID = h.Pid;
        pk.IV_HP = h.Ivs[0]; pk.IV_ATK = h.Ivs[1]; pk.IV_DEF = h.Ivs[2]; pk.IV_SPA = h.Ivs[3]; pk.IV_SPD = h.Ivs[4]; pk.IV_SPE = h.Ivs[5];
        pk.Nature = h.Nature;
        pk.StatAlignment = h.Nature;
        pk.RefreshAbility(h.AbilityIndex);
        pk.Gender = h.Species == 876 ? h.Form : h.Gender;
        if (h.Form != pk.Form) pk.Form = h.Form;
        pk.Ball = (byte)h.Ball;
        pk.HeightScalar = (byte)(random.Next(0x81) + random.Next(0x80));
        pk.WeightScalar = (byte)(random.Next(0x81) + random.Next(0x80));
    }

    private int[] RandomIvs() => [random.Next(32), random.Next(32), random.Next(32), random.Next(32), random.Next(32), random.Next(32)];

    /// <summary>The gender the hatched one must have: forms that are a gender (Meowstic, Indeedee), evolutions that need one.</summary>
    private static byte? NeededGender(Entry e)
    {
        if (e.Species is 678 or 876) return e.Form == 1 ? (byte)1 : (byte)0;
        if (e.FromSpecies is 876) return e.FromForm == 1 ? (byte)1 : (byte)0;
        return Evolve8.NeededGender(e.FromSpecies, e.FromForm, e.Species, e.Form);
    }

    /// <summary>Toxtricity: Amped or Low Key is decided by the nature of the Toxel that evolves.</summary>
    private static Func<Nature, bool>? NeededNature(Entry e) =>
        e.Species == 849 ? n => ToxtricityUtil.GetAmpLowKeyResult(n) == e.Form : null;

    /// <summary>GO encounters come out in the Let's Go format; through HOME they become Sword's.</summary>
    private static PK8 AsPk8(PKM pkm)
    {
        if (pkm is PK8 pk8) return pk8;
        var converted = EntityConverter.ConvertToType(pkm, typeof(PK8), out var result);
        return converted as PK8 ?? throw new InvalidOperationException($"PK8 로 변환하지 못했습니다: {result}");
    }

    /// <summary>The dates the GO encounter was available, read off PKHeX's own description ("2021.07.05-2021.07.16", or "-X" for no end).</summary>
    private static (DateOnly From, DateOnly To) GoWindow(EncounterSlot8GO go)
    {
        var text = go.LongName[(go.LongName.LastIndexOf(' ') + 1)..];
        var parts = text.Split('-');
        var from = DateOnly.ParseExact(parts[0], "yyyy.MM.dd");
        var to = parts[1] == "X" ? new DateOnly(2099, 12, 31) : DateOnly.ParseExact(parts[1], "yyyy.MM.dd");
        return (from, to);
    }

    private static string Describe(Source s) => s switch
    {
        Source.Go => "Pokémon GO 에서 HOME 으로",
        Source.Fossil => "화석 복원", Source.Static => "고정 조우", Source.Gift => "선물", Source.Adventure => "다이맥스 어드벤처",
        Source.Wild => "야생", Source.Trade => "게임 안 교환", _ => s.ToString(),
    };
}

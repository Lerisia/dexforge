using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>One made Pokémon with how it came to be, for the record.</summary>
public sealed record Made9a(Entry9a Entry, PA9 Pk, string How, ulong? Seed, bool Legal, string Report);

/// <summary>
/// Makes each entry of the Z-A dex: a hyperspace or wild catch drawn from a seed the way the game draws one (shiny, alpha
/// or the size asked for), a static, a gift or a trade as the game fixes it; then evolves it or changes its form as the
/// plan says, in the owner's ball, on a day of the period.
/// </summary>
public sealed class Maker9a(SimpleTrainerInfo trainer, Random random, Options9 opt)
{
    /// <summary>The trainer's other Z-A (the second console): the same name, its own ids. A trade evolution goes there and comes back.</summary>
    public SimpleTrainerInfo Other { get; } = new(GameVersion.ZA) { OT = trainer.OT, Gender = trainer.Gender, Language = trainer.Language, ID32 = (uint)random.Next(0, 4295) * 1_000_000u + (uint)random.Next(0, 1_000_000) };

    private DateOnly Day() => opt.From.AddDays(random.Next(opt.To.DayNumber - opt.From.DayNumber + 1));
    private Scale9a Scale => opt.Size switch { SizeChoice.Smallest => Scale9a.Smallest, SizeChoice.Largest => Scale9a.Largest, _ => Scale9a.Random };

    private byte? WantedGender(ushort species, byte form)
    {
        var pi = Plan9a.Table[species, form];
        if (pi.Genderless || pi.OnlyFemale || pi.OnlyMale) return null;
        return opt.Sex switch { SexChoice.Male => 0, SexChoice.Female => 1, _ => null };
    }

    public Made9a Make(Entry9a e)
    {
        try { return MakeOne(e); }
        catch (InvalidOperationException ex) { return new Made9a(e, new PA9(), "", null, false, ex.Message); }
    }

    private Made9a MakeOne(Entry9a e)
    {
        PA9 pk; string how; ulong? seed = null;
        // the sex the entry or the evolution insists on, else the one asked for
        byte? gender = e.Gender ?? Evolve9a.NeededGender(e.FromSpecies, e.FromForm, e.Species, e.Form) ?? WantedGender(e.FromSpecies, e.FromForm);
        bool wantShiny = opt.Shiny && !e.ShinyLocked;
        switch (e.Source)
        {
            case Source9a.Hyperspace or Source9a.Wild:
            {
                bool hyper = e.Source == Source9a.Hyperspace;
                bool alpha = hyper && opt.Size == SizeChoice.Alpha && Plan9a.Slot(e.FromSpecies, e.FromForm, true, gender) is not null;
                var slot = (hyper ? Plan9a.Slot(e.FromSpecies, e.FromForm, alpha, gender) : Plan9a.WildSlot(e.FromSpecies, e.FromForm, false, gender))
                    ?? throw new InvalidOperationException($"{Plan9a.Label(e.FromSpecies, e.FromForm)}은(는) 그 성별로 나오지 않습니다.");
                var criteria = Spawn9a.Criteria(slot, wantShiny, gender, random);
                pk = slot.ConvertToPKM(trainer, criteria);
                seed = Spawn9a.Find(pk, slot, criteria, Scale, random, NatureRule(e)) ?? throw new InvalidOperationException("시드를 찾지 못했습니다.");
                how = (hyper ? "이차원" : "야생 " + Plan9a.Ko.GetLocationName(false, slot.Location, 9, 9, GameVersion.ZA)) + $" Lv{slot.LevelMin}" + (alpha ? " 우두머리" : "");
                break;
            }
            default:
            {
                var t = (IEncounterConvertible)e.Template;
                var crit = EncounterCriteria.Unrestricted with { Shiny = wantShiny ? Shiny.Always : Shiny.Never, Gender = gender is { } g ? (g == 0 ? Gender.Male : Gender.Female) : Gender.Random };
                pk = (PA9)t.ConvertToPKM(trainer, crit);
                for (int i = 0; i < 20 && wantShiny && !pk.IsShiny; i++) pk = (PA9)t.ConvertToPKM(trainer, crit);
                if (wantShiny && !pk.IsShiny) throw new InvalidOperationException("이로치가 나오지 않습니다.");
                // a gift that keeps its giver's name (AZ's Floette, Magearna) was received: this trainer handles it
                if (pk.OriginalTrainerName != trainer.OT || pk.ID32 != trainer.ID32) pk.UpdateHandler(trainer);
                how = e.Source switch { Source9a.Static => "고정", Source9a.Gift => "선물", Source9a.Trade => "게임 안 교환", _ => e.Source.ToString() }
                    + (e.Template is ILocation l && l.Location != Plan9a.HyperspaceLocation ? " " + Plan9a.Ko.GetLocationName(false, l.Location, 9, 9, GameVersion.ZA) : e.Template is ILocation ? " 이차원" : "");
                break;
            }
        }
        pk.MetDate = Day();

        // the balls the owner wants, best first, the first the game allows
        if (e.Template.FixedBall == Ball.None)
        {
            var first = Balls.Choose(pk, Balls.Shared.Wanted(opt.Ball, e.Species, e.Form, pk.Gender == 2 ? null : pk.Gender, pk.IsShiny));
            if ((Ball)pk.Ball != first) how += $" (볼 {Plan9a.Ko.balllist[(int)first]} 불가 → {Plan9a.Ko.balllist[pk.Ball]})";
        }

        if (e.Evolves) Evolve9a.Evolve(pk, e.Species, e.Form, Other);
        else if (e.ChangesForm) Evolve9a.ChangeForm(pk, e.Form);
        if (opt.Level == LevelChoice.Hundred)
        {
            // levelling masters the moves learned on the way (PKHeX looks for every flag up to the level)
            pk.CurrentLevel = 100;
            var (_, plus) = LearnSource9ZA.GetLearnsetAndPlus(pk.Species, pk.Form);
            pk.SetPlusFlagsEncounter(Plan9a.Table[pk.Species, pk.Form], plus, 100);
            pk.ResetPartyStats();
        }
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        return new Made9a(e, pk, how, seed, la.Valid, la.Valid ? "" : la.Report());
    }

    /// <summary>Toxtricity's form is its nature (Amped or Low Key), decided at the catch: the seed must give one of the right kind, whether Toxtricity or the Toxel that becomes it.</summary>
    private static Func<PA9, bool>? NatureRule(Entry9a e) => e.Species == (int)Species.Toxtricity
        ? pk => ToxtricityUtil.GetAmpLowKeyResult(pk.Nature) == e.Form
        : null;
}

using System.Reflection;
using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>One made Pokémon with how it came to be, for the record.</summary>
public sealed record Made8a(Entry8a Entry, PA8 Pk, string How, ulong? Generator, ulong? FixedSeed, bool Legal, string Report);

/// <summary>
/// Makes each entry: catches the first stage the way the game draws it, then evolves or changes form as needed, dates it,
/// and checks it with PKHeX. A field catch is drawn from a generator seed that lands on its slot, so the bot's own check
/// (the seed the fields prove, a generator seed that gives it, a slot draw that picks it) holds as well as PKHeX's.
/// </summary>
public sealed class Maker8a(SAV8LA sav, SimpleTrainerInfo trainer, Random random, Options8a opt)
{
    private static readonly MethodInfo Boost = typeof(EncounterSlot8a).GetMethod("GetRollCountBoost", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
    private readonly Dictionary<int, SizeSeeds8a> samplers = [];

    private DateOnly Day() => opt.From.AddDays(random.Next(opt.To.DayNumber - opt.From.DayNumber + 1));
    private ulong Seed64() => (ulong)random.NextInt64() ^ ((ulong)random.NextInt64() << 11);

    public Made8a Make(Entry8a e)
    {
        try { return MakeOne(e); }
        catch (InvalidOperationException ex) { return new Made8a(e, new PA8(), "", null, null, false, ex.Message); }
    }

    private Made8a MakeOne(Entry8a e)
    {
        bool alpha = opt.Size == SizeChoice.Alpha && e.CanBeAlpha;
        PA8 pk; string how; ulong? generator = null, fixedSeed = null;
        byte? gender = e.Gender ?? Evolve8a.NeededGender(e.FromSpecies, e.FromForm, e.Species, e.Form);

        if (alpha && e.Alpha.Spawner is { } alphaSpawner) (pk, generator, fixedSeed, how) = Field(alphaSpawner, gender);
        else if (alpha && e.Alpha.Slot is { } alphaSlot) (pk, fixedSeed, how) = Other(alphaSlot, gender);
        else if (e.Source == Source8a.Field) (pk, generator, fixedSeed, how) = Field(e.Spawner!, gender);
        else if (e.Source == Source8a.OtherSlot) (pk, fixedSeed, how) = Other(e.Slot!, gender);
        else (pk, how) = Static(e.Static!, gender);

        if (pk.Species != e.Species)
        {
            Evolve8a.Evolve(pk, e.Species, e.Form);
            how += $" → {Plan8a.Label(e.Species, e.Form)}";
        }
        else if (pk.Form != e.Form)
        {
            Evolve8a.ChangeForm(pk, e.Form);
            how += $" → {Plan8a.FormName(e.Species, e.Form)}";
        }
        if (opt.Level == LevelChoice.Hundred) { pk.CurrentLevel = 100; pk.ResetPartyStats(); }
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk, sav.Personal);
        return new Made8a(e, pk, how, generator, fixedSeed, la.Valid, la.Report());
    }

    /// <summary>The gender this catch must come out as: the entry's, the evolution's, or the one asked for where the species has either.</summary>
    private byte? Wanted(byte? needed, int ratio)
    {
        if (!Spawn8a.RollsGender(ratio)) return null;
        if (needed is { } n) return n;
        return opt.Sex switch { SexChoice.Male => 0, SexChoice.Female => 1, _ => null };
    }

    private SpawnParams Params(ushort species, byte form, bool alpha, int flawless, byte? lock_, SlotType8a type)
    {
        int ratio = lock_ switch { 0 => 0, 1 => 254, _ => Plan8a.Table.GetFormEntry(species, form).Gender };
        int rolls = sav.GetShinyRolls(species) + (byte)Boost.Invoke(null, [type])!;
        return new SpawnParams(rolls, flawless, ratio, alpha);
    }

    /// <summary>A fixed seed whose Pokémon is what is asked: shiny or not, the gender, and the size when the smallest is asked.</summary>
    private (ulong fixedSeed, Drawn drawn) Draw(SpawnParams p, byte? gender, Func<ulong>? nextSeed = null)
    {
        bool smallest = opt.Size == SizeChoice.Smallest && !p.IsAlpha;
        for (int tries = 0; tries < 2_000_000; tries++)
        {
            ulong f;
            if (smallest)
            {
                // the shiny is placed at a roll chosen at random so that the size draws sit where the sampler puts them
                int roll = opt.Shiny ? random.Next(1, p.Rolls + 1) : p.Rolls;
                int position = SizeSeeds8a.Position(roll, Spawn8a.RollsGender(p.GenderRatio));
                if (!samplers.TryGetValue(position, out var sampler)) samplers[position] = sampler = new SizeSeeds8a(position, SizeSeeds8a.Smallest);
                if (sampler.Sample(random) is not { } s) continue;
                f = s;
            }
            else f = nextSeed?.Invoke() ?? Seed64();
            var d = Spawn8a.FromFixed(f, p, trainer.ID32);
            if (d.IsShiny != opt.Shiny) continue;
            if (gender is { } g && d.Gender != g) continue;
            if (smallest && (d.Height != 0 || d.Weight != 0)) continue;
            return (f, d);
        }
        throw new InvalidOperationException("조건에 맞는 시드를 찾지 못했습니다.");
    }

    private (PA8, ulong, ulong, string) Field(FieldSpawner sp, byte? needed)
    {
        var p = Params(sp.Species, sp.Form, sp.IsAlpha, sp.Flawless, sp.GenderLock, SlotType8a.Standard);
        var gender = Wanted(needed, p.GenderRatio);
        ulong generator, fixedSeed; Drawn drawn;
        if (opt.Size == SizeChoice.Smallest && !sp.IsAlpha)
        {
            // the size is chosen first; then a generator seed that gives this fixed seed and lands on the slot
            while (true)
            {
                (fixedSeed, drawn) = Draw(p, gender);
                var gens = Generator8a.Of(fixedSeed).Where(sp.Lands).ToList();
                if (gens.Count != 0) { generator = gens[random.Next(gens.Count)]; break; }
            }
        }
        else
        {
            // the slot is chosen first; the fixed seed follows from the generator seed
            ulong g = 0;
            (fixedSeed, drawn) = Draw(p, gender, () => { g = sp.DrawGenerator(random); return Spawn8a.FromGenerator(g).fixedSeed; });
            generator = g;
        }
        var slot = PkhexSlot(sp) ?? throw new InvalidOperationException($"{Plan8a.Label(sp.Species, sp.Form)}의 야생 슬롯을 PKHeX 에서 찾지 못했습니다.");
        int level = Spawn8a.Level(generator, sp.LevelMin, sp.LevelMax);
        var pk = Build(slot, drawn, level, p);
        string how = $"{Plan8a.Label(sp.Species, sp.Form)}{(sp.IsAlpha ? " 우두머리" : "")} 야생, {Plan8a.Area(sp.Area)} {Weather(sp.Time, sp.Weather)}, 슬롯 몫 {sp.Share:P1}";
        return (pk, generator, fixedSeed, how);
    }

    private (PA8, ulong, string) Other(EncounterSlot8a slot, byte? needed)
    {
        byte? lock_ = slot.Gender switch { PKHeX.Core.Gender.Male => 0, PKHeX.Core.Gender.Female => 1, _ => null };
        var p = Params(slot.Species, slot.Form, slot.IsAlpha, slot.FlawlessIVCount, lock_, slot.Type);
        var (fixedSeed, drawn) = Draw(p, Wanted(needed, p.GenderRatio));
        int level = slot.LevelMin + random.Next(slot.LevelMax - slot.LevelMin + 1);
        var pk = Build(slot, drawn, level, p);
        return (pk, fixedSeed, $"{Plan8a.Label(slot.Species, slot.Form)}{(slot.IsAlpha ? " 우두머리" : "")} 야생, {Plan8a.Kind(slot.Type)}");
    }

    private (PA8, string) Static(EncounterStatic8a st, byte? needed)
    {
        var criteria = EncounterCriteria.Unrestricted;
        if (needed is { } g) criteria = criteria with { Gender = g == 0 ? PKHeX.Core.Gender.Male : PKHeX.Core.Gender.Female };
        var pk = st.ConvertToPKM(trainer, criteria);
        if (st.FixedBall == PKHeX.Core.Ball.None) pk.Ball = (byte)opt.Ball;
        pk.MetDate = Day();
        pk.RefreshChecksum();
        return (pk, $"{Plan8a.Label(st.Species, st.Form)} 고정 조우, {Plan8a.Ko.GetLocationName(false, st.Location, 8, 8, GameVersion.PLA)}");
    }

    /// <summary>The entity: PKHeX's own catch for the trainer, the moves and the met place, with what the seed drew put in.</summary>
    private PA8 Build(EncounterSlot8a slot, Drawn d, int level, SpawnParams p)
    {
        var pk = slot.ConvertToPKM(trainer);
        pk.EncryptionConstant = d.Ec;
        pk.PID = d.Pid;
        pk.IV_HP = d.Ivs[0]; pk.IV_ATK = d.Ivs[1]; pk.IV_DEF = d.Ivs[2]; pk.IV_SPA = d.Ivs[3]; pk.IV_SPD = d.Ivs[4]; pk.IV_SPE = d.Ivs[5];
        pk.RefreshAbility(d.Ability);
        pk.Gender = (byte)d.Gender;
        pk.Nature = (Nature)d.Nature; pk.StatAlignment = (Nature)d.Nature;
        pk.HeightScalar = (byte)d.Height; pk.WeightScalar = (byte)d.Weight; pk.Scale = (byte)d.Height;
        pk.ResetHeight(); pk.ResetWeight();
        pk.IsAlpha = p.IsAlpha;
        pk.CurrentLevel = (byte)level; pk.MetLevel = (byte)level;
        // the moves it has at this level and which it has mastered, as PKHeX's own catch sets them
        var (learn, _) = ((IMasteryInitialMoveShop8)slot).GetLevelUpInfo();
        Span<ushort> moves = stackalloc ushort[4];
        ((IMasteryInitialMoveShop8)slot).LoadInitialMoveset(pk, moves, learn, (byte)level);
        pk.SetMoves(moves);
        pk.SetMasteryFlags();
        if (slot.FixedBall == PKHeX.Core.Ball.None) pk.Ball = (byte)opt.Ball;
        pk.MetDate = Day();
        pk.ResetPartyStats();
        pk.RefreshChecksum();
        return pk;
    }

    /// <summary>PKHeX's slot for a field spawner's catch: the same species, form and alpha-ness in the same land, covering the level.</summary>
    private static EncounterSlot8a? PkhexSlot(FieldSpawner sp)
    {
        string land = Plan8a.Area(sp.Area);
        var all = Plan8a.Areas.SelectMany(a => a.Slots).Where(s => s.Species == sp.Species && s.Form == sp.Form && s.IsAlpha == sp.IsAlpha && s.Type == SlotType8a.Standard).ToList();
        return all.FirstOrDefault(s => Plan8a.Ko.GetLocationName(false, s.Location, 8, 8, GameVersion.PLA) == land && s.LevelMin <= sp.LevelMin && sp.LevelMax <= s.LevelMax)
            ?? all.FirstOrDefault(s => Plan8a.Ko.GetLocationName(false, s.Location, 8, 8, GameVersion.PLA) == land)
            ?? all.FirstOrDefault();
    }

    private static string Weather(string time, string weather) => (time switch { "dawn" => "새벽", "day" => "낮", "dusk" => "저녁", "night" => "밤", _ => time })
        + " " + (weather switch { "sunny" => "맑음", "cloudy" => "흐림", "rain" => "비", "snow" => "눈", "drought" => "가뭄", "fog" => "안개", "rainstorm" => "폭우", "snowstorm" => "눈보라", _ => weather });
}

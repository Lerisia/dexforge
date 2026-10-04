using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// What was met standing still or handed over in this generation, drawn from a session of the game:
/// a seed the console could have started from, and a moment in that session at which the button could have been pressed.
/// </summary>
public sealed class Caught7(Draw draw, Options opt)
{
    /// <summary>
    /// Whether the trainer carries the Shiny Charm. One who has filled the Pokedex does; nobody does in the first days of the adventure,
    /// when what travels in the party was caught.
    /// </summary>
    public bool Charm = true;
    /// <summary>How far into a session the button is pressed at the latest: this many numbers past the start, a good half hour of standing about.</summary>
    public const int Span = 60000;

    public List<string> Notes { get; } = [];

    private static bool Perfect(Drawn7 d) => d.Ivs.All(v => v == 31);

    /// <summary>
    /// Whether the numbers from here on would come to six perfect individual values, were the encryption constant drawn here.
    /// Cheap, and wrong only in saying yes too often: whatever passes is generated in full afterwards.
    /// </summary>
    private bool Promising(Stream7 stream, int at, Met7 met, int tsv, bool shines)
    {
        int p = at + 1;
        int rolls = Charm && !met.ShinyLocked && !met.AlwaysSync ? 3 : 1;
        bool shiny = false;
        for (int i = rolls; i > 0; i--)
        {
            uint pid = (uint)stream[p++];
            if ((((pid >> 16) ^ (pid & 0xFFFF)) >> 4) == tsv) { shiny = !met.ShinyLocked; break; }
        }
        if (shiny != shines) return false;
        int left = 6;
        if (met.Iv3)
        {
            int seen = 0;
            for (int picked = 0; picked < 3;)
            {
                int which = (int)(stream[p++] % 6);
                if ((seen & (1 << which)) == 0) { seen |= 1 << which; picked++; }
            }
            left = 3;
        }
        for (int i = 0; i < left; i++) if ((stream[p++] & 0x1F) != 31) return false;
        return true;
    }

    /// <summary>A meeting that came out as asked, or none if this session holds none.</summary>
    private Drawn7? Search(Stream7 stream, Met7 met, int tsv, bool shines, bool six, byte? gender)
    {
        int first = Timeline7.Begins, last = Timeline7.Begins + Span;
        if (six)
        {
            // Six perfect values are one draw in tens of thousands, and shiny besides one in tens of millions:
            // the numbers are looked through as they are first, and only a session that holds such a run is played out.
            bool any = false;
            for (int at = first; at <= last + 400 && !any; at++) any = Promising(stream, at, met, tsv, shines);
            if (!any) return null;
        }
        var hits = new List<Drawn7>();
        foreach (var (index, pressable, models) in Timeline7.Walk(stream, first, last, met.Npc + 1, met.Raining))
        {
            if (!pressable) continue;
            var d = new Meeting7(stream, index, models).Still(met, tsv, Charm);
            if (d.Shiny != shines || gender is { } g && d.Gender != g) continue;
            if (six && !Perfect(d)) continue;
            hits.Add(d);
        }
        return hits.Count == 0 ? null : hits[draw.Rnd.Next(hits.Count)];
    }

    /// <summary>The values of one meeting for this trainer, and the session and moment they came of.</summary>
    /// <param name="gender">0 male, 1 female, 2 none; none, and either will do.</param>
    public (Drawn7 Drawn, uint Seed)? Still(Met7 met, ushort tid, ushort sid, bool shines, byte? gender, HashSet<uint> used)
    {
        int tsv = (tid ^ sid) >> 4;
        // Six perfect values are asked only of what the game promises three: of the rest that is one draw in a thousand million.
        bool six = opt.Ivs == IvChoice.Six && met.Iv3;
        // The tool counts genders its own way: 0 none, 1 male, 2 female.
        byte? theirs = gender switch { null => null, 0 => 1, 1 => 2, _ => 0 };
        int sessions = six && shines ? 400000 : six ? 4000 : 2000;
        for (int n = 0; n < sessions; n++)
        {
            uint seed = (uint)draw.Rnd.NextInt64(0, 1L << 32);
            var found = Search(new Stream7(seed), met, tsv, shines, six, theirs);
            if (found is null || used.Contains(found.Pid) || used.Contains(found.Ec) || found.Ec == 0) continue;
            return (found, seed);
        }
        return null;
    }

    /// <summary>The Ultra Beasts that come out of the grass in place of what lives there (3DSRNGTool, PKMW7.Species_USUM): species, version, place, level, how often in a hundred.</summary>
    public static readonly (ushort Species, GameVersion Version, short Location, byte Level, byte Rate)[] Beasts =
    [
        (805, GameVersion.UM, 164, 60, 80),
        (806, GameVersion.US, 164, 60, 80),
    ];

    /// <summary>
    /// The values of one meeting in the grass for this trainer: the species and level asked for, out of whatever else the grass holds.
    /// Six perfect values are not asked of what the game promises none: that is one draw in a thousand million.
    /// </summary>
    public (Drawn7 Drawn, uint Seed, bool Night)? Grass(Area7 area, bool moon, int speciesForm, byte level, (byte Min, byte Max)? levels,
                                                       ushort tid, ushort sid, bool shines, byte? gender, HashSet<uint> used, (byte Level, byte Rate)? beast = null)
    {
        int tsv = (tid ^ sid) >> 4;
        byte? theirs = gender switch { null => null, 0 => 1, 1 => 2, _ => 0 };
        bool six = opt.Ivs == IvChoice.Six && beast is not null;
        var hours = beast is not null ? new[] { false, true }
                  : new[] { false, true }.Where(night => area.Slots(moon, night)?.Contains(speciesForm) == true).ToArray();
        if (hours.Length == 0) return null;
        int sessions = six && shines ? 400000 : 4000;
        for (int n = 0; n < sessions; n++)
        {
            uint seed = (uint)draw.Rnd.NextInt64(0, 1L << 32);
            bool night = hours[draw.Rnd.Next(hours.Length)];
            var stream = new Stream7(seed);
            var hits = new List<Drawn7>();
            foreach (var (index, pressable, models) in Timeline7.Walk(stream, Timeline7.Begins, Timeline7.Begins + Span, area.Npc + 1, area.Raining))
            {
                if (!pressable) continue;
                var d = beast is { } b
                    ? new Meeting7(stream, index, models).Wild(area, moon, night, tsv, Charm, speciesForm, b.Level, b.Rate)
                    : new Meeting7(stream, index, models).Wild(area, moon, night, tsv, Charm, levels: levels);
                if (d.Species != speciesForm || d.Level != level || d.Shiny != shines || theirs is { } g && d.Gender != g) continue;
                if (six && !Perfect(d)) continue;
                hits.Add(d);
            }
            if (hits.Count == 0) continue;
            var found = hits[draw.Rnd.Next(hits.Count)];
            if (used.Contains(found.Pid) || used.Contains(found.Ec) || found.Ec == 0) continue;
            return (found, seed, night);
        }
        return null;
    }

    /// <summary>The values of a meeting, put on the Pokemon.</summary>
    public static void Put(PK7 pk, Drawn7 d)
    {
        pk.EncryptionConstant = d.Ec;
        pk.PID = d.Pid;
        pk.IV_HP = d.Ivs[0]; pk.IV_ATK = d.Ivs[1]; pk.IV_DEF = d.Ivs[2]; pk.IV_SPA = d.Ivs[3]; pk.IV_SPD = d.Ivs[4]; pk.IV_SPE = d.Ivs[5];
        pk.Nature = (Nature)d.Nature;
        pk.RefreshAbility(d.Ability - 1);
        pk.Gender = d.Gender switch { 1 => (byte)0, 2 => (byte)1, _ => (byte)2 };
    }
}

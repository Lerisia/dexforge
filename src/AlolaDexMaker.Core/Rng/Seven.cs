namespace AlolaDexMaker;

/// <summary>
/// The generator the seventh generation draws from when a Pokemon is met or handed over: SFMT-19937, begun from 32 bits, read 64 bits at a time.
/// </summary>
public sealed class Sfmt
{
    private const int N = 19937 / 128 + 1, N32 = N * 4, Pos1 = 122, Sl1 = 18, Sr1 = 11;
    private const uint Msk1 = 0xdfffffefu, Msk2 = 0xddfecb7fu, Msk3 = 0xbffaffffu, Msk4 = 0xbffffff6u;
    private static readonly uint[] Parity = [1u, 0u, 0u, 0x13c9e684u];
    private readonly uint[] s = new uint[N32];
    private int idx;

    public Sfmt(uint seed)
    {
        s[0] = seed;
        for (int i = 1; i < N32; i++) s[i] = (uint)(1812433253u * (s[i - 1] ^ (s[i - 1] >> 30)) + (uint)i);
        Certify();
        idx = N32;
    }

    private void Certify()
    {
        uint inner = 0;
        for (int i = 0; i < 4; i++) inner ^= s[i] & Parity[i];
        for (int i = 16; i > 0; i >>= 1) inner ^= inner >> i;
        if ((inner & 1) == 1) return;
        for (int i = 0; i < 4; i++)
        {
            uint work = 1;
            for (int j = 0; j < 32; j++, work <<= 1)
                if ((work & Parity[i]) != 0) { s[i] ^= work; return; }
        }
    }

    private void Refill()
    {
        int a = 0, b = Pos1 * 4, c = (N - 2) * 4, d = (N - 1) * 4;
        var p = s;
        do
        {
            p[a + 3] = p[a + 3] ^ (p[a + 3] << 8) ^ (p[a + 2] >> 24) ^ (p[c + 3] >> 8) ^ ((p[b + 3] >> Sr1) & Msk4) ^ (p[d + 3] << Sl1);
            p[a + 2] = p[a + 2] ^ (p[a + 2] << 8) ^ (p[a + 1] >> 24) ^ (p[c + 3] << 24) ^ (p[c + 2] >> 8) ^ ((p[b + 2] >> Sr1) & Msk3) ^ (p[d + 2] << Sl1);
            p[a + 1] = p[a + 1] ^ (p[a + 1] << 8) ^ (p[a + 0] >> 24) ^ (p[c + 2] << 24) ^ (p[c + 1] >> 8) ^ ((p[b + 1] >> Sr1) & Msk2) ^ (p[d + 1] << Sl1);
            p[a + 0] = p[a + 0] ^ (p[a + 0] << 8) ^ (p[c + 1] << 24) ^ (p[c + 0] >> 8) ^ ((p[b + 0] >> Sr1) & Msk1) ^ (p[d + 0] << Sl1);
            c = d; d = a; a += 4; b += 4;
            if (b >= N32) b = 0;
        } while (a < N32);
    }

    public uint Next32()
    {
        if (idx >= N32) { Refill(); idx = 0; }
        return s[idx++];
    }

    public ulong Next64() => Next32() | ((ulong)Next32() << 32);
}

/// <summary>Everything one game session draws, in order, from the moment it is switched on: number 0 is the first.</summary>
public sealed class Stream7(uint seed)
{
    private readonly Sfmt rng = new(seed);
    private readonly List<ulong> drawn = [];
    public uint Seed => seed;

    public ulong this[int index]
    {
        get
        {
            while (drawn.Count <= index) drawn.Add(rng.Next64());
            return drawn[index];
        }
    }
}

/// <summary>
/// The characters in view, each of which blinks now and then and draws a number to decide when.
/// A count of 0 or less means it may blink; below zero it is about to; above one it has just blinked and is waiting.
/// </summary>
public sealed class Models7
{
    public int Number;
    public int[] Remain;
    public bool Phase;
    public bool Raining;

    public Models7(int number, bool raining) { Number = number; Remain = new int[number]; Raining = raining; }

    public Models7 Clone() => new(Number, Raining) { Remain = (int[])Remain.Clone(), Phase = Phase };
}

/// <summary>What one meeting came to.</summary>
public sealed record Drawn7(int Index, uint Ec, uint Pid, int[] Ivs, byte Ability, byte Nature, byte Gender, bool Shiny, bool Synchronized, int Used, byte Level = 0, byte Slot = 0)
{
    /// <summary>The species, with its form in the high bits, where what comes is not settled beforehand.</summary>
    public int Species { get; init; }
    public bool Special { get; init; }
}

/// <summary>What is settled by the kind of Pokemon before anything is drawn: whether three values are perfect, and its sex.</summary>
public static class Kind7
{
    private static readonly HashSet<int> Babies = [30, 31, 172, 173, 174, 175, 201, 236, 238, 239, 240, 298, 360, 406, 433, 438, 439, 440, 446, 447, 458];

    /// <summary>The sex as the tool counts it: fixed (0 none, 1 male, 2 female), or drawn against a threshold.</summary>
    public static (bool Iv3, byte Gender, bool Random) Of(int speciesForm, bool beast)
    {
        int species = speciesForm & 0x7FF, form = speciesForm >> 11;
        var info = PKHeX.Core.PersonalTable.USUM.GetFormEntry((ushort)species, (byte)form);
        int ratio = info.Gender;
        bool random = 0x0F < ratio && ratio < 0xEF;
        byte gender = ratio switch
        {
            0x1F or 0x3F or 0x7F or 0xBF or 0xE1 => (byte)(ratio - 1),
            0x00 => 1,
            0xFE => 2,
            _ => 0,
        };
        bool iv3 = beast || info.EggGroup1 == 0xF && !Babies.Contains(species);
        return (iv3, gender, random);
    }
}

/// <summary>
/// One meeting, generated the way the game generates it, from the number the session has reached when the button is pressed.
/// This follows 3DSRNGTool (RNGPool, Stationary7, Wild7) step for step and is checked against that tool's own code.
/// </summary>
public sealed class Meeting7
{
    private readonly Stream7 stream;
    private readonly int start;
    private int next;
    private Models7 models;

    public Meeting7(Stream7 stream, int index, Models7 models)
    {
        this.stream = stream; start = next = index; this.models = models.Clone();
    }

    private ulong Rand() => stream[next++];
    private void Advance(int n) => next += n;
    private int Used => next - start;

    private void Blink(int i)
    {
        var remain = models.Remain;
        if (remain[i] > 1) { remain[i]--; return; }                  // waiting, second part
        if (remain[i] < 0)                                           // waiting, first part
        {
            if (++remain[i] == 0) remain[i] = Rand() % 3 == 0 ? 36 : 30;
            return;
        }
        if ((int)(Rand() & 0x7F) == 0) remain[i] = -5;               // not blinking
    }

    private void Elapse(int frames)
    {
        for (int f = 0; f < frames; f++)
        {
            for (int i = 0; i < models.Number; i++) Blink(i);
            if (models.Raining && (models.Phase = !models.Phase)) Advance(2);
        }
    }

    private void ChangeNumber(int n)
    {
        if (n == models.Number) return;
        models.Number = n;
        if (n > models.Remain.Length) { var grown = new int[n]; models.Remain.CopyTo(grown, 0); models.Remain = grown; }
    }

    private void Cry(int at)
    {
        for (int i = 0; i < models.Number; i++)
        {
            if (i == at) Advance(1);
            Blink(i);
        }
        if (at >= models.Number) Advance(1);
    }

    private void Split(int total, int cry)
    {
        Elapse(total - cry);
        Advance(1);
        Elapse(cry);
    }

    /// <summary>From the button to the draw, for what stands still or is handed over.</summary>
    private void Wait(int type, int time)
    {
        switch (type)
        {
            case 4:
                Advance(2);
                Elapse(time - 2);
                models.Number = 1;
                Elapse(2);
                break;
            case 6:
                Advance(2);
                Elapse(time - 27);
                Advance(1);
                Elapse(25);
                models.Number = 1;
                Elapse(2);
                break;
            case 11: Split(time, 36); break;
            case 12: Split(time, 51); break;
            case 13:
                Elapse(29);
                ChangeNumber(3);
                Elapse(time - 64);
                Cry(2);
                Elapse(34);
                break;
            case 14:
                Elapse(11);
                ChangeNumber(2);
                Split(time - 11, 35);
                break;
            case 15: Split(time, 50); break;
            case 16: Split(time, 43); break;
            case 17: Split(time, 40); break;
            case 0: Elapse(time); break;
            default: throw new NotSupportedException($"the wait of kind {type} is not written");
        }
    }

    /// <summary>
    /// What comes out of the grass when honey is used. With a rate above nothing, an Ultra Beast may come in place of what lives there:
    /// it is given as the species, at the level it always has.
    /// </summary>
    public Drawn7 Wild(Area7 area, bool moon, bool night, int tsv, bool charm, int beast = 0, byte beastLevel = 0, byte beastRate = 0, (byte Min, byte Max)? levels = null)
    {
        var slots = area.Slots(moon, night) ?? throw new InvalidOperationException("nothing lives there at that hour in that version");
        var (min, max) = levels ?? area.Levels(moon);

        // From the bag to the grass
        Elapse(8 / 2 + 2);
        models = new Models7(models.Number, models.Raining);
        Advance(1);                               // Ultra Sun and Ultra Moon
        if (models.Raining) Advance(2);
        Elapse(1);
        Advance(area.Correction - models.Number);
        Elapse(63);
        int delayed = Used;

        bool special = beastRate > 0 && Rand() % 100 < beastRate;
        int slot; byte level; int species; bool sync;
        if (!special)
        {
            sync = Rand() % 100 >= 50;
            int roll = (int)(Rand() % 100);
            slot = area.Split.Length;
            for (int i = 1; i < area.Split.Length; i++)
            {
                roll -= area.Split[i - 1];
                if (roll < 0) { slot = i; break; }
            }
            level = (byte)(Rand() % (ulong)(max - min + 1) + min);
            Rand();                               // how far a flute would move the level
            species = slots[slot - 1];
            if (species == 774) Rand();           // Minior's colour
        }
        else
        {
            slot = 0;
            Elapse(7);
            sync = Rand() % 100 >= 50;
            Elapse(3);
            level = beastLevel;
            species = beast;
        }
        Advance(60);

        var (iv3, gender, randomGender) = Kind7.Of(species, special);
        uint ec = (uint)Rand();
        int rolls = charm ? 3 : 1;
        uint pid = 0; bool shiny = false;
        for (int i = rolls; i > 0; i--)
        {
            pid = (uint)Rand();
            if ((((pid >> 16) ^ (pid & 0xFFFF)) >> 4) == tsv) { shiny = true; break; }
        }

        var ivs = new int[6];
        for (int i = iv3 ? 3 : 0; i > 0;)
        {
            int at = (int)(Rand() % 6);
            if (ivs[at] == 0) { i--; ivs[at] = 31; }
        }
        for (int i = 0; i < 6; i++) if (ivs[i] == 0) ivs[i] = (int)(Rand() & 0x1F);

        byte ability = special ? (byte)1 : (byte)((Rand() & 1) + 1);
        byte nature = (byte)(Rand() % 25);
        byte sex = randomGender ? (byte)(Rand() % 252 >= gender ? 1 : 2) : gender;
        if (!special) Rand();                     // what it holds
        return new Drawn7(start, ec, pid, ivs, ability, nature, sex, shiny, sync, delayed, level, (byte)slot) { Species = species, Special = special };
    }

    /// <summary>What stands still or is handed over.</summary>
    public Drawn7 Still(Met7 met, int tsv, bool charm)
    {
        int time = met.Delay / 2 + 2;
        int type = met.DelayType == 4 && (met.Delay & 1) == 1 ? 6 : met.DelayType;
        Wait(type, time);
        int delayed = Used;

        bool sync;
        if (met.AlwaysSync) sync = true;
        else
        {
            sync = Rand() % 100 >= 50;
            Elapse(3);
            Advance(60);
        }

        uint ec = (uint)Rand();
        int rolls = charm && !met.ShinyLocked && !met.AlwaysSync ? 3 : 1;
        uint pid = 0; bool shiny = false;
        for (int i = rolls; i > 0; i--)
        {
            pid = (uint)Rand();
            if ((((pid >> 16) ^ (pid & 0xFFFF)) >> 4) == tsv)
            {
                if (met.ShinyLocked) pid ^= 0x10000000;
                else shiny = true;
                break;
            }
        }

        var ivs = new[] { -1, -1, -1, -1, -1, -1 };
        for (int i = met.Iv3 ? 3 : 0; i > 0;)
        {
            int at = (int)(Rand() % 6);
            if (ivs[at] < 0) { i--; ivs[at] = 31; }
        }
        for (int i = 0; i < 6; i++) if (ivs[i] < 0) ivs[i] = (int)(Rand() & 0x1F);

        byte ability = met.Ability > 0 ? met.Ability : (byte)((Rand() & 1) + 1);
        // No Pokemon with Synchronize leads the party: the nature is always drawn.
        byte nature = (byte)(Rand() % 25);
        byte gender = met.RandomGender ? (byte)(Rand() % 252 >= met.Gender ? 1 : 2) : met.Gender;
        return new Drawn7(start, ec, pid, ivs, ability, nature, gender, shiny, sync, delayed, met.Level);
    }
}

/// <summary>
/// A session as time passes in it: at each moment the button can be pressed, the number the session has reached and how the characters in view stand.
/// </summary>
public static class Timeline7
{
    /// <summary>The number a session of Ultra Sun or Ultra Moon has reached when the save has finished loading.</summary>
    public const int Begins = 478;

    /// <summary>
    /// Every number from the first to the last, each with the characters as they stand at the moment it can be reached.
    /// Several numbers fall within one moment when several characters draw in it; only the first of them is where a press lands.
    /// </summary>
    public static IEnumerable<(int Index, bool First, Models7 Models)> Walk(Stream7 stream, int first, int last, int number, bool raining)
    {
        var status = new Models7(number, raining);
        int at = first; // the next number the characters will draw
        for (int i = first; i <= last;)
        {
            var before = status.Clone();
            int used;
            do used = Step(stream, status, ref at); while (used == 0);
            for (int k = 0; k < used && i <= last; k++, i++)
                yield return (i, k == 0, before);
        }
    }

    private static int Step(Stream7 stream, Models7 m, ref int at)
    {
        int from = at;
        for (int i = 0; i < m.Number; i++)
        {
            if (m.Remain[i] > 1) { m.Remain[i]--; continue; }
            if (m.Remain[i] < 0)
            {
                if (++m.Remain[i] == 0) m.Remain[i] = stream[at++] % 3 == 0 ? 36 : 30;
                continue;
            }
            if ((int)(stream[at++] & 0x7F) == 0) m.Remain[i] = -5;
        }
        if (m.Raining && (m.Phase = !m.Phase)) at += 2;
        return at - from;
    }
}

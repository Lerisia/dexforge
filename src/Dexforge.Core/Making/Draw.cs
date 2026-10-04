using PKHeX.Core;

namespace Dexforge;

/// <summary>What is drawn afresh for each Pokemon, drawn the way the games leave it to chance.</summary>
public sealed class Draw(Random rnd)
{
    public Random Rnd => rnd;

    public DateOnly Day(DateOnly from, DateOnly to) => from.AddDays(rnd.Next(0, to.DayNumber - from.DayNumber + 1));

    /// <summary>A shiny personality value for this trainer: any of the sixteen values that count, each as likely as the next.</summary>
    public uint ShinyPid(ushort tid, ushort sid)
    {
        uint low = (uint)rnd.Next(65536), value = (uint)rnd.Next(16);
        return ((uint)(tid ^ sid) ^ low ^ value) << 16 | low;
    }

    /// <summary>A personality value that is not shiny for this trainer.</summary>
    public uint PlainPid(ushort tid, ushort sid)
    {
        while (true)
        {
            uint pid = (uint)rnd.NextInt64(0, (long)uint.MaxValue + 1);
            if (((tid ^ sid) ^ (pid >> 16) ^ (pid & 0xFFFF)) >= 16) return pid;
        }
    }

    public Nature Nature() => (Nature)rnd.Next(25);

    public ushort Id16() => (ushort)rnd.Next(1, 65536);
}

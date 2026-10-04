using PKHeX.Core;

namespace Dexforge;

/// <summary>Somebody whose name a Pokemon carries.</summary>
public sealed record Trainer(string Name, byte Gender, ushort Tid, ushort Sid, int Language, GameVersion Version,
                             byte ConsoleRegion, byte Country, byte Region)
{
    public uint Id32 => (uint)(Sid << 16 | Tid);
    /// <summary>The six-digit TID the game shows from the seventh generation on.</summary>
    public int Shown => (int)(Id32 % 1000000);
    /// <summary>The four-digit SID that goes with it.</summary>
    public int Sid7 => (int)(Id32 / 1000000);

    public SimpleTrainerInfo Info => new(Version)
    {
        OT = Name, Gender = Gender, TID16 = Tid, SID16 = Sid, Language = Language,
        ConsoleRegion = ConsoleRegion, Country = Country, Region = Region,
    };
}

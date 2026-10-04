namespace Dexforge;

/// <summary>
/// Where a Pokemon's values came from, written down so that they can be drawn again and held against the Pokemon:
/// the state of the game's generator, the moment, and whatever else decides what is drawn. The encryption constant tells which Pokemon it is.
/// </summary>
public abstract record Origin(string Name, uint Ec, ushort Tid, ushort Sid)
{
    public int Tsv => (Tid ^ Sid) >> 4;
}

/// <summary>Met standing still or handed over, in a session of Ultra Sun or Ultra Moon.</summary>
public sealed record StillOrigin(string Name, uint Ec, ushort Tid, ushort Sid, Met7 Met, uint Seed, int Index, bool Charm) : Origin(Name, Ec, Tid, Sid);

/// <summary>Come out of the grass, in a session of Ultra Sun or Ultra Moon.</summary>
public sealed record GrassOrigin(string Name, uint Ec, ushort Tid, ushort Sid, Area7 Area, bool Moon, bool Night, int SpeciesForm, byte Level,
                                 (byte Min, byte Max)? Levels, (byte Level, byte Rate)? Beast, uint Seed, int Index, bool Charm) : Origin(Name, Ec, Tid, Sid);

/// <summary>Hatched of an egg the Nursery handed over.</summary>
public sealed record EggOrigin(string Name, uint Ec, ushort Tid, ushort Sid, uint[] State, Parents7 Parents) : Origin(Name, Ec, Tid, Sid);

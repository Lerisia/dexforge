using PKHeX.Core;

namespace AlolaDexMaker.Sword;

/// <summary>Which ball each species and form goes in: the table carried inside, picked by the developer, one line per species and form.</summary>
public sealed class Balls8
{
    private readonly Dictionary<(ushort, byte), Ball> chosen = new();

    public Balls8(GameStrings ko)
    {
        var ballNames = new Dictionary<string, Ball>();
        for (int i = 1; i < ko.balllist.Length; i++) if (ko.balllist[i].Length != 0) ballNames.TryAdd(ko.balllist[i], (Ball)i); // the list goes on into Legends: Arceus balls of the same names
        foreach (var raw in Resources.Lines("sword.balls"))
        {
            var line = raw.Split('#')[0].TrimEnd();
            if (line.Trim().Length == 0) continue;
            var f = line.Split('\t');
            var species = ushort.Parse(f[0]);
            var forms = FormConverter.GetFormList(species, ko.types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen8);
            int idx = Array.IndexOf(forms, f[1].Trim());
            byte form = idx >= 0 ? (byte)idx : (byte)0;
            if (!ballNames.TryGetValue(f[2].Trim(), out var ball)) throw new InvalidDataException("balls: " + raw);
            chosen.TryAdd((species, form), ball);
        }
    }

    /// <summary>The ball picked for this species and form, or none.</summary>
    public Ball? For(ushort species, byte form) => chosen.TryGetValue((species, form), out var b) ? b : null;
}

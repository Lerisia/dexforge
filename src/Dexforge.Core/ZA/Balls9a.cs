using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>The ball for each species and form of the Z-A dex (za.balls), some hanging on the sex: a draft from the Scarlet, Sword and Ultra Sun picks until the owner picks for Z-A.</summary>
public sealed class Balls9a
{
    private readonly Dictionary<(ushort Species, byte Form, int Sex), Ball> table = new();

    public Balls9a()
    {
        var ko = Plan9a.Ko;
        var names = new Dictionary<string, Ball>();
        foreach (Ball b in Enum.GetValues<Ball>()) if ((int)b < ko.balllist.Length && (int)b > 0) names.TryAdd(ko.balllist[(int)b], b);
        var rows = new EventBox.Rows(Embedded.Text("za.balls"));
        foreach (var r in rows.All)
        {
            var sex = rows.Get(r, "sex") switch { "수" => 0, "암" => 1, _ => -1 };
            if (!names.TryGetValue(rows.Get(r, "ball"), out var ball)) throw new InvalidDataException("za.balls: " + rows.Get(r, "ball"));
            table[(ushort.Parse(rows.Get(r, "species")), byte.Parse(rows.Get(r, "form")), sex)] = ball;
        }
    }

    /// <summary>The ball for this species, form and sex (0 male, 1 female, none when it does not matter); none when nothing was picked.</summary>
    public Ball? For(ushort species, byte form, int? sex)
    {
        if (sex is { } s && table.TryGetValue((species, form, s), out var b)) return b;
        if (table.TryGetValue((species, form, -1), out b)) return b;
        if (sex is null && (table.TryGetValue((species, form, 0), out b))) return b;
        return null;
    }
}

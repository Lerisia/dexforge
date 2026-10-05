using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// The one ball table every game draws on (balls.tsv): for each species and form — split by sex where the owner wants it,
/// and by colour (shiny or plain) where the owner wants that — up to three balls in order of preference. A maker tries them
/// from the top and keeps the first the game allows, and a Poké Ball when none of the three will do; so a ball picked with
/// a later game in mind falls through to the next in an older one that has no such ball.
/// </summary>
public sealed class Balls
{
    private static readonly Lazy<Balls> shared = new(() => new Balls(Embedded.Text("balls")));
    public static Balls Shared => shared.Value;

    /// <summary>sex: 0 male, 1 female, -1 either; colour: 1 shiny, 0 plain, -1 either.</summary>
    private readonly Dictionary<(ushort Species, byte Form, int Sex, int Colour), Ball[]> table = new();

    public Balls(string text)
    {
        var ko = GameInfo.GetStrings("ko");
        var names = new Dictionary<string, Ball>();
        for (int i = 1; i < ko.balllist.Length && i < (int)Ball.LAPoke; i++) if (ko.balllist[i].Length != 0) names.TryAdd(ko.balllist[i], (Ball)i);
        var rows = new EventBox.Rows(text);
        foreach (var r in rows.All)
        {
            int sex = rows.Get(r, "sex") switch { "수" => 0, "암" => 1, _ => -1 };
            int colour = rows.Get(r, "color") switch { "이로치" => 1, "일반" => 0, _ => -1 };
            var balls = rows.Get(r, "balls").Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => names.TryGetValue(n, out var b) ? b : throw new InvalidDataException($"balls.tsv: '{n}' 라는 볼이 없습니다")).ToArray();
            var key = (ushort.Parse(rows.Get(r, "species")), byte.Parse(rows.Get(r, "form")), sex, colour);
            if (!table.TryAdd(key, balls)) throw new InvalidDataException($"balls.tsv: {key} 가 두 번 나옵니다");
        }
    }

    public int Count => table.Count;

    /// <summary>Three balls are as many as the owner ranks; when none of them will do, a Poké Ball (owner, 2026-10-05).</summary>
    public const int Ranks = 3;

    /// <summary>
    /// The balls wanted for this Pokémon, best first — the three ranked, then a Poké Ball: the row for its sex and colour, else its
    /// sex for either colour, else its sex for the other colour (a pick is better than none), then the same without a sex.
    /// </summary>
    public IReadOnlyList<Ball> Prefer(ushort species, byte form, byte? gender, bool shiny)
    {
        int sex = gender is 0 or 1 ? gender.Value : -1;
        int colour = shiny ? 1 : 0;
        foreach (var (s, c) in new[] { (sex, colour), (sex, -1), (sex, 1 - colour), (-1, colour), (-1, -1), (-1, 1 - colour) })
            if (table.TryGetValue((species, form, s, c), out var balls))
            {
                var ranked = balls.TakeWhile(b => b != Ball.Poke).Take(Ranks).ToList();
                return [.. ranked, Ball.Poke];
            }
        // no sex given where the rows go by sex: the male's
        if (sex < 0 && HangsOnSex(species, form)) return Prefer(species, form, 0, shiny);
        return [Ball.Poke];
    }

    /// <summary>Whether anything at all was picked for this species and form.</summary>
    public bool Has(ushort species, byte form) => table.Keys.Any(k => k.Species == species && k.Form == form);

    /// <summary>Whether a row of its own exists for this colour (rather than the other colour's or either's standing in).</summary>
    public bool PickedFor(ushort species, byte form, byte? gender, bool shiny)
    {
        int sex = gender is 0 or 1 ? gender.Value : -1;
        return table.ContainsKey((species, form, sex, shiny ? 1 : 0)) || table.ContainsKey((species, form, -1, shiny ? 1 : 0));
    }

    /// <summary>Whether a row splits this species and form by sex.</summary>
    public bool HangsOnSex(ushort species, byte form) => table.Keys.Any(k => k.Species == species && k.Form == form && k.Sex >= 0);

    /// <summary>
    /// Puts the Pokémon in the first ball of the list it can legally be in (the last is taken whatever happens). Returns the ball
    /// first wanted, so a report can say when it was not to be.
    /// </summary>
    public static Ball Choose(PKM pk, IReadOnlyList<Ball> wanted)
    {
        for (int i = 0; i < wanted.Count; i++)
        {
            pk.Ball = (byte)wanted[i];
            pk.RefreshChecksum();
            if (i == wanted.Count - 1 || new LegalityAnalysis(pk).Valid) break;
        }
        return wanted[0];
    }

    /// <summary>The list a maker goes through: one ball asked for over everything (and a Poké Ball behind it), else the table's.</summary>
    public IReadOnlyList<Ball> Wanted(int? one, ushort species, byte form, byte? gender, bool shiny) =>
        one is { } b ? ((Ball)b == Ball.Poke ? [Ball.Poke] : [(Ball)b, Ball.Poke]) : Prefer(species, form, gender, shiny);
}

using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>One distribution Scarlet received: its card, when it was given out, and what its final evolutions are by the event box's rules.</summary>
/// <param name="Group">What one code hands over together; cards of a group are received on one day. A card alone is its own group.</param>
/// <param name="Violet">A card only Violet could receive: received in the trainer's own Violet and traded over.</param>
/// <param name="Variants">The card's variants, where the gift was rolled at receipt (Mew's Tera type and first move: one card per type); <paramref name="Card"/> is the first.</param>
public sealed record Event9(WC9 Card, string Title, DateOnly Start, DateOnly? End, string Region, string Verdict, IReadOnlyList<(ushort Species, byte Form)> Evolutions, string Group, bool Violet, IReadOnlyList<WC9> Variants)
{
    /// <summary>A day it was received: inside the window; an open window closes a year after it opened.</summary>
    public DateOnly Day(Random random)
    {
        var end = End ?? Start.AddYears(1);
        return Start.AddDays(random.Next(end.DayNumber - Start.DayNumber + 1));
    }
}

/// <summary>
/// Every distribution a Scarlet save could receive in the game itself (not through HOME), one per card, with the dates the
/// catalogue settled (Serebii's event dex and Bulbapedia's Scarlet and Violet list, the Korean window first, then Japan's).
/// Unevolved gifts get their final evolutions too, as the event box does: not those in a Poké Ball and plain, not Pikachu.
/// </summary>
public static class Events9
{
    private static readonly Lazy<List<Event9>> all = new(Load);
    public static IReadOnlyList<Event9> All => all.Value;

    /// <summary>The final stages reachable from a species and form in Scarlet (and Violet, whose items it can borrow), by any path.</summary>
    public static List<(ushort, byte)> Finals(ushort species, byte form)
    {
        var tree = Plan9.Tree;
        var outList = new List<(ushort, byte)>();
        foreach (var (sp, fo) in tree.Forward.GetEvolutions(species, form))
        {
            if (!Plan9.Table.GetFormEntry(sp, fo).IsPresentInGame) continue;
            if (!tree.Forward.GetEvolutions(sp, fo).Any()) outList.Add((sp, fo));
            else outList.AddRange(Finals(sp, fo));
        }
        return outList.Distinct().ToList();
    }

    private static List<Event9> Load()
    {
        var cards = EncounterEvent.MGDB_G9.OfType<WC9>().Where(c => c.IsEntity && c.Species != 0).ToList();
        var rows = new EventBox.Rows(Embedded.Text("scarlet.events"));
        var list = new List<Event9>();
        foreach (var r in rows.All)
        {
            string G(string c) => rows.Get(r, c);
            int id = int.Parse(G("카드번호")); ushort tid = ushort.Parse(G("TID")); ushort sid = ushort.Parse(G("SID"));
            string title = G("카드 제목"); byte form = byte.Parse(G("폼"));
            var species = Plan9.Ko.Species.ToList().IndexOf(G("포켓몬"));
            // the same card more than once: a dump of each roll the gift could come as (Mew, one per Tera type), or the same bytes twice over
            var variants = cards.Where(c => c.CardID == id && c.CardTitle == title && c.TID16 == tid && c.SID16 == sid && c.Form == form && c.Species == species).ToList();
            var card = variants.FirstOrDefault() ?? throw new InvalidOperationException($"scarlet.events: no card {id} {title} in this PKHeX");
            var start = DateOnly.Parse(G("시작"));
            DateOnly? end = G("끝").Length > 0 ? DateOnly.Parse(G("끝")) : null;
            // a plain gift in a Poké Ball is not evolved, and Pikachu is left as it is
            var evolutions = card.Species == 25 || (card.Ball == (int)Ball.Poke && !card.IsShiny) ? [] : Finals(card.Species, card.Form);
            var group = G("같은 코드").Length > 0 ? G("같은 코드") : $"row-{list.Count}";
            list.Add(new Event9(card, title, start, end, G("지역단"), G("교차"), evolutions, group, G("버전") == "바이올렛", variants));
        }
        return list;
    }
}

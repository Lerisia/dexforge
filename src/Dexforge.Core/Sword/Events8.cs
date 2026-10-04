using PKHeX.Core;

namespace Dexforge.Sword;

/// <summary>One distribution Sword received: its card, when it was given out, and what its final evolutions are by the event box's rules.</summary>
/// <param name="Group">What one code hands over together; cards of a group are received on one day. A card alone is its own group.</param>
public sealed record Event8(WC8 Card, string Title, DateOnly Start, DateOnly? End, string Region, string Verdict, IReadOnlyList<(ushort Species, byte Form)> Evolutions, string Group)
{
    /// <summary>A day it was received: inside the window; an open window closes a year after it opened.</summary>
    public DateOnly Day(Random random)
    {
        var end = End ?? Start.AddYears(1);
        return Start.AddDays(random.Next(end.DayNumber - Start.DayNumber + 1));
    }
}

/// <summary>
/// Every distribution a Sword save could receive in the game itself (not through HOME), one per card, with the dates the
/// catalogue settled (Serebii's event dex and Bulbapedia's Sword and Shield list, the Korean window first, then Japan's).
/// Unevolved gifts get their final evolutions too, as the event box does: not those in a Poké Ball and plain, not Pikachu.
/// </summary>
public static class Events8
{
    private static readonly Lazy<List<Event8>> all = new(Load);
    public static IReadOnlyList<Event8> All => all.Value;

    /// <summary>The final stages reachable from a species and form in Sword, by any path.</summary>
    public static List<(ushort, byte)> Finals(ushort species, byte form)
    {
        var tree = Plan8.Tree;
        var outList = new List<(ushort, byte)>();
        foreach (var (sp, fo) in tree.Forward.GetEvolutions(species, form))
        {
            if (!Plan8.Table.GetFormEntry(sp, fo).IsPresentInGame) continue;
            if (!tree.Forward.GetEvolutions(sp, fo).Any()) outList.Add((sp, fo));
            else outList.AddRange(Finals(sp, fo));
        }
        return outList.Distinct().ToList();
    }

    private static List<Event8> Load()
    {
        var cards = EncounterEvent.MGDB_G8.OfType<WC8>().Where(c => c.IsEntity && c.Species != 0).ToList();
        var rows = new Dexforge.EventBox.Rows(Embedded.Text("sword.events"));
        var list = new List<Event8>();
        foreach (var r in rows.All)
        {
            string G(string c) => rows.Get(r, c);
            int id = int.Parse(G("카드번호")); ushort sp = ushort.Parse(G("TID")); ushort sid = ushort.Parse(G("SID"));
            string title = G("카드 제목");
            var card = cards.FirstOrDefault(c => c.CardID == id && c.CardTitle == title && c.TID16 == sp && c.SID16 == sid && c.Form == byte.Parse(G("폼")))
                ?? throw new InvalidOperationException($"sword.events: no card {id} {title} in this PKHeX");
            var start = DateOnly.Parse(G("시작"));
            DateOnly? end = G("끝").Length > 0 ? DateOnly.Parse(G("끝")) : null;
            // a Pokémon with the Gigantamax factor cannot evolve, Pikachu is left as it is, and a plain gift in a Poké Ball is not evolved either
            var evolutions = card.Species == 25 || card.CanGigantamax || (card.Ball == (int)Ball.Poke && !card.IsShiny) ? [] : Finals(card.Species, card.Form);
            var group = G("같은 코드").Length > 0 ? G("같은 코드") : $"row-{list.Count}";   // alone unless the table says otherwise (birthday cards share an id)
            list.Add(new Event8(card, title, start, end, G("지역단"), G("교차"), evolutions, group));
        }
        return list;
    }
}

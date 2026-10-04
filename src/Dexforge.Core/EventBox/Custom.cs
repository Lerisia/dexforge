using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>Something the box does not hold by the rules, which whoever asks may put in the spare room: a distribution left out, or an evolution not made.</summary>
/// <param name="Key">"D:행" for a distribution, "E:행:종-폼" for one of its final evolutions.</param>
public sealed record Candidate(string Key, Dist Dist, ushort EvolvesTo, byte EvolvesToForm, string Label);

/// <summary>"나만의 배포박스": what may be picked for the spare slots, and how a pick is read back.</summary>
public static class Custom
{
    /// <summary>Everything the rules leave out, each with a label for the list: dropped distributions and their final evolutions, and the evolutions of what is kept but not evolved.</summary>
    public static List<Candidate> Candidates(Rows rows, Dictionary<string, ushort> numbers)
    {
        var ko = GameInfo.GetStrings("ko");
        var list = new List<Candidate>();
        foreach (var r in rows.All)
        {
            var d = new Dist(rows, r);
            bool dropped = EventBoxMaking.Dropped(d);
            string who = d.Label2(ko);
            if (dropped) list.Add(new Candidate($"D:{d.CardRow}", d, 0, 0, who));
            ushort species = numbers[d.Species]; byte form = byte.Parse(d["폼"]);
            bool evolves = EventMaker.Evolves(d) && !dropped;
            if (evolves) continue;  // its finals are in the box already
            foreach (var (sp, fo) in Evolve7.Finals(species, form))
                list.Add(new Candidate($"E:{d.CardRow}:{sp}-{fo}", d, sp, fo, $"{who} → {ko.Species[sp]}{(fo != 0 ? "(" + FormName(ko, sp, fo) + ")" : "")}"));
        }
        return list;
    }

    public static string FormName(GameStrings ko, ushort species, byte form)
    {
        var forms = FormConverter.GetFormList(species, ko.types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen7);
        return form < forms.Length ? forms[form] : form.ToString();
    }
}

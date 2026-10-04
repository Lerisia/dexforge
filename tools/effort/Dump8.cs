using PKHeX.Core;

SaveUtil.TryGetSaveFile(File.ReadAllBytes(args[0]), out var sf); var sav = (SAV8SWSH)sf!;
var ko = GameInfo.GetStrings("ko");
var tree = EvolutionTree.Evolves8;
var pt = PersonalTable.SWSH;

string Sd(ushort sp, byte fo, int gender)
{
    var pk = new PK8 { Species = sp, Form = fo, Gender = (byte)gender };
    var line = new ShowdownSet(pk).Text.Split('\n')[0];
    var at = line.IndexOf(" @"); if (at >= 0) line = line[..at];
    foreach (var g in new[] { " (M)", " (F)" }) if (line.EndsWith(g)) line = line[..^4];
    return line.Trim();
}
string Ko(ushort sp, byte fo)
{
    var forms = FormConverter.GetFormList(sp, ko.Types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen8);
    return ko.Species[sp] + (fo > 0 && fo < forms.Length ? "(" + forms[fo] + ")" : "");
}
string Stats(ushort sp, byte fo) { var p = pt.GetFormEntry(sp, fo); return $"{p.HP}/{p.ATK}/{p.DEF}/{p.SPA}/{p.SPD}/{p.SPE}"; }
List<(ushort, byte)> Finals(ushort sp, byte fo)
{
    var o = new List<(ushort, byte)>();
    foreach (var (s, f) in tree.Forward.GetEvolutions(sp, fo)) { if (!tree.Forward.GetEvolutions(s, f).Any()) o.Add((s, f)); else o.AddRange(Finals(s, f)); }
    return o.Distinct().ToList();
}

Console.WriteLine("box\tslot\tspecies\tform\tgender\tko\tshowdown\tstats\tevent\tfinals");
for (int b = 0; b < sav.BoxCount; b++)
{
    var box = sav.GetBoxData(b);
    for (int s = 0; s < box.Length; s++)
    {
        var pk = (PK8)box[s]; if (pk.Species == 0) continue;
        bool ev = pk.FatefulEncounter || new LegalityAnalysis(pk).EncounterMatch is MysteryGift;
        var fin = Finals(pk.Species, pk.Form);
        var fs = string.Join("|", fin.Select(x => $"{x.Item1}:{x.Item2}:{Sd(x.Item1, x.Item2, pk.Gender)}:{Stats(x.Item1, x.Item2)}"));
        Console.WriteLine(string.Join('\t', b + 1, s + 1, pk.Species, pk.Form, pk.Gender, Ko(pk.Species, pk.Form), Sd(pk.Species, pk.Form, pk.Gender), Stats(pk.Species, pk.Form), ev ? 1 : 0, fs));
    }
}

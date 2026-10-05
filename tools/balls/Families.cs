using System.Text;
using System.Text.Json;
using Dexforge;
using PKHeX.Core;
// The families of every dex for the two-colour ball picker: one per first stage the table knows, its members in dex order, each
// member's current first ball for a shiny and for a plain one (and whether that colour was picked for itself), the games it is in.
var ko = GameInfo.GetStrings("ko"); var en = GameInfo.GetStrings("en");
// no one game's tree knows every line (Scarlet's has no Yamask, no Alolan Raichu): the trees of every game are read together
var trees = new[] { EntityContext.Gen7, EntityContext.Gen8, EntityContext.Gen8a, EntityContext.Gen8b, EntityContext.Gen9, EntityContext.Gen9a }.Select(EvolutionTree.GetEvolutionTree).ToList();
IEnumerable<(ushort Species, byte Form)> Pre(ushort sp, byte f) => trees.SelectMany(t => t.Reverse.GetPreEvolutions(sp, f).Select(x => (x.Species, x.Form))).Distinct();
IEnumerable<(ushort Species, byte Form)> Fwd(ushort sp, byte f) => trees.SelectMany(t => t.Forward.GetEvolutions(sp, f).Select(x => (x.Species, x.Form))).Distinct();
var pt = PersonalTable.SV;
// every species and form any dex holds
var keys = new HashSet<(ushort, byte)>();
var games = new Dictionary<(ushort, byte), HashSet<string>>();
void Mark((ushort, byte) k, string g) { keys.Add(k); if (!games.TryGetValue(k, out var s)) games[k] = s = new(); s.Add(g); }
var usum = new SAV7USUM(File.ReadAllBytes("/home/elyss/storage/dexforge/src/Dexforge.Core/Data/template/dex"));
foreach (var p in usum.BoxData.Where(p => p.Species != 0)) if (new LegalityAnalysis(p).EncounterMatch is not MysteryGift) Mark((p.Species, p.Form), "울트라썬");
foreach (var e in Dexforge.Sword.Plan8.All()) if (e.Source != Dexforge.Sword.Source.None && e.Source != Dexforge.Sword.Source.Card && (e.Template?.FixedBall is null or Ball.None)) Mark((e.Species, e.Form), "소드");
foreach (var e in Dexforge.Scarlet.Plan9.All) if (e.Template.FixedBall == Ball.None) Mark((e.Species, e.Form), "스칼렛");
foreach (var e in Dexforge.ZA.Plan9a.All) if (e.Template.FixedBall == Ball.None) Mark((e.Species, e.Form), "Z-A");
string Label(ushort sp, byte f)
{
    var names = FormConverter.GetFormList(sp, ko.types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9);
    return ko.Species[sp] + (f > 0 && f < names.Length && names[f].Trim().Length > 0 ? "(" + names[f].Trim() + ")" : f > 0 ? $"({f})" : "");
}
string Slug(ushort sp, byte f)
{
    var name = en.Species[sp].ToLowerInvariant().Replace("♀", "-f").Replace("♂", "-m").Replace(" ", "").Replace(".", "").Replace("'", "").Replace("’", "").Replace(":", "").Replace("é", "e");
    var forms = FormConverter.GetFormList(sp, en.types, en.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9);
    var fn = f > 0 && f < forms.Length ? forms[f].ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("%", "") : "";
    return fn.Length > 0 ? name + "-" + fn : name;
}
(ushort, byte) Base((ushort, byte) k)
{
    // the earliest stage the table knows, walking back one step at a time
    var (sp, f) = k;
    for (int guard = 0; guard < 4; guard++)
    {
        var pre = Pre(sp, f).Where(keys.Contains).OrderBy(x => x.Species).FirstOrDefault();
        if (pre == default) break;
        (sp, f) = pre;
    }
    return (sp, f);
}
// one family per line of species: the forms of a first stage go together unless a form's line runs into other species
// (the Galarian Meowth's, Yamask's, Corsola's…), which is a line of its own (owner, 2026-10-05: 진화 계통은 하나로, 분기하는 경우 제외)
string Reach((ushort, byte) b)
{
    var seen = new HashSet<ushort>(); var stack = new Stack<(ushort, byte)>(); stack.Push(b);
    while (stack.Count > 0) { var (sp, f) = stack.Pop(); foreach (var e in Fwd(sp, f)) if (seen.Add(e.Species)) stack.Push((e.Species, e.Form)); }
    return string.Join(",", seen.OrderBy(x => x));
}
var baseOf = keys.ToDictionary(k => k, Base);
var lineOf = new Dictionary<(ushort, byte), (ushort, byte)>();   // base node → the base node its family is named after
foreach (var b in baseOf.Values.Distinct())
{
    var same = baseOf.Values.Distinct().Where(o => o.Item1 == b.Item1 && Reach(o) == Reach(b)).OrderBy(o => o.Item2).First();
    lineOf[b] = same;
}
var balls = Balls.Shared;
var groups = keys.GroupBy(k => lineOf[baseOf[k]]).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2).ToList();
var fams = new List<object>();
foreach (var g in groups)
{
    var (bsp, bf) = g.Key;
    var members = g.OrderBy(k => k.Item1).ThenBy(k => k.Item2).ToList();
    var ms = new List<object>();
    foreach (var (sp, f) in members)
    {
        bool bySex = balls.HangsOnSex(sp, f);
        object One(byte? sex) => new
        {
            sex = sex is null ? "" : sex == 0 ? "수" : "암",
            shiny = ko.balllist[(int)balls.Prefer(sp, f, sex, true)[0]], shinyList = string.Join(" > ", balls.Prefer(sp, f, sex, true).Select(b => ko.balllist[(int)b])), shinyOwn = balls.PickedFor(sp, f, sex, true) || !balls.PickedFor(sp, f, sex, false),
            plain = ko.balllist[(int)balls.Prefer(sp, f, sex, false)[0]], plainList = string.Join(" > ", balls.Prefer(sp, f, sex, false).Select(b => ko.balllist[(int)b])), plainOwn = balls.PickedFor(sp, f, sex, false),
        };
        ms.Add(new { id = $"{sp}-{f}", label = Label(sp, f), slug = Slug(sp, f), games = games[(sp, f)].OrderBy(x => x).ToList(), picked = balls.Has(sp, f),
                     final = !Fwd(sp, f).Any(x => keys.Contains((x.Species, x.Form))),
                     sexes = bySex ? new[] { One(0), One(1) } : new[] { One(null) } });
    }
    fams.Add(new { id = $"{bsp}-{bf}", name = Label(bsp, bf), icon = Slug(bsp, bf), members = ms, games = members.SelectMany(k => games[k]).Distinct().OrderBy(x => x).ToList() });
}
File.WriteAllText("/tmp/claude-1000/-home-elyss-storage-pla-permute-bot/fead6049-833e-4015-b469-e41e183a7056/scratchpad/allballs-page/families.json", JsonSerializer.Serialize(new { families = fams }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
Console.WriteLine($"{fams.Count} families, {keys.Count} species/forms");

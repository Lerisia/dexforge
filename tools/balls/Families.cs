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
var plainIn = new Dictionary<(ushort, byte), HashSet<string>>();   // the games where the dex keeps it plain (no shiny to be had, or shiny only by a card)
void Plain((ushort, byte) k, string g) { if (!plainIn.TryGetValue(k, out var s)) plainIn[k] = s = new(); s.Add(g); }
var usum = new SAV7USUM(File.ReadAllBytes("/home/elyss/storage/dexforge/src/Dexforge.Core/Data/template/dex"));
foreach (var p in usum.BoxData.Where(p => p.Species != 0)) if (new LegalityAnalysis(p).EncounterMatch is not MysteryGift) { Mark((p.Species, p.Form), "울트라썬"); if (!p.IsShiny) Plain((p.Species, p.Form), "울트라썬"); }
foreach (var e in Dexforge.Sword.Plan8.All()) if (e.Source != Dexforge.Sword.Source.None && e.Source != Dexforge.Sword.Source.Card && (e.Template?.FixedBall is null or Ball.None)) { Mark((e.Species, e.Form), "소드"); if (!e.Shiny || e.Template?.Shiny == Shiny.Never) Plain((e.Species, e.Form), "소드"); }
foreach (var e in Dexforge.Scarlet.Plan9.All) if (e.Template.FixedBall == Ball.None) { Mark((e.Species, e.Form), "스칼렛"); if (e.ShinyLocked) Plain((e.Species, e.Form), "스칼렛"); }
foreach (var e in Dexforge.ZA.Plan9a.All) if (e.Template.FixedBall == Ball.None) { Mark((e.Species, e.Form), "Z-A"); if (e.ShinyLocked) Plain((e.Species, e.Form), "Z-A"); }
// Legends: Arceus picks no balls (one Hisuian ball for all), but its species belong to their lines (the Ursaluna an Ursaring becomes there)
foreach (var e in Dexforge.Arceus.Plan8a.All) Mark((e.Species, e.Form), "아르세우스");
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
// a family is one line of one form: a stage joins the stage before it unless it is a form branch of that stage — a form the
// stage also reaches in its own form (Pikachu gives a Raichu and an Alolan Raichu; the Alolan one is a line of its own), since
// a different form looks different and gets balls of its own (owner, 2026-10-05: 폼이 다른 건 아예 다르게 생겨서 볼맞춤도 달라요)
bool FormBranch((ushort Species, byte Form) n, (ushort Species, byte Form) pre) => n.Form != pre.Form && Fwd(pre.Species, pre.Form).Any(x => x.Species == n.Species && x.Form == pre.Form);
(ushort, byte) Base((ushort, byte) k)
{
    var (sp, f) = k;
    for (int guard = 0; guard < 4; guard++)
    {
        var pre = Pre(sp, f).Where(keys.Contains).Where(x => !FormBranch((sp, f), x)).OrderBy(x => x.Species).FirstOrDefault();
        if (pre == default) break;
        (sp, f) = pre;
    }
    return (sp, f);
}
var baseOf = keys.ToDictionary(k => k, Base);
// the generation a species and form belongs to: the species' by number, a regional form's by the region's game (Alola 7, Galar and Hisui 8, Paldea and the Bloodmoon 9)
int Gen(ushort sp, byte f)
{
    int g = sp <= 151 ? 1 : sp <= 251 ? 2 : sp <= 386 ? 3 : sp <= 493 ? 4 : sp <= 649 ? 5 : sp <= 721 ? 6 : sp <= 809 ? 7 : sp <= 905 ? 8 : 9;
    var name = FormConverter.GetFormList(sp, ko.types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9).ElementAtOrDefault(f) ?? "";
    if (f > 0 && name.Contains("알로라")) g = Math.Max(g, 7);
    if (f > 0 && (name.Contains("가라르") || name.Contains("히스이"))) g = Math.Max(g, 8);
    if (f > 0 && (name.Contains("팔데아") || name.Contains("붉은 달"))) g = Math.Max(g, 9);
    return g;
}
var balls = Balls.Shared;
// in dex order of the lowest-numbered member (a baby comes after what it hatches into: Pichu sits with Pikachu at 25), named after the first stage
var groups = keys.GroupBy(k => baseOf[k]).OrderBy(g => g.Min(k => k.Item1)).ThenBy(g => g.Min(k => k.Item2)).ToList();
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
        var pi = pt.GetFormEntry(sp, f);
        var plainGames = plainIn.GetValueOrDefault((sp, f)) ?? new();
        var shinyGames = games[(sp, f)].Where(g => g != "아르세우스" && !plainGames.Contains(g)).ToList();
        ms.Add(new { id = $"{sp}-{f}", label = Label(sp, f), slug = Slug(sp, f), games = games[(sp, f)].OrderBy(x => x).ToList(), picked = balls.Has(sp, f), gen = Gen(sp, f),
                     shinyGames = shinyGames.OrderBy(x => x).ToList(), plainGames = plainGames.OrderBy(x => x).ToList(),
                     sexed = !(pi.Genderless || pi.OnlyMale || pi.OnlyFemale),
                     final = !Fwd(sp, f).Any(x => keys.Contains((x.Species, x.Form))),
                     sexes = bySex ? new[] { One(0), One(1) } : new[] { One(null) } });
    }
    // the family sits in the generation of its latest final stage (owner, 2026-10-05: 최종진화체의 세대 기준으로 정렬)
    var finals = members.Where(k => !Fwd(k.Item1, k.Item2).Any(x => keys.Contains((x.Species, x.Form)))).ToList();
    int gen = (finals.Count > 0 ? finals : members).Max(k => Gen(k.Item1, k.Item2));
    fams.Add(new { id = $"{bsp}-{bf}", name = Label(bsp, bf), icon = Slug(bsp, bf), members = ms, games = members.SelectMany(k => games[k]).Distinct().OrderBy(x => x).ToList(), gen, order = members.Min(k => k.Item1) * 100 + members.Min(k => k.Item2) });
}
fams = fams.OrderBy(f => (int)f.GetType().GetProperty("gen")!.GetValue(f)!).ThenBy(f => (int)f.GetType().GetProperty("order")!.GetValue(f)!).ToList();
File.WriteAllText("/tmp/claude-1000/-home-elyss-storage-pla-permute-bot/fead6049-833e-4015-b469-e41e183a7056/scratchpad/allballs-page/families.json", JsonSerializer.Serialize(new { families = fams }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
Console.WriteLine($"{fams.Count} families, {keys.Count} species/forms");

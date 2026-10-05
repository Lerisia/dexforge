using System.Text;
using PKHeX.Core;
using Dexforge;
// One ball table for every game: for each species, form and (where it matters) sex, the balls the owner wants in order of
// preference — the newest pick first (Z-A 2026-10-05, then Scarlet's, then Sword's, then the national dex's list with its own
// fallbacks), each once. The maker takes the first the game allows; a Poké Ball is always the last resort and is not written.
var ko = GameInfo.GetStrings("ko");
var root = "/home/elyss/storage/dexforge/src/Dexforge.Core/Data/";
int BallOf(string name) { int i = Array.IndexOf(ko.balllist, name.Trim()); if (i <= 0) throw new Exception("ball? " + name); return i; }
// key: species, form, sex (-1 any), colour (-1 any, 0 plain) → ordered list of (ball, source). A pick made where the game
// allows no shiny (a static, a raid) was a pick for a plain Pokémon: it goes to the plain list (owner, 2026-10-05).
var lists = new Dictionary<(ushort, byte, int, int), List<(int Ball, string Source)>>();
var lockedSV = Dexforge.Scarlet.Plan9.All.Where(e => e.ShinyLocked).Select(e => (e.Species, e.Form)).ToHashSet();
var lockedZA = Dexforge.ZA.Plan9a.All.Where(e => e.ShinyLocked).Select(e => (e.Species, e.Form)).ToHashSet();
var lockedSW = Dexforge.Sword.Plan8.All().Where(e => !e.Shiny || e.Template?.Shiny == Shiny.Never).Select(e => (e.Species, e.Form)).ToHashSet();
void Add(ushort sp, byte f, int sex, int ball, string source, bool plainOnly = false)
{
    var key = (sp, f, sex, plainOnly ? 0 : -1);
    if (!lists.TryGetValue(key, out var l)) lists[key] = l = new();
    if (!l.Any(x => x.Ball == ball)) l.Add((ball, source));
}
// 1. Z-A: the owner's picks of 2026-10-05
foreach (var l in File.ReadAllLines("sources/za-balls.tsv").Skip(1))
{
    var f = l.Split('\t'); if (!f[5].StartsWith("주인장 선택")) continue;
    var k = (ushort.Parse(f[0]), byte.Parse(f[1])); Add(k.Item1, k.Item2, f[2] == "수" ? 0 : f[2] == "암" ? 1 : -1, BallOf(f[3]), "Z-A", lockedZA.Contains(k));
}
// 2. Scarlet: the owner's picks (the drafts there are copies of Sword's and the national dex's)
foreach (var l in File.ReadAllLines("sources/scarlet-balls.tsv").Skip(1))
{
    var f = l.Split('\t'); if (!f[5].StartsWith("주인장")) continue;
    var k = (ushort.Parse(f[0]), byte.Parse(f[1])); Add(k.Item1, k.Item2, f[2] == "수" ? 0 : f[2] == "암" ? 1 : -1, BallOf(f[3]), "스칼렛", lockedSV.Contains(k));
}
// 3. Sword: the table (forms by name, eighth-generation context)
foreach (var raw in File.ReadAllLines("sources/sword-balls.tsv"))
{
    var line = raw.Split('#')[0].TrimEnd(); if (line.Trim().Length == 0) continue;
    var f = line.Split('\t'); var sp = ushort.Parse(f[0]);
    var forms = FormConverter.GetFormList(sp, ko.types, ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen8);
    int idx = Array.IndexOf(forms, f[1].Trim()); byte form = idx >= 0 ? (byte)idx : (byte)0;
    int sex = f.Length > 3 && f[3].Trim() == "수" ? 0 : f.Length > 3 && f[3].Trim() == "암" ? 1 : -1;
    Add(sp, form, sex, BallOf(f[2]), "소드", lockedSW.Contains((sp, form)));
}
// 4. The national dex: the owner's list (dex-balls.tsv), lines in order, a family line for every form in the template, later lines winning;
//    each line carries its own fallbacks
var sav = new SAV7USUM(File.ReadAllBytes(root + "template/dex"));
var inDex = sav.BoxData.Where(p => p.Species != 0).ToList();
var tree = EvolutionTree.GetEvolutionTree(EntityContext.Gen7);
HashSet<ushort> Family(ushort sp)
{
    var set = new HashSet<ushort> { sp };
    foreach (var p in tree.Reverse.GetPreEvolutions(sp, 0)) set.Add(p.Species);
    void Down(ushort s) { foreach (var e in tree.Forward.GetEvolutions(s, 0)) if (set.Add(e.Species)) Down(e.Species); }
    foreach (var s in set.ToList()) Down(s);
    return set;
}
var usum = new Dictionary<(ushort, byte, int), List<int>>();
foreach (var line in File.ReadAllLines("sources/usum-balls.tsv"))
{
    if (line.Length == 0 || line.StartsWith('#')) continue;
    var f = line.Split('\t'); string name = f[0].Trim();
    bool family = name.StartsWith("계열:"); if (family) name = name[3..];
    int sex = name.EndsWith("/수") ? 0 : name.EndsWith("/암") ? 1 : -1; if (sex >= 0) name = name[..^2];
    int species = Array.IndexOf(ko.specieslist, name); if (species <= 0) throw new Exception("species? " + name);
    var balls = f.Skip(2).Select(x => x.Trim()).Where(x => x.Length != 0).Select(BallOf).ToList();
    if (family)
    {
        var members = Family((ushort)species);
        foreach (var (sp, form) in inDex.Where(p => members.Contains(p.Species)).Select(p => (p.Species, p.Form)).Distinct())
            usum[(sp, form, sex)] = balls;
    }
    else
    {
        byte form = f[1].Trim().Length == 0 ? (byte)0 : byte.Parse(f[1]);
        usum[((ushort)species, form, sex)] = balls;
    }
}
var plainInDex = inDex.Where(p => !p.IsShiny && new LegalityAnalysis(p).EncounterMatch is not MysteryGift).Select(p => (p.Species, p.Form)).ToHashSet();
foreach (var (key, balls) in usum) foreach (var b in balls) Add(key.Item1, key.Item2, key.Item3, b, "전국도감", plainInDex.Contains((key.Item1, key.Item2)));
// the template's actual ball, as the last word for what the list left out (a species the list never named)
foreach (var p in inDex)
{
    if (new LegalityAnalysis(p).EncounterMatch is MysteryGift) continue;
    bool named = lists.Keys.Any(k => k.Item1 == p.Species && k.Item2 == p.Form);
    if (!named) Add(p.Species, p.Form, -1, p.Ball, "전국도감 세이브", !p.IsShiny);
}
// 5. the drafts of the Scarlet and Z-A tables, confirmed as they stood (the owner reviewed everything up to the eighth generation): behind everything, never a Cherish Ball
foreach (var (file, source) in new[] { ("scarlet/balls.tsv", "스칼렛 초안"), ("za/balls.tsv", "Z-A 초안") })
    foreach (var l in File.ReadAllLines("sources/" + file.Replace("/balls.tsv", "-balls.tsv")).Skip(1))
    {
        var f = l.Split('\t'); if (f[5].StartsWith("주인장") || f[5].StartsWith("없음") || f[3] == "프레셔스볼") continue;
        var k = (ushort.Parse(f[0]), byte.Parse(f[1])); Add(k.Item1, k.Item2, f[2] == "수" ? 0 : f[2] == "암" ? 1 : -1, BallOf(f[3]), source, (source.StartsWith("Z-A") ? lockedZA : lockedSV).Contains(k));
    }
// rows: for each species and form, by sex where any source split it, a generic list and a plain-only list where one exists
var keys = lists.Keys.ToList();
var sb = new StringBuilder("species\tform\tsex\tcolor\tballs\tnote\n");
int rows = 0;
foreach (var (sp, form) in keys.Select(k => (k.Item1, k.Item2)).Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2))
{
    bool split = keys.Any(k => k.Item1 == sp && k.Item2 == form && k.Item3 >= 0);
    var label = ko.Species[sp] + (form != 0 ? "(" + form + ")" : "");
    foreach (int sex in split ? new[] { 0, 1 } : new[] { -1 })
    foreach (int colour in new[] { -1, 0 })
    {
        var own = lists.GetValueOrDefault((sp, form, sex, colour)) ?? new();
        var generic = sex >= 0 ? lists.GetValueOrDefault((sp, form, -1, colour)) ?? new() : new();
        var merged = own.Concat(generic.Where(g => !own.Any(o => o.Ball == g.Ball))).Where(x => x.Ball != (int)Ball.Poke).Take(3).ToList();   // three ranks, then a Poké Ball (owner)
        if (merged.Count == 0) continue;
        sb.Append($"{sp}\t{form}\t{(sex == 0 ? "수" : sex == 1 ? "암" : "")}\t{(colour == 0 ? "일반" : "")}\t{string.Join(">", merged.Select(x => ko.balllist[x.Ball]))}\t{label}: {string.Join(">", merged.Select(x => x.Source))}\n"); rows++;
    }
}
File.WriteAllText(root + "balls.tsv", sb.ToString(), new UTF8Encoding(false));
Console.WriteLine($"{rows} rows, {keys.Select(k => (k.Item1, k.Item2)).Distinct().Count()} species/forms; lists longer than one: {lists.Values.Count(l => l.Count > 1)}; plain-only rows {keys.Count(k => k.Item4 == 0)}");
// check: the template's ball is the first allowed of its list? (just report where the template's ball is not first)
int notFirst = 0;
foreach (var p in inDex)
{
    if (new LegalityAnalysis(p).EncounterMatch is MysteryGift) continue;
    int col = p.IsShiny ? -1 : 0;
    var l = lists.GetValueOrDefault((p.Species, p.Form, (int)p.Gender, col)) ?? lists.GetValueOrDefault((p.Species, p.Form, -1, col)) ?? lists.GetValueOrDefault((p.Species, p.Form, (int)p.Gender, -1)) ?? lists.GetValueOrDefault((p.Species, p.Form, -1, -1));
    if (l is null) { Console.WriteLine("  no list: " + ko.Species[p.Species] + "-" + p.Form); continue; }
    if (l[0].Ball != p.Ball) notFirst++;
}
Console.WriteLine($"template Pokémon whose ball is not the list's first: {notFirst} of {inDex.Count}");

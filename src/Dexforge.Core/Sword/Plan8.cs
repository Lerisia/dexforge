using System.Reflection;
using PKHeX.Core;

namespace Dexforge.Sword;

public enum Source { Egg, Fossil, Static, Gift, Adventure, Wild, Card, Trade, Go, None }

/// <summary>One box entry to make: a species and form, where it comes from, and what it is hatched or caught as before evolving.</summary>
public sealed record Entry(ushort Species, byte Form, Source Source, ushort FromSpecies, byte FromForm, IEncounterTemplate? Template, string Note = "", bool Shiny = true, Ball? BallOverride = null, byte? Gender = null, bool FromShield = false)
{
    public bool Evolves => FromSpecies != Species;
    public bool ChangesForm => FromSpecies == Species && FromForm != Form;
    /// <summary>Other ways to make this entry, tried in order when the first does not pass.</summary>
    public List<Entry> Fallbacks { get; init; } = [];
}

/// <summary>
/// Every species and form that can sit in a Sword box, with its source by the owner's rules: an egg wherever the line can
/// breed (hatched as the first stage, then evolved), fossils as fossils, legendaries from their static encounters or Dynamax
/// Adventures, gifts as gifts, mythicals from their event cards; battle-only and fused forms are left out.
/// </summary>
public static class Plan8
{
    public static readonly PersonalTable8SWSH Table = PersonalTable.SWSH;
    public static readonly EvolutionTree Tree = EvolutionTree.GetEvolutionTree(EntityContext.Gen8);
    public static readonly GameStrings Ko = GameInfo.GetStrings("ko");

    public static readonly EncounterStatic8[] Statics;
    public static readonly EncounterStatic8U[] Adventures;
    public static readonly EncounterSlot8[] Slots;
    public static readonly EncounterTrade8[] Trades;
    public static readonly IReadOnlyList<WC8> Cards;
    public static readonly EncounterSlot8GO[] GoSlots;
    /// <summary>HOME gift cards are only used for species a tracker is carried for.</summary>
    public static readonly Dictionary<ushort, ulong> Trackers = new();

    static Plan8()
    {
        var asm = typeof(PK8).Assembly;
        T Get<T>(string type, string field) => (T)(asm.GetType(type)!.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) ?? throw new MissingFieldException(field)).GetValue(null)!;
        Statics = Get<EncounterStatic8[]>("PKHeX.Core.Encounters8", "StaticSWSH").Concat(Get<EncounterStatic8[]>("PKHeX.Core.Encounters8", "StaticSW")).ToArray();
        Adventures = Get<EncounterStatic8U[]>("PKHeX.Core.Encounters8Nest", "DynAdv_SWSH");
        Slots = Get<EncounterArea8[]>("PKHeX.Core.Encounters8", "SlotsSW_Symbol").Concat(Get<EncounterArea8[]>("PKHeX.Core.Encounters8", "SlotsSW_Hidden")).SelectMany(a => a.Slots).ToArray();
        Trades = Get<EncounterTrade8[]>("PKHeX.Core.Encounters8", "TradeSWSH").Concat(Get<EncounterTrade8[]>("PKHeX.Core.Encounters8", "TradeSW")).ToArray();
        // Trackers carried inside: a tracker is what HOME stamps on a Pokemon, and these four came through HOME with the numbers
        // a publicly circulated box file gives them. (The help says what that means for moving them to HOME.)
        {
            var names = new Dictionary<string, ushort>();
            for (ushort i = 1; i < Ko.specieslist.Length; i++) if (Ko.specieslist[i].Length != 0) names[Ko.specieslist[i]] = i;
            foreach (var raw in Resources.Lines("sword.trackers"))
            {
                var line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                var f = line.Split('\t');
                if (f.Length < 2 || !names.TryGetValue(f[0].Trim(), out var sp)) throw new InvalidDataException("trackers: " + raw);
                Trackers[sp] = Convert.ToUInt64(f[1].Trim().Replace("0x", ""), 16);
            }
        }
        // HOME gifts need a tracker nobody can make: only where the owner brought one.
        Cards = EncounterEvent.MGDB_G8.Where(c => c.IsEntity && c.Species != 0 && (!c.IsHOMEGift || Trackers.ContainsKey(c.Species))).ToList();
        GoSlots = Get<EncounterArea8g[]>("PKHeX.Core.EncountersGO", "SlotsGO").SelectMany(a => a.Slots).ToArray();
    }

    private static readonly ushort[] Fossils = [880, 881, 882, 883];

    /// <summary>Dynamax Adventure bosses only Shield has: caught there by a friend and traded over.</summary>
    private static readonly ushort[] ShieldAdventures = [249, 380, 382, 484, 642, 644, 717, 792]; // Lugia, Latias, Kyogre, Palkia, Thundurus, Zekrom, Yveltal, Lunala

    /// <summary>Species and forms the Alola dex keeps in both sexes, because they look different: the Sword dex does the same.</summary>
    public static readonly HashSet<(ushort Species, byte Form)> BothSexes =
        Sexes.Both(Enumerable.Range(0, 960).Select(i => new SAV7USUM(Making.Template()).GetBoxSlotAtIndex(i)).Where(p => p.Species != 0));

    /// <summary>Whether somebody in this species' line can breed, so that the line can be hatched.</summary>
    public static bool LineBreeds(ushort species, byte form) =>
        Family(species, form, Tree).Any(m => Table[m.Species, m.Form].EggGroup1 != 15 && Table[m.Species, m.Form].EggGroup2 != 15);

    /// <summary>The evolutionary line through one species and form, as species+form pairs.</summary>
    public static HashSet<(ushort Species, byte Form)> Family(ushort species, byte form, EvolutionTree tree)
    {
        var set = new HashSet<(ushort, byte)> { (species, form) };
        foreach (var pre in tree.Reverse.GetPreEvolutions(species, form)) set.Add((pre.Species, pre.Form));
        var queue = new Queue<(ushort, byte)>(set);
        while (queue.Count != 0)
        {
            var (s, f) = queue.Dequeue();
            foreach (var next in tree.Forward.GetEvolutions(s, f))
                if (set.Add((next.Species, next.Form))) queue.Enqueue((next.Species, next.Form));
        }
        return set;
    }

    public static (ushort Species, byte Form) Base(ushort species, byte form)
    {
        var pre = Tree.Reverse.GetPreEvolutions(species, form).ToList();
        return pre.Count == 0 ? (species, form) : (pre[0].Species, pre[0].Form);
    }

    /// <summary>All entries, in dex order; forms that cannot be made say so in their Note with Source.None.</summary>
    public static List<Entry> All()
    {
        var list = new List<Entry>();
        for (ushort sp = 1; sp <= Table.MaxSpeciesID; sp++)
        {
            var pi0 = Table.GetFormEntry(sp, 0);
            if (!pi0.IsPresentInGame) continue;
            byte forms = Math.Max((byte)1, pi0.FormCount);
            for (byte f = 0; f < forms; f++)
            {
                if (!Table.GetFormEntry(sp, f).IsPresentInGame) continue;
                if (FormInfo.IsBattleOnlyForm(sp, f, 8) || FormInfo.IsFusedForm(sp, f, 8)) continue;
                if (sp == 25 && f != 0) continue; // cap Pikachu: cards of other games only
                var entry = Decide(sp, f);
                // Alcremie: the creams are its own colours, so they stay ordinary; one shiny one is added after them.
                if (sp == 869) entry = entry with { Shiny = false };
                if (BothSexes.Contains((sp, f)) && entry.Source != Source.None)
                {
                    // The sexes look different: one of each, as the Alola dex keeps them.
                    list.Add(entry with { Gender = 0, Fallbacks = entry.Fallbacks.Select(x => x with { Gender = 0 }).ToList() });
                    list.Add(entry with { Gender = 1, Fallbacks = entry.Fallbacks.Select(x => x with { Gender = 1 }).ToList() });
                    continue;
                }
                list.Add(entry);
            }
            if (sp == 869) list.Add(Decide(sp, 0) with { Shiny = true, BallOverride = Ball.Luxury, Note = "이로치" });
        }
        return list;
    }

    public static string FormName(ushort species, byte form)
    {
        var names = FormConverter.GetFormList(species, Ko.types, Ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen8);
        return form < names.Length ? names[form] : $"폼 {form}";
    }

    private static Entry Decide(ushort sp, byte f)
    {
        var all = Candidates(sp, f).ToList();
        if (all.Count == 0)
        {
            var fc = DecideFormChange(sp, f);
            return fc;
        }
        return all[0] with { Fallbacks = all.Skip(1).ToList() };
    }

    /// <summary>Every way to make this species and form, in the owner's order of preference.</summary>
    private static IEnumerable<Entry> Candidates(ushort sp, byte f)
    {
        if (LineBreeds(sp, f))
        {
            var (bs, bf) = Base(sp, f);
            if (Breeding.CanHatchAsEgg(bs, bf, EntityContext.Gen8) && bs != 132)
                yield return new Entry(sp, f, Source.Egg, bs, bf, null);
        }
        if (Fossils.Contains(sp))
            yield return new Entry(sp, f, Source.Fossil, sp, f, Statics.First(e => e.Species == sp && e.Gift));
        foreach (var gift in Statics.Where(e => e.Species == sp && e.Form == f && e.Gift))
            yield return new Entry(sp, f, Source.Gift, sp, f, gift);
        foreach (var st in Statics.Where(e => e.Species == sp && e.Form == f && !e.Gift))
            yield return new Entry(sp, f, Source.Static, sp, f, st);
        foreach (var ad in Adventures.Where(e => e.Species == sp && e.Form == f))
            yield return new Entry(sp, f, Source.Adventure, sp, f, ad, ShieldAdventures.Contains(sp) ? "실드에서 잡아 교환" : "", FromShield: ShieldAdventures.Contains(sp));
        foreach (var pre in Tree.Reverse.GetPreEvolutions(sp, f))
        {
            var g = Statics.FirstOrDefault(e => e.Species == pre.Species && e.Form == pre.Form && e.Gift);
            if (g is not null) yield return new Entry(sp, f, Source.Gift, pre.Species, pre.Form, g);
        }
        foreach (var wild in Slots.Where(e => e.Species == sp && e.Form == f).Take(3))
            yield return new Entry(sp, f, Source.Wild, sp, f, wild);
        foreach (var pre in Tree.Reverse.GetPreEvolutions(sp, f))
            foreach (var w in Slots.Where(e => e.Species == pre.Species && e.Form == pre.Form).Take(2))
                yield return new Entry(sp, f, Source.Wild, pre.Species, pre.Form, w);
        // From Pokémon GO through HOME, only with a tracker the owner brought: a shiny-capable window in the year first.
        if (Trackers.ContainsKey(sp))
            foreach (var go in GoSlots.Where(e => e.Species == sp && e.Form == f && e.Shiny != Shiny.Never)
                         .OrderByDescending(e => e.LongName.Contains($"{Maker8.DefaultYear}.")).ThenBy(e => e.LevelMin))
                yield return new Entry(sp, f, Source.Go, sp, f, go);
        foreach (var card in Cards.Where(c => c.Species == sp && c.Form == f))
            yield return new Entry(sp, f, Source.Card, sp, f, card);
        foreach (var trade in Trades.Where(e => e.Species == sp && e.Form == f))
            yield return new Entry(sp, f, Source.Trade, sp, f, trade);
    }

    private static Entry DecideFormChange(ushort sp, byte f)
    {
        // A form reached by changing form (a held item, a key item): made as a form that can be had, and changed afterwards.
        byte forms = Math.Max((byte)1, Table.GetFormEntry(sp, 0).FormCount);
        for (byte g = 0; g < forms; g++)
        {
            if (g == f || !FormInfo.IsFormChangeable(sp, g, f, EntityContext.Gen8, EntityContext.Gen8)) continue;
            var options = Candidates(sp, g).Select(b => b with { Form = f, Note = $"{FormName(sp, g)} 폼에서 폼 체인지" }).ToList();
            if (options.Count != 0)
                return options[0] with { Fallbacks = options.Skip(1).ToList() };
        }
        return new Entry(sp, f, Source.None, sp, f, null, "소드에서 얻는 길이 없음");
    }
}

using System.Reflection;
using PKHeX.Core;

namespace Dexforge.Scarlet;

/// <summary>Where a Scarlet entry comes from.</summary>
public enum Source9
{
    /// <summary>Caught in the wild (a mass outbreak, as a hunter would), from a slot of the species.</summary>
    Wild,
    /// <summary>A static encounter: a symbol that stands where it stands, a gift; shiny-locked almost always.</summary>
    Static,
    /// <summary>Hatched: the Paldean starters, and what has no wild form but a parent that can be traded for.</summary>
    Egg,
    /// <summary>A Tera Raid, only where nothing else gives the species (event raids: Mewtwo, the Hisuian starters' finals, Dialga, Palkia, Walking Wake, Iron Leaves).</summary>
    Raid,
    /// <summary>An in-game trade (the Alolan Meowth).</summary>
    Trade,
}

/// <summary>
/// One box entry of the Scarlet dex: a species and form, where it comes from, and what it is caught or hatched as before
/// evolving. <paramref name="Violet"/> marks what only Violet has: caught in the trainer's own Violet and traded over.
/// </summary>
public sealed record Entry9(ushort Species, byte Form, Source9 Source, ushort FromSpecies, byte FromForm, IEncounterTemplate Template, bool Violet, bool ShinyLocked, string Note)
{
    public bool Evolves => FromSpecies != Species || FromForm != Form;
}

/// <summary>
/// The Scarlet dex as the owner planned it (scarlet.plan, 950 species and forms): everything the game can hold, each
/// from the source the plan names — wild wherever the species is wild, evolved from a wild pre-evolution otherwise, static
/// where the game fixes it, hatched only for the starters and the Alolan Persian, a raid only where nothing else gives it.
/// </summary>
public static class Plan9
{
    public static readonly GameStrings Ko = GameInfo.GetStrings("ko");
    public static readonly EvolutionTree Tree = EvolutionTree.GetEvolutionTree(EntityContext.Gen9);
    public static readonly PersonalTable9SV Table = PersonalTable.SV;

    public static readonly EncounterSlot9[] Slots;
    public static readonly EncounterStatic9[] Statics;
    public static readonly EncounterFixed9[] Fixed;
    public static readonly EncounterTera9[] Tera;
    public static readonly EncounterDist9[] Dist;
    public static readonly EncounterMight9[] Might;
    public static readonly EncounterTrade9[] Trades;

    static Plan9()
    {
        var t = typeof(PK9).Assembly.GetType("PKHeX.Core.Encounters9")!;
        T Get<T>(string f) => (T)t.GetField(f, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
        Slots = Get<EncounterArea9[]>("Slots").SelectMany(a => a.Slots).ToArray();
        Statics = Get<EncounterStatic9[]>("Encounter_SV").Concat(Get<EncounterStatic9[]>("StaticSL")).Concat(Get<EncounterStatic9[]>("StaticVL")).ToArray();
        Fixed = Get<EncounterFixed9[]>("Fixed");
        Tera = Get<EncounterTera9[]>("TeraBase").Concat(Get<EncounterTera9[]>("TeraDLC1")).Concat(Get<EncounterTera9[]>("TeraDLC2")).ToArray();
        Dist = Get<EncounterDist9[]>("Dist");
        Might = Get<EncounterMight9[]>("Might");
        Trades = Get<EncounterTrade9[]>("TradeGift_SV");
    }

    /// <summary>One line of the plan.</summary>
    public sealed record Row(ushort Species, byte Form, string Name, string FormName, string Source, string Why, bool ShinyLocked, bool Violet, bool Final);

    private static readonly Lazy<IReadOnlyList<Row>> rows = new(() =>
    {
        var table = new EventBox.Rows(Embedded.Text("scarlet.plan"));
        return table.All.Select(r => new Row(ushort.Parse(table.Get(r, "번호")), byte.Parse(table.Get(r, "폼")), table.Get(r, "이름"), table.Get(r, "폼이름"), table.Get(r, "출처"), table.Get(r, "근거"),
                                             table.Get(r, "이로치") == "불가", table.Get(r, "버전") == "바이올렛", table.Get(r, "최종") == "1")).ToList();
    });
    public static IReadOnlyList<Row> Rows => rows.Value;

    private static readonly Lazy<IReadOnlyList<Entry9>> all = new(Build);
    public static IReadOnlyList<Entry9> All => all.Value;

    public static string Label(ushort species, byte form)
    {
        var names = FormConverter.GetFormList(species, Ko.Types, Ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9);
        return Ko.Species[species] + (form > 0 && form < names.Length ? $"({names[form]})" : "");
    }

    /// <summary>The Paldean starters hatch (owner: a starter is bred, raids give no shiny); the Alolan Persian hatches from the traded Meowth.</summary>
    private static readonly HashSet<ushort> HatchedStarters = [906, 907, 908, 909, 910, 911, 912, 913, 914];

    private static List<Entry9> Build()
    {
        var byKey = Rows.ToDictionary(r => (r.Species, r.Form));
        var list = new List<Entry9>();
        foreach (var r in Rows)
        {
            if (r.Source == "없음") continue;
            var e = Decide(r, byKey);
            if (e is not null) list.Add(e);
        }
        return list;
    }

    private static Entry9? Decide(Row r, Dictionary<(ushort, byte), Row> byKey)
    {
        ushort sp = r.Species; byte f = r.Form;
        if (HatchedStarters.Contains(sp))
        {
            var bs = sp - (sp - 906) % 3; // 906/909/912
            return new Entry9(sp, f, Source9.Egg, (ushort)bs, 0, new EncounterEgg9((ushort)bs, 0, GameVersion.SL), false, false, "알 (스타팅은 알로)");
        }
        switch (r.Source)
        {
            case "야생": return Wild(sp, f, sp, f, r);
            case "고정": return Static(sp, f, r);
            case "알":
                // the Alolan Persian: an egg of the Meowth got by trade
                return new Entry9(sp, f, Source9.Egg, 52, 1, new EncounterEgg9(52, 1, GameVersion.SL), r.Violet, false, "알 — " + r.Why);
            case "교환":
                var trade = Trades.FirstOrDefault(t => t.Species == sp && t.Form == f) ?? throw new InvalidOperationException($"plan: no trade for {Label(sp, f)}");
                return new Entry9(sp, f, Source9.Trade, sp, f, trade, false, true, "게임 안 교환");
            case "레이드":
                IEncounterTemplate raid = (IEncounterTemplate?)Might.FirstOrDefault(m => m.Species == sp && m.Form == f) ?? (IEncounterTemplate?)Dist.FirstOrDefault(d => d.Species == sp && d.Form == f)
                    ?? throw new InvalidOperationException($"plan: no raid for {Label(sp, f)}");
                return new Entry9(sp, f, Source9.Raid, sp, f, raid, r.Violet, raid.Shiny == Shiny.Never, "레이드에서만 (이로치 불가)");
            case "진화":
            case "진화(고정)":
            {
                // the nearest pre-evolution the plan gets from somewhere: caught (or hatched, or handed) and evolved up
                foreach (var pre in Tree.Reverse.GetPreEvolutions(sp, f).Reverse())
                {
                    if (!byKey.TryGetValue((pre.Species, pre.Form), out var pr) || pr.Source is "없음" or "진화" or "진화(고정)") continue;
                    var origin = Decide(pr, byKey);
                    if (origin is null) continue;
                    return origin with { Species = sp, Form = f, Note = $"{Label(pre.Species, pre.Form)}({origin.Note})에서 진화", Violet = r.Violet || origin.Violet };
                }
                throw new InvalidOperationException($"plan: nothing to evolve {Label(sp, f)} from");
            }
            default: throw new InvalidOperationException($"plan: source '{r.Source}' of {Label(sp, f)}");
        }
    }

    private static Entry9 Wild(ushort sp, byte f, ushort baseSp, byte baseF, Row r)
    {
        var slot = Slots.Where(s => s.Species == baseSp && (s.Form == baseF || s.IsRandomUnspecificForm)).OrderBy(s => s.LevelMin).ThenBy(s => s.Location).FirstOrDefault()
            ?? throw new InvalidOperationException($"plan: no wild slot for {Label(baseSp, baseF)}");
        return new Entry9(sp, f, Source9.Wild, baseSp, baseF, slot, r.Violet, false, "야생" + (r.Violet ? " (바이올렛에서 잡아 교환)" : ""));
    }

    private static Entry9 Static(ushort sp, byte f, Row r)
    {
        var version = r.Violet ? GameVersion.VL : GameVersion.SL;
        // the version's own first; a ball of the trainer's choosing over a fixed one (Koraidon ridden cannot be traded, Koraidon caught can)
        var st = Statics.Where(s => s.Species == sp && s.Form == f && (s.Version == version || s.Version == GameVersion.SV)).OrderBy(s => s.Version == GameVersion.SV ? 1 : 0).ThenBy(s => s.FixedBall != Ball.None ? 1 : 0).ThenBy(s => s.Level).FirstOrDefault();
        if (st is not null) return new Entry9(sp, f, Source9.Static, sp, f, st, r.Violet, st.Shiny == Shiny.Never, "고정 " + (st.Shiny == Shiny.Never ? "(이로치 불가)" : "") + (r.Violet ? " 바이올렛에서 잡아 교환" : ""));
        var fx = Fixed.Where(s => s.Species == sp && s.Form == f).OrderBy(s => s.Level).FirstOrDefault()
            ?? throw new InvalidOperationException($"plan: no static for {Label(sp, f)}");
        return new Entry9(sp, f, Source9.Static, sp, f, fx, r.Violet, fx.Shiny == Shiny.Never, "고정 심볼" + (r.Violet ? " (바이올렛에서 잡아 교환)" : ""));
    }
}

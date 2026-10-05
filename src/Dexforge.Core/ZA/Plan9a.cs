using System.Reflection;
using PKHeX.Core;

namespace Dexforge.ZA;

/// <summary>Where a Z-A entry's first stage comes from.</summary>
public enum Source9a
{
    /// <summary>A hyperspace (Rogue Mega / 이차원) slot: where the owner hunts, in the lowest-starred zone the species appears in.</summary>
    Hyperspace,
    /// <summary>A wild slot of Lumiose City, for what hyperspace never has.</summary>
    Wild,
    /// <summary>A static encounter: a legendary, a story Pokémon.</summary>
    Static,
    /// <summary>A gift: the starters, the fossils, the mythicals handed over.</summary>
    Gift,
    /// <summary>An in-game trade.</summary>
    Trade,
}

/// <summary>
/// One box entry of the Z-A dex: a species and form, what is caught first (its species, form and template) and whether
/// it is then evolved or changed in form. The slot in <paramref name="Template"/> is the plain, lowest-level one; the
/// maker asks <see cref="Plan9a.Slot"/> again for an alpha or a sex.
/// </summary>
public sealed record Entry9a(ushort Species, byte Form, Source9a Source, ushort FromSpecies, byte FromForm, IEncounterTemplate Template, bool ShinyLocked, string Note, byte? Gender = null)
{
    public bool Evolves => FromSpecies != Species;
    public bool ChangesForm => FromSpecies == Species && FromForm != Form;
    /// <summary>Whether hyperspace has an alpha of what is caught.</summary>
    public bool CanBeAlpha => Source == Source9a.Hyperspace && Plan9a.Slot(FromSpecies, FromForm, alpha: true, null) is not null;
}

/// <summary>
/// The Z-A dex: every species and form the game's data holds that a box can keep (no Mega or other battle-only forms),
/// each from the first source that has it, in the order the owner set for the hex bot: the species itself in
/// hyperspace; an earlier stage in hyperspace, caught and evolved; the species in the wild; a static, a gift or a trade;
/// an earlier stage from those; a form reached by changing another form of the species. What none of these gives
/// (the Vivillon patterns Lumiose never shows) is left out and listed.
/// </summary>
public static class Plan9a
{
    public static readonly PersonalTable9ZA Table = PersonalTable.ZA;
    public static readonly EvolutionTree Tree = EvolutionTree.GetEvolutionTree(EntityContext.Gen9a);
    public static readonly GameStrings Ko = GameInfo.GetStrings("ko");
    public const ushort HyperspaceLocation = 273;

    public static readonly EncounterSlot9a[] Hyperspace;
    public static readonly EncounterSlot9a[] Wild;
    public static readonly EncounterStatic9a[] Statics;
    public static readonly EncounterGift9a[] Gifts;
    public static readonly EncounterTrade9a[] Trades;

    /// <summary>The species the owner boxes in both sexes: none picked yet (Pyroar's mane is the one real difference; the owner decides).</summary>
    public static readonly ushort[] BothSexes = [];

    static Plan9a()
    {
        var t = typeof(PA9).Assembly.GetType("PKHeX.Core.Encounters9a")!;
        T Get<T>(string f) => (T)t.GetField(f, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
        Hyperspace = Get<EncounterArea9a[]>("Hyperspace").SelectMany(a => a.Slots).ToArray();
        Wild = Get<EncounterArea9a[]>("Slots").SelectMany(a => a.Slots).ToArray();
        Statics = Get<EncounterStatic9a[]>("Static");
        Gifts = Get<EncounterGift9a[]>("Gifts");
        Trades = Get<EncounterTrade9a[]>("Trades");
    }

    public static string FormName(ushort species, byte form)
    {
        var list = FormConverter.GetFormList(species, Ko.types, Ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9a);
        return form < list.Length ? list[form] : form.ToString();
    }

    public static string Label(ushort species, byte form) => Ko.Species[species] + (form != 0 ? "(" + FormName(species, form).Trim() + ")" : "");

    /// <summary>Every species and form the game's data holds, battle-only forms (the Megas) included.</summary>
    public static IEnumerable<(ushort Species, byte Form)> Present()
    {
        for (ushort sp = 1; sp <= Table.MaxSpeciesID; sp++)
        {
            var pi = Table[sp];
            for (byte f = 0; f < pi.FormCount; f++) if (Table.GetFormEntry(sp, f).IsPresentInGame) yield return (sp, f);
        }
    }

    /// <summary>The lowest-level hyperspace slot of a species and form, alpha or not, of a sex where the slots fix one.</summary>
    public static EncounterSlot9a? Slot(ushort species, byte form, bool alpha, byte? gender) =>
        Hyperspace.Where(s => s.Species == species && s.Form == form && s.IsAlpha == alpha && (gender is null || s.Gender >= 2 || s.Gender == gender))
                  .OrderBy(s => s.LevelMin).ThenBy(s => s.LevelMax).FirstOrDefault();

    /// <summary>The lowest-level wild slot of a species and form (hyperspace never has it), of a sex where the slots fix one.</summary>
    public static EncounterSlot9a? WildSlot(ushort species, byte form, bool alpha, byte? gender) =>
        Wild.Where(s => s.Species == species && s.Form == form && s.IsAlpha == alpha && (gender is null || s.Gender >= 2 || s.Gender == gender))
            .OrderBy(s => s.Shiny == Shiny.Never ? 1 : 0).ThenBy(s => s.LevelMin).ThenBy(s => s.LevelMax).FirstOrDefault();

    private static readonly Lazy<(List<Entry9a> All, List<(ushort, byte)> LeftOut)> built = new(Build);
    public static IReadOnlyList<Entry9a> All => built.Value.All;
    /// <summary>The species and forms the game's data holds but nothing in the game gives (battle-only forms are not listed).</summary>
    public static IReadOnlyList<(ushort Species, byte Form)> LeftOut => built.Value.LeftOut;

    private static (List<Entry9a>, List<(ushort, byte)>) Build()
    {
        var list = new List<Entry9a>(); var left = new List<(ushort, byte)>();
        foreach (var (sp, f) in Present())
        {
            if (FormInfo.IsBattleOnlyForm(sp, f, 9)) continue;
            var e = Decide(sp, f) ?? ByFormChange(sp, f);
            if (e is null) { left.Add((sp, f)); continue; }
            if (BothSexes.Contains(sp) && !Table.GetFormEntry(sp, f).Genderless)
            {
                list.Add(e with { Gender = 0, Note = e.Note + " 수컷" });
                list.Add(e with { Gender = 1, Note = e.Note + " 암컷" });
            }
            else list.Add(e);
        }
        return (list, left);
    }

    /// <summary>The first source that gives a species and form, in the owner's order; none when nothing does.</summary>
    private static Entry9a? Decide(ushort sp, byte f)
    {
        if (Slot(sp, f, false, null) is { } own) return new Entry9a(sp, f, Source9a.Hyperspace, sp, f, own, false, "이차원");
        // the nearest earlier stage hyperspace has (PKHeX lists the earliest first)
        foreach (var pre in Tree.Reverse.GetPreEvolutions(sp, f).Reverse())
            if (Slot(pre.Species, pre.Form, false, null) is { } earlier)
                return new Entry9a(sp, f, Source9a.Hyperspace, pre.Species, pre.Form, earlier, false, $"{Label(pre.Species, pre.Form)}(이차원)에서 진화");
        if (Direct(sp, f) is { } direct) return direct;
        foreach (var pre in Tree.Reverse.GetPreEvolutions(sp, f).Reverse())
            if (Direct(pre.Species, pre.Form) is { } d)
                return d with { Species = sp, Form = f, Note = $"{Label(pre.Species, pre.Form)}({d.Note})에서 진화" };
        return null;
    }

    /// <summary>The species and form itself in the wild, or as a static, a gift or a trade.</summary>
    private static Entry9a? Direct(ushort sp, byte f)
    {
        if (WildSlot(sp, f, false, null) is { } wild) return new Entry9a(sp, f, Source9a.Wild, sp, f, wild, wild.Shiny == Shiny.Never, "야생" + (wild.Shiny == Shiny.Never ? " (이로치 불가)" : ""));
        // a static that can shine over one that cannot, then the lowest level
        var st = Statics.Where(s => s.Species == sp && s.Form == f).OrderBy(s => s.Shiny == Shiny.Never ? 1 : 0).ThenBy(s => s.Level).FirstOrDefault();
        if (st is not null) return new Entry9a(sp, f, Source9a.Static, sp, f, st, st.Shiny == Shiny.Never, "고정" + (st.Shiny == Shiny.Never ? " (이로치 불가)" : ""));
        var gift = Gifts.Where(g => g.Species == sp && g.Form == f).OrderBy(g => g.Shiny == Shiny.Never ? 1 : 0).ThenBy(g => g.Level).FirstOrDefault();
        if (gift is not null) return new Entry9a(sp, f, Source9a.Gift, sp, f, gift, gift.Shiny == Shiny.Never, "선물" + (gift.Shiny == Shiny.Never ? " (이로치 불가)" : ""));
        var trade = Trades.FirstOrDefault(t => t.Species == sp && t.Form == f);
        if (trade is not null) return new Entry9a(sp, f, Source9a.Trade, sp, f, trade, true, "게임 안 교환");
        return null;
    }

    /// <summary>A form reached by changing another form of the species the game gives (Furfrou's trims, Genesect's drives, Hoopa unbound, Keldeo resolute, Zygarde's 50%).</summary>
    private static Entry9a? ByFormChange(ushort sp, byte f)
    {
        foreach (var (osp, of) in Present().Where(x => x.Species == sp && x.Form != f))
        {
            if (FormInfo.IsBattleOnlyForm(sp, of, 9) || !FormInfo.IsFormChangeable(sp, of, f, EntityContext.Gen9a, EntityContext.Gen9a)) continue;
            if (Decide(sp, of) is { } origin && !origin.Evolves)
                return origin with { Form = f, Note = $"{Label(sp, of)}({origin.Note})의 폼 변경" };
        }
        return null;
    }
}

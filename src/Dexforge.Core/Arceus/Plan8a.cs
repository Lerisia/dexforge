using System.Reflection;
using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>Where the first stage of an entry comes from.</summary>
public enum Source8a
{
    /// <summary>A field spawner the program knows the table of: the slot draw is made to land on it.</summary>
    Field,
    /// <summary>A wild slot PKHeX knows but no field spawner carries: a space-time distortion, a shaking tree or ore, an outbreak.</summary>
    OtherSlot,
    /// <summary>A static encounter: a legendary, a gift, a request's Pokémon.</summary>
    Static,
}

/// <summary>
/// One box entry: a species and form, what is caught first and where, and whether it then evolves or changes form.
/// </summary>
/// <param name="Gender">A gender to insist on, for the species boxed in both.</param>
/// <param name="Shiny">Whether this entry can be shiny at all (a static may be locked).</param>
public sealed record Entry8a(ushort Species, byte Form, Source8a Source, ushort FromSpecies, byte FromForm, FieldSpawner? Spawner,
                             EncounterSlot8a? Slot, EncounterStatic8a? Static, string How, byte? Gender = null, bool Shiny = true)
{
    public bool Evolves => FromSpecies != Species;
    public bool ChangesForm => FromSpecies == Species && FromForm != Form;
    /// <summary>What an alpha of this entry is caught as, when the game has one: the entry's own alpha spawner or slot, or its first stage's.</summary>
    public (FieldSpawner? Spawner, EncounterSlot8a? Slot) Alpha { get; init; }
    public bool CanBeAlpha => Alpha.Spawner is not null || Alpha.Slot is not null;
}

/// <summary>
/// Every species and form a Legends: Arceus box can hold, with its source: a field spawner wherever one carries it,
/// otherwise its nearest earlier stage evolved, otherwise a distortion, tree or outbreak slot, otherwise its static
/// encounter; form changes (Rotom's appliances, Arceus's plates, the Origin and Therian forms) come from the form caught.
/// The lords, the lady and the battle-only forms are left out.
/// </summary>
public static class Plan8a
{
    public static readonly PersonalTable8LA Table = PersonalTable.LA;
    public static readonly EvolutionTree Tree = EvolutionTree.Evolves8a;
    public static readonly GameStrings Ko = GameInfo.GetStrings("ko");
    public static readonly EncounterArea8a[] Areas;
    public static readonly EncounterStatic8a[] Statics;

    /// <summary>The species the owner boxes in both sexes, as in the other dexes: the ones whose looks differ.</summary>
    public static readonly ushort[] BothSexes = [449, 450];
    /// <summary>The noble Pokémon's forms (Arcanine, Electrode, Lilligant, Avalugg, Kleavor) and forms that exist only in battle (Cherrim's sunshine, Arceus's legend).</summary>
    private static readonly HashSet<(ushort, byte)> LeftOut = [(59, 2), (101, 2), (549, 2), (713, 2), (900, 1), (421, 1), (493, 18)];

    static Plan8a()
    {
        var t = typeof(PA8).Assembly.GetType("PKHeX.Core.Encounters8a")!;
        Areas = (EncounterArea8a[])t.GetField("SlotsLA", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Statics = (EncounterStatic8a[])t.GetField("StaticLA", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    }

    public static string FormName(ushort species, byte form)
    {
        var list = FormConverter.GetFormList(species, Ko.types, Ko.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen8a);
        return form < list.Length ? list[form] : form.ToString();
    }

    public static string Label(ushort species, byte form) => Ko.Species[species] + (form != 0 ? " " + FormName(species, form) : "");

    /// <summary>Every species and form the game's data holds (the ones left out included).</summary>
    public static IEnumerable<(ushort Species, byte Form)> Present()
    {
        for (ushort sp = 1; sp <= Table.MaxSpeciesID; sp++)
        {
            var pi = Table[sp];
            for (byte f = 0; f < pi.FormCount; f++) if (Table.GetFormEntry(sp, f).IsPresentInGame) yield return (sp, f);
        }
    }

    /// <summary>The wild slots PKHeX has for a species and form, the kinds a player finds after the story first.</summary>
    public static IEnumerable<EncounterSlot8a> SlotsOf(ushort species, byte form, bool alpha) =>
        Areas.SelectMany(a => a.Slots).Where(s => s.Species == species && s.Form == form && s.IsAlpha == alpha)
             .OrderBy(s => s.Type switch { SlotType8a.Distortion => 0, SlotType8a.Landmark => 1, SlotType8a.Standard => 2, SlotType8a.MassOutbreakMassive => 3, _ => 4 });

    public static EncounterStatic8a? StaticOf(ushort species, byte form) =>
        Statics.Where(s => s.Species == species && s.Form == form).OrderByDescending(s => s.Shiny == PKHeX.Core.Shiny.Always).ThenBy(s => s.IsAlpha).FirstOrDefault();

    /// <summary>The earlier stages of a species and form in this game, nearest first.</summary>
    public static IEnumerable<(ushort Species, byte Form)> Earlier(ushort species, byte form) => Tree.Reverse.GetPreEvolutions(species, form).Reverse().Select(e => (e.Species, e.Form));

    private static readonly Lazy<List<Entry8a>> all = new(Build);
    public static IReadOnlyList<Entry8a> All => all.Value;

    private static List<Entry8a> Build()
    {
        var entries = new List<Entry8a>();
        var cache = new Dictionary<(ushort, byte), Entry8a?>();
        foreach (var (sp, f) in Present())
        {
            if (LeftOut.Contains((sp, f))) continue;
            var e = Of(sp, f, cache);
            if (e is null) continue;
            if (BothSexes.Contains(sp)) { entries.Add(e with { Gender = 0, How = e.How + ", 수컷" }); entries.Add(e with { Gender = 1, How = e.How + ", 암컷" }); }
            else entries.Add(e);
        }
        return entries;
    }

    private static Entry8a? Of(ushort sp, byte f, Dictionary<(ushort, byte), Entry8a?> cache)
    {
        if (cache.TryGetValue((sp, f), out var known)) return known;
        cache[(sp, f)] = null;   // while it is being found, another form asking for it gets nothing rather than a loop
        var e = Find(sp, f, cache);
        if (e is not null) e = e with { Alpha = AlphaOf(e) };
        cache[(sp, f)] = e;
        return e;
    }

    private static Entry8a? Find(ushort sp, byte f, Dictionary<(ushort, byte), Entry8a?> cache)
    {
        // a field spawner counts only where PKHeX agrees the species is caught in the wild: the game places its legendaries
        // through spawners too, but those are static encounters, shiny-locked
        if (FieldOf(sp, f) is { } own) return new Entry8a(sp, f, Source8a.Field, sp, f, own, null, null, $"{Ko.Species[sp]} 야생 ({Area(own.Area)})");
        foreach (var (ps, pf) in Earlier(sp, f))
            if (FieldOf(ps, pf) is { } pre) return new Entry8a(sp, f, Source8a.Field, ps, pf, pre, null, null, $"{Label(ps, pf)} 야생 ({Area(pre.Area)}) → 진화");
        if (SlotsOf(sp, f, false).FirstOrDefault() is { } slot) return new Entry8a(sp, f, Source8a.OtherSlot, sp, f, null, slot, null, $"{Ko.Species[sp]} 야생 ({Kind(slot.Type)})");
        foreach (var (ps, pf) in Earlier(sp, f))
            if (SlotsOf(ps, pf, false).FirstOrDefault() is { } preSlot) return new Entry8a(sp, f, Source8a.OtherSlot, ps, pf, null, preSlot, null, $"{Label(ps, pf)} 야생 ({Kind(preSlot.Type)}) → 진화");
        if (StaticOf(sp, f) is { } st) return new Entry8a(sp, f, Source8a.Static, sp, f, null, null, st, $"{Ko.Species[sp]} 고정 조우", Shiny: st.Shiny != PKHeX.Core.Shiny.Never);
        foreach (var (ps, pf) in Earlier(sp, f))
            if (StaticOf(ps, pf) is { } preSt) return new Entry8a(sp, f, Source8a.Static, ps, pf, null, null, preSt, $"{Label(ps, pf)} 고정 조우 → 진화", Shiny: preSt.Shiny != PKHeX.Core.Shiny.Never);
        // a form of a species whose other form is obtainable: caught as that and changed
        foreach (var (os, of) in Present().Where(p => p.Species == sp && p.Form != f && !LeftOut.Contains(p)))
            if (Of(os, of, cache) is { } other && !other.ChangesForm)
                return other with { Form = f, How = other.How + $" → {FormName(sp, f)}로 폼 체인지" };
        return null;
    }

    private static FieldSpawner? FieldOf(ushort sp, byte f, bool alpha = false) =>
        SlotsOf(sp, f, alpha).Any(s => s.Type == SlotType8a.Standard) ? Spawners8a.For(sp, f, alpha) : null;

    private static (FieldSpawner?, EncounterSlot8a?) AlphaOf(Entry8a e)
    {
        if (FieldOf(e.Species, e.Form, true) is { } own) return (own, null);
        if (e.Evolves && FieldOf(e.FromSpecies, e.FromForm, true) is { } pre) return (pre, null);
        if (SlotsOf(e.Species, e.Form, true).FirstOrDefault() is { } slot) return (null, slot);
        if (e.Evolves && SlotsOf(e.FromSpecies, e.FromForm, true).FirstOrDefault() is { } preSlot) return (null, preSlot);
        return (null, null);
    }

    public static string Area(string name) => name switch
    {
        "OBSIDIAN_FIELDLANDS" => "흑요 들판",
        "CRIMSON_MIRELANDS" => "홍련 습지",
        "COBALT_COASTLANDS" => "군청 해안",
        "CORONET_HIGHLANDS" => "천관산 기슭",
        "ALABASTER_ICELANDS" => "순백 동토",
        _ => name,
    };

    public static string Kind(SlotType8a type) => type switch
    {
        SlotType8a.Distortion => "시공의 뒤틀림",
        SlotType8a.Landmark => "나무·광석",
        SlotType8a.MassOutbreakRegular => "대량발생",
        SlotType8a.MassOutbreakMassive => "초대량발생",
        _ => "야생",
    };
}

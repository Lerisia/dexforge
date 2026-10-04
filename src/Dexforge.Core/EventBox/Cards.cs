using System.Reflection;
using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>
/// The cards as the catalogue numbers them: PKHeX's event database enumerated in the order dump-catalog.cs walked it
/// (gen 4, 5, 6, 7 gifts that hold a Pokémon; then the gen 3 tables Encounter_WC3, PCNY, PCJP; then the Colosseum gifts).
/// The catalogue's row number is the handle the distribution list carries.
/// </summary>
public static class Cards
{
    public static List<object> All()
    {
        var list = new List<object>();
        foreach (var gifts in new IEnumerable<MysteryGift>[] { EncounterEvent.MGDB_G4, EncounterEvent.MGDB_G5, EncounterEvent.MGDB_G6, EncounterEvent.MGDB_G7 })
            foreach (var g in gifts.Where(g => g.IsEntity && g.Species != 0)) list.Add(g);
        var asm = typeof(PK3).Assembly;
        var wc3 = asm.GetType("PKHeX.Core.EncountersWC3")!;
        foreach (var fn in new[] { "Encounter_WC3", "PCNY", "PCJP" })
            foreach (var o in (System.Collections.IEnumerable)wc3.GetField(fn, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!)
                list.Add(o);
        foreach (var (tn, fn) in new[] { ("PKHeX.Core.Encounters3Colo", "Gifts"), ("PKHeX.Core.Encounters3RSE", "ColoGiftsR"), ("PKHeX.Core.Encounters3RSE", "ColoGiftsS") })
            foreach (var o in (System.Collections.IEnumerable)asm.GetType(tn)!.GetField(fn, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!)
                list.Add(o);
        return list;
    }
}

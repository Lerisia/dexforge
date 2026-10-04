using PKHeX.Core;

namespace Dexforge;

/// <summary>How a Pokémon's 510 effort points are spent, the way a player who trains it would.</summary>
public enum EffortClass
{
    /// <summary>HP 252, Defense 252, Sp. Defense 6.</summary>
    Tank,
    /// <summary>Attack 252, HP 252, Defense 6.</summary>
    SlowPhysical,
    /// <summary>Sp. Attack 252, HP 252, Defense 6.</summary>
    SlowSpecial,
    /// <summary>Attack 252, Speed 252, HP 6.</summary>
    FastPhysical,
    /// <summary>Sp. Attack 252, Speed 252, HP 6.</summary>
    FastSpecial,
}

/// <summary>
/// The effort spread the Effort Ribbon asks for, species by species: a tank, a slow attacker or a fast one, physical or
/// special. A Pokémon not yet evolved spends as its final form does. The classes come from how the Pokémon are actually
/// trained (Smogon's singles sets of the generation, by majority), with the owner's choice where a line forks into final
/// forms that differ; one table per game (template.effort for Ultra Sun, sword.effort for Sword, which takes the Ultra Sun
/// class wherever the form is in that table).
/// </summary>
public sealed class Effort
{
    public static readonly IReadOnlyDictionary<string, EffortClass> ByName = new Dictionary<string, EffortClass>
    {
        ["탱커"] = EffortClass.Tank, ["저속물리"] = EffortClass.SlowPhysical, ["저속특수"] = EffortClass.SlowSpecial, ["고속물리"] = EffortClass.FastPhysical, ["고속특수"] = EffortClass.FastSpecial,
    };

    public static readonly Effort UltraSun = new("template.effort");
    public static readonly Effort Sword = new("sword.effort");

    /// <summary>The table for a Pokémon, by the game it is from.</summary>
    public static Effort For(PKM pk) => pk is PK8 ? Sword : UltraSun;

    private readonly Dictionary<(ushort Species, byte Form, int Sex), EffortClass> table;

    private Effort(string resource)
    {
        var rows = new EventBox.Rows(Embedded.Text(resource));
        table = new Dictionary<(ushort, byte, int), EffortClass>();
        foreach (var r in rows.All)
        {
            var sex = rows.Get(r, "sex") switch { "수" => 0, "암" => 1, _ => -1 };
            table[(ushort.Parse(rows.Get(r, "species")), byte.Parse(rows.Get(r, "form")), sex)] = ByName[rows.Get(r, "class")];
        }
    }

    public int Count => table.Count;

    /// <summary>The class of a species and form (for the sex given, where the table tells the sexes apart); none when the table has no line for it.</summary>
    public EffortClass? Of(ushort species, byte form, int sex)
        => table.TryGetValue((species, form, sex), out var c) || table.TryGetValue((species, form, -1), out c) ? c : null;

    /// <summary>The six values, in the Pokémon's own order (HP, Attack, Defense, Speed, Sp. Attack, Sp. Defense).</summary>
    public static int[] Spread(EffortClass c) => c switch
    {
        EffortClass.Tank => [252, 0, 252, 0, 0, 6],
        EffortClass.SlowPhysical => [252, 252, 6, 0, 0, 0],
        EffortClass.SlowSpecial => [252, 0, 6, 0, 252, 0],
        EffortClass.FastPhysical => [6, 252, 0, 252, 0, 0],
        _ => [6, 0, 0, 252, 252, 0],
    };

    public static string Name(EffortClass c) => ByName.First(kv => kv.Value == c).Key;

    public static string Describe(EffortClass c) => c switch
    {
        EffortClass.Tank => "HP 252 · 방어 252 · 특방 6",
        EffortClass.SlowPhysical => "공격 252 · HP 252 · 방어 6",
        EffortClass.SlowSpecial => "특공 252 · HP 252 · 방어 6",
        EffortClass.FastPhysical => "공격 252 · 스핏 252 · HP 6",
        _ => "특공 252 · 스핏 252 · HP 6",
    };

    /// <summary>Spends the Pokémon's effort points as its class says; a species the table does not know is spent as a tank.</summary>
    public EffortClass Apply(PKM pk)
    {
        var c = Of(pk.Species, pk.Form, pk.Gender) ?? EffortClass.Tank;
        pk.SetEVs(Spread(c));
        return c;
    }
}

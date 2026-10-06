using PKHeX.Core;

namespace Dexforge.Arceus;

/// <summary>What a slot decides about how its Pokémon is drawn.</summary>
/// <param name="Rolls">How many PIDs may be drawn looking for a shiny: the player's research standing plus the spawner's bonus.</param>
/// <param name="Flawless">Guaranteed perfect IVs (three for an alpha).</param>
/// <param name="GenderRatio">The species' ratio, or the spawner's lock: 0 male only, 254 female only, 255 genderless.</param>
/// <param name="IsAlpha">Alphas skip the size draws and are the largest size.</param>
public readonly record struct SpawnParams(int Rolls, int Flawless, int GenderRatio, bool IsAlpha);

/// <summary>Everything one fixed seed decides. Shiny is 0 none, 1 star, 2 square.</summary>
public readonly record struct Drawn(uint Ec, uint FakeTid, uint Pid, int[] Ivs, int Ability, int Gender, int Nature, int Height, int Weight, int Shiny, int RollsUsed)
{
    public bool IsShiny => Shiny != 0;
}

/// <summary>
/// A spawn as the game draws it, transcribed from pla-reverse (the PLA bot's reference, plaseed/generate.py) and checked
/// field for field against PKHeX's own generation. A generator seed first decides the slot (its first output, scaled into
/// [0, 1)), then the fixed seed (its second output), then the level (its third). The fixed seed decides the rest in this
/// order: encryption constant, a throwaway trainer id, up to Rolls PIDs stopping at a shiny, the guaranteed IVs, the other
/// IVs, ability, gender, nature, then height and weight unless it is an alpha.
/// </summary>
public static class Spawn8a
{
    public static int ShinyType(uint pid, uint id32)
    {
        uint x = (pid >> 16) ^ (id32 >> 16) ^ (pid & 0xFFFF) ^ (id32 & 0xFFFF);
        return x == 0 ? 2 : x < 16 ? 1 : 0;
    }

    /// <summary>The slot roll and the fixed seed a generator seed gives.</summary>
    public static (ulong slot, ulong fixedSeed) FromGenerator(ulong generator)
    {
        var rng = new Xoroshiro128Plus(generator);
        ulong slot = rng.Next(), fixedSeed = rng.Next();
        return (slot, fixedSeed);
    }

    /// <summary>The slot roll as the game compares it with the table's weights: the first output scaled into [0, 1).</summary>
    public static double SlotRoll(ulong generator) => (generator + Xoroshiro128Plus.XOROSHIRO_CONST) * (1.0 / 18446744073709551616.0);

    /// <summary>The level a generator seed gives a slot: min plus a draw of the range, made after the fixed seed.</summary>
    public static int Level(ulong generator, int min, int max)
    {
        if (max == min) return min;
        var rng = new Xoroshiro128Plus(generator);
        rng.Next(); rng.Next();   // the slot roll and the fixed seed come first
        return min + (int)rng.NextInt((ulong)(max - min + 1));
    }

    /// <summary>
    /// The Pokémon a fixed seed gives. With the trainer's id the PID is stored the way the game stores it: a shiny roll is
    /// rewritten to be shiny for that trainer (a square stays square, anything else becomes a star), and a plain roll that
    /// happened to be shiny for that trainer is made plain. Zero leaves the PID as drawn.
    /// </summary>
    public static Drawn FromFixed(ulong fixedSeed, SpawnParams p, uint id32 = 0)
    {
        var rng = new Xoroshiro128Plus(fixedSeed);
        uint ec = (uint)rng.NextInt(0xFFFFFFFF), fakeTid = (uint)rng.NextInt(0xFFFFFFFF), pid = 0;
        int shiny = 0, used = 0;
        for (int i = 0; i < p.Rolls; i++)
        {
            pid = (uint)rng.NextInt(0xFFFFFFFF); used++;
            shiny = ShinyType(pid, fakeTid);
            if (shiny != 0) break;
        }
        if (id32 != 0)
        {
            uint tid = id32 & 0xFFFF, sid = id32 >> 16;
            if (shiny != 0)
            {
                uint stored = shiny == 2 ? 0u : 1u;
                pid = (((tid ^ sid ^ (pid & 0xFFFF) ^ stored) & 0xFFFF) << 16) | (pid & 0xFFFF);
            }
            else if (ShinyType(pid, id32) != 0) pid ^= 0x10000000;
        }
        var ivs = new int[6];
        for (int i = 0; i < p.Flawless; i++)
        {
            int k = (int)rng.NextInt(6);
            while (ivs[k] != 0) k = (int)rng.NextInt(6);
            ivs[k] = 31;
        }
        for (int i = 0; i < 6; i++) if (ivs[i] == 0) ivs[i] = (int)rng.NextInt(32);
        int ability = (int)rng.NextInt(2);
        int gender = p.GenderRatio switch
        {
            0 => 0,
            254 => 1,
            255 => 2,
            _ => (int)rng.NextInt(253) + 1 < p.GenderRatio ? 1 : 0,
        };
        int nature = (int)rng.NextInt(25);
        int height = 255, weight = 255;
        if (!p.IsAlpha)
        {
            height = (int)rng.NextInt(0x81) + (int)rng.NextInt(0x80);
            weight = (int)rng.NextInt(0x81) + (int)rng.NextInt(0x80);
        }
        return new Drawn(ec, fakeTid, pid, ivs, ability, gender, nature, height, weight, shiny, used);
    }

    /// <summary>Whether the gender draw happens for this ratio (it is skipped for single-sex and genderless species).</summary>
    public static bool RollsGender(int ratio) => ratio is not (0 or 254 or 255);
}

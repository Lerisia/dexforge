using System.Text;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>A test that takes minutes. It runs only when DEXGEN_SLOW is set to 1.</summary>
public sealed class SlowFactAttribute : FactAttribute
{
    public SlowFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DEXGEN_SLOW") != "1") Skip = "몇 분 걸리는 시험입니다. DEXGEN_SLOW=1 로 켭니다.";
    }
}

/// <summary>The save the generator carries inside it.</summary>
public static class Template
{
    private static readonly Lazy<byte[]> Held = new(() =>
    {
        using var stream = typeof(Generator).Assembly.GetManifestResourceStream("template.dex") ?? throw new InvalidOperationException("no template inside");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    public static byte[] Bytes => (byte[])Held.Value.Clone();

    public static SAV7 Read(byte[] bytes) =>
        SaveUtil.TryGetSaveFile(bytes.ToArray(), out var sav) && sav is SAV7 s7 ? s7 : throw new InvalidOperationException("not a save of this generation");

    /// <summary>Every Pokemon of a save: the boxes in order, then the party.</summary>
    public static List<PK7> Pokemon(SAV7 sav, bool party = true)
    {
        var all = new List<PK7>();
        for (int b = 0; b < sav.BoxCount; b++) foreach (var p in sav.GetBoxData(b)) if (p.Species != 0) all.Add((PK7)p);
        if (party) foreach (var p in sav.PartyData) if (p.Species != 0) all.Add((PK7)p);
        return all;
    }

    /// <summary>Whether a name stands anywhere in the save outside the Pokemon, which are stored enciphered and are looked at by themselves.</summary>
    public static bool Holds(byte[] save, string name) => save.AsSpan().IndexOf(Encoding.Unicode.GetBytes(name)) >= 0;
}

/// <summary>How a Pokemon came to be, as the tests need to tell them apart.</summary>
public enum Came { Hatched, Caught, Card, Older }

public static class Tell
{
    public static Came Of(PKM pk) => new LegalityAnalysis(pk).EncounterMatch switch
    {
        PGT { IsManaphyEgg: true } => Came.Older,
        MysteryGift => Came.Card,
        _ when pk.Generation < 7 => Came.Older,
        IEncounterEgg => Came.Hatched,
        _ => Came.Caught,
    };

    public static int Perfect(PKM pk) => Enumerable.Range(0, 6).Count(i => pk.GetIV(i) == 31);

    /// <summary>Whether the game promises the Pokemon three perfect values.</summary>
    public static bool Promised(PKM pk) => new LegalityAnalysis(pk).EncounterMatch is IFlawlessIVCount { FlawlessIVCount: >= 3 };
}

/// <summary>A save made once and looked at by many tests.</summary>
public abstract class Made
{
    public Options Asked { get; }
    public Generator Generator { get; }
    public byte[] Bytes { get; }
    public Check.Result Result { get; }
    public SAV7 Save { get; }
    public List<PK7> All { get; }
    public SAV7 Before { get; }
    public List<PK7> Was { get; }

    protected Made(Options asked)
    {
        Asked = asked;
        var template = Template.Bytes;
        Generator = new Generator(template, asked);
        Bytes = Generator.Run();
        Result = Check.Run(template, Bytes, Generator.Me, Generator.Was, Generator.Began, asked.Ball, asked.Ivs, asked.Shiny, asked.Level, asked.Sex, asked);
        Save = Template.Read(Bytes);
        All = Template.Pokemon(Save);
        Before = Template.Read(template);
        Was = Template.Pokemon(Before);
    }
}

/// <summary>
/// The same seed, twice: what a seed draws must come out again, Pokémon for Pokémon. Only what the program draws from its seed is
/// held to that; what PKHeX draws for itself (static encounters, gifts, cards) comes out of PKHeX's own unseeded random.
/// </summary>
public static class SameSeed
{
    /// <summary>Makes the save twice into fresh folders and reads back both boxes, slot for slot.</summary>
    public static (IReadOnlyList<PKM> First, IReadOnlyList<PKM> Second) Twice(Func<string, Dexforge.Made> make, Func<byte[], SaveFile> read)
    {
        var dirs = new[] { Fresh(), Fresh() };
        try
        {
            var boxes = dirs.Select(d =>
            {
                var made = make(d);
                Assert.True(made.Code == 0, string.Join("\n", made.Refused.Take(10)));
                var sav = read(File.ReadAllBytes(Path.Combine(made.Folder!, "main")));
                return (IReadOnlyList<PKM>)Enumerable.Range(0, sav.SlotCount).Select(i => sav.GetBoxSlotAtIndex(i)).ToList();
            }).ToList();
            return (boxes[0], boxes[1]);
        }
        finally { foreach (var d in dirs) if (Directory.Exists(d)) Directory.Delete(d, true); }
    }

    /// <summary>The same draw: species and form, PID, encryption constant, the six IVs and the nature.</summary>
    public static bool Drawn(PKM a, PKM b) => a.Species == b.Species && a.Form == b.Form && a.PID == b.PID && a.EncryptionConstant == b.EncryptionConstant
        && a.IV_HP == b.IV_HP && a.IV_ATK == b.IV_ATK && a.IV_DEF == b.IV_DEF && a.IV_SPA == b.IV_SPA && a.IV_SPD == b.IV_SPD && a.IV_SPE == b.IV_SPE && a.Nature == b.Nature;

    private static string Fresh() => Path.Combine(Path.GetTempPath(), "dexforge-same-" + Guid.NewGuid().ToString("N"));
}

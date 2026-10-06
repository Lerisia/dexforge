using System.Text;
using PKHeX.Core;

namespace Dexforge;

/// <summary>
/// What the four Switch dex makers (Sword, Legends: Arceus, Scarlet, Legends: Z-A) share around their own making: checking what
/// is asked, the trainer's ids, what a refused Pokémon was refused for, the trainer's lines of the record, and the JKSV backup
/// folder the save is written to. Only <see cref="Id32"/> draws, and it draws as every maker did before: the SID, then the TID.
/// </summary>
public static class SwitchMaking
{
    /// <summary>Why the trainer's name or ids cannot be had; none when they will do. Six Korean letters, as in the eighth generation.</summary>
    public static string? RefusedTrainer(string name, uint? tid, uint? sid)
    {
        int room = Legal.GetMaxLengthOT(8, LanguageID.Korean);
        if (name.Length < 1 || name.Length > room) return $"어버이 이름은 1글자에서 {room}글자 사이여야 합니다.";
        if (sid is > 4294) return "SID 는 0000 에서 4294 사이여야 합니다.";
        if (tid is > 999_999) return "TID 는 000000 에서 999999 사이여야 합니다.";
        if (sid == 4294 && tid > 967_295) return "SID 4294 에서는 TID 가 967295 까지입니다.";
        return null;
    }

    /// <summary>Why the days things were caught on cannot be had: before the game came out, the wrong way round, past 2099.</summary>
    public static string? RefusedPeriod(DateOnly from, DateOnly to, DateOnly released, string game)
    {
        if (from < released) return $"첫날은 {released:yyyy-MM-dd} ({game} 발매일) 이후여야 합니다.";
        if (to < from) return "마지막 날이 첫날보다 앞섭니다.";
        if (to.Year > 2099) return "마지막 날은 2099년까지입니다.";
        return null;
    }

    /// <summary>What was asked for refused, before anything was made.</summary>
    public static Made Refused(string why) => new(2, null, [], [why]);

    /// <summary>The trainer's 32-bit id: the SID and TID asked for, and whichever was not asked for drawn, the SID first.</summary>
    public static uint Id32(Random random, uint? tid, uint? sid)
    {
        uint sid7 = sid ?? (uint)random.Next(0, 4295);
        uint tid7 = tid ?? (uint)random.Next(0, sid7 == 4294 ? 967_296 : 1_000_000);
        return sid7 * 1_000_000 + tid7;
    }

    /// <summary>What a legality report found wrong: its Invalid lines, or the whole report when it names none.</summary>
    public static string Faults(string report) =>
        report.Contains("Invalid") ? string.Join(" | ", report.Split('\n').Where(l => l.Contains("Invalid"))) : report;

    /// <summary>The record's lines for the trainer: the name and sex, the SID and TID as the game shows them.</summary>
    public static string[] TrainerLines(Trainer me) =>
    [
        $"어버이        {me.Name} ({(me.Gender == 0 ? "남" : "여")})",
        $"SID / TID     {me.Sid7:0000} / {me.Shown:000000}",
    ];

    /// <summary>The backup folder named after the game and the trainer.</summary>
    public static string FolderFor(string under, string game, Trainer me) => Path.Combine(under, $"Dexforge-{game}-{me.Name}-{me.Shown:000000}");

    /// <summary>
    /// Writes a JKSV backup folder: the save as main, the files JKSV wants beside it (carried inside with the template), and the
    /// record. The folder is the one asked for, or one named after the game and the trainer under <paramref name="under"/>.
    /// </summary>
    public static Made Write(string? outDir, string under, string game, Trainer me, byte[] save, IEnumerable<(string Resource, string File)> sidecars, List<string> lines)
    {
        outDir ??= FolderFor(under, game, me);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "main"), save);
        foreach (var (resource, file) in sidecars) File.WriteAllBytes(Path.Combine(outDir, file), Embedded.Bytes(resource));
        File.WriteAllLines(Path.Combine(outDir, Making.RecordName), lines, new UTF8Encoding(true));
        return new Made(0, outDir, lines, [], me);
    }
}

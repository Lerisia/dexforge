namespace Dexforge;

/// <summary>
/// The trainer's number as the seventh generation writes it, and as PKHeX does: a four-digit SID and a six-digit TID, [SID]TID,
/// which are one 32-bit number (SID × 1,000,000 + TID) split differently from the two 16-bit halves the games keep.
/// </summary>
public static class Ids7
{
    public const uint MaxTid = 999999, MaxSid = 4294, MaxTidAtMaxSid = 967295;

    /// <summary>Why the numbers asked for cannot be one trainer's, or nothing.</summary>
    public static string? Refused(uint? tid, uint? sid)
    {
        if (tid > MaxTid) return "TID 는 000000 에서 999999 사이의 여섯 자리 수여야 합니다.";
        if (sid > MaxSid) return "SID 는 0000 에서 4294 사이의 네 자리 수여야 합니다.";
        if (sid == MaxSid && tid > MaxTidAtMaxSid) return "SID 가 4294 이면 TID 는 967295 까지입니다. 두 수를 합친 값이 게임이 담는 크기를 넘습니다.";
        return null;
    }

    /// <summary>The two 16-bit halves of the trainer asked for; what is not given is drawn, both halves at once where neither is.</summary>
    public static (ushort Tid16, ushort Sid16) Halves(uint? tid, uint? sid, Draw draw)
    {
        if (tid is null && sid is null) return (draw.Id16(), draw.Id16());
        uint s = sid ?? (uint)draw.Rnd.Next(0, tid > MaxTidAtMaxSid ? (int)MaxSid : (int)MaxSid + 1);
        uint t = tid ?? (uint)draw.Rnd.Next(0, s == MaxSid ? (int)MaxTidAtMaxSid + 1 : (int)MaxTid + 1);
        uint id32 = s * 1000000 + t;
        return ((ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16));
    }
}

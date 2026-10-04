namespace Dexforge;

public static class Words
{
    /// <summary>What the generator refuses, said to whoever is using it.</summary>
    public static string Korean(string message) => message switch
    {
        _ when message.StartsWith("the days must be within") => $"기간은 {Generator.Earliest:yyyy-MM-dd} 부터 {Generator.Latest:yyyy-MM-dd} 사이여야 합니다. 3DS 의 시계가 이 사이만 됩니다.",
        _ when message.StartsWith("the last day is before") => "기간의 마지막 날이 첫날보다 앞섭니다.",
        _ => message,
    };
}

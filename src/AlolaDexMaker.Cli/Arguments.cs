namespace AlolaDexMaker.Cli;

/// <summary>What is asked for on the command line.</summary>
internal static class Arguments
{
    public const string Unreadable = "옵션의 값을 읽지 못했습니다. TID 는 여섯 자리, SID 는 네 자리 수이고, 날짜는 2018-01-01 같은 모양입니다.";

    /// <summary>The order the arguments make; or why they cannot be read.</summary>
    public static Order? Parse(IReadOnlyList<string> args, out string why)
    {
        var o = new Order();
        why = "";
        try
        {
            for (int i = 0; i < args.Count; i++)
            {
                string Value() => args[++i];
                switch (args[i])
                {
                    case "--game":
                        var g = Value();
                        if (g is "sword" or "소드") o.Game = Game.Sword;
                        else if (g is "ultrasun" or "울트라썬") o.Game = Game.UltraSun;
                        else { why = "--game 은 울트라썬 또는 소드입니다."; return null; }
                        break;
                    case "--year": o.Year = int.Parse(Value()); break;
                    case "--name": o.Name = Value(); break;
                    case "--english": o.English = Value(); break;
                    case "--japanese": o.Japanese = Value(); break;
                    case "--chinese": o.Chinese = Value(); break;
                    case "--tid": o.Tid = uint.Parse(Value()); break;
                    case "--sid": o.Sid = uint.Parse(Value()); break;
                    case "--ball": o.Ball = Value(); break;
                    case "--color": if (!ColourNames.Find(Value(), out o.Shiny)) { why = ColourNames.Help; return null; } break;
                    case "--ivs": if (!IvNames.Find(Value(), out o.Ivs)) { why = IvNames.Help; return null; } break;
                    case "--sex": if (!SexNames.Find(Value(), out o.Sex)) { why = SexNames.Help; return null; } break;
                    case "--level": if (!LevelNames.Find(Value(), out o.Level)) { why = LevelNames.Help; return null; } break;
                    case "--from": o.From = DateOnly.Parse(Value()); break;
                    case "--to": o.To = DateOnly.Parse(Value()); break;
                    case "--seed": o.Seed = int.Parse(Value()); break;
                    case "--out": o.Out = Value(); break;
                    default: why = $"모르는 옵션: {args[i]}"; return null;
                }
            }
        }
        catch (ArgumentOutOfRangeException) { why = $"{args[^1]} 뒤에 값이 없습니다."; return null; }
        catch (Exception ex) when (ex is FormatException or OverflowException) { why = Unreadable; return null; }
        return o;
    }

    /// <summary>What <c>--refresh &lt;save&gt; &lt;folder&gt; [--seed n] [--only 590,591]</c> asks for; or why it cannot be read.</summary>
    public static (string Save, string Folder, int Seed, HashSet<ushort>? Only)? ParseRefresh(IReadOnlyList<string> args, out string why)
    {
        why = "";
        int seed = 20180101; HashSet<ushort>? only = null;
        try
        {
            for (int i = 3; i < args.Count; i += 2)
            {
                if (i + 1 >= args.Count) { why = $"{args[i]} 뒤에 값이 없습니다."; return null; }
                if (args[i] == "--seed") seed = int.Parse(args[i + 1]);
                else if (args[i] == "--only") only = args[i + 1].Split(',').Select(ushort.Parse).ToHashSet();
                else { why = $"모르는 옵션: {args[i]}"; return null; }
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { why = "--seed 와 --only 는 수로 적어 주세요."; return null; }
        return (args[1], args[2], seed, only);
    }
}

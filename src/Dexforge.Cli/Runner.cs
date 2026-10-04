namespace Dexforge.Cli;

/// <summary>The command line from start to end, with its input and output handed in.</summary>
internal sealed class Runner(TextReader input, TextWriter output, TextWriter error, string here)
{
    /// <returns>0 written; 1 what was made did not pass; 2 what was asked for cannot be read or made.</returns>
    public int Run(IReadOnlyList<string> args)
    {
        if (args.Count >= 3 && args[0] == "--refresh")
        {
            if (Arguments.ParseRefresh(args, out var bad) is not var (save, folder, seed, only)) { error.WriteLine(bad); return 2; }
            return Mend.Run(save, folder, seed, only, output, error);
        }
        bool asking = args.Count == 0;
        if (Arguments.Parse(args, out var why) is not { } order) { error.WriteLine(why); return 2; }
        if (order.ListPicks)
        {
            // what the event box's spare room may hold, one per line: key, then the label
            var ko = PKHeX.Core.GameInfo.GetStrings("ko");
            var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
            foreach (var c in Dexforge.EventBox.Custom.Candidates(Dexforge.EventBox.Rows.Distributions(), numbers)) output.WriteLine($"{c.Key}\t{c.Label}");
            return 0;
        }
        if (asking) new Questions(input, output).Fill(order);
        return Finish(asking, Make(order));
    }

    private int Make(Order order)
    {
        Made made;
        if (order.Game == Game.EventBox)
        {
            if (order.ToEventOptions(out var whyE) is not { } askedE) { error.WriteLine(whyE); return 2; }
            output.WriteLine("만드는 중입니다. 1분쯤 걸립니다 (배포 751건과 진화체를 카드에서 만듭니다)...");
            int shownE = 0;
            made = Dexforge.EventBox.EventBoxMaking.Run(askedE, order.Out, here, (done, of) => { if (done * 10 / of > shownE) { shownE = done * 10 / of; output.WriteLine($"  {done} / {of}"); } });
        }
        else if (order.Game == Game.Sword)
        {
            if (order.ToOptions8(out var why8) is not { } asked8) { error.WriteLine(why8); return 2; }
            output.WriteLine("만드는 중입니다. 몇 분 걸립니다 (755마리의 시드를 하나씩 찾습니다)...");
            int shown = 0;
            made = Dexforge.Sword.Making8.Run(asked8, order.Out, here, (done, of) => { if (done * 10 / of > shown) { shown = done * 10 / of; output.WriteLine($"  {done} / {of}"); } });
        }
        else
        {
            if (order.ToOptions(out var why) is not { } asked) { error.WriteLine(why); return 2; }
            output.WriteLine("만드는 중입니다. 몇 초 걸립니다...");
            made = Making.Run(asked, order.Out, here);
        }
        if (made.Code != 0)
        {
            foreach (var l in made.Refused) error.WriteLine(l);
            return made.Code;
        }
        foreach (var l in made.Lines.Take(20)) output.WriteLine(l);
        output.WriteLine($"썼습니다: {(order.Game == Game.Sword ? made.Folder : made.Save)}");
        output.WriteLine($"기록:     {(order.Game == Game.Sword ? Path.Combine(made.Folder!, Dexforge.Sword.Making8.RecordName) : order.Game == Game.EventBox ? Path.Combine(made.Folder!, Dexforge.EventBox.EventBoxMaking.RecordName) : made.Record)}");
        return 0;
    }

    /// <summary>Whoever was asked one question at a time sees the answer before the window closes.</summary>
    private int Finish(bool asking, int code)
    {
        if (asking) { output.WriteLine(); output.Write("Enter 를 누르면 닫힙니다."); input.ReadLine(); }
        return code;
    }
}

using Dexforge.EventBox;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>The event box: the list the program carries, the rules on it, the picker's candidates, and a whole save.</summary>
public class EventBoxTests
{
    [Fact]
    public void The_list_matches_this_PKHeX_and_the_rules_leave_room_for_a_box()
    {
        var rows = Rows.Distributions();
        Assert.Equal(855, rows.All.Count);
        var cards = Cards.All();
        var ko = GameInfo.GetStrings("ko");
        foreach (var r in rows.All)
        {
            var d = new Dist(rows, r);
            var c = cards[d.CardRow];
            ushort sp = c is MysteryGift g ? g.Species : (ushort)c.GetType().GetProperty("Species")!.GetValue(c)!;
            Assert.Equal(d.Species, ko.Species[sp]);
        }
        Assert.Equal(104, rows.All.Count(r => EventBoxMaking.Dropped(new Dist(rows, r))));
        Assert.Equal(928, EventBoxMaking.Planned);
        Assert.Equal(32, EventBoxMaking.Room);
    }

    [Fact]
    public void Candidates_are_what_the_rules_leave_out()
    {
        var ko = GameInfo.GetStrings("ko");
        var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
        var list = Custom.Candidates(Rows.Distributions(), numbers);
        Assert.Equal(104, list.Count(c => c.EvolvesTo == 0));
        Assert.Contains(list, c => c.EvolvesTo == 26 && c.EvolvesToForm == 1 && c.Dist.Region == "일본");   // a Japanese Pikachu's Alolan Raichu
        Assert.DoesNotContain(list, c => c.EvolvesTo == 26 && c.Dist.Region == "한국" && c.Dist["볼"] == "프레셔스볼");  // Korea's Pikachu evolve by the rules
        Assert.All(list, c => Assert.True(c.Key.StartsWith("D:") || c.Key.StartsWith("E:")));
    }

    [Fact]
    public void Evolutions_reach_the_final_stages_only()
    {
        Assert.Equal(8, Evolve7.Finals(133, 0).Count);
        Assert.Equal([(658, 0)], Evolve7.Finals(656, 0).Select(x => ((int)x.Species, (int)x.Form)));
        Assert.Equal(2, Evolve7.Finals(172, 0).Count);
        Assert.Empty(Evolve7.Finals(25, 1));
    }

    [Fact]
    public void Makes_a_whole_event_box_with_two_picks()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dexforge-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var ko = GameInfo.GetStrings("ko");
            var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
            var candidates = Custom.Candidates(Rows.Distributions(), numbers);
            var picks = new[] { candidates.First(c => c.EvolvesTo == 0).Key, candidates.First(c => c.EvolvesTo == 26 && c.Dist.Region == "일본").Key };
            var made = EventBoxMaking.Run(new EventOptions("재연", 123456, 1234, 20261004, 7, picks), dir, dir);
            Assert.Equal(0, made.Code);
            Assert.True(File.Exists(Path.Combine(dir, "main")));
            Assert.Contains(made.Lines, l => l.StartsWith("합법") && l.Contains("930 / 930"));
            Assert.Contains(made.Lines, l => l.StartsWith("박스") && l.Contains("직접 고른 것 2"));
            Assert.NotNull(made.Me);
            Assert.Equal("재연", made.Me!.Name);
            Assert.Equal(123456, made.Me.Shown);
            Assert.Equal(1234, made.Me.Sid7);
            // what was written reads back as that trainer's save, boxes full of legal Pokémon
            Assert.True(SaveUtil.TryGetSaveFile(File.ReadAllBytes(Path.Combine(dir, "main")), out var sav));
            var s7 = Assert.IsType<SAV7USUM>(sav);
            Assert.Equal("재연", s7.OT);
            int count = 0; for (int b = 0; b < s7.BoxCount; b++) foreach (var p in s7.GetBoxData(b)) if (p.Species != 0) count++;
            Assert.Equal(930, count);
            // what was handed out together came on one day: the Bank's Regi trio of 2016 and the Korean Eclipse trio of 2019
            var all = s7.BoxData.Where(p => p.Species != 0).ToList();
            Assert.Single(all.Where(p => p.Species is 377 or 378 or 379 && p.MetDate!.Value.Year == 2016 && p.OriginalTrainerName == "재연").Select(p => p.MetDate).Distinct());
            Assert.Single(all.Where(p => p.Species is 791 or 792 or 800 && p.IsShiny && p.OriginalTrainerName == "이클립스").Select(p => p.MetDate).Distinct());
            // the sixteen Arceus of the 2015 film came one per viewing: not on one day
            Assert.True(all.Where(p => p.Species == 493 && p.OriginalTrainerName == "デセルシティ").Select(p => p.MetDate).Distinct().Count() > 3);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Sets_handed_out_together_are_small_and_of_different_species()
    {
        var rows = Rows.Distributions();
        var sets = EventMaker.Groups(rows.All.Select(r => new Dist(rows, r)));
        Assert.Equal(62, sets.Values.Distinct().Count());   // 61 among the distributions kept, one among the Pokémon Center ones left out
        Assert.Equal(151, sets.Count);
        Assert.All(sets.GroupBy(x => x.Value), g => Assert.InRange(g.Count(), 2, 3));
        Assert.DoesNotContain(sets.Keys, d => d.Species == "아르세우스");   // one per viewing of the film
        Assert.DoesNotContain(sets.Keys, d => d["대표 어버이"] == "지우" && d.Generation == 7);   // Ash's caps, one a week
        Assert.Contains(sets.Keys, d => d["대표 어버이"] == "이클립스");
    }

    [Fact]
    public void A_make_leaves_nothing_behind_for_the_next_one_in_the_same_process()
    {
        // an event box, then a national dex in another trainer's name: the second is judged on its own, not by the first's trainer
        var into = Path.Combine(Path.GetTempPath(), "dexforge-twice-" + Guid.NewGuid().ToString("N"));
        try
        {
            var box = EventBoxMaking.Run(new EventOptions("미월", 567890, 1234, 1, 0, []), Path.Combine(into, "box"), into, null);
            Assert.Equal(0, box.Code);
            var dex = Making.Run(new Options("재연", 333333, 2222, new DateOnly(2018, 1, 1), new DateOnly(2018, 12, 31), 7), Path.Combine(into, "dex"), into);
            Assert.True(dex.Code == 0, string.Join(" / ", dex.Refused.Take(3)));
        }
        finally { if (Directory.Exists(into)) Directory.Delete(into, true); }
    }
}

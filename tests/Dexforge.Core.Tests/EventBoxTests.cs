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
        }
        finally { Directory.Delete(dir, true); }
    }
}

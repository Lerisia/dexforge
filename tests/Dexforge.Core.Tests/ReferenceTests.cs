using System.Text.Json;
using PKHeX.Core;
using Xunit;

namespace Dexforge.Tests;

/// <summary>
/// Our generation of a Pokemon's values, held against what 3DSRNGTool's own code gives for the same state of the game.
/// The vectors were written down once by dex-tools/make-vectors.py from that tool's source, compiled as it is.
/// </summary>
public class ReferenceTests
{
    private static JsonElement Load(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors", name))).RootElement;

    private static int[] Ints(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetInt32())];

    [Fact]
    public void MetStandingStillOrHandedOver()
    {
        int rows = 0, shiny = 0;
        foreach (var c in Load("still.json").EnumerateArray())
        {
            var version = c.GetProperty("version").GetString() == "US" ? GameVersion.US : GameVersion.UM;
            ushort species = c.GetProperty("species").GetUInt16(); byte form = c.GetProperty("form").GetByte();
            var met = Met7.All.First(m => m.Species == species && m.Form == form && (m.Version == GameVersion.USUM || m.Version == version) && !m.Group.Contains("First Encounter"));
            var stream = new Stream7(Convert.ToUInt32(c.GetProperty("seed").GetString(), 16));
            var walk = Timeline7.Walk(stream, c.GetProperty("first").GetInt32(), c.GetProperty("last").GetInt32(), met.Npc + 1, met.Raining).ToList();
            var want = c.GetProperty("rows").EnumerateArray().ToList();
            Assert.Equal(want.Count, walk.Count);
            for (int i = 0; i < want.Count; i++)
            {
                var (index, first, models) = walk[i]; var w = want[i];
                var d = new Meeting7(stream, index, models).Still(met, c.GetProperty("tsv").GetInt32(), c.GetProperty("charm").GetBoolean());
                string about = $"{species} seed {stream.Seed:X8} number {index}";
                Assert.True(w.GetProperty("frame").GetInt32() == index && w.GetProperty("first").GetBoolean() == first, about);
                Assert.True(w.GetProperty("ec").GetUInt32() == d.Ec && w.GetProperty("pid").GetUInt32() == d.Pid, about);
                Assert.True(Ints(w.GetProperty("ivs")).SequenceEqual(d.Ivs), about);
                Assert.True(w.GetProperty("ability").GetInt32() == d.Ability && w.GetProperty("nature").GetInt32() == d.Nature && w.GetProperty("gender").GetInt32() == d.Gender, about);
                Assert.True(w.GetProperty("shiny").GetBoolean() == d.Shiny && w.GetProperty("sync").GetBoolean() == d.Synchronized && w.GetProperty("used").GetInt32() == d.Used, about);
                rows++; if (d.Shiny) shiny++;
            }
        }
        Assert.True(rows > 4000);
    }

    [Fact]
    public void OutOfTheGrass()
    {
        int rows = 0;
        foreach (var c in Load("wild.json").EnumerateArray())
        {
            var area = Area7.All.First(a => a.Location == c.GetProperty("location").GetInt32() && a.Index == c.GetProperty("index").GetInt32());
            bool moon = c.GetProperty("version").GetString() == "UM", night = c.GetProperty("night").GetBoolean();
            int beast = c.GetProperty("beast").GetInt32();
            var stream = new Stream7(Convert.ToUInt32(c.GetProperty("seed").GetString(), 16));
            var walk = Timeline7.Walk(stream, c.GetProperty("first").GetInt32(), c.GetProperty("last").GetInt32(), area.Npc + 1, area.Raining).ToList();
            var want = c.GetProperty("rows").EnumerateArray().ToList();
            Assert.Equal(want.Count, walk.Count);
            for (int i = 0; i < want.Count; i++)
            {
                var (index, first, models) = walk[i]; var w = want[i];
                var d = new Meeting7(stream, index, models).Wild(area, moon, night, c.GetProperty("tsv").GetInt32(), c.GetProperty("charm").GetBoolean(), beast, 60, (byte)(beast == 0 ? 0 : 80));
                string about = $"place {area.Location}-{area.Index} seed {stream.Seed:X8} number {index}";
                Assert.True(w.GetProperty("frame").GetInt32() == index && w.GetProperty("first").GetBoolean() == first, about);
                Assert.True(w.GetProperty("special").GetBoolean() == d.Special && w.GetProperty("slot").GetInt32() == d.Slot, about);
                Assert.True(w.GetProperty("species").GetInt32() == (d.Species & 0x7FF) && w.GetProperty("level").GetInt32() == d.Level, about);
                Assert.True(w.GetProperty("ec").GetUInt32() == d.Ec && w.GetProperty("pid").GetUInt32() == d.Pid, about);
                Assert.True(Ints(w.GetProperty("ivs")).SequenceEqual(d.Ivs), about);
                Assert.True(w.GetProperty("ability").GetInt32() == d.Ability && w.GetProperty("nature").GetInt32() == d.Nature && w.GetProperty("gender").GetInt32() == d.Gender, about);
                Assert.True(w.GetProperty("shiny").GetBoolean() == d.Shiny && w.GetProperty("sync").GetBoolean() == d.Synchronized && w.GetProperty("used").GetInt32() == d.Used, about);
                rows++;
            }
        }
        Assert.True(rows > 2000);
    }

    [Fact]
    public void Eggs()
    {
        int rows = 0;
        foreach (var c in Load("egg7.json").EnumerateArray())
        {
            var state = c.GetProperty("state").GetString()!.Split(',').Select(x => Convert.ToUInt32(x, 16)).ToArray();
            var parents = new Parents7(Ints(c.GetProperty("maleIvs")), Ints(c.GetProperty("femaleIvs")), (Held)c.GetProperty("maleHolds").GetByte(), (Held)c.GetProperty("femaleHolds").GetByte(),
                                       c.GetProperty("ability").GetByte(), c.GetProperty("ratio").GetInt32(), c.GetProperty("eitherSex").GetBoolean(),
                                       c.GetProperty("sameSpecies").GetBoolean(), c.GetProperty("femaleIsDitto").GetBoolean(), c.GetProperty("charm").GetBoolean(), c.GetProperty("masuda").GetBoolean());
            var walker = new Tiny(state[0], state[1], state[2], state[3]);
            for (int i = 0; i < c.GetProperty("first").GetInt32(); i++) walker.Next();
            foreach (var w in c.GetProperty("rows").EnumerateArray())
            {
                var s = walker.State;
                var d = Egg7.Lay(new Tiny(s[0], s[1], s[2], s[3]), parents, c.GetProperty("tsv").GetInt32());
                string about = $"state {c.GetProperty("state").GetString()} number {w.GetProperty("frame").GetInt32()}";
                Assert.True(w.GetProperty("gender").GetInt32() == d.Gender && w.GetProperty("nature").GetInt32() == d.Nature && w.GetProperty("ability").GetInt32() == d.Ability, about);
                Assert.True(Ints(w.GetProperty("ivs")).SequenceEqual(d.Ivs) && Ints(w.GetProperty("from")).SequenceEqual(d.From), about);
                Assert.True(w.GetProperty("ec").GetUInt32() == d.Ec && w.GetProperty("pid").GetUInt32() == d.Pid && w.GetProperty("shiny").GetBoolean() == d.Shiny, about);
                Assert.True(w.GetProperty("ball").GetInt32() == d.Ball && w.GetProperty("used").GetInt32() == d.Used, about);
                walker.Next();
                rows++;
            }
        }
        Assert.True(rows > 1000);
    }

    /// <summary>The third and fourth generations, against PokeFinder's own test vectors: secret ids, Method 1, the seeds and ids of a new game, Method J.</summary>
    [Fact]
    public void TheOlderGenerations()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "vectors", "pokefinder");
        var bad = new List<string>(); int n = 0;
        void Check(bool ok, string what) { n++; if (!ok) bad.Add(what); }

        int e = 0;
        using (var d = JsonDocument.Parse(File.ReadAllText($"{dir}/Gen3/id3.json")))
            foreach (var t in d.RootElement.GetProperty("frlge").EnumerateArray())
                foreach (var r in t.GetProperty("results").EnumerateArray())
                    Check(Old.SecretId3(t.GetProperty("tid").GetUInt16(), r.GetProperty("advances").GetUInt32()) == r.GetProperty("sid").GetUInt16(), $"id3 {t.GetProperty("name")} {r.GetProperty("advances")}");
        int a = n;

        using (var d = JsonDocument.Parse(File.ReadAllText($"{dir}/Gen3/static3.json")))
            foreach (var t in d.RootElement.GetProperty("staticgenerator3").GetProperty("generate").EnumerateArray())
            {
                if (t.GetProperty("method").GetString() != "Method1") continue;
                foreach (var r in t.GetProperty("results").EnumerateArray())
                {
                    var sp = Old.Method1(Old.Advance(t.GetProperty("seed").GetUInt32(), r.GetProperty("advances").GetUInt32()));
                    Check(sp.Pid == r.GetProperty("pid").GetUInt32() && sp.Ivs.SequenceEqual(r.GetProperty("ivs").EnumerateArray().Select(x => x.GetInt32())), $"static3 {t.GetProperty("name")} {r.GetProperty("advances")}");
                }
            }
        int b = n;

        using (var d = JsonDocument.Parse(File.ReadAllText($"{dir}/Gen4/id4.json")))
            foreach (var t in d.RootElement.GetProperty("idgenerator4").GetProperty("generate").EnumerateArray())
                foreach (var r in t.GetProperty("results").EnumerateArray())
                {
                    uint seed = ClassicEraRNG.GetInitialSeed(t.GetProperty("year").GetUInt32(), t.GetProperty("month").GetUInt32(), t.GetProperty("day").GetUInt32(),
                        t.GetProperty("hour").GetUInt32(), t.GetProperty("minute").GetUInt32(), r.GetProperty("seconds").GetUInt32(), r.GetProperty("delay").GetUInt32());
                    var (tid, sid) = Old.Ids4(seed);
                    Check(seed == r.GetProperty("seed").GetUInt32() && tid == r.GetProperty("tid").GetUInt16() && sid == r.GetProperty("sid").GetUInt16(), $"id4 {t.GetProperty("name")} seed {seed:X8} vs {r.GetProperty("seed").GetUInt32():X8} -> {tid}/{sid}");
                }
        int c = n;

        using (var d = JsonDocument.Parse(File.ReadAllText($"{dir}/Gen4/static4.json")))
        {
            var g = d.RootElement.GetProperty("staticgenerator4");
            foreach (var t in g.GetProperty("generateMethod1").EnumerateArray())
            {
                // Those with a fixed shininess are made another way; the plain ones are what is being checked.
                foreach (var r in t.GetProperty("results").EnumerateArray())
                {
                    var sp = Old.Method1(Old.Advance(t.GetProperty("seed").GetUInt32(), r.GetProperty("advances").GetUInt32()));
                    bool same = sp.Pid == r.GetProperty("pid").GetUInt32() && sp.Ivs.SequenceEqual(r.GetProperty("ivs").EnumerateArray().Select(x => x.GetInt32()));
                    // The red Gyarados is always shiny and is made another way.
                    if (t.GetProperty("name").GetString()!.Contains("Gyrados")) continue;
                    Check(same, $"static4 M1 {t.GetProperty("name")} {r.GetProperty("advances")}");
                }
            }
        e = n;
            foreach (var t in g.GetProperty("generateMethodJ").EnumerateArray())
            {
                if (t.GetProperty("lead").GetString() != "None") continue;
                foreach (var r in t.GetProperty("results").EnumerateArray())
                {
                    var sp = Old.MethodJ(Old.Advance(t.GetProperty("seed").GetUInt32(), r.GetProperty("advances").GetUInt32()));
                    Check(sp.Pid == r.GetProperty("pid").GetUInt32() && sp.Ivs.SequenceEqual(r.GetProperty("ivs").EnumerateArray().Select(x => x.GetInt32())) && sp.Pid % 25 == r.GetProperty("nature").GetUInt32(),
                        $"static4 MJ {t.GetProperty("name")} {r.GetProperty("advances")}");
                }
            }
            Assert.True(n - e > 0, "no Method J vectors");
        }
        Assert.True(a > 0 && b > a && c > b && e > c, $"vectors missing: {a} {b} {c} {e}");
        Assert.Empty(bad);
    }
}

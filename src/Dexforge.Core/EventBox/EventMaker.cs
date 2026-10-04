using PKHeX.Core;

namespace Dexforge.EventBox;

/// <summary>One line of the distribution list: a distribution, its representative card, and what the rules say about it.</summary>
public sealed class Dist(Rows table, string[] row)
{
    public string this[string column] => table.Get(row, column);
    public int Generation => int.Parse(this["세대"]);
    public int CardRow => int.Parse(this["대표 행"]);
    public string Species => this["포켓몬"];
    public string Region => this["대표 지역"];
    public bool KoreanPossible => this["한국어 가능"] == "한국어";
    public bool LanguageFollowsGame => this["언어"] == "게임";
    public bool IsEgg => this["알"] == "알";
    public string Start => this["시작"];
    public string End => this["끝"];
    public string Label => $"{Generation}세대 {Species}{(this["폼"] != "0" ? "(" + this["폼"] + ")" : "")} {this["대표 어버이"]}#{this["대표 TID"]} [{Region}]";
    /// <summary>For a list people read: generation, species and form, shiny, level, OT, region, the year it was given out.</summary>
    public string Label2(GameStrings ko)
    {
        ushort sp = (ushort)ko.Species.ToList().IndexOf(Species); byte form = byte.Parse(this["폼"]);
        string formName = form != 0 ? "(" + Custom.FormName(ko, sp, form) + ")" : "";
        string year = Start.Length >= 4 ? Start[..4] : "?";
        return $"{Generation}세대 {Species}{formName}{(this["이로치"] == "Always" ? "★" : "")} Lv{this["레벨"]} · {(this["대표 어버이"] == "" ? "(받는 사람)" : this["대표 어버이"])} · {Region} {year}";
    }
}

public sealed record Made7(Dist Dist, PK7 Pokemon, SimpleTrainerInfo Receiver, DateOnly Received, string Notes, object Card);

/// <summary>Makes the Pokémon of one distribution from its card, the way the rules say (규칙.md).</summary>
public sealed class EventMaker(Random rnd, Receivers receivers, List<object> cards, DateOnly openEndCap)
{
    /// <summary>The save's own trainer: the Korean Ultra Sun game everything ends up in, and the one that received Korea's seventh-generation cards.</summary>
    public SimpleTrainerInfo Save => receivers.For(GameVersion.US, LanguageID.Korean);
    /// <summary>The first day this save itself could receive anything: a few days into its adventure.</summary>
    public DateOnly NotBefore { get; init; } = DateOnly.MinValue;
    /// <summary>
    /// Only the first so many days of each distribution's window are drawn from (owner's option, 2026-10-04: a card picked up months
    /// after it came out looks odd to some). 0 draws from the whole window.
    /// </summary>
    public int FirstDays { get; init; } = 0;

    public DateOnly Day(DateOnly from, DateOnly to) => from.AddDays(rnd.Next(0, Math.Max(0, to.DayNumber - from.DayNumber) + 1));

    private static DateOnly ParseDay(string s)
    {
        // 'YYYY-MM-DD'; a month-only or year-only date is taken as its first day (start) — callers pass which end they want
        var p = s.Split('-');
        int y = int.Parse(p[0]), m = p.Length > 1 && p[1] != "00" && p[1] != "?" ? int.Parse(p[1]) : 1, d = p.Length > 2 && p[2] != "00" && p[2] != "?" ? int.Parse(p[2]) : 1;
        return new DateOnly(y, m, d);
    }

    private static DateOnly ParseEnd(string s)
    {
        var p = s.Split('-');
        int y = int.Parse(p[0]);
        int m = p.Length > 1 && p[1] != "00" && p[1] != "?" ? int.Parse(p[1]) : 12;
        int d = p.Length > 2 && p[2] != "00" && p[2] != "?" ? int.Parse(p[2]) : DateTime.DaysInMonth(y, m);
        return new DateOnly(y, m, Math.Min(d, DateTime.DaysInMonth(y, m)));  // a source once wrote April 31
    }

    /// <summary>The window the card could be received in: the first of the distribution's windows (several are listed with '; ').</summary>
    public (DateOnly from, DateOnly to, bool open) Window(Dist d)
    {
        var starts = d.Start.Split("; "); var ends = d.End.Split("; ");
        var from = ParseDay(starts[0]);
        string e = ends.Length > 0 ? ends[0] : "";
        bool open = e == "" || e == "No End Date";
        var to = open ? from.AddYears(1) : ParseEnd(e);
        if (to > openEndCap) to = openEndCap;
        if (FirstDays > 0 && to > from.AddDays(FirstDays - 1)) to = from.AddDays(FirstDays - 1);
        if (to < from) to = from;
        return (from, to, open);
    }

    /// <summary>What language the Pokémon is: the card's, or the receiving game's where the card leaves it open (Korean where a Korean game could have had it).</summary>
    public LanguageID LanguageOf(Dist d, object card)
    {
        int fixedLang = card.GetType().GetProperty("Language")?.GetValue(card) is int l ? l : 0;
        if (fixedLang > 0) return (LanguageID)fixedLang;
        if (d.KoreanPossible) return LanguageID.Korean;
        return d.Region switch
        {
            "한국" => LanguageID.Korean,
            "일본" => LanguageID.Japanese,
            "기타지역" => LanguageID.English,
            _ => LanguageID.English,
        };
    }

    /// <summary>Versions to try for a card of this generation, most likely first.</summary>
    private static GameVersion[] Versions(int gen, object card)
    {
        if (card is MysteryGift g)
        {
            GameVersion[] pool = gen switch
            {
                4 => [GameVersion.Pt, GameVersion.D, GameVersion.P, GameVersion.HG, GameVersion.SS],
                5 => [GameVersion.B2, GameVersion.W2, GameVersion.B, GameVersion.W],
                6 => [GameVersion.OR, GameVersion.AS, GameVersion.X, GameVersion.Y],
                _ => [GameVersion.US, GameVersion.UM, GameVersion.SN, GameVersion.MN],
            };
            bool Can(GameVersion v) => g switch { WC7 c => c.CanBeReceivedByVersion(v), WC6 c => c.CanBeReceivedByVersion(v), PGF c => c.CanBeReceivedByVersion(v), PCD c => c.CanBeReceivedByVersion(v), _ => true };
            return pool.Where(Can).ToArray() is { Length: > 0 } ok ? ok : pool;
        }
        // gen 3 encounter templates carry their own version or version group
        var ver = (GameVersion)card.GetType().GetProperty("Version")!.GetValue(card)!;
        return ver switch
        {
            GameVersion.RS => [GameVersion.R, GameVersion.S],
            GameVersion.RSE => [GameVersion.E, GameVersion.R, GameVersion.S],
            GameVersion.FRLG => [GameVersion.FR, GameVersion.LG],
            GameVersion.Gen3 or GameVersion.RSBOX => [GameVersion.E, GameVersion.FR, GameVersion.R],
            GameVersion.COLO or GameVersion.XD or GameVersion.CXD => [GameVersion.CXD],
            _ => [ver],
        };
    }

    public Made7 Make(Dist d)
    {
        var card = cards[d.CardRow];
        var language = LanguageOf(d, card);
        var (from, to, open) = Window(d);
        var received = Day(from, to);
        Exception? last = null;
        // a card that leaves the colour to chance is made shiny (owner's rule); if no version manages that, plain, with a note
        bool wantShiny = d["이로치"] == "Random";
        foreach (bool shiny in wantShiny ? new[] { true, false } : new[] { false })
        foreach (var version in Versions(d.Generation, card))
        {
            var tr = receivers.For(version, language);
            if (ReferenceEquals(tr, Save) && received < NotBefore)
            {
                // this save received it itself: not before its adventure had got going
                if (to < NotBefore) { last = new InvalidOperationException($"이 세이브가 받기엔 배포가 모험 시작 전에 끝남 ({from}~{to})"); continue; }
                received = Day(NotBefore, to);
            }
            for (int attempt = 0; attempt < 20; attempt++)
            {
                PKM pk;
                try
                {
                    // gen 3 gifts fix their personality value or draw it by a method of their own; asking for a nature there breaks the correlation
                    var criteria = d.Generation == 3 ? EncounterCriteria.Unrestricted : EncounterCriteria.Unrestricted with { Nature = (Nature)rnd.Next(25) };
                    if (shiny) criteria = criteria with { Shiny = Shiny.Always };
                    pk = card switch
                    {
                        MysteryGift g => g.ConvertToPKM(tr, criteria),
                        IEncounterConvertible e => e.ConvertToPKM(tr, criteria),
                        _ => throw new InvalidOperationException("not a card"),
                    };
                }
                catch (Exception ex) { last = ex; continue; }
                if (pk.Species == 0) { last = new InvalidOperationException("nothing came out"); continue; }
                // the game draws these personality values freely (eggs, cards without a fixed value): one that is shiny for whoever
                // received it is as likely as any other; PKHeX's generator does not aim for it, so the value is drawn here
                string notes = wantShiny ? (shiny ? "랜덤→이로치" : "랜덤 색: 이로치는 PKHeX 가 못 만듦") : "";
                if (pk.IsEgg)
                {
                    // hatched by whoever received it, within a month of taking the egg
                    var hatched = Day(received, received.AddDays(30));
                    pk.EggMetDate = received;
                    pk.ForceHatchPKM();
                    pk.MetDate = hatched;
                    notes = (notes + $" 알 {received:yyyy-MM-dd} 부화 {hatched:yyyy-MM-dd}").Trim();
                }
                else pk.MetDate = received;
                if (shiny && !pk.IsShiny && !ForceShiny(pk, d.Generation)) { last = new InvalidOperationException("이로치로 나오지 않음"); continue; }
                pk.RefreshChecksum();
                var la0 = new LegalityAnalysis(pk);
                if (!la0.Valid) { last = new InvalidOperationException($"as received ({version}): " + Problems(la0)); continue; }

                PK7 up;
                if (pk is PK7 p7) up = p7;
                else
                {
                    var moved = EntityConverter.ConvertToType(pk, typeof(PK7), out var result) as PK7;
                    if (moved is null) { last = new InvalidOperationException($"would not move up ({version}): {result}"); continue; }
                    up = moved;
                    // what the DS-era transfers write: the day it went through Pal Park (gen 3) or Poké Transfer (gen 4); later moves keep the date.
                    // Not before the game that does the transfer came out in that language, and within two years of the first day it could.
                    if (d.Generation <= 4)
                    {
                        var first = d.Generation == 3 ? Releases.DiamondPearl(language) : Releases.BlackWhite(language);
                        if (first < received.AddDays(1)) first = received.AddDays(1);
                        up.MetDate = Day(first, first.AddYears(2));
                    }
                    // Bank leaves held items behind — except that a plate, orb, drive or memory is what gives Arceus, Giratina, Genesect and Silvally
                    // their form, and the player hands it back in Alola
                    if (!(up.Species is 493 or 487 or 649 or 773 && up.Form != 0)) up.HeldItem = 0;
                }
                // whatever came up through Bank, or from another trainer's game, is in the save's trainer's hands now (PKHeX: a transferred
                // Pokémon's current handler is never its OT; one the save's trainer received itself keeps handler 0)
                up.UpdateHandler(Save);
                up.RefreshChecksum();
                var la7 = new LegalityAnalysis(up);
                if (!la7.Valid) { last = new InvalidOperationException($"after moving up ({version}): " + Problems(la7)); continue; }
                return new Made7(d, up, tr, received, notes + (open ? " (종료일 없음: 시작+1년 안)" : ""), card);
            }
        }
        throw new InvalidOperationException(last?.Message ?? "no version could receive it");
    }

    /// <summary>Whether the rules give this distribution evolved copies: not a plain Poké Ball non-shiny, and Pikachu only from Korea.</summary>
    public static bool Evolves(Dist d)
    {
        if ((d["볼"] is "몬스터볼" or "") && d["이로치"] != "Always") return false;
        if (d.Species == "피카츄" && d["폼"] == "0" && d.Region != "한국") return false;
        return true;
    }

    /// <summary>One final evolution of a distribution: a fresh receipt of the same card, evolved in place; or why it could not be.</summary>
    public Made7? MakeEvolution(Dist d, ushort toSpecies, byte toForm, out string? why)
    {
        var ko = GameInfo.GetStrings("ko");
        string target = ko.Species[toSpecies] + (toForm != 0 ? $"({Custom.FormName(ko, toSpecies, toForm)})" : "");
        string? last = null;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Made7 m;
            try { m = Make(d); } catch (Exception ex) { last = ex.Message; continue; }
            if (!Evolve7.GenderAllows(toSpecies, toForm, m.Pokemon.Gender)) { last = "성별이 안 맞음"; continue; }
            var pk = (PK7)m.Pokemon.Clone();
            try { Evolve7.Evolve(pk, toSpecies, toForm); } catch (Exception ex) { last = ex.Message; break; }
            var la = new LegalityAnalysis(pk);
            if (!la.Valid) { last = Problems(la); continue; }
            why = null;
            return m with { Pokemon = pk, Notes = (m.Notes + " → " + target).Trim() };
        }
        why = last ?? "만들지 못함";
        return null;
    }

    /// <summary>The final evolutions of a distribution the rules evolve, each a fresh receipt of the same card evolved in place.</summary>
    public List<(Made7? made, string target, string? problem)> MakeEvolutions(Dist d, Dictionary<string, ushort> numbers)
    {
        var results = new List<(Made7?, string, string?)>();
        if (!Evolves(d)) return results;
        ushort species = numbers[d.Species]; byte form = byte.Parse(d["폼"]);
        var ko = GameInfo.GetStrings("ko");
        foreach (var (toSpecies, toForm) in Evolve7.Finals(species, form))
        {
            var done = MakeEvolution(d, toSpecies, toForm, out var why);
            results.Add((done, ko.Species[toSpecies] + (toForm != 0 ? $"({toForm})" : ""), why));
        }
        return results;
    }

    /// <summary>A personality value shiny for this Pokémon's own ids, keeping what the older games tie to it (gender, ability slot, nature).</summary>
    private bool ForceShiny(PKM pk, int generation)
    {
        var pi = pk.PersonalInfo;
        byte gender = pk.Gender; int abilityBit = (pk.AbilityNumber >> 1) & 1;
        for (int i = 0; i < 20000; i++)
        {
            uint low = (uint)rnd.Next(65536), xor = (uint)rnd.Next(generation >= 6 ? 16 : 8);
            uint pid = ((uint)(pk.TID16 ^ pk.SID16) ^ low ^ xor) << 16 | low;
            if (generation <= 5)
            {
                if (!pi.Genderless && !pi.OnlyMale && !pi.OnlyFemale && EntityGender.GetFromPIDAndRatio(pid, pi.Gender) != gender) continue;
                if (generation <= 4 && (pid & 1) != abilityBit) continue;
            }
            var test = pk.Clone();
            test.PID = pid;
            if (generation <= 5) test.Nature = (Nature)(pid % 25);
            test.RefreshChecksum();
            if (!test.IsShiny) continue;
            var la = new LegalityAnalysis(test);
            if (!la.Valid) continue;
            pk.PID = pid;
            if (generation <= 5) pk.Nature = (Nature)(pid % 25);
            pk.RefreshChecksum();
            return true;
        }
        return false;
    }

    public static string Problems(LegalityAnalysis la) => string.Join(" | ", la.Report().Split('\n').Where(l => l.Contains("Invalid") || l.Contains("Fishy")).Take(3));
}
